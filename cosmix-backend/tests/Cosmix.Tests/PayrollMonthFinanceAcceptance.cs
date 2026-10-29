using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Security;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Application.HR.StatutoryContributions;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.StatutoryContributions;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cosmix.Tests;

/// <summary>
/// SQL-backed finance leg of the expressly synthetic one-employee month acceptance.
/// Authentication is the named test fixture; every financial service, repository,
/// persisted monetary value and purpose-bound evidence read is real application code.
/// The fictional 300/1500 contribution amounts are test inputs, not legal rates.
/// </summary>
internal sealed class PayrollMonthFinanceAcceptance
{
    private const string Reason = "SYNTHETIC TEST ONLY: September 2026 single-employee acceptance; no payment or legal rate approval";
    private const string OutputDirectory = "D:/CosmixPayrollAcceptance20261028";
    private readonly Guid maker = Guid.NewGuid(), reviewer = Guid.NewGuid();
    private readonly PayrollFinanceSetupService setup;
    private readonly StatutoryContributionService statutory;
    private readonly PayrollAccountingService accounting;
    private readonly IPayrollFrozenBatchReader frozen;
    private readonly List<string> checks = [];
    private AccountingPeriodResponse period = null!;
    private PostingProfileResponse profile = null!;
    private ContributionResponse contribution = null!;

    private PayrollMonthFinanceAcceptance(PayrollMonthAcceptanceFixture f)
    {
        f.AddPermissions(["payroll.accounting.configure", "payroll.accounting.configure.approve", "payroll.accounting.accrue",
            "statutory.contributions.manage", "statutory.contributions.approve"]);
        var sensitive = new SensitiveComplianceOperation(f.Current, f.Access, f.Authorization, f.Journal, f.Clock);
        var writes = new PayrollFinanceOperation(sensitive, f.Journal, f.Files, f.Clock);
        var financeRepository = new PayrollFinanceRepository(f.Db);
        frozen = new PayrollFrozenBatchReader(new PayrollHandoffRepository(f.Db), f.Files);
        setup = new(financeRepository, sensitive, writes, f.Clock);
        accounting = new(new PayrollAccountingRepository(f.Db), f.Clock, sensitive, setup, frozen, financeRepository, f.Files, writes);
        statutory = new(new StatutoryContributionRepository(f.Db), f.Clock, sensitive, financeRepository, f.Files, writes, accounting);
    }

    public static async Task<PayrollMonthFinanceAcceptance> ConfigureFinanceAsync(PayrollMonthAcceptanceFixture f)
    {
        var helper = new PayrollMonthFinanceAcceptance(f);
        await helper.ConfigureAsync(f);
        return helper;
    }

    private async Task ConfigureAsync(PayrollMonthAcceptanceFixture f)
    {
        var originalActor = f.ActorId;
        var month = PayrollMonthAcceptanceFixture.Month;
        var context = PayrollMonthAcceptanceFixture.Context;
        try
        {
            f.ActorId = maker;
            foreach (var (code, name, kind) in new[] {
                ("TEST-SAL", "Synthetic salary expense", PayrollAccountKind.Expense),
                ("TEST-PAY", "Synthetic salary payable", PayrollAccountKind.Liability),
                ("TEST-DED", "Synthetic fixed deduction payable", PayrollAccountKind.Liability),
                ("TEST-ER", "Synthetic employer contribution expense", PayrollAccountKind.Expense),
                ("TEST-STAT", "Synthetic statutory payable", PayrollAccountKind.Liability) })
                await setup.CreateAccountAsync(new(code, name, kind, Reason), "month-account-" + code, context, default);

            period = await setup.CreatePeriodAsync(new(month, month.AddMonths(1), "PKR", Reason), "month-period-create", context, default);
            await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => setup.DecidePeriodAsync(period.Id,
                new(period.Version, Reason), false, "month-period-self-denied", context, default));
            f.ActorId = reviewer;
            period = await setup.DecidePeriodAsync(period.Id, new(period.Version, Reason), false, "month-period-approve", context, default);
            Assert.Equal("Open", period.State);
            Assert.NotEqual(period.CreatedByUserId, period.ApprovedByUserId);
            checks.Add("Accounting period requires a different maker/reviewer and is open for the exact month.");

            f.ActorId = maker;
            profile = await setup.CreateProfileAsync(new("SYNTHETIC September salary posting", "PKR", month, month.AddMonths(1),
                "TEST-SAL", "TEST-PAY", [new("SalaryAdjustment", "TEST-DED")], [new(StatutoryScheme.EOBI, "TEST-ER", "TEST-STAT")], Reason),
                "month-profile-create", context, default);
            await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => setup.ApproveProfileAsync(profile.Id,
                new(profile.Version, Reason), "month-profile-self-denied", context, default));
            f.ActorId = reviewer;
            profile = await setup.ApproveProfileAsync(profile.Id, new(profile.Version, Reason), "month-profile-approve", context, default);
            Assert.Equal("Approved", profile.State);
            Assert.NotEqual(profile.CreatedByUserId, profile.ApprovedByUserId);
            checks.Add("Posting profile uses approved accounts and rejects maker self-approval.");

