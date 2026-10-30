using System.Security.Cryptography;
using System.Text;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollReconciliation;
using Xunit;

namespace Cosmix.Tests;

public sealed class ParallelPayrollSignOffAcceptanceTests
{
    private readonly MockParallelPayrollRepository _parallelRepo = new();
    private readonly MockHistoricalPayrollRepo _histRepo = new();
    private readonly ParallelPayrollService _service;

    public ParallelPayrollSignOffAcceptanceTests()
    {
        var histService = new HistoricalPayrollReconciliationService(_histRepo, TimeProvider.System);
        _service = new ParallelPayrollService(_parallelRepo, histService, TimeProvider.System);
    }

    [Fact]
    public async Task Complete_parallel_payroll_multi_stage_maker_checker_approval_workflow_succeeds()
    {
        var preparerId = "user_payroll_preparer";
        var hrReviewerId = "user_hr_officer";
        var financeReviewerId = "user_finance_manager";
        var managementApproverId = "user_ceo_management";

        var csvContent = "EmployeeCode,BasicSalary,Allowances,GrossSalary,Overtime,Deductions,Eobi,Sessi,IncomeTax,NetSalary\n" +
                         "EMP-0001,50000,10000,60000,0,300,300,0,0,59700\n";
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(csvContent));
        var mapping = new Dictionary<string, string>
        {
            ["EmployeeCode"] = "EmployeeCode",
            ["BasicSalary"] = "BasicSalary",
            ["Allowances"] = "Allowances",
            ["GrossSalary"] = "GrossSalary",
            ["Overtime"] = "Overtime",
            ["Deductions"] = "Deductions",
            ["Eobi"] = "Eobi",
            ["Sessi"] = "Sessi",
            ["IncomeTax"] = "IncomeTax",
            ["NetSalary"] = "NetSalary"
        };

        // Step 1: Initiate Parallel Run
        var initRequest = new InitiateParallelPayrollRunRequest(
            Year: 2026,
            Month: 9,
            ErpBatchId: Guid.NewGuid(),
            LegacyFileName: "Legacy_Payroll_Sep_2026.csv",
            LegacySheetBase64: base64,
            ColumnMapping: mapping,
            Reason: "Parallel pilot verification for Sep 2026"
        );

        var run = await _service.InitiateAsync(initRequest, preparerId, new AuthRequestContext("127.0.0.1", "c-1"), CancellationToken.None);
        Assert.Equal(ParallelPayrollRunState.DraftPrepared, run.State);
        Assert.Equal(1, run.Version);
        Assert.Equal(preparerId, run.PreparedByUserId);

        // Step 2: Maker-Checker Enforcement - Preparer cannot self-approve HR review
        var selfReviewEx = await Assert.ThrowsAsync<AuthenticationFailure>(() =>
            _service.HrReviewAsync(new ParallelHrReviewRequest(run.Id, true, [], "Self review attempt", 1), preparerId, CancellationToken.None));
        Assert.Equal("payroll_parallel_self_review_forbidden", selfReviewEx.Code);

        // Step 3: Independent HR Review
        var hrResult = await _service.HrReviewAsync(
            new ParallelHrReviewRequest(run.Id, true, [], "HR verified all employee variances are within policy", 1),
            hrReviewerId,
            CancellationToken.None);
        Assert.Equal(ParallelPayrollRunState.HrReviewed, hrResult.State);
        Assert.Equal(2, hrResult.Version);
        Assert.Equal(hrReviewerId, hrResult.HrReviewerUserId);

        // Step 4: Maker-Checker Enforcement - HR Reviewer or Preparer cannot review Finance
        var financeSelfEx = await Assert.ThrowsAsync<AuthenticationFailure>(() =>
            _service.FinanceReviewAsync(new ParallelFinanceReviewRequest(run.Id, true, true, true, "Self finance review", 2), hrReviewerId, CancellationToken.None));
        Assert.Equal("payroll_parallel_self_review_forbidden", financeSelfEx.Code);

        // Step 5: Independent Finance Review
        var finResult = await _service.FinanceReviewAsync(
            new ParallelFinanceReviewRequest(run.Id, true, true, true, "Finance reconciled with GL account 501000 and 201000", 2),
            financeReviewerId,
            CancellationToken.None);
        Assert.Equal(ParallelPayrollRunState.FinanceReconciled, finResult.State);
        Assert.Equal(3, finResult.Version);
        Assert.Equal(financeReviewerId, finResult.FinanceReviewerUserId);

        // Step 6: Maker-Checker Enforcement - Finance Reviewer cannot approve as Management
        var mgmtSelfEx = await Assert.ThrowsAsync<AuthenticationFailure>(() =>
            _service.ManagementApprovalAsync(new ParallelManagementApprovalRequest(run.Id, true, "Self mgmt approval", 3), financeReviewerId, CancellationToken.None));
        Assert.Equal("payroll_parallel_self_review_forbidden", mgmtSelfEx.Code);

