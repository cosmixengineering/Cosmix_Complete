using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Authentication;

namespace Cosmix.Application.HR.PayrollReconciliation;

public sealed class ParallelPayrollService(
    IParallelPayrollRepository repository,
    HistoricalPayrollReconciliationService reconciliation,
    TimeProvider clock)
{
    public const string PermissionInitiate = "attendance.payroll-salary.request";
    public const string PermissionHrReview = "attendance.payroll-salary.review";
    public const string PermissionFinanceReview = "payroll.accounting.configure.approve";
    public const string PermissionManagementApproval = "payroll.accounting.bank.export";

    public async Task<ParallelPayrollRunRecord> InitiateAsync(
        InitiateParallelPayrollRunRequest request,
        string actorUserId,
        AuthRequestContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(actorUserId)) throw new ArgumentException("Actor user ID required.");

        var reconRequest = new ReconcileHistoricalPayrollRequest(
            FileName: request.LegacyFileName,
            ContentType: "application/octet-stream",
            Base64: request.LegacySheetBase64,
            Year: request.Year,
            Month: request.Month,
            ColumnMapping: request.ColumnMapping,
            Reason: request.Reason
        );

        var report = await reconciliation.ReconcileAsync(reconRequest, context, ct);

        var explanations = new List<ParallelVarianceExplanation>();
        foreach (var v in report.Variances)
        {
            var rootCause = v.Classification switch
            {
                VarianceClassification.ExactMatch => ParallelRootCause.ExactMatch,
                VarianceClassification.RoundingDifference => ParallelRootCause.RoundingDifference,
                VarianceClassification.MissingInSheet => ParallelRootCause.RosterMissingEmployee,
                _ => Math.Abs(v.OvertimeVariance) > 0 ? ParallelRootCause.OvertimeRatePolicy :
                     Math.Abs(v.StatutoryVariance) > 0 ? ParallelRootCause.StatutoryBracketUpdate :
                     Math.Abs(v.BasicVariance) > 0 ? ParallelRootCause.ProrationPolicyDifference :
                     ParallelRootCause.ManualUnapprovedAdjustment
            };

            explanations.Add(new ParallelVarianceExplanation(
                v.EmployeeId,
                v.EmployeeCode,
                "NetPay",
                v.ErpNetSalary,
                v.SheetNetSalary,
                v.NetVariance,
                rootCause,
                v.Details
            ));
        }

        var summary = new ParallelPayrollRunSummary(
            TotalEmployees: report.Summary.TotalEmployeesInSheet,
            MatchedEmployees: report.Summary.MatchedEmployeesCount,
            DiscrepancyCount: report.Summary.VarianceCount + report.Summary.MissingInErpCount + report.Summary.MissingInSheetCount,
            ErpTotalGross: report.Summary.TotalErpGross,
            LegacyTotalGross: report.Summary.TotalSheetGross,
            GrossVariance: report.Summary.TotalGrossVariance,
            ErpTotalNet: report.Summary.TotalErpNet,
            LegacyTotalNet: report.Summary.TotalSheetNet,
            NetVariance: report.Summary.TotalNetVariance,
            AllVariancesExplained: false
        );

        var runRecord = new ParallelPayrollRunRecord(
            Id: Guid.NewGuid(),
            Year: request.Year,
            Month: request.Month,
            State: ParallelPayrollRunState.DraftPrepared,
            Version: 1,
            PreparedByUserId: actorUserId,
            PreparedAt: clock.GetUtcNow(),
            HrReviewerUserId: null,
            HrReviewedAt: null,
            HrNotes: null,
            FinanceReviewerUserId: null,
            FinanceReviewedAt: null,
            FinanceNotes: null,
            ManagementApproverUserId: null,
            ManagementApprovedAt: null,
            ManagementNotes: null,
            ApprovalFingerprint: null,
            Summary: summary,
            Variances: explanations
        );

        await repository.SaveRunAsync(runRecord, ct);
        return runRecord;
    }

    public async Task<ParallelPayrollRunRecord> HrReviewAsync(
        ParallelHrReviewRequest request,
        string actorUserId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var run = await repository.GetByIdAsync(request.ParallelRunId, ct)
            ?? throw new InvalidOperationException("Parallel payroll run not found.");

        if (run.State != ParallelPayrollRunState.DraftPrepared)
            throw new InvalidOperationException($"Invalid state transition from {run.State}. DraftPrepared required.");

        if (run.Version != request.ExpectedVersion)
            throw new InvalidOperationException("Concurrency conflict: version has changed.");

        // Maker-Checker Invariant: Preparer cannot approve as HR
        if (string.Equals(run.PreparedByUserId, actorUserId, StringComparison.OrdinalIgnoreCase))
            throw new AuthenticationFailure("payroll_parallel_self_review_forbidden", 403);

        if (!request.Approved)
        {
            var rejected = run with
            {
                State = ParallelPayrollRunState.Rejected,
                Version = run.Version + 1,
                HrReviewerUserId = actorUserId,
                HrReviewedAt = clock.GetUtcNow(),
                HrNotes = request.Justification
            };
            await repository.SaveRunAsync(rejected, ct);
            return rejected;
        }

        var updatedSummary = run.Summary with { AllVariancesExplained = true };

        var updated = run with
        {
            State = ParallelPayrollRunState.HrReviewed,
            Version = run.Version + 1,
            HrReviewerUserId = actorUserId,
            HrReviewedAt = clock.GetUtcNow(),
            HrNotes = request.Justification,
            Summary = updatedSummary,
            Variances = request.Explanations.Count > 0 ? request.Explanations : run.Variances
        };

        await repository.SaveRunAsync(updated, ct);
        return updated;
    }

    public async Task<ParallelPayrollRunRecord> FinanceReviewAsync(
        ParallelFinanceReviewRequest request,
        string actorUserId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var run = await repository.GetByIdAsync(request.ParallelRunId, ct)
            ?? throw new InvalidOperationException("Parallel payroll run not found.");

        if (run.State != ParallelPayrollRunState.HrReviewed)
            throw new InvalidOperationException($"Invalid state transition from {run.State}. HrReviewed required.");

        if (run.Version != request.ExpectedVersion)
            throw new InvalidOperationException("Concurrency conflict: version has changed.");

        // Maker-Checker Invariant: Independent Finance Review required
        if (string.Equals(run.PreparedByUserId, actorUserId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(run.HrReviewerUserId, actorUserId, StringComparison.OrdinalIgnoreCase))
            throw new AuthenticationFailure("payroll_parallel_self_review_forbidden", 403);

        if (!request.Approved)
        {
            var rejected = run with
            {
                State = ParallelPayrollRunState.Rejected,
                Version = run.Version + 1,
                FinanceReviewerUserId = actorUserId,
                FinanceReviewedAt = clock.GetUtcNow(),
                FinanceNotes = request.LedgerReconciliationNotes
            };
            await repository.SaveRunAsync(rejected, ct);
            return rejected;
        }

        if (!request.GeneralLedgerAccountsVerified || !request.AccrualBalanceMatches)
            throw new InvalidOperationException("Finance approval requires verified GL accounts and matching accrual balance.");

        var updated = run with
        {
            State = ParallelPayrollRunState.FinanceReconciled,
            Version = run.Version + 1,
            FinanceReviewerUserId = actorUserId,
            FinanceReviewedAt = clock.GetUtcNow(),
            FinanceNotes = request.LedgerReconciliationNotes
        };

        await repository.SaveRunAsync(updated, ct);
        return updated;
    }

    public async Task<ParallelPayrollRunRecord> ManagementApprovalAsync(
        ParallelManagementApprovalRequest request,
        string actorUserId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var run = await repository.GetByIdAsync(request.ParallelRunId, ct)
            ?? throw new InvalidOperationException("Parallel payroll run not found.");

        if (run.State != ParallelPayrollRunState.FinanceReconciled)
            throw new InvalidOperationException($"Invalid state transition from {run.State}. FinanceReconciled required.");

        if (run.Version != request.ExpectedVersion)
            throw new InvalidOperationException("Concurrency conflict: version has changed.");

        // Maker-Checker Invariant: Independent Executive Sign-Off required
        if (string.Equals(run.PreparedByUserId, actorUserId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(run.HrReviewerUserId, actorUserId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(run.FinanceReviewerUserId, actorUserId, StringComparison.OrdinalIgnoreCase))
            throw new AuthenticationFailure("payroll_parallel_self_review_forbidden", 403);

        if (!request.Approved)
        {
            var rejected = run with
            {
                State = ParallelPayrollRunState.Rejected,
                Version = run.Version + 1,
                ManagementApproverUserId = actorUserId,
                ManagementApprovedAt = clock.GetUtcNow(),
                ManagementNotes = request.ExecutiveDecisionNotes
            };
            await repository.SaveRunAsync(rejected, ct);
            return rejected;
        }

        var approvalTimestamp = clock.GetUtcNow();

        // Calculate tamper-proof approval fingerprint
        var payloadToHash = JsonSerializer.SerializeToUtf8Bytes(new
        {
            run.Id,
            run.Year,
            run.Month,
            run.PreparedByUserId,
            run.HrReviewerUserId,
            run.FinanceReviewerUserId,
            ManagementApproverUserId = actorUserId,
            ApprovalTimestamp = approvalTimestamp,
            run.Summary
        });

        var fingerprint = Convert.ToHexString(SHA256.HashData(payloadToHash));

        var approved = run with
        {
            State = ParallelPayrollRunState.ManagementApproved,
            Version = run.Version + 1,
            ManagementApproverUserId = actorUserId,
            ManagementApprovedAt = approvalTimestamp,
            ManagementNotes = request.ExecutiveDecisionNotes,
            ApprovalFingerprint = fingerprint
        };

        await repository.SaveRunAsync(approved, ct);
        return approved;
    }
}
