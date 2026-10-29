using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Application.HR.StatutoryContributions;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Domain.HR.StatutoryContributions;
using Xunit;
using Cosmix.Application.Finance.Costing;
using Cosmix.Application.Authentication;

namespace Cosmix.Tests;

public sealed class PayrollFinanceSourceTests
{
    [Fact]
    public async Task Project_cost_uses_actual_salary_expense_and_requires_employer_contribution_posting()
    {
        var f = new Fixture(); await f.AddStatutory(20, 100); await f.Finalize(); await f.Accrue();
        await Assert.ThrowsAsync<AuthenticationFailure>(() => f.CostSource.ReadAsync(f.Source.BatchId, default));
        await Invoke<JournalEntryResponse?>(f.Service, "PostStatutoryJournalCoreAsync",
            new PostStatutoryJournalRequest(f.Obligation!.Id, StatutoryContributionId: f.Contribution!.Id,
                PostingProfileId: f.Profile.Id, PostingDate: f.Start));
        var basis = await f.CostSource.ReadAsync(f.Source.BatchId, default);
        Assert.Equal(1100m, Assert.Single(basis.Employees).Amount);
        Assert.Equal(830m, f.Obligation.TotalNetAmount);
    }
    [Fact]
    public async Task MissingFrozenReaderCannotTurnDatabaseLinesIntoSalaryAuthority()
    {
        var f = new Fixture(includeFrozenReader: false);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Finalize());
        Assert.Equal("Frozen payroll reader is required.", error.Message);
        Assert.Null(f.Obligation);
        Assert.Empty(f.Journals);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task MissingStatutoryRepositoryOrPrivateStoreCannotSkipEntryValidation(bool repository, bool files)
    {
        var f = new Fixture(includeFinanceRepository: repository, includeFiles: files);
        await f.AddStatutory(20, 100);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Finalize());
        Assert.Equal("Statutory source repository and evidence storage are required.", error.Message);
        Assert.Null(f.Obligation);
        Assert.Empty(f.Journals);
    }

    [Fact]
    public async Task FinalizationUsesOnlyFrozenLinesAndRejectsOneCentClientDifference()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Finalize(1000.01m));
        var result = await f.Finalize();
        Assert.Equal(1000, result.TotalGrossAmount);
        Assert.Equal(850, result.TotalNetAmount);
        Assert.Equal(150, result.TotalDeductions);
        Assert.Equal(f.Source.Fingerprint, result.SourceFingerprint);
        Assert.Equal(f.End, result.PeriodEndExclusive);
        Assert.Single(JsonSerializer.Deserialize<BatchEmployeeEntitlementDto[]>(f.Obligation!.EmployeeEntitlementsJson!)!);
    }

    [Fact]
    public async Task ExistingObligationCannotBeReplayedWithChangedSourceAssertion()
    {
        var f = new Fixture();
        await f.Finalize();
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => Invoke<ObligationResponse>(f.Service, "FinalizeObligationCoreAsync",
            new FinalizeObligationRequest(f.Source.BatchId, ExpectedSourceFingerprint: new string('B', 64))));
    }

    [Fact]
    public async Task ManagementLockAloneCannotFinalizeBeforeFinanceHandoff()
    {
        var f = new Fixture();
        typeof(PayrollHandoffBatch).GetProperty(nameof(PayrollHandoffBatch.ReviewState))!.SetValue(f.Batch, "locked");
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Finalize());
        Assert.Null(f.Obligation);
    }

    [Fact]
    public async Task AccrualBalancesFrozenGrossAcrossNetTaxAndLoanAccountsAndRejectsDuplicate()
    {
        var f = new Fixture();
        await f.Finalize();
        await f.Accrue();
        Assert.Equal(1000, f.Journals.Sum(x => x.Amount));
        Assert.All(f.Journals, x => Assert.Equal("SAL", x.DebitAccountCode));
        Assert.Equal(850, f.Journals.Single(x => x.CreditAccountCode == "PAY").Amount);
        Assert.Equal(100, f.Journals.Single(x => x.CreditAccountCode == "LOAN").Amount);
        Assert.Equal(50, f.Journals.Single(x => x.CreditAccountCode == "TAX").Amount);
        Assert.Equal(3, f.Journals.Select(x => x.SourceKey).Distinct().Count());
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Accrue());
        Assert.Equal(3, f.Journals.Count);
    }

    [Fact]
    public async Task ClosedPeriodRejectsPostingBeforeWritingAnyJournal()
    {
        var f = new Fixture();
        await f.Finalize();
        f.Period.Close("reviewer", Fixture.Now);
        await Assert.ThrowsAnyAsync<Exception>(() => f.Accrue());
        Assert.Empty(f.Journals);
    }

    [Fact]
    public async Task AccountNameCannotGrantDisbursementAuthority()
    {
        var f = new Fixture();
        f.Accounts.Add(new PayrollLedgerAccount("FAKE", "Approved Cash Bank Disbursement", PayrollAccountKind.Asset));
        await Assert.ThrowsAnyAsync<Exception>(() => f.Setup.RequireDisbursementAccountAsync("FAKE", default));
        await f.Setup.RequireDisbursementAccountAsync("BANK", default);
    }

    [Fact]
    public async Task PostingProfileMustCoverLastPayrollDayUsingExclusiveEnd()
    {
        var f = new Fixture();
        f.Profile = new PayrollPostingProfile("Ends early", "PKR", f.Start, f.End.AddDays(-1), JsonSerializer.Serialize(f.Rules), "maker", Fixture.Now);
        f.Profile.Approve("reviewer", Fixture.Now);
        await Assert.ThrowsAnyAsync<Exception>(() => f.Setup.ResolvePostingAsync(f.Profile.Id, f.Start, "PKR", f.Start, f.End, default));
    }

    [Fact]
    public async Task EmployerPostingIsSeparateFromNetAndUsesExactApprovedEmployeeEntries()
    {
        var f = new Fixture();
        await f.AddStatutory(20, 100);
        var result = await f.Finalize();
        Assert.Equal(1000, result.TotalGrossAmount);
        Assert.Equal(830, result.TotalNetAmount);
        await f.Accrue();
        var journal = await Invoke<JournalEntryResponse?>(f.Service, "PostStatutoryJournalCoreAsync",
            new PostStatutoryJournalRequest(f.Obligation!.Id, StatutoryContributionId: f.Contribution!.Id, PostingProfileId: f.Profile.Id, PostingDate: f.Start));
        Assert.NotNull(journal);
        Assert.Equal(100, journal.Amount);
        Assert.Equal("ER", journal.DebitAccountCode);
        Assert.Equal("STAT", journal.CreditAccountCode);
        Assert.Equal(20, f.Journals.Single(x => x.Kind == JournalEntryKind.SalaryAccrual && x.CreditAccountCode == "STAT").Amount);
        Assert.Equal(ContributionState.Posted, f.Contribution.State);
        Assert.Equal(f.Obligation.Id, f.Contribution.ObligationId);
        Assert.Equal(830, f.Obligation.TotalNetAmount);
    }

    [Fact]
    public async Task Employee_group_posts_only_its_statutory_share_and_keeps_remaining_month_open()
    {
        var f = new Fixture();
        await f.AddStatutory(20, 100, includeAnotherEmployee: true);
        await f.Finalize(); await f.Accrue();
        var journal = await Invoke<JournalEntryResponse?>(f.Service, "PostStatutoryJournalCoreAsync",
            new PostStatutoryJournalRequest(f.Obligation!.Id, StatutoryContributionId: f.Contribution!.Id,
                PostingProfileId: f.Profile.Id, PostingDate: f.Start));
        Assert.Equal(100m, journal!.Amount);
        Assert.Equal(ContributionState.Approved, f.Contribution.State);
        Assert.Null(f.Contribution.PostedAt);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => Invoke<JournalEntryResponse?>(f.Service,
            "PostStatutoryJournalCoreAsync", new PostStatutoryJournalRequest(f.Obligation.Id,
                StatutoryContributionId: f.Contribution.Id, PostingProfileId: f.Profile.Id, PostingDate: f.Start)));
        f.UseOtherEmployeeGroup();
        await f.Finalize(); await f.Accrue();
        var second = await Invoke<JournalEntryResponse?>(f.Service, "PostStatutoryJournalCoreAsync",
            new PostStatutoryJournalRequest(f.Obligation!.Id, StatutoryContributionId: f.Contribution.Id,
                PostingProfileId: f.Profile.Id, PostingDate: f.Start));
        Assert.Equal(200m, second!.Amount);
        Assert.Equal(ContributionState.Posted, f.Contribution.State);
        Assert.Null(f.Contribution.ObligationId);
        Assert.Equal(2, f.ContributionPostings.Count);
        Assert.Equal(300m, f.Journals.Where(x => x.Kind == JournalEntryKind.StatutoryContribution).Sum(x => x.Amount));
    }

    [Fact]
    public async Task EmployeeOnlySchemeClosesWithoutInventingEmployerExpense()
    {
        var f = new Fixture();
        await f.AddStatutory(20, 0);
        await f.Finalize();
        await f.Accrue();
        var journal = await Invoke<JournalEntryResponse?>(f.Service, "PostStatutoryJournalCoreAsync",
            new PostStatutoryJournalRequest(f.Obligation!.Id, StatutoryContributionId: f.Contribution!.Id, PostingProfileId: f.Profile.Id, PostingDate: f.Start));
        Assert.Null(journal);
        Assert.Equal(ContributionState.Posted, f.Contribution.State);
        Assert.DoesNotContain(f.Journals, x => x.Kind == JournalEntryKind.StatutoryContribution);
    }

    [Fact]
    public async Task AlteredStatutoryEvidenceCannotFinalizeAnObligation()
    {
        var f = new Fixture();
        await f.AddStatutory(20, 100);
        typeof(StatutoryContributionEntry).GetProperty(nameof(StatutoryContributionEntry.EvidenceSha256))!.SetValue(f.Entry, new string('B', 64));
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Finalize());
        Assert.Null(f.Obligation);
    }

    [Fact]
    public async Task MissingTaxMappingCannotProducePartialJournal()
    {
        var f = new Fixture();
        await f.Finalize();
        f.Profile = new PayrollPostingProfile("Missing tax", "PKR", f.Start, f.End,
            JsonSerializer.Serialize(f.Rules with { Deductions = [new("LoanRepayment", "LOAN")] }), "maker", Fixture.Now);
        f.Profile.Approve("reviewer", Fixture.Now);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Accrue());
        Assert.Empty(f.Journals);
    }

    [Fact]
    public async Task AttendanceReductionCreditsConfiguredContraExpenseWithoutInventingPayable()
    {
        var f = new Fixture();
        f.Accounts.Add(new PayrollLedgerAccount("REDUCTION", "Salary reduction", PayrollAccountKind.Expense));
        f.Source = f.Source with { Lines = f.Source.Lines.Select(x => x.Component == "LoanRepayment" ? x with { Component = "AttendanceDeduction" } : x).ToArray() };
        f.Profile = new PayrollPostingProfile("Attendance mapping", "PKR", f.Start, f.End,
            JsonSerializer.Serialize(f.Rules with { Deductions = [new("AttendanceDeduction", "REDUCTION"), new("TaxDeduction", "TAX")] }), "maker", Fixture.Now);
        f.Profile.Approve("reviewer", Fixture.Now);
        await f.Finalize();
        await f.Accrue();
        Assert.Equal(100, f.Journals.Single(x => x.CreditAccountCode == "REDUCTION").Amount);
        Assert.Equal(900, f.Journals.Sum(x => x.Amount) - f.Journals.Where(x => x.CreditAccountCode == "REDUCTION").Sum(x => x.Amount));
        Assert.DoesNotContain(f.Journals, x => x.CreditAccountCode == "LOAN");
        Assert.Equal(900m, Assert.Single((await f.CostSource.ReadAsync(f.Source.BatchId, default)).Employees).Amount);
    }

    [Theory]
    [InlineData("AttendanceDeduction", "TAX")]
    [InlineData("LoanRepayment", "TAX")]
    [InlineData("TaxDeduction", "LOAN")]
    public async Task KnownDeductionComponentsRejectWrongAccountKind(string component, string account)
    {
        var f = new Fixture();
        f.Profile = new PayrollPostingProfile("Wrong kind", "PKR", f.Start, f.End,
            JsonSerializer.Serialize(f.Rules with { Deductions = [new(component, account)] }), "maker", Fixture.Now);
        f.Profile.Approve("reviewer", Fixture.Now);
        await Assert.ThrowsAnyAsync<Exception>(() => f.Setup.ResolvePostingAsync(f.Profile.Id, f.Start, "PKR", f.Start, f.End, default));
    }

    [Fact]
    public async Task SalaryPaymentCannotUseTaxAccrualAsSalaryPayable()
    {
        var f = new Fixture();
        await f.Finalize();
        await f.Accrue();
        var tax = f.Journals.Single(x => x.CreditAccountCode == "TAX");
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => Invoke<SalaryPaymentResponse>(f.Service, "RecordSalaryPaymentCoreAsync",
            new RecordSalaryPaymentRequest(f.Obligation!.Id, tax.Id, "SYNTHETIC-PAY", "TAX", "BANK", 50, f.Employee, f.Start)));
        Assert.DoesNotContain(f.Journals, x => x.Kind == JournalEntryKind.SalaryPayment);
    }

    private static async Task<T> Invoke<T>(object target, string method, object request)
    {
        var info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!;
        return await (Task<T>)info.Invoke(target, [request, "finance-user", CancellationToken.None])!;
    }

    internal sealed class Fixture
    {
        public readonly List<StatutoryEmployeePosting> ContributionPostings = [];
        public static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        public readonly DateOnly Start = new(2026, 10, 1);
        public readonly DateOnly End = new(2026, 11, 1);
        public readonly Guid Employee = Guid.NewGuid();
        public PayrollFrozenBatch Source;
        public PayrollHandoffBatch Batch;
        public PayrollObligation? Obligation;
        public StatutoryContribution? Contribution;
        public StatutoryContributionEntry? Entry;
        public readonly List<PayrollJournalEntry> Journals = [];
        public readonly List<PayrollLedgerAccount> Accounts = [new("SAL", "Salary", PayrollAccountKind.Expense), new("PAY", "Salary payable", PayrollAccountKind.Liability),
            new("LOAN", "Loan receivable", PayrollAccountKind.Asset), new("TAX", "Tax payable", PayrollAccountKind.Liability), new("BANK", "Corporate account", PayrollAccountKind.Bank),
            new("ER", "Employer expense", PayrollAccountKind.Expense), new("STAT", "Statutory payable", PayrollAccountKind.Liability)];
        public readonly PayrollPostingRules Rules = new("SAL", "PAY", [new("LoanRepayment", "LOAN"), new("TaxDeduction", "TAX")], [new(StatutoryScheme.EOBI, "ER", "STAT")]);
        public readonly PayrollAccountingPeriod Period;
        public PayrollPostingProfile Profile;
        public readonly PayrollFinanceSetupService Setup;
        public readonly PayrollAccountingService Service;
        public readonly PayrollCostSourceReader CostSource;
        private readonly MemoryFiles files = new();

        public Fixture(bool includeFrozenReader = true, bool includeFinanceRepository = true, bool includeFiles = true)
        {
            Source = new(Guid.NewGuid(), "salary-v2", Start, End, "PKR", new string('A', 64),
                [Line("BaseCompensation", 1000), Line("LoanRepayment", -100), Line("TaxDeduction", -50)], [], "frozen-test-reference");
            Batch = new("Synthetic", Start, End, "PKR", "maker", Now);
            typeof(PayrollHandoffBatch).GetProperty(nameof(PayrollHandoffBatch.Id))!.SetValue(Batch, Source.BatchId);
            typeof(PayrollHandoffBatch).GetProperty(nameof(PayrollHandoffBatch.ReviewState))!.SetValue(Batch, "finance_handed_off");
            Period = new(Start, End, "PKR", "maker", Now); Period.Approve("reviewer", Now);
            Profile = new("Synthetic", "PKR", Start, End, JsonSerializer.Serialize(Rules), "maker", Now); Profile.Approve("reviewer", Now);
            var finance = Proxy<IPayrollFinanceRepository>((method, args) => method switch
            {
                "AccountsAsync" => Task.FromResult<IReadOnlyList<PayrollLedgerAccount>>(Accounts.ToArray()),
                "PeriodForDateAsync" => Task.FromResult<PayrollAccountingPeriod?>(Period),
                "ProfileAsync" => Task.FromResult<PayrollPostingProfile?>(Profile),
                "AccountAsync" => Task.FromResult(Accounts.SingleOrDefault(x => x.Code == (string)args![0]!)),
                "EntryAsync" => Task.FromResult(Entry),
                "EntryUsedAsync" => Task.FromResult(false),
                _ => throw new InvalidOperationException($"Unexpected finance repository call {method}")
            });
            Setup = new(finance);
            var repository = Proxy<IPayrollAccountingRepository>((method, args) => method switch
            {
                "GetObligationByBatchIdAsync" or "GetObligationByIdAsync" => Task.FromResult(Obligation),
                "GetBatchByIdAsync" => Task.FromResult<PayrollHandoffBatch?>(Batch),
                "AddObligationAsync" => SetObligation((PayrollObligation)args![0]!),
                "GetJournalEntriesForObligationAsync" => Task.FromResult<IReadOnlyList<PayrollJournalEntry>>(Journals.Where(x => x.ObligationId == (Guid)args![0]!).ToArray()),
                "AddJournalEntryAsync" => AddJournal((PayrollJournalEntry)args![0]!),
                "GetJournalEntryByIdAsync" => Task.FromResult(Journals.SingleOrDefault(x => x.Id == (Guid)args![0]!)),
                "GetStatutoryContributionByIdAsync" => Task.FromResult(Contribution),
                "ContributionPostingsAsync" => Task.FromResult<IReadOnlyList<StatutoryEmployeePosting>>(ContributionPostings.ToArray()),
                "AddContributionPostingAsync" => AddContributionPosting((StatutoryEmployeePosting)args![0]!),
                "AcquireStatutoryPostingLockAsync" or "SaveChangesAsync" => Task.CompletedTask,
                _ => throw new InvalidOperationException($"Unexpected accounting repository call {method}")
            });
            var reader = Proxy<IPayrollFrozenBatchReader>((_, _) => Task.FromResult(Source));
            Service = new(repository, TimeProvider.System, null!, Setup, includeFrozenReader ? reader : null,
                includeFinanceRepository ? finance : null, includeFiles ? files : null);
            CostSource = new(reader, repository, finance);
        }

        public Task<ObligationResponse> Finalize(decimal? gross = null) => Invoke<ObligationResponse>(Service, "FinalizeObligationCoreAsync",
            new FinalizeObligationRequest(Source.BatchId, TotalGrossAmount: gross, ExpectedSourceFingerprint: Source.Fingerprint));
        public Task<JournalEntryResponse> Accrue() => Invoke<JournalEntryResponse>(Service, "PostAccrualJournalCoreAsync",
            new PostAccrualJournalRequest(Obligation!.Id, PostingProfileId: Profile.Id, PostingDate: Start));
        private Task SetObligation(PayrollObligation row) { Obligation = row; return Task.CompletedTask; }
        private Task AddJournal(PayrollJournalEntry row) { Journals.Add(row); return Task.CompletedTask; }
        private Task AddContributionPosting(StatutoryEmployeePosting row) { ContributionPostings.Add(row); return Task.CompletedTask; }
        private PayrollHandoffLineResponse Line(string component, decimal amount, string sourceType = "synthetic", Guid? sourceId = null) =>
            new(Guid.NewGuid(), Employee, component, sourceType, sourceId ?? Guid.NewGuid(), 1, Guid.NewGuid(), amount, "PKR", null, null, "synthetic-trace");

        public async Task AddStatutory(decimal employeeAmount, decimal employerAmount, bool includeAnotherEmployee = false)
        {
            Contribution = new(StatutoryScheme.EOBI, Start, employeeAmount + (includeAnotherEmployee ? 30 : 0),
                employerAmount + (includeAnotherEmployee ? 200 : 0), "PKR", "maker", Now, hasEmployeeEntries: true);
            var registration = Guid.NewGuid();
            var proof = new StatutoryEntryProof(Contribution.Id, Employee, registration, "REG", StatutoryScheme.EOBI, Start, "PKR", employeeAmount, employerAmount, "synthetic", new string('A', 64));
            var reference = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(proof), StatutoryContributionService.EntryPurpose(Contribution.Id, Employee), default);
            Entry = new(Contribution.Id, Employee, registration, "REG", employeeAmount, employerAmount, "synthetic", new string('A', 64), JsonSerializer.Serialize(reference));
            typeof(StatutoryContributionEntry).GetProperty(nameof(StatutoryContributionEntry.Contribution))!.SetValue(Entry, Contribution);
            Contribution.Entries.Add(Entry);
            if (includeAnotherEmployee)
            {
                var otherEmployee = Guid.NewGuid(); var otherRegistration = Guid.NewGuid();
                var otherProof = new StatutoryEntryProof(Contribution.Id, otherEmployee, otherRegistration, "REG-OTHER",
                    StatutoryScheme.EOBI, Start, "PKR", 30, 200, "synthetic-other", new string('A', 64));
                var otherReference = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(otherProof),
                    StatutoryContributionService.EntryPurpose(Contribution.Id, otherEmployee), default);
                var other = new StatutoryContributionEntry(Contribution.Id, otherEmployee, otherRegistration, "REG-OTHER", 30, 200,
                    "synthetic-other", new string('A', 64), JsonSerializer.Serialize(otherReference));
                typeof(StatutoryContributionEntry).GetProperty(nameof(StatutoryContributionEntry.Contribution))!.SetValue(other, Contribution);
                Contribution.Entries.Add(other);
            }
            Contribution.Approve("reviewer", Now);
            Source = Source with { Lines = Source.Lines.Append(Line("StatutoryEmployeeDeduction", -employeeAmount, nameof(StatutoryContributionEntry), Entry.Id)).ToArray() };
        }

        public void UseOtherEmployeeGroup()
        {
            Entry = Contribution!.Entries.Single(x => x.EmployeeId != Employee);
            Batch = new("Synthetic group two", Start, End, "PKR", "maker", Now);
            typeof(PayrollHandoffBatch).GetProperty(nameof(PayrollHandoffBatch.ReviewState))!.SetValue(Batch, "finance_handed_off");
            Source = Source with { BatchId = Batch.Id, Fingerprint = new('B', 64), Lines = Source.Lines.Select(x => x with
            {
                Id = Guid.NewGuid(), EmployeeId = Entry.EmployeeId,
                Amount = x.Component == "StatutoryEmployeeDeduction" ? -Entry.EmployeeContributionAmount : x.Amount,
                SourceId = x.Component == "StatutoryEmployeeDeduction" ? Entry.Id : Guid.NewGuid()
            }).ToArray() };
            Obligation = null;
        }
    }

    private static T Proxy<T>(Func<string, object?[]?, object?> handler) where T : class
    {
        var result = DispatchProxy.Create<T, PayrollFinanceRepositoryProxy>();
        ((PayrollFinanceRepositoryProxy)(object)result).Handler = handler;
        return result;
    }

    private sealed class MemoryFiles : IPrivateFileStore
    {
        private readonly Dictionary<string, (string Purpose, byte[] Bytes)> content = [];
        public Task<PrivateFileReference> WriteAsync(byte[] bytes, string purpose, CancellationToken ct)
        {
            var key = Guid.NewGuid().ToString("N"); content[key] = (purpose, bytes);
            return Task.FromResult(new PrivateFileReference(key, "1", Convert.ToHexString(SHA256.HashData(bytes)), "test", bytes.Length));
        }
        public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
        {
            var item = content[reference.StorageKey];
            if (item.Purpose != purpose) throw new InvalidOperationException("Wrong private evidence purpose.");
            return Task.FromResult(item.Bytes);
        }
    }
}

public class PayrollFinanceRepositoryProxy : DispatchProxy
{
    public Func<string, object?[]?, object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!.Name, args);
}