        // Step 7: Independent Management Approval & Fingerprint Generation
        var mgmtResult = await _service.ManagementApprovalAsync(
            new ParallelManagementApprovalRequest(run.Id, true, "Executive board authorizes September 2026 parallel sign-off", 3),
            managementApproverId,
            CancellationToken.None);
        Assert.Equal(ParallelPayrollRunState.ManagementApproved, mgmtResult.State);
        Assert.Equal(4, mgmtResult.Version);
        Assert.Equal(managementApproverId, mgmtResult.ManagementApproverUserId);
        Assert.NotNull(mgmtResult.ApprovalFingerprint);
        Assert.Equal(64, mgmtResult.ApprovalFingerprint.Length); // Cryptographic SHA-256 fingerprint
    }

    [Fact]
    public async Task Rejection_by_reviewer_transitions_state_to_rejected()
    {
        var run = await CreateDraftRunAsync();

        var rejected = await _service.HrReviewAsync(
            new ParallelHrReviewRequest(run.Id, false, [], "Unexplained salary variance detected in basic pay", 1),
            "hr_independent_reviewer",
            CancellationToken.None);

        Assert.Equal(ParallelPayrollRunState.Rejected, rejected.State);
        Assert.Contains("Unexplained salary variance", rejected.HrNotes);
    }

    [Fact]
    public async Task Out_of_sequence_state_transitions_are_strictly_prevented()
    {
        var run = await CreateDraftRunAsync();

        // Cannot skip HR and Finance and go straight to Management Approval
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ManagementApprovalAsync(new ParallelManagementApprovalRequest(run.Id, true, "Direct approval", 1), "mgmt_user", CancellationToken.None));
    }

    [Fact]
    public async Task Concurrency_conflict_throws_when_version_mismatches()
    {
        var run = await CreateDraftRunAsync();

        // Passing stale version 99 instead of 1
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.HrReviewAsync(new ParallelHrReviewRequest(run.Id, true, [], "Review", 99), "hr_user", CancellationToken.None));
    }

    private async Task<ParallelPayrollRunRecord> CreateDraftRunAsync()
    {
        var csvContent = "EmployeeCode,BasicSalary,Allowances,GrossSalary,Overtime,Deductions,Eobi,Sessi,IncomeTax,NetSalary\n" +
                         "EMP-0001,50000,10000,60000,0,300,300,0,0,59700\n";
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(csvContent));
        var mapping = new Dictionary<string, string>
        {
            ["EmployeeCode"] = "EmployeeCode",
            ["BasicSalary"] = "BasicSalary",
            ["Allowances"] = "Allowances",
            ["GrossSalary"] = "GrossSalary",
            ["Overtime"] = "Overtime",
            ["Deductions"] = "Deductions",
            ["Eobi"] = "Eobi",
            ["Sessi"] = "Sessi",
            ["IncomeTax"] = "IncomeTax",
            ["NetSalary"] = "NetSalary"
        };

        var initRequest = new InitiateParallelPayrollRunRequest(
            Year: 2026,
            Month: 9,
            ErpBatchId: Guid.NewGuid(),
            LegacyFileName: "Legacy.csv",
            LegacySheetBase64: base64,
            ColumnMapping: mapping,
            Reason: "Draft test"
        );

        return await _service.InitiateAsync(initRequest, "preparer_1", new AuthRequestContext("127.0.0.1", "c-1"), CancellationToken.None);
    }

    private sealed class MockParallelPayrollRepository : IParallelPayrollRepository
    {
        private readonly Dictionary<Guid, ParallelPayrollRunRecord> _runs = new();

        public Task<ParallelPayrollRunRecord?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            _runs.TryGetValue(id, out var run);
            return Task.FromResult(run);
        }

        public Task<ParallelPayrollRunRecord?> GetActiveByPeriodAsync(int year, int month, CancellationToken ct)
        {
            var run = _runs.Values.FirstOrDefault(r => r.Year == year && r.Month == month && r.State != ParallelPayrollRunState.Superseded);
            return Task.FromResult(run);
        }

        public Task SaveRunAsync(ParallelPayrollRunRecord record, CancellationToken ct)
        {
            _runs[record.Id] = record;
            return Task.CompletedTask;
        }
    }

    private sealed class MockHistoricalPayrollRepo : IHistoricalPayrollRepository
    {
        public Task<IReadOnlyList<ErpEmployeePayrollRecord>> GetErpPayrollRecordsAsync(int year, int month, CancellationToken ct)
        {
            IReadOnlyList<ErpEmployeePayrollRecord> records =
            [
                new(
                    EmployeeId: Guid.NewGuid(),
                    EmployeeNumber: 1,
                    FullLegalName: "Ahmed Khan",
                    BasicSalary: 50000m,
                    Allowances: 10000m,
                    GrossSalary: 60000m,
                    Overtime: 0m,
                    Deductions: 300m,
                    Eobi: 300m,
                    Sessi: 0m,
                    IncomeTax: 0m,
                    NetSalary: 59700m
                )
            ];
            return Task.FromResult(records);
        }
    }
}
