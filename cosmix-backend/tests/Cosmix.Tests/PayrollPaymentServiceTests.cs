using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Application.HR.Payslips;
using Cosmix.Domain.HR.PayrollAccounting;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollPaymentServiceTests
{
    [Fact]
    public async Task Confirmation_requires_independent_actor_evidence_and_exact_version()
    {
        var f = new Fixture(); var p = await f.Record(40, "REF-1");
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Confirm(p, "maker", "receipt"));
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Confirm(p, "checker", null));
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Service.ConfirmPaymentWithEvidenceCoreAsync(p.Id, new("reason", "receipt", p.Version - 1), "checker", default));
        Assert.Equal(SalaryPaymentState.Posted, p.State); Assert.Null(p.ConfirmationProofReference);
        var response = await f.Confirm(p, "checker", "statement row REF-1 paid");
        Assert.Equal("Confirmed", response.State); Assert.NotNull(p.ConfirmationProofReference);
        Assert.DoesNotContain("statement", JsonSerializer.Serialize(response), StringComparison.OrdinalIgnoreCase);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Confirm(p, "checker", "receipt"));
    }

    [Fact]
    public async Task Pending_reversal_keeps_reservation_and_approved_failure_restores_exact_amount_once()
    {
        var f = new Fixture(); var first = await f.Record(60, "REF-1");
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Record(41, "REF-2"));
        await f.Request(first, true);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Record(41, "REF-2"));
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Approve(first, "requester"));
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Approve(first, "maker"));
        await f.Approve(first, "reviewer");
        Assert.Equal(SalaryPaymentState.Failed, first.State);
        var reverse = Assert.Single(f.Journals, x => x.Kind == JournalEntryKind.SalaryPaymentReversal);
        Assert.Equal(60m, reverse.Amount); Assert.Equal("BANK", reverse.DebitAccountCode); Assert.Equal("PAY", reverse.CreditAccountCode);
        Assert.Equal(first.ReversalJournalEntryId, reverse.Id);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Approve(first, "reviewer"));
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Record(60, " ref-1 "));
        await f.Record(100, "REF-2");
        Assert.Equal(100m, f.Payments.Where(PayrollAccountingService.ReservesEntitlement).Sum(x => x.Amount));
    }

    [Fact]
    public async Task Confirmed_payment_retains_confirmation_and_reverses_in_new_open_period()
    {
        var f = new Fixture(); var p = await f.Record(100, "REF");
        await f.Confirm(p, "checker", "paid receipt");
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Request(p, true));
        await f.Request(p, false); await f.Approve(p, "reviewer");
        Assert.Equal(SalaryPaymentState.Reversed, p.State);
        Assert.NotNull(p.ConfirmedAt); Assert.NotNull(p.ConfirmationProofReference); Assert.NotNull(p.ReversalApprovalProofReference);
        Assert.Single(f.Journals, x => x.Kind == JournalEntryKind.SalaryPayment);
    }

    [Fact]
    public async Task Closed_period_blocks_confirmation_and_reversal_without_releasing_entitlement()
    {
        var f = new Fixture(); var p = await f.Record(100, "REF");
        f.Period.Close("reviewer", Fixture.Now);
        await Assert.ThrowsAnyAsync<Exception>(() => f.Confirm(p, "checker", "receipt"));
        await f.Request(p, true);
        await Assert.ThrowsAnyAsync<Exception>(() => f.Approve(p, "reviewer"));
        Assert.True(PayrollAccountingService.ReservesEntitlement(p));
        Assert.DoesNotContain(f.Journals, x => x.Kind == JournalEntryKind.SalaryPaymentReversal);
    }

    [Fact]
    public async Task Altered_retained_reversal_source_is_rejected_before_any_compensating_posting()
    {
        var f = new Fixture(); var p = await f.Record(60, "REF"); await f.Request(p, true);
        typeof(SalaryPayment).GetProperty(nameof(SalaryPayment.ReversalReason))!.SetValue(p, "changed reason");
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Approve(p, "reviewer"));
        Assert.Null(p.ReversalJournalEntryId); Assert.Equal(SalaryPaymentState.Posted, p.State);
    }

    [Fact]
    public async Task Unavailable_private_storage_cannot_confirm_payment()
    {
        var f = new Fixture(); var p = await f.Record(100, "REF"); f.Files.FailWrites = true;
        await Assert.ThrowsAsync<IOException>(() => f.Confirm(p, "checker", "receipt"));
        Assert.Equal(SalaryPaymentState.Posted, p.State); Assert.Null(p.ConfirmedAt);
    }

    [Fact]
    public async Task Reporting_rejects_confirmation_state_rewrite_and_changed_reversal_journal()
    {
        var f = new Fixture(); var p = await f.Record(60, "REF"); await f.Confirm(p, "checker", "retained receipt");
        await f.Service.ValidatePaymentProofsForReadAsync(p, f.Obligation, default);
        typeof(SalaryPayment).GetProperty(nameof(SalaryPayment.State))!.SetValue(p, SalaryPaymentState.Posted);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Service.ValidatePaymentProofsForReadAsync(p, f.Obligation, default));
        typeof(SalaryPayment).GetProperty(nameof(SalaryPayment.State))!.SetValue(p, SalaryPaymentState.Confirmed);
        await f.Request(p, false); await f.Approve(p, "reviewer");
        await f.Service.ValidatePaymentProofsForReadAsync(p, f.Obligation, default);
        var reversal = f.Journals.Single(x => x.Kind == JournalEntryKind.SalaryPaymentReversal);
        typeof(PayrollJournalEntry).GetProperty(nameof(PayrollJournalEntry.Amount))!.SetValue(reversal, 59m);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Service.ValidatePaymentProofsForReadAsync(p, f.Obligation, default));
    }

    [Fact]
    public async Task Legacy_failed_posting_without_reversal_cannot_release_salary_for_another_payment()
    {
        var f = new Fixture(); var p = await f.Record(60, "REF-1");
        typeof(SalaryPayment).GetProperty(nameof(SalaryPayment.State))!.SetValue(p, SalaryPaymentState.Failed);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Record(100, "REF-2"));
        Assert.Single(f.Payments); Assert.Single(f.Journals, x => x.Kind == JournalEntryKind.SalaryPayment);
    }

    [Fact]
    public async Task Payslip_payment_state_uses_retained_proof_and_never_the_unverified_sql_sum()
    {
        var f = new Fixture(); var p = await f.Record(40, "REF"); await f.Confirm(p, "checker", "retained paid receipt");
        var slips = await f.Payslips.OwnPageInTransactionAsync(f.Employee, 1, 20, default);
        Assert.Equal("PartiallyPaid", Assert.Single(slips.Items).PaymentState);
        typeof(SalaryPayment).GetProperty(nameof(SalaryPayment.ConfirmationProofReference))!.SetValue(p, null);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Payslips.OwnPageInTransactionAsync(f.Employee, 1, 20, default));
    }

    [Fact]
    public async Task Changing_payment_maker_cannot_turn_original_maker_into_independent_confirmer()
    {
        var f = new Fixture(); var p = await f.Record(40, "REF");
        typeof(SalaryPayment).GetProperty(nameof(SalaryPayment.CreatedByUserId))!.SetValue(p, "different-maker");
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Confirm(p, "maker", "receipt"));
        Assert.Equal(SalaryPaymentState.Posted, p.State); Assert.Null(p.ConfirmationProofReference);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"bad\")")]
    [InlineData("  +123")]
    [InlineData("\t@SUM(1)")]
    [InlineData("-1")]
    [InlineData("ok\r\nnew row")]
    public void Bank_export_rejects_formula_or_control_cells_without_altering_account_data(string value) =>
        Assert.Throws<PayrollFinanceConflictException>(() => PayrollBankExportService.CsvCell(value));

    [Fact]
    public void Bank_csv_preserves_leading_zeroes_quotes_and_commas()
    {
        Assert.Equal("\"001234\"", PayrollBankExportService.CsvCell("001234"));
        Assert.Equal("\"A, \"\"B\"\"\"", PayrollBankExportService.CsvCell("A, \"B\""));
    }

    [Fact]
    public void Bank_format_requires_explicit_usable_schema_and_independent_retained_review()
    {
        Assert.Throws<PayrollFinanceConflictException>(() => PayrollBankExportService.ValidateFormat(new(",", true, false, "yyyy-MM-dd", [new("Amount", "Amount")])));
        var p = new PayrollBankFormatProfile("Synthetic supplied format", 1, "PKR", "{}", "maker", Fixture.Now);
        Assert.Throws<InvalidOperationException>(() => p.Approve("reviewer", Fixture.Now, "proof"));
        p.BindTemplate("protected-template");
        Assert.Throws<InvalidOperationException>(() => p.Approve("maker", Fixture.Now, "proof"));
        p.Approve("reviewer", Fixture.Now, "protected-review");
        Assert.Throws<InvalidOperationException>(() => p.BindTemplate("replacement"));
        Assert.Throws<InvalidOperationException>(() => p.Approve("reviewer", Fixture.Now, "replacement"));
    }

    private sealed class Fixture
    {
        public static readonly DateTimeOffset Now = new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
        public readonly DateOnly Start = new(2026, 10, 1);
        public readonly Guid Employee = Guid.NewGuid();
        public readonly PayrollObligation Obligation;
        public readonly PayrollFrozenBatch Source;
        public readonly PayrollAccountingPeriod Period;
        public readonly List<SalaryPayment> Payments = [];
        public readonly List<PayrollJournalEntry> Journals = [];
        public readonly MemoryFiles Files = new();
        public readonly PayrollAccountingService Service;
        public readonly PayslipService Payslips;
        public Fixture()
        {
            var batch = Guid.NewGuid();
            Source = new(batch, "salary-v2", Start, Start.AddMonths(1), "PKR", new('A', 64),
                [new PayrollHandoffLineResponse(Guid.NewGuid(), Employee, "BaseCompensation", "synthetic", Guid.NewGuid(), 1, Guid.NewGuid(), 100m, "PKR", null, null, "trace")], [new(Employee, 100, 0, 100)], "frozen-proof");
            Obligation = new(batch, 100, 100, "PKR", "obligation-maker", Now);
            Obligation.BindSource(Source.Fingerprint, Start, Start.AddMonths(1), JsonSerializer.Serialize(new[] { new BatchEmployeeEntitlementDto(Employee, 100, 0, 100) }));
            Obligation.Finalize("finance", Now);
            var accrual = new PayrollJournalEntry(Obligation.Id, JournalEntryKind.SalaryAccrual, "SAL", "PAY", 100, "PKR", "salary", "finance", Now);
            accrual.BindPosting(Start, Guid.NewGuid(), Guid.NewGuid(), $"ACCRUAL-SALARY-{Obligation.Id:N}"); Journals.Add(accrual);
            Period = new(Start, Start.AddMonths(1), "PKR", "period-maker", Now); Period.Approve("period-reviewer", Now);
            var finance = Proxy<IPayrollFinanceRepository>((method, args) => method switch
            {
                "PeriodForDateAsync" => Task.FromResult<PayrollAccountingPeriod?>(Period),
                "AccountAsync" => Task.FromResult<PayrollLedgerAccount?>(new((string)args![0]!, "account", (string)args[0]! == "BANK" ? PayrollAccountKind.Bank : PayrollAccountKind.Liability)),
                _ => throw new InvalidOperationException(method)
            });
            var repository = Proxy<IPayrollAccountingRepository>((method, args) => method switch
            {
                "GetObligationByIdAsync" or "GetObligationByBatchIdAsync" => Task.FromResult<PayrollObligation?>(Obligation),
                "GetJournalEntryByIdAsync" => Task.FromResult(Journals.SingleOrDefault(x => x.Id == (Guid)args![0]!)),
                "GetSalaryPaymentByIdAsync" => Task.FromResult(Payments.SingleOrDefault(x => x.Id == (Guid)args![0]!)),
                "GetSalaryPaymentsForObligationAsync" => Task.FromResult<IReadOnlyList<SalaryPayment>>(Payments.ToArray()),
                "GetJournalEntriesForObligationAsync" => Task.FromResult<IReadOnlyList<PayrollJournalEntry>>(Journals.ToArray()),
                "AddSalaryPaymentAsync" => AddPayment((SalaryPayment)args![0]!),
                "AddJournalEntryAsync" => AddJournal((PayrollJournalEntry)args![0]!),
                "AcquirePaymentLockAsync" or "RefreshSalaryPaymentAsync" or "SaveChangesAsync" => Task.CompletedTask,
                _ => throw new InvalidOperationException(method)
            });
            var frozen = Proxy<IPayrollFrozenBatchReader>((_, _) => Task.FromResult(Source));
            Service = new(repository, TimeProvider.System, null!, new(finance), frozen, finance, Files);
            var identities = Proxy<IPayslipRepository>((method, _) => method switch
            {
                "OwnBatchIdsAsync" => Task.FromResult<IReadOnlyList<Guid>>([Source.BatchId]),
                "ContainsEmployeeAsync" => Task.FromResult(true),
                "BatchAsync" => Task.FromResult<PayslipBatchIdentity?>(new(Source.BatchId, "SYNTHETIC", "AAAAAAAAAAE=", Now)),
                // Deliberately wrong raw sum must never determine a protected payslip's payment state.
                "ConfirmedPaymentsAsync" => Task.FromResult(999m),
                _ => throw new InvalidOperationException(method)
            });
            Payslips = new(null!, frozen, identities, TimeProvider.System, new(frozen, identities, repository, TimeProvider.System, Service));
        }
        public async Task<SalaryPayment> Record(decimal amount, string reference)
        {
            var method = typeof(PayrollAccountingService).GetMethod("RecordSalaryPaymentCoreAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await (Task<SalaryPaymentResponse>)method.Invoke(Service, [new RecordSalaryPaymentRequest(Obligation.Id, Journals[0].Id, reference, "PAY", "BANK", amount, Employee, Start), "maker", CancellationToken.None])!;
            return Payments[^1];
        }
        public Task<SalaryPaymentResponse> Confirm(SalaryPayment p, string actor, string? evidence) => Service.ConfirmPaymentWithEvidenceCoreAsync(p.Id, new("confirmation", evidence, p.Version), actor, default);
        public Task<SalaryPaymentResponse> Request(SalaryPayment p, bool failed) => Service.RequestPaymentReversalCoreAsync(p.Id, new("bank result", "retained bank result", failed, p.Version), "requester", default);
        public Task<SalaryPaymentResponse> Approve(SalaryPayment p, string actor) => Service.ApprovePaymentReversalCoreAsync(p.Id, new("reviewed result", "independent review evidence", Start, p.Version), actor, default);
        private Task AddPayment(SalaryPayment p) { Payments.Add(p); return Task.CompletedTask; }
        private Task AddJournal(PayrollJournalEntry p) { Journals.Add(p); return Task.CompletedTask; }
    }
    private static T Proxy<T>(Func<string, object?[]?, object?> handler) where T : class
    {
        var p = DispatchProxy.Create<T, PayrollFinanceRepositoryProxy>(); ((PayrollFinanceRepositoryProxy)(object)p).Handler = handler; return p;
    }
    private sealed class MemoryFiles : IPrivateFileStore
    {
        private readonly Dictionary<string, (string Purpose, byte[] Bytes)> values = [];
        public bool FailWrites;
        public Task<PrivateFileReference> WriteAsync(byte[] bytes, string purpose, CancellationToken ct)
        {
            if (FailWrites) throw new IOException("Synthetic unavailable storage.");
            var key = Guid.NewGuid().ToString("N"); values[key] = (purpose, bytes.ToArray());
            return Task.FromResult(new PrivateFileReference(key, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length));
        }
        public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
        {
            var value = values[reference.StorageKey]; Assert.Equal(purpose, value.Purpose); return Task.FromResult(value.Bytes.ToArray());
        }
    }
}