            f.ActorId = maker;
            var registration = await statutory.AddRegistrationAsync(f.EmployeeId,
                new(StatutoryScheme.EOBI, "SYNTHETIC-EMPLOYEE-0001", month, Reason), "month-registration-create", context, default);
            var evidenceBytes = Encoding.UTF8.GetBytes("SYNTHETIC TEST ONLY\nEmployee contribution PKR 300; employer contribution PKR 1500. Fictional inputs; no legal rate certification.\n");
            Directory.CreateDirectory(OutputDirectory);
            var evidencePath = Path.Combine(OutputDirectory, "synthetic-contribution-input.txt");
            await File.WriteAllBytesAsync(evidencePath, evidenceBytes);
            var hash = Convert.ToHexString(SHA256.HashData(evidenceBytes));
            var coverage = await statutory.CreateCoverageAsync(f.EmployeeId,
                new(month, month.AddMonths(1), [StatutoryScheme.EOBI], evidencePath, hash, Reason), "month-coverage-create", context, default);
            await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => statutory.ApproveCoverageAsync(f.EmployeeId, coverage.Id,
                new(coverage.Version, Reason), "month-coverage-self-denied", context, default));
            f.ActorId = reviewer;
            coverage = await statutory.ApproveCoverageAsync(f.EmployeeId, coverage.Id, new(coverage.Version, Reason), "month-coverage-approve", context, default);
            Assert.NotEqual(coverage.CreatedByUserId, coverage.ApprovedByUserId);
            checks.Add("Employee has registered, independently approved full-month statutory coverage with retained private proof.");

            f.ActorId = maker;
            var request = new CreateContributionRequest(StatutoryScheme.EOBI, month, 300m, 1500m, "PKR", "TEST-STAT", Reason,
                "SYNTHETIC-EMPLOYER-0001", Entries: [new(f.EmployeeId, 300m, 1500m, evidencePath, hash)], Reason: Reason);
            contribution = await statutory.CreateAsync(request, "month-contribution-create", context, default);
            var entry = Assert.Single(contribution.Entries!);
            Assert.Equal(registration.Id, entry.RegistrationId);
            Assert.Equal(f.EmployeeId, entry.EmployeeId);
            await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => statutory.CreateAsync(request, "month-contribution-duplicate", context, default));
            await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => statutory.ApproveAsync(contribution.Id,
                new(contribution.Version, Reason), "month-contribution-self-denied", context, default));
            f.ActorId = reviewer;
            contribution = await statutory.ApproveAsync(contribution.Id, new(contribution.Version, Reason), "month-contribution-approve", context, default);
            Assert.Equal("Approved", contribution.State);
            Assert.NotEqual(contribution.CreatedByUserId, contribution.ApprovedByUserId);
            Assert.Equal(300m, contribution.EmployeeContributionAmount);
            Assert.Equal(1500m, contribution.EmployerContributionAmount);
            checks.Add("Actual employee contribution entry is independently approved; duplicate scheme/month and self-approval are rejected.");
        }
        finally { f.ActorId = originalActor; }
    }

    public async Task<PayrollMonthFinanceResult> FinalizeAndReconcileAsync(PayrollMonthAcceptanceFixture f, Guid batchId)
    {
        var originalActor = f.ActorId;
        var context = PayrollMonthAcceptanceFixture.Context;
        var month = PayrollMonthAcceptanceFixture.Month;
        try
        {
            f.ActorId = reviewer;
            var source = await frozen.ReadAsync(batchId, default);
            Assert.Single(source.Employees);
            Assert.Equal(33000m, source.Lines.Where(x => x.Amount > 0).Sum(x => x.Amount));
            Assert.Equal(32200m, source.Lines.Sum(x => x.Amount));
            Assert.Equal(-300m, Assert.Single(source.Lines, x => x.SourceType == nameof(StatutoryContributionEntry)).Amount);
            var finalize = new FinalizeObligationRequest(batchId, 33000m, 32200m, "PKR", source.Fingerprint, Reason);
            await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => accounting.FinalizeObligationAsync(
                finalize with { TotalGrossAmount = 33001m }, "month-finalize-wrong-total", context, default));
            await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => accounting.FinalizeObligationAsync(
                finalize with { ExpectedSourceFingerprint = new string('0', 64) }, "month-finalize-wrong-source", context, default));
            var obligation = await accounting.FinalizeObligationAsync(finalize, "month-finalize", context, default);
            Assert.Equal("Finalized", obligation.State);
            Assert.Equal(800m, obligation.TotalDeductions);
            Assert.Equal(obligation.Id, (await accounting.FinalizeObligationAsync(finalize, "month-finalize", context, default)).Id);
            checks.Add("Finalized obligation matches exact frozen source and 33000 gross / 800 deductions / 32200 net; changed totals/fingerprint rejected and receipt replay retained.");

            var accrue = new PostAccrualJournalRequest(obligation.Id, Description: Reason,
                PostingProfileId: profile.Id, PostingDate: month.AddMonths(1).AddDays(-1), Reason: Reason);
            var outside = await Assert.ThrowsAsync<AuthenticationFailure>(() => accounting.PostAccrualJournalAsync(
                accrue with { PostingDate = month.AddMonths(1) }, "month-accrual-outside-period", context, default));
            Assert.Equal("payroll_accounting_period_not_open", outside.Code);
            var accrual = await accounting.PostAccrualJournalAsync(accrue, "month-accrual", context, default);
            Assert.Equal(accrual.Id, (await accounting.PostAccrualJournalAsync(accrue, "month-accrual", context, default)).Id);
            await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => accounting.PostAccrualJournalAsync(accrue, "month-accrual-duplicate", context, default));
            checks.Add("Salary accrual posts once, receipt replay is stable, fresh duplicate posting and date outside open period are rejected.");

            var post = new PostContributionRequest(obligation.Id, profile.Id, month.AddMonths(1).AddDays(-1), contribution.Version, Reason);
            contribution = await statutory.PostAsync(contribution.Id, post, "month-statutory-post", context, default);
            Assert.Equal("Posted", contribution.State);
            Assert.Equal(obligation.Id, contribution.ObligationId);
            Assert.NotNull(contribution.PostingJournalEntryId);
            Assert.Equal(contribution.PostingJournalEntryId,
                (await statutory.PostAsync(contribution.Id, post, "month-statutory-post", context, default)).PostingJournalEntryId);
            await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => accounting.PostStatutoryJournalAsync(
                new(obligation.Id, StatutoryContributionId: contribution.Id, PostingProfileId: profile.Id, PostingDate: month, Reason: Reason),
                "month-statutory-duplicate", context, default));
            checks.Add("Employer contribution posts separately once at 1500, links to obligation, and cannot reduce employee net pay.");

            // Independently query persisted SQL rows; do not use service responses as the reconciliation source.
            var journals = await f.Db.PayrollJournalEntries.AsNoTracking().Where(x => x.ObligationId == obligation.Id).OrderBy(x => x.SourceKey).ToArrayAsync();
            Assert.Equal(4, journals.Length);
            Assert.Equal(33000m, journals.Where(x => x.DebitAccountCode == "TEST-SAL").Sum(x => x.Amount));
            Assert.Equal(1500m, journals.Where(x => x.DebitAccountCode == "TEST-ER").Sum(x => x.Amount));
            Assert.Equal(32200m, journals.Where(x => x.CreditAccountCode == "TEST-PAY").Sum(x => x.Amount));
            Assert.Equal(500m, journals.Where(x => x.CreditAccountCode == "TEST-DED").Sum(x => x.Amount));
            Assert.Equal(1800m, journals.Where(x => x.CreditAccountCode == "TEST-STAT").Sum(x => x.Amount));
            Assert.All(journals, row => { Assert.Equal(period.Id, row.AccountingPeriodId); Assert.Equal(profile.Id, row.PostingProfileId); Assert.Equal("PKR", row.Currency); });
            Assert.Equal(journals.Length, journals.Select(x => x.SourceKey).Distinct().Count());
            var debit = journals.GroupBy(x => x.DebitAccountCode).Sum(g => g.Sum(x => x.Amount));
            var credit = journals.GroupBy(x => x.CreditAccountCode).Sum(g => g.Sum(x => x.Amount));
            Assert.Equal(34500m, debit);
            Assert.Equal(debit, credit);
            Assert.Empty(await f.Db.SalaryPayments.AsNoTracking().Where(x => x.ObligationId == obligation.Id).ToArrayAsync());
            checks.Add("Independent SQL reconciliation: four journals; 34500 debit = 34500 credit; salary payable 32200, fixed deductions 500, statutory payable 1800; no payment rows.");

            var entitlement = Assert.Single(await accounting.GetBatchEmployeesAsync(obligation.Id, context, default));
            Assert.Equal(f.EmployeeId, entitlement.EmployeeId);
            Assert.Equal(32200m, entitlement.NetEntitlement);
            Assert.Equal(0m, entitlement.DisbursedAmount);
            Assert.Equal(32200m, entitlement.RemainingBalance);
            var schedule = await statutory.ExportScheduleCsvAsync(contribution.Id, context, default);
            var csv = Encoding.UTF8.GetString(schedule.Bytes);
            Assert.Contains(f.EmployeeId.ToString(), csv);
            Assert.Contains("300.00,1500.00,1800.00,PKR,Posted", csv);
            await File.WriteAllBytesAsync(Path.Combine(OutputDirectory, "SYNTHETIC-" + schedule.FileName), schedule.Bytes);
            checks.Add("Actual service employee/month schedule exports 300 employee + 1500 employer = 1800; salary remains entirely unpaid.");

            period = await setup.DecidePeriodAsync(period.Id, new(period.Version, Reason), true, "month-period-close", context, default);
            var closed = await Assert.ThrowsAsync<AuthenticationFailure>(() => accounting.PostAccrualJournalAsync(accrue, "month-accrual-closed-period", context, default));
            Assert.Equal("payroll_accounting_period_not_open", closed.Code);
            Assert.Equal(4, await f.Db.PayrollJournalEntries.CountAsync(x => x.ObligationId == obligation.Id));
            checks.Add("After closing the period, a fresh posting request is rejected and the four persisted journals remain unchanged.");

            var result = new PayrollMonthFinanceResult(obligation.Id, contribution.Id, profile.Id, period.Id, source.Fingerprint,
                obligation.TotalGrossAmount, obligation.TotalDeductions, obligation.TotalNetAmount, contribution.EmployeeContributionAmount,
                contribution.EmployerContributionAmount, debit, credit, journals.Length, period.State, checks.ToArray());
            await File.WriteAllTextAsync(Path.Combine(OutputDirectory, "finance-reconciliation.json"), JsonSerializer.Serialize(new {
                Label = Reason, Result = result, Journals = journals.Select(x => new { x.Id, x.Kind, x.DebitAccountCode, x.CreditAccountCode,
                    x.Amount, x.Currency, x.PostingDate, x.AccountingPeriodId, x.PostingProfileId, x.SourceKey, x.StatutoryContributionId }) }, new JsonSerializerOptions { WriteIndented = true }));
            var journalCsv = new StringBuilder("Label,JournalId,DebitAccount,CreditAccount,Amount,Currency,PostingDate,SourceKey\n");
            foreach (var row in journals) journalCsv.AppendLine(FormattableString.Invariant($"SYNTHETIC TEST ONLY,{row.Id},{row.DebitAccountCode},{row.CreditAccountCode},{row.Amount:F2},{row.Currency},{row.PostingDate:yyyy-MM-dd},{row.SourceKey}"));
            await File.WriteAllTextAsync(Path.Combine(OutputDirectory, "SYNTHETIC-journals.csv"), journalCsv.ToString());
            return result;
        }
        finally { f.ActorId = originalActor; }
    }
}

internal sealed record PayrollMonthFinanceResult(Guid ObligationId, Guid ContributionId, Guid PostingProfileId, Guid AccountingPeriodId,
    string FrozenSourceFingerprint, decimal Gross, decimal Deductions, decimal Net, decimal EmployeeContribution, decimal EmployerContribution,
    decimal TotalDebits, decimal TotalCredits, int JournalCount, string PeriodState, IReadOnlyList<string> Checks);
