using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.EmployeeSelfService;
using Cosmix.Application.HR.Payslips;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollMonthAcceptanceFactAttribute : FactAttribute
{
    public PayrollMonthAcceptanceFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COSMIX_PAYROLL_ACCEPTANCE_CONNECTION")))
            Skip = "Requires the specifically authorized one-employee synthetic month database and input workbook.";
    }
}

public sealed class PayrollMonthAcceptanceTests
{
    [PayrollMonthAcceptanceFact]
    public async Task Synthetic_September_month_import_HR_management_finance_and_own_payslip_reconcile_in_SQL()
    {
        await using var f = new PayrollMonthAcceptanceFixture();
        await f.SeedEmployeeAsync();
        await f.ImportAndApproveAsync();
        var handoff = await f.RetainSyntheticAttendanceAsync();
        var finance = await PayrollMonthFinanceAcceptance.ConfigureFinanceAsync(f);
        var context = PayrollMonthAcceptanceFixture.Context;
        var preview = await handoff.PreviewAsync(2026, 9, context, default);
        Assert.Equal("salary-v2", preview.CalculationVersion); Assert.True(preview.SalaryComplete);
        Assert.Equal(30000m, Assert.Single(preview.Lines, x => x.Component == "BaseCompensation").Amount);
        Assert.Equal(3000m, Assert.Single(preview.Lines, x => x.Component == "FixedAllowance").Amount);
        Assert.Equal(-500m, Assert.Single(preview.Lines, x => x.Component == "SalaryAdjustment").Amount);
        Assert.Equal(-300m, Assert.Single(preview.Lines, x => x.Component == "StatutoryEmployeeDeduction").Amount);
        Assert.Equal(32200m, Assert.Single(preview.EmployeeTotals).NetPay);
        await PayrollMonthAcceptanceFixture.SaveEvidenceAsync("salary-preview.json", preview);
        var batch = await handoff.PrepareAsync(new(2026, 9, preview.PreviewFingerprint, "SYNTHETIC HR preparation"), PayrollMonthAcceptanceFixture.Key(), context, default);
        var forbidden = await Assert.ThrowsAsync<AuthenticationFailure>(() => handoff.HrReviewAsync(batch.Batch.Id,
            new(batch.Batch.Version, ApprovalState.Approved, "SYNTHETIC prohibited self-review"), PayrollMonthAcceptanceFixture.Key(), context, default));
        Assert.Equal("payroll_handoff_self_review_forbidden", forbidden.Code);
        f.ActorId = Guid.NewGuid();
        batch = await handoff.HrReviewAsync(batch.Batch.Id, new(batch.Batch.Version, ApprovalState.Approved, "SYNTHETIC independent HR verification"),
            PayrollMonthAcceptanceFixture.Key(), context, default);
        Assert.Equal("hr_approved", batch.Batch.State);
        f.ActorId = Guid.NewGuid();
        batch = await handoff.LockAsync(batch.Batch.Id, new(batch.Batch.Version, "SYNTHETIC independent management approval"), PayrollMonthAcceptanceFixture.Key(), context, default);
        Assert.Equal("locked", batch.Batch.State);
        f.ActorId = Guid.NewGuid();
        batch = await handoff.ProcessingAsync(batch.Batch.Id, new("SYNTHETIC-202609-FINANCE-HANDOFF", "SYNTHETIC finance handoff; no payment"),
            PayrollMonthAcceptanceFixture.Key(), context, default);
        Assert.Equal("finance_handed_off", batch.Batch.State);
        var financeResult = await finance.FinalizeAndReconcileAsync(f, batch.Batch.Id);
        await PayrollMonthAcceptanceFixture.SaveEvidenceAsync("approved-salary-batch.json", batch);

        var frozen = new PayrollFrozenBatchReader(new PayrollHandoffRepository(f.Db), f.Files);
        var payslips = new PayslipService(f.Operation, frozen, new PayslipRepository(f.Db), f.Clock);
        var own = new EmployeeSelfService(null!, f.Current, f.Access, f.Clock, f.Authorization, f.Journal, payslips: payslips);
        f.ActorId = Guid.NewGuid(); f.ActorEmployeeId = f.EmployeeId;
        var page = await own.PayslipsAsync(1, 10, context, default);
        var summary = Assert.Single(page.Items);
        Assert.Equal(33000m, summary.GrossAmount); Assert.Equal(800m, summary.DeductionAmount); Assert.Equal(32200m, summary.NetAmount);
        Assert.Equal("Unpaid", summary.PaymentState);
        foreach (var format in new[] { "pdf", "xlsx" })
        {
            var document = await own.PayslipAsync(batch.Batch.Id, format, context, default);
            var bytes = Convert.FromBase64String(document.Base64);
            Assert.Equal(document.Sha256, Convert.ToHexString(SHA256.HashData(bytes)));
            if (format == "pdf") Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes));
            else
            {
                using var zip = new ZipArchive(new MemoryStream(bytes));
                var text = string.Join("\n", zip.Entries.Where(x => x.FullName.EndsWith(".xml")).Select(x =>
                { using var reader = new StreamReader(x.Open()); return reader.ReadToEnd(); }));
                Assert.Contains("32200.00", text); Assert.Contains("SYNTHETIC Payroll Acceptance", text); Assert.Contains("Unpaid", text);
            }
            await File.WriteAllBytesAsync(Path.Combine(PayrollMonthAcceptanceFixture.EvidenceRoot, "SYNTHETIC-" + document.FileName), bytes);
        }
        f.ActorEmployeeId = Guid.NewGuid();
        // Exercise document ownership denial before any audit is saved. A real current session
        // cannot reference a nonexistent employee; collection isolation has its own unit coverage.
        var denied = await Assert.ThrowsAsync<AuthenticationFailure>(() => own.PayslipAsync(batch.Batch.Id, "pdf", context, default));
        Assert.Equal("payslip_not_found", denied.Code);
        f.ActorEmployeeId = f.EmployeeId;
        var unknown = await Assert.ThrowsAsync<AuthenticationFailure>(() => own.PayslipAsync(Guid.NewGuid(), "pdf", context, default));
        Assert.Equal("payslip_not_found", unknown.Code);
        f.Db.ChangeTracker.Clear();
        var saved = await f.Db.PayrollHandoffBatches.AsNoTracking().Include(x => x.Lines).SingleAsync(x => x.Id == batch.Batch.Id);
        Assert.Equal(32200m, saved.Lines.Sum(x => x.Amount)); Assert.Equal("finance_handed_off", saved.ReviewState);
        Assert.Equal(30, await f.Db.DailyAttendanceSummaries.CountAsync(x => x.EmployeeId == f.EmployeeId));
        await PayrollMonthAcceptanceFixture.SaveEvidenceAsync("acceptance-result.json", new
        {
            Scenario = "SYNTHETIC September 2026 single-employee persisted service acceptance", f.EmployeeId, f.Employee.EmployeeNumber,
            BatchId = batch.Batch.Id, NetPay = 32200m, Gross = 33000m, Deductions = 800m, EmployerContribution = 1500m,
            PaymentState = "Unpaid", Finance = financeResult, OwnPayslip = summary, SqlDailySummaries = 30,
            Limitation = "Synthetic authentication/session and upstream hiring/attendance fixtures. No live biometric, real employee, legal-rate, company signoff or payment claim."
        });
    }
}
