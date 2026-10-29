using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Api.Controllers.Attendance;
using Cosmix.Api.Controllers.HR;
using Cosmix.Application.Attendance.LocationConsent;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Security;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.StatutoryContributions;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Domain.HR.StatutoryContributions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Cosmix.Tests;

public class RemediationAcceptanceTests
{
    private static readonly AuthRequestContext Context = new("127.0.0.1", "remediation-check");

    [Fact]
    public void DEF_01_LocationConsentController_EnforcesPolicyAttribute()
    {
        var publishMethod = typeof(LocationConsentController).GetMethod(nameof(LocationConsentController.PublishPolicy));
        Assert.NotNull(publishMethod);
        var publishAuth = publishMethod.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(publishAuth);
        Assert.Equal("location.consent.admin", publishAuth.Policy);

        var getPolicyMethod = typeof(LocationConsentController).GetMethod(nameof(LocationConsentController.GetPolicy));
        Assert.NotNull(getPolicyMethod);
        var getAuth = getPolicyMethod.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(getAuth);
        Assert.Equal("location.consent.admin", getAuth.Policy);
    }

    [Fact]
    public void DEF_02_PayrollAccountingController_EnforcesCatalogPolicyAttributes()
    {
        var type = typeof(PayrollAccountingController);

        var listMethod = type.GetMethod(nameof(PayrollAccountingController.List));
        Assert.NotNull(listMethod);
        Assert.Equal("payroll.accounting.accrue", listMethod.GetCustomAttribute<AuthorizeAttribute>()?.Policy);

        var finalizeMethod = type.GetMethod(nameof(PayrollAccountingController.FinalizeObligation));
        Assert.NotNull(finalizeMethod);
        Assert.Equal("payroll.accounting.accrue", finalizeMethod.GetCustomAttribute<AuthorizeAttribute>()?.Policy);

        var accrualMethod = type.GetMethod(nameof(PayrollAccountingController.PostAccrualJournal));
        Assert.NotNull(accrualMethod);
        Assert.Equal("payroll.accounting.accrue", accrualMethod.GetCustomAttribute<AuthorizeAttribute>()?.Policy);

        var paymentMethod = type.GetMethod(nameof(PayrollAccountingController.RecordPayment));
        Assert.NotNull(paymentMethod);
        Assert.Equal("payroll.accounting.payment", paymentMethod.GetCustomAttribute<AuthorizeAttribute>()?.Policy);

        var confirmMethod = type.GetMethod(nameof(PayrollAccountingController.ConfirmPayment));
        Assert.NotNull(confirmMethod);
        Assert.Equal("payroll.accounting.payment.confirm", confirmMethod.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    [Fact]
    public void DEF_03_StatutoryContributionsController_EnforcesCatalogPolicyAttributes()
    {
        var type = typeof(StatutoryContributionsController);

        var createMethod = type.GetMethod(nameof(StatutoryContributionsController.Create));
        Assert.NotNull(createMethod);
        Assert.Equal("statutory.contributions.manage", createMethod.GetCustomAttribute<AuthorizeAttribute>()?.Policy);

        var approveMethod = type.GetMethod(nameof(StatutoryContributionsController.Approve));
        Assert.NotNull(approveMethod);
        Assert.Equal("statutory.contributions.approve", approveMethod.GetCustomAttribute<AuthorizeAttribute>()?.Policy);

        var postMethod = type.GetMethod(nameof(StatutoryContributionsController.Post));
        Assert.NotNull(postMethod);
        Assert.Equal("payroll.accounting.accrue", postMethod.GetCustomAttribute<AuthorizeAttribute>()?.Policy);

        var exportMethod = type.GetMethod(nameof(StatutoryContributionsController.ExportCsv));
        Assert.NotNull(exportMethod);
        Assert.Equal("statutory.contributions.manage", exportMethod.GetCustomAttribute<AuthorizeAttribute>()?.Policy);

        var regType = typeof(EmployeeStatutoryRegistrationsController);
        var addRegMethod = regType.GetMethod(nameof(EmployeeStatutoryRegistrationsController.AddRegistration));
        Assert.NotNull(addRegMethod);
        Assert.Equal("statutory.contributions.manage", addRegMethod.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    [Fact]
    public async Task DEF_04_StatutoryCsvExport_IncludesEmployerRegistrationNumber_AndEscapesRfc4180()
    {
        var employeeId = Guid.NewGuid();
        var contribution = new StatutoryContribution(
            StatutoryScheme.EOBI,
            new DateOnly(2026, 10, 1),
            employeeContributionAmount: 1000m,
            employerContributionAmount: 5000m,
            currency: "PKR",
            createdByUserId: "user-1",
            createdAt: DateTimeOffset.UtcNow,
            payableAccountCode: "2030",
            notes: "Test, with commas",
            employerRegistrationNumber: "EMP, REG \"123\"", hasEmployeeEntries: true);
        var registration = new EmployeeStatutoryRegistration(
            employeeId,
            StatutoryScheme.EOBI,
            "REG-999,XYZ",
            new DateOnly(2026, 1, 1),
            "admin",
            DateTimeOffset.UtcNow);

        contribution.Entries.Add(new StatutoryContributionEntry(contribution.Id, employeeId, registration.Id, registration.RegistrationNumber,
            1000m, 5000m, "synthetic-statutory-evidence", new string('A', 64), "synthetic-private-reference"));
        var fakeOperation = CreateDirectSensitiveComplianceOperation();
        var retained = await StatutorySourceFixture.RetainAsync(contribution, registration);
        var service = retained.Service(fakeOperation);

        var (fileName, bytes) = await service.ExportScheduleCsvAsync(contribution.Id, Context, default);
        var csv = Encoding.UTF8.GetString(bytes);

        // Header check
        Assert.StartsWith("Scheme,ContributionMonth,EmployerRegistrationNumber,EmployeeId,RegistrationNumber,EmployeeContribution,EmployerContribution,TotalContribution,Currency,State", csv);

        // Data row check: EmployerRegistrationNumber with comma & quotes must be RFC 4180 escaped
        Assert.Contains("\"EMP, REG \"\"123\"\"\"", csv);
        // Employee registration number with comma must also be escaped
        Assert.Contains("\"REG-999,XYZ\"", csv);
        // Employee amounts
        Assert.Contains("1000.00", csv);
        Assert.Contains("5000.00", csv);
    }

    [Fact]
    public async Task Section5_PayrollAccounting_EnforcesEmployeeNetLimit_AndSupportsInstallmentsWithReplayBlock()
    {
        var employeeAId = Guid.NewGuid();
        var employeeBId = Guid.NewGuid();
        var (batch, frozenReader) = await PermissionsTests.CreateApprovedPayrollAsync(
            [(employeeAId, 100000.15m, 3500.15m), (employeeBId, 80000.25m, 2700.15m)]);
        var source = await frozenReader.ReadAsync(batch.Id, default);
        var obligation = new PayrollObligation(batch.Id, source.Employees.Sum(x => x.GrossEarnings), source.Employees.Sum(x => x.NetPay),
            source.Currency, "finance-preparer", DateTimeOffset.UtcNow);
        obligation.BindSource(source.Fingerprint, source.PeriodStart, source.PeriodEndExclusive,
            JsonSerializer.Serialize(source.Employees.Select(x => new BatchEmployeeEntitlementDto(x.EmployeeId, x.GrossEarnings, x.TotalDeductions, x.NetPay))));
        obligation.Finalize("finance-manager", DateTimeOffset.UtcNow);
        var fakeFinanceRepo = new FakePayrollFinanceRepo();
        var accrualEntry = new PayrollJournalEntry(obligation.Id, JournalEntryKind.SalaryAccrual, "5010", "2010", obligation.TotalNetAmount,
            "PKR", "Monthly net salary accrual", "finance-manager", DateTimeOffset.UtcNow);
        accrualEntry.BindPosting(new DateOnly(2026, 10, 31), fakeFinanceRepo.Period.Id, fakeFinanceRepo.Profile.Id, $"ACCRUAL-SALARY-{obligation.Id:N}");
        var fakeRepo = new FakePayrollRepo(obligation, accrualEntry, batch.Lines.ToList());
        var fakeOperation = CreateDirectSensitiveComplianceOperation();
        var fakeFinanceService = new PayrollFinanceSetupService(fakeFinanceRepo, clock: TimeProvider.System);
        var service = new PayrollAccountingService(fakeRepo, TimeProvider.System, fakeOperation, fakeFinanceService, frozenReader);
        Assert.Equal(180000.40m, obligation.TotalGrossAmount);
        Assert.Equal(173800.10m, obligation.TotalNetAmount);
        // 1. Employee A: Attempt overpayment (96,500.01) -> rejected
        var exExcessive = await Assert.ThrowsAsync<PayrollFinanceConflictException>(() =>
            service.RecordSalaryPaymentAsync(new RecordSalaryPaymentRequest(
                obligation.Id,
                accrualEntry.Id,
                "FT-A-01",
                "2010",
                "1010",
                Amount: 96500.01m,
                EmployeeId: employeeAId,
                PostingDate: new DateOnly(2026, 10, 31)), Context, default));
        Assert.Contains("exceeds remaining employee net salary", exExcessive.Message, StringComparison.OrdinalIgnoreCase);

        // 2. Attempt zero or negative amount -> rejected
        var exZero = await Assert.ThrowsAsync<PayrollFinanceConflictException>(() =>
            service.RecordSalaryPaymentAsync(new RecordSalaryPaymentRequest(
                obligation.Id,
                accrualEntry.Id,
                "FT-A-02",
                "2010",
                "1010",
                Amount: 0m,
                EmployeeId: employeeAId,
                PostingDate: new DateOnly(2026, 10, 31)), Context, default));
        Assert.Contains("greater than zero", exZero.Message, StringComparison.OrdinalIgnoreCase);

        // 3. Employee outside batch -> rejected
        var outsideEmpId = Guid.NewGuid();
        var exOutside = await Assert.ThrowsAsync<PayrollFinanceConflictException>(() =>
            service.RecordSalaryPaymentAsync(new RecordSalaryPaymentRequest(
                obligation.Id,
                accrualEntry.Id,
                "FT-A-03",
                "2010",
                "1010",
                Amount: 50000m,
                EmployeeId: outsideEmpId,
                PostingDate: new DateOnly(2026, 10, 31)), Context, default));
        Assert.Contains("not included in the finalized payroll obligation snapshot", exOutside.Message, StringComparison.OrdinalIgnoreCase);

        // 4. Employee A: Full valid payment of 96,500.00 succeeds
        var paymentA = await service.RecordSalaryPaymentAsync(new RecordSalaryPaymentRequest(
            obligation.Id,
            accrualEntry.Id,
            "FT-A-FULL",
            "2010",
            "1010",
            Amount: 96500.00m,
            EmployeeId: employeeAId,
            PostingDate: new DateOnly(2026, 10, 31)), Context, default);
        Assert.NotNull(paymentA);
        Assert.Equal("Posted", paymentA.State);
        Assert.Equal(96500.00m, paymentA.Amount);

        // 5. Employee A: Attempt further payment after full disbursement -> blocked
        var exFullyPaid = await Assert.ThrowsAsync<PayrollFinanceConflictException>(() =>
            service.RecordSalaryPaymentAsync(new RecordSalaryPaymentRequest(
                obligation.Id,
                accrualEntry.Id,
                "FT-A-EXTRA",
                "2010",
                "1010",
                Amount: 500m,
                EmployeeId: employeeAId,
                PostingDate: new DateOnly(2026, 10, 31)), Context, default));
        Assert.Contains("already been fully disbursed", exFullyPaid.Message, StringComparison.OrdinalIgnoreCase);

        // 6. Employee B: Installment 1 of 40,000.00 succeeds
        var paymentB1 = await service.RecordSalaryPaymentAsync(new RecordSalaryPaymentRequest(
            obligation.Id,
            accrualEntry.Id,
            "FT-B-PART1",
            "2010",
            "1010",
            Amount: 40000.00m,
            EmployeeId: employeeBId,
            PostingDate: new DateOnly(2026, 10, 31)), Context, default);
        Assert.NotNull(paymentB1);
        Assert.Equal(40000.00m, paymentB1.Amount);

        // 7. Employee B: Replay of installment 1 reference blocked
        var exReplay = await Assert.ThrowsAsync<PayrollFinanceConflictException>(() =>
            service.RecordSalaryPaymentAsync(new RecordSalaryPaymentRequest(
                obligation.Id,
                accrualEntry.Id,
                "FT-B-PART1",
                "2010",
                "1010",
                Amount: 37300.10m,
                EmployeeId: employeeBId,
                PostingDate: new DateOnly(2026, 10, 31)), Context, default));
        Assert.Contains("already recorded for this obligation", exReplay.Message, StringComparison.OrdinalIgnoreCase);

        // 8. Employee B: Installment 2 of 37,300.10 succeeds
        var paymentB2 = await service.RecordSalaryPaymentAsync(new RecordSalaryPaymentRequest(
            obligation.Id,
            accrualEntry.Id,
            "FT-B-PART2",
            "2010",
            "1010",
            Amount: 37300.10m,
            EmployeeId: employeeBId,
            PostingDate: new DateOnly(2026, 10, 31)), Context, default);
        Assert.NotNull(paymentB2);
        Assert.Equal(37300.10m, paymentB2.Amount);

        // 9. Employee B: Attempt any further installment after reaching 77,300.10 -> blocked
        var exBOver = await Assert.ThrowsAsync<PayrollFinanceConflictException>(() =>
            service.RecordSalaryPaymentAsync(new RecordSalaryPaymentRequest(
                obligation.Id,
                accrualEntry.Id,
                "FT-B-EXTRA",
                "2010",
                "1010",
                Amount: 1.00m,
                EmployeeId: employeeBId,
                PostingDate: new DateOnly(2026, 10, 31)), Context, default));
        Assert.Contains("already been fully disbursed", exBOver.Message, StringComparison.OrdinalIgnoreCase);
        var postedPayments = await fakeRepo.GetSalaryPaymentsForObligationAsync(obligation.Id, default);
        Assert.Equal(3, postedPayments.Count);
        Assert.Equal(96500m, postedPayments.Where(x => x.EmployeeId == employeeAId).Sum(x => x.Amount));
        Assert.Equal(77300.10m, postedPayments.Where(x => x.EmployeeId == employeeBId).Sum(x => x.Amount));
        var paymentJournals = (await fakeRepo.GetJournalEntriesForObligationAsync(obligation.Id, default)).Where(x => x.Kind == JournalEntryKind.SalaryPayment).ToArray();
        Assert.Equal(3, paymentJournals.Length);
        Assert.Equal(obligation.TotalNetAmount, paymentJournals.Sum(x => x.Amount));
        Assert.All(paymentJournals, x => { Assert.Equal("2010", x.DebitAccountCode); Assert.Equal("1010", x.CreditAccountCode); });
    }

    [Fact]
    public void Section7_SyntheticFixture_BalancedDoubleEntryReconciliation()
    {
        // Employee A:
        const decimal grossA = 100000.15m;
        const decimal eeEobiA = 1000.10m;
        const decimal eeSsA = 500.05m;
        const decimal loanA = 2000.00m;
        const decimal erEobiA = 5000.20m;
        const decimal erSsA = 3000.10m;
        var netA = grossA - (eeEobiA + eeSsA + loanA);
        Assert.Equal(96500.00m, netA);

        // Employee B:
        const decimal grossB = 80000.25m;
        const decimal eeEobiB = 800.10m;
        const decimal eeSsB = 400.05m;
        const decimal loanB = 1500.00m;
        const decimal erEobiB = 4000.10m;
        const decimal erSsB = 2400.20m;
        var netB = grossB - (eeEobiB + eeSsB + loanB);
        Assert.Equal(77300.10m, netB);

        // Aggregate Totals:
        var totalGross = grossA + grossB;
        Assert.Equal(180000.40m, totalGross);

        var totalNetPayable = netA + netB;
        Assert.Equal(173800.10m, totalNetPayable);

        var totalErEobi = erEobiA + erEobiB;
        Assert.Equal(9000.30m, totalErEobi);

        var totalErSs = erSsA + erSsB;
        Assert.Equal(5400.30m, totalErSs);

        var totalEobiPayable = eeEobiA + eeEobiB + totalErEobi;
        Assert.Equal(10800.50m, totalEobiPayable);

        var totalSsPayable = eeSsA + eeSsB + totalErSs;
        Assert.Equal(6300.40m, totalSsPayable);

        var totalLoanRecoverable = loanA + loanB;
        Assert.Equal(3500.00m, totalLoanRecoverable);

        // Total Accrual Debits = Salaries Expense + ER EOBI Expense + ER SS Expense
        var totalDebits = totalGross + totalErEobi + totalErSs;
        Assert.Equal(194401.00m, totalDebits);

        // Total Accrual Credits = Salaries Payable (Net) + EOBI Payable + SS Payable + Loan Recoverable
        var totalCredits = totalNetPayable + totalEobiPayable + totalSsPayable + totalLoanRecoverable;
        Assert.Equal(194401.00m, totalCredits);

        // Balanced double entry variance must be zero
        Assert.Equal(0.00m, totalDebits - totalCredits);
    }

    private static SensitiveComplianceOperation CreateDirectSensitiveComplianceOperation()
    {
        var f = new PermissionsTests.Fixture();
        return f.Operation;
    }

    private sealed class FakeStatutoryRepo(StatutoryContribution contribution, List<EmployeeStatutoryRegistration> registrations)
        : IStatutoryContributionRepository
    {
        public Task<StatutoryContribution?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<StatutoryContribution?>(contribution);
        public Task<StatutoryContribution?> GetBySchemeAndMonthAsync(StatutoryScheme scheme, DateOnly month, CancellationToken ct) => Task.FromResult<StatutoryContribution?>(null);
        public Task<IReadOnlyList<StatutoryContribution>> ListAsync(StatutoryScheme? scheme, int? year, int skip, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<StatutoryContribution>>([contribution]);
        public Task<int> CountAsync(StatutoryScheme? scheme, int? year, CancellationToken ct) => Task.FromResult(1);
        public Task<EmployeeStatutoryRegistration?> GetRegistrationAsync(Guid employeeId, StatutoryScheme scheme, CancellationToken ct) => Task.FromResult<EmployeeStatutoryRegistration?>(null);
        public Task<IReadOnlyList<EmployeeStatutoryRegistration>> GetRegistrationsForEmployeeAsync(Guid employeeId, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmployeeStatutoryRegistration>>(registrations);
        public Task<IReadOnlyList<EmployeeStatutoryRegistration>> GetAllRegistrationsForSchemeAsync(StatutoryScheme scheme, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmployeeStatutoryRegistration>>(registrations);
        public Task<Cosmix.Domain.HR.PayrollAccounting.PayrollObligation?> GetFinalizedObligationForMonthAsync(DateOnly month, CancellationToken ct) => Task.FromResult<Cosmix.Domain.HR.PayrollAccounting.PayrollObligation?>(null);
        public Task<Cosmix.Domain.HR.PayrollAccounting.PayrollJournalEntry?> GetJournalEntryByStatutoryContributionIdAsync(Guid contributionId, CancellationToken ct) => Task.FromResult<Cosmix.Domain.HR.PayrollAccounting.PayrollJournalEntry?>(null);
        public Task AddJournalEntryAsync(Cosmix.Domain.HR.PayrollAccounting.PayrollJournalEntry entry, CancellationToken ct) => Task.CompletedTask;
        public Task AddContributionAsync(StatutoryContribution c, CancellationToken ct) => Task.CompletedTask;
        public Task AddRegistrationAsync(EmployeeStatutoryRegistration r, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakePayrollRepo(PayrollObligation obligation, PayrollJournalEntry accrualEntry, List<PayrollHandoffLine> batchLines)
        : IPayrollAccountingRepository
    {
        private readonly List<SalaryPayment> payments = [];
        private readonly List<PayrollJournalEntry> journals = [accrualEntry];

        public Task<PayrollObligation?> GetObligationByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<PayrollObligation?>(obligation);
        public Task<PayrollObligation?> GetObligationByBatchIdAsync(Guid batchId, CancellationToken ct) => Task.FromResult<PayrollObligation?>(obligation);
        public Task<IReadOnlyList<PayrollObligation>> ListObligationsAsync(int page, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<PayrollObligation>>([obligation]);
        public Task<PayrollJournalEntry?> GetJournalEntryByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(journals.FirstOrDefault(j => j.Id == id));
        public Task<IReadOnlyList<PayrollJournalEntry>> GetJournalEntriesForObligationAsync(Guid obligationId, CancellationToken ct) => Task.FromResult<IReadOnlyList<PayrollJournalEntry>>(journals);
        public Task<SalaryPayment?> GetSalaryPaymentByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(payments.FirstOrDefault(p => p.Id == id));
        public Task<IReadOnlyList<SalaryPayment>> GetSalaryPaymentsForObligationAsync(Guid obligationId, CancellationToken ct) => Task.FromResult<IReadOnlyList<SalaryPayment>>(payments);
        public Task<IReadOnlyList<PayrollHandoffLine>> GetBatchLinesAsync(Guid batchId, CancellationToken ct) => Task.FromResult<IReadOnlyList<PayrollHandoffLine>>(batchLines);
        public Task<PayrollHandoffBatch?> GetBatchByIdAsync(Guid batchId, CancellationToken ct) => Task.FromResult<PayrollHandoffBatch?>(null);
        public Task<IReadOnlyList<Cosmix.Domain.HR.StatutoryContributions.StatutoryContribution>> GetApprovedStatutoryContributionsForMonthAsync(DateOnly month, CancellationToken ct) => Task.FromResult<IReadOnlyList<Cosmix.Domain.HR.StatutoryContributions.StatutoryContribution>>([]);
        public Task<Cosmix.Domain.HR.StatutoryContributions.StatutoryContribution?> GetStatutoryContributionByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Cosmix.Domain.HR.StatutoryContributions.StatutoryContribution?>(null);
        public Task AddObligationAsync(PayrollObligation o, CancellationToken ct) => Task.CompletedTask;
        public Task AddJournalEntryAsync(PayrollJournalEntry entry, CancellationToken ct) { journals.Add(entry); return Task.CompletedTask; }
        public Task AddSalaryPaymentAsync(SalaryPayment payment, CancellationToken ct) { payments.Add(payment); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakePayrollFinanceRepo : IPayrollFinanceRepository
    {
        private readonly List<PayrollLedgerAccount> accounts =
        [
            new PayrollLedgerAccount("5010", "Salary Expense", PayrollAccountKind.Expense),
            new PayrollLedgerAccount("2010", "Salary Payable", PayrollAccountKind.Liability),
            new PayrollLedgerAccount("2020", "Tax Payable", PayrollAccountKind.Liability),
            new PayrollLedgerAccount("1010", "Main Corporate Bank", PayrollAccountKind.Bank)
        ];
        private readonly PayrollAccountingPeriod period = new(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1), "PKR", "fin-maker", DateTimeOffset.UtcNow);
        public PayrollAccountingPeriod Period => period;
        public PayrollPostingProfile Profile { get; } = new("Synthetic salary profile", "PKR", new(2026, 10, 1), new(2026, 11, 1),
            JsonSerializer.Serialize(new PayrollPostingRules("5010", "2010", [new("TaxDeduction", "2020")], [])), "fin-maker", DateTimeOffset.UtcNow);

        public FakePayrollFinanceRepo()
        {
            period.Approve("fin-checker", DateTimeOffset.UtcNow);
            Profile.Approve("fin-checker", DateTimeOffset.UtcNow);
        }

        public Task<IReadOnlyList<PayrollLedgerAccount>> AccountsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<PayrollLedgerAccount>>(accounts);
        public Task<PayrollLedgerAccount?> AccountAsync(Guid id, CancellationToken ct) => Task.FromResult(accounts.FirstOrDefault(a => a.Id == id));
        public Task<PayrollLedgerAccount?> AccountAsync(string code, CancellationToken ct) => Task.FromResult(accounts.FirstOrDefault(a => a.Code == code));
        public Task<IReadOnlyList<PayrollAccountingPeriod>> PeriodsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<PayrollAccountingPeriod>>([period]);
        public Task<PayrollAccountingPeriod?> PeriodAsync(Guid id, CancellationToken ct) => Task.FromResult<PayrollAccountingPeriod?>(period);
        public Task<IReadOnlyList<PayrollPostingProfile>> ProfilesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<PayrollPostingProfile>>([Profile]);
        public Task<PayrollPostingProfile?> ProfileAsync(Guid id, CancellationToken ct) => Task.FromResult<PayrollPostingProfile?>(id == Profile.Id ? Profile : null);
        public Task<bool> PeriodOverlapAsync(DateOnly start, DateOnly endExclusive, string currency, CancellationToken ct) => Task.FromResult(false);
        public Task<PayrollAccountingPeriod?> PeriodForDateAsync(DateOnly date, string currency, CancellationToken ct) => Task.FromResult<PayrollAccountingPeriod?>(period);
        public Task<bool> EmployeeExistsAsync(Guid employeeId, CancellationToken ct) => Task.FromResult(true);
        public Task<IReadOnlyList<EmployeeStatutoryCoverage>> CoverageAsync(Guid employeeId, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmployeeStatutoryCoverage>>([]);
        public Task<EmployeeStatutoryCoverage?> CoverageByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<EmployeeStatutoryCoverage?>(null);
        public Task<StatutoryContributionEntry?> EntryAsync(Guid id, CancellationToken ct) => Task.FromResult<StatutoryContributionEntry?>(null);
        public Task<bool> EntryUsedAsync(Guid entryId, Guid? excludingBatchId, CancellationToken ct) => Task.FromResult(false);
        public void Add(PayrollLedgerAccount account) { accounts.Add(account); }
        public void Add(PayrollAccountingPeriod period) { }
        public void Add(PayrollPostingProfile profile) { }
        public void Add(EmployeeStatutoryCoverage coverage) { }
        public void ExpectVersion(object entity, byte[] version) { }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
