using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.PayrollAccounting;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollBankExportTests
{
    [Fact]
    public async Task Missing_or_unreviewed_format_cannot_generate_a_transfer_file()
    {
        var f = await Fixture.Create();
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.CreateExport(Guid.NewGuid()));
        typeof(PayrollBankFormatProfile).GetProperty(nameof(PayrollBankFormatProfile.State))!.SetValue(f.Format, PayrollFinanceApprovalState.Draft);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.CreateExport());
        Assert.Empty(f.Exports); Assert.Null(f.Payment.BankExportId);
    }

    [Fact]
    public async Task Export_uses_verified_protected_banking_data_and_binds_source_without_confirming_payment()
    {
        var f = await Fixture.Create(); var result = await f.CreateExport();
        var row = Assert.Single(f.Exports);
        Assert.Equal(row.Id, f.Payment.BankExportId); Assert.Equal(SalaryPaymentState.Posted, f.Payment.State);
        Assert.Equal(40m, result.TotalAmount); Assert.Equal(1, result.PaymentCount);
        var file = await f.Files.ReadAsync(JsonSerializer.Deserialize<PrivateFileReference>(row.PrivateFileReference!)!, $"payroll-bank-export/{row.Id:N}/output", default);
        Assert.Equal("\"Account\",\"Amount\",\"Currency\",\"Reference\"\r\n\"00123456\",\"40.00\",\"PKR\",\"PAY-TEST\"\r\n", Encoding.UTF8.GetString(file));
        Assert.DoesNotContain("00123456", JsonSerializer.Serialize(result));
        Assert.NotNull(row.PrivateManifestReference);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.CreateExport());
        Assert.Single(f.Exports);
    }

    [Fact]
    public async Task Revoked_bank_verification_and_stale_payroll_fingerprint_are_rejected()
    {
        var f = await Fixture.Create();
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Service.CreateExportCoreAsync(f.Request() with { ExpectedSourceFingerprint = new('B', 64) }, "exporter", default));
        typeof(EmployeeBankDetails).GetProperty(nameof(EmployeeBankDetails.IsVerified))!.SetValue(f.Bank, false);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.CreateExport());
        Assert.Empty(f.Exports); Assert.Null(f.Payment.BankExportId);
    }

    [Fact]
    public async Task Reversal_pending_payment_cannot_be_exported()
    {
        var f = await Fixture.Create();
        f.Payment.RequestReversal("requester", Fixture.Now, "rejected", "protected-proof", true);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.CreateExport());
        Assert.Empty(f.Exports);
    }

    [Fact]
    public async Task Altered_profile_rules_are_rejected_by_retained_template_binding()
    {
        var f = await Fixture.Create();
        typeof(PayrollBankFormatProfile).GetProperty(nameof(PayrollBankFormatProfile.RulesJson))!.SetValue(f.Format,
            JsonSerializer.Serialize(f.Rules with { Delimiter = ";" }));
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.CreateExport());
        Assert.Empty(f.Exports);
    }

    [Fact]
    public async Task Bank_account_formula_is_rejected_instead_of_mutating_account_identifier()
    {
        var f = await Fixture.Create("=HYPERLINK(\"bad\")");
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.CreateExport());
        Assert.Empty(f.Exports); Assert.Null(f.Payment.BankExportId);
    }

    [Fact]
    public async Task Wrong_salary_accrual_or_employee_membership_cannot_be_exported()
    {
        var f = await Fixture.Create();
        typeof(SalaryPayment).GetProperty(nameof(SalaryPayment.AccrualJournalEntryId))!.SetValue(f.Payment, Guid.NewGuid());
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.CreateExport());
        var second = await Fixture.Create();
        typeof(SalaryPayment).GetProperty(nameof(SalaryPayment.EmployeeId))!.SetValue(second.Payment, Guid.NewGuid());
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => second.CreateExport());
        Assert.Empty(f.Exports); Assert.Empty(second.Exports);
    }

    [Fact]
    public async Task Changed_beneficiary_name_during_file_storage_or_after_export_blocks_release()
    {
        var f = await Fixture.Create(includeBeneficiaryName: true);
        f.Files.OnWrite = () => typeof(Employee).GetProperty(nameof(Employee.FullLegalName))!.SetValue(f.Employee, "Changed source");
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.CreateExport());
        Assert.Empty(f.Exports); Assert.Null(f.Payment.BankExportId);
        var second = await Fixture.Create(includeBeneficiaryName: true); var exported = await second.CreateExport();
        typeof(Employee).GetProperty(nameof(Employee.FullLegalName))!.SetValue(second.Employee, "Changed source");
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => second.Service.DownloadCoreAsync(exported.Id, default));
    }

    [Fact]
    public async Task Expired_export_can_renew_only_unchanged_exact_bytes_without_new_payment_or_file()
    {
        var f = await Fixture.Create(); var created = await f.CreateExport();
        var original = await f.Service.DownloadCoreAsync(created.Id, default);
        f.Clock.Now = f.Clock.Now.AddHours(25);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Service.DownloadCoreAsync(created.Id, default));
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Service.RenewCoreAsync(created.Id, new(new('B', 64), "renew access"), "exporter", default));
        var renewed = await f.Service.RenewCoreAsync(created.Id, new(created.Sha256, "renew access"), "exporter", default);
        Assert.Equal(created.Id, renewed.Id); Assert.Equal(created.Sha256, renewed.Sha256); Assert.True(renewed.ExpiresAt > created.ExpiresAt);
        var replay = await f.Service.DownloadCoreAsync(created.Id, default);
        Assert.Equal(original.Content, replay.Content); Assert.Single(f.Exports); Assert.Single(f.Renewals);
        Assert.Equal(created.Id, f.Payment.BankExportId); Assert.Equal(SalaryPaymentState.Posted, f.Payment.State);
        f.Payment.RequestReversal("requester", f.Clock.Now, "bank rejected", "protected-proof", true);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(() => f.Service.RenewCoreAsync(created.Id, new(created.Sha256, "renew access"), "exporter", default));
        Assert.Single(f.Renewals);
    }

    private sealed class Fixture
    {
        public static readonly DateTimeOffset Now = new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
        public PayrollBankFormatRules Rules = new(",", true, false, "yyyy-MM-dd",
            [new("Account", "AccountNumber"), new("Amount", "Amount"), new("Currency", "Currency"), new("Reference", "Reference")]);
        public PayrollBankFormatProfile Format = null!;
        public EmployeeBankDetails Bank = null!;
        public SalaryPayment Payment = null!;
        public PayrollObligation Obligation = null!;
        public PayrollBankExportService Service = null!;
        public Employee Employee = null!;
        public readonly FixedClock Clock = new();
        public readonly MemoryFiles Files = new();
        public readonly List<PayrollBankExport> Exports = [];
        public readonly List<PayrollBankExportRenewal> Renewals = [];
        public CreatePayrollBankExportRequest Request() => new(Obligation.Id, Format.Id, new(2026, 10, 28), [Payment.Id], Obligation.SourceFingerprint!, "reviewed payroll export");
        public Task<PayrollBankExportResponse> CreateExport(Guid? formatId = null) => Service.CreateExportCoreAsync(Request() with { FormatProfileId = formatId ?? Format.Id }, "exporter", default);
        public static async Task<Fixture> Create(string account = "00123456", bool includeBeneficiaryName = false)
        {
            var f = new Fixture(); var employee = Guid.NewGuid(); var batch = Guid.NewGuid(); var start = new DateOnly(2026, 10, 1);
            f.Employee = new(Now, "hr");
            typeof(Employee).GetProperty(nameof(Employee.Id))!.SetValue(f.Employee, employee);
            typeof(Employee).GetProperty(nameof(Employee.FullLegalName))!.SetValue(f.Employee, "Synthetic employee");
            if (includeBeneficiaryName) f.Rules = f.Rules with { Columns = [.. f.Rules.Columns, new("Name", "BeneficiaryName")] };
            f.Obligation = new(batch, 100, 100, "PKR", "maker", Now);
            f.Obligation.BindSource(new('A', 64), start, start.AddMonths(1), JsonSerializer.Serialize(new[] { new BatchEmployeeEntitlementDto(employee, 100, 0, 100) }));
            f.Obligation.Finalize("finalizer", Now);
            var source = new PayrollFrozenBatch(batch, "salary-v2", start, start.AddMonths(1), "PKR", new('A', 64),
                [new PayrollHandoffLineResponse(Guid.NewGuid(), employee, "BaseCompensation", "synthetic", Guid.NewGuid(), 1, Guid.NewGuid(), 100, "PKR", null, null, "trace")], [], "protected-frozen");
            var accrual = new PayrollJournalEntry(f.Obligation.Id, JournalEntryKind.SalaryAccrual, "SAL", "PAY", 100, "PKR", "accrual", "maker", Now);
            accrual.BindPosting(start, Guid.NewGuid(), Guid.NewGuid(), $"ACCRUAL-SALARY-{f.Obligation.Id:N}");
            f.Payment = new(f.Obligation.Id, accrual.Id, "PAY-TEST", 40m, "maker", Now, employee);
            var journal = new PayrollJournalEntry(f.Obligation.Id, JournalEntryKind.SalaryPayment, "PAY", "BANK", 40, "PKR", "payment", "maker", Now);
            journal.BindPosting(start, Guid.NewGuid(), Guid.NewGuid(), $"PAYMENT-{f.Obligation.Id:N}-{f.Payment.Id:N}");
            f.Payment.RecordPaymentJournal(journal.Id);
            async Task<string> Field(string value, string field) => JsonSerializer.Serialize(await f.Files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(value), $"employee/{employee:N}/approved/bank/{field}", default));
            f.Bank = new(employee, 1, await Field("Synthetic Bank", "name"), await Field("Synthetic Employee", "title"), await Field(account, "account"), null,
                "synthetic", "****3456", true, "bank-reviewer", Now, start, null, "hr", Now);
            f.Format = new("Synthetic supplied CSV", 1, "PKR", JsonSerializer.Serialize(f.Rules), "format-maker", Now);
            async Task<string> Proof(string actor, string section) => JsonSerializer.Serialize(await f.Files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new
            {
                Schema = 1, f.Format.Id, f.Format.Name, f.Format.FormatVersion, f.Format.Currency, f.Format.RulesJson, Actor = actor, At = Now,
                Reason = "synthetic supplied format", Evidence = "explicit synthetic sample and review, no bank certification"
            }), $"payroll-bank-format/{f.Format.Id:N}/{section}", default));
            f.Format.BindTemplate(await Proof("format-maker", "template")); f.Format.Approve("format-reviewer", Now, await Proof("format-reviewer", "approval"));
            var repository = Proxy<IPayrollAccountingRepository>((method, args) => method switch
            {
                "GetObligationByIdAsync" => Task.FromResult<PayrollObligation?>(f.Obligation),
                "GetSalaryPaymentByIdAsync" => Task.FromResult<SalaryPayment?>(f.Payment),
                "GetJournalEntryByIdAsync" => Task.FromResult<PayrollJournalEntry?>((Guid)args![0]! == journal.Id ? journal : (Guid)args[0]! == accrual.Id ? accrual : null),
                "GetSalaryPaymentsForObligationAsync" => Task.FromResult<IReadOnlyList<SalaryPayment>>([f.Payment]),
                "GetBankExportAsync" => Task.FromResult(f.Exports.SingleOrDefault(x => x.Id == (Guid)args![0]!)),
                "LatestBankExportRenewalAsync" => Task.FromResult(f.Renewals.LastOrDefault()),
                "AddBankExportRenewalAsync" => f.Add((PayrollBankExportRenewal)args![0]!),
                "GetBankFormatAsync" => Task.FromResult((Guid)args![0]! == f.Format.Id ? f.Format : null),
                "AddBankExportAsync" => f.Add((PayrollBankExport)args![0]!),
                "AcquirePaymentLockAsync" or "RefreshSalaryPaymentAsync" or "SaveChangesAsync" => Task.CompletedTask,
                _ => throw new InvalidOperationException(method)
            });
            var banks = Proxy<IEmployeeBankRepository>((method, _) => method switch
            {
                "CurrentBankAsync" => Task.FromResult<EmployeeBankDetails?>(f.Bank),
                "LatestAsync" => Task.FromResult<EmployeeBankVerification?>(null),
                "HasConflictingHistoryAsync" => Task.FromResult(false),
                _ => throw new InvalidOperationException(method)
            });
            f.Service = new(repository, Proxy<IPayrollFrozenBatchReader>((_, _) => Task.FromResult(source)), banks,
                Proxy<IEmployeeUpdateRepository>((_, _) => Task.FromResult(false)), f.Files, null!, null!, f.Clock,
                Proxy<IEmployeeHiringRepository>((_, _) => Task.FromResult<Employee?>(f.Employee)));
            return f;
        }
        private Task Add(PayrollBankExport export) { Exports.Add(export); return Task.CompletedTask; }
        private Task Add(PayrollBankExportRenewal renewal) { Renewals.Add(renewal); return Task.CompletedTask; }
    }
    private sealed class FixedClock : TimeProvider { public DateTimeOffset Now = Fixture.Now; public override DateTimeOffset GetUtcNow() => Now; }
    private static T Proxy<T>(Func<string, object?[]?, object?> handler) where T : class
    {
        var result = DispatchProxy.Create<T, PayrollFinanceRepositoryProxy>(); ((PayrollFinanceRepositoryProxy)(object)result).Handler = handler; return result;
    }
    private sealed class MemoryFiles : IPrivateFileStore
    {
        private readonly Dictionary<string, (string Purpose, byte[] Bytes)> values = [];
        public Action? OnWrite;
        public Task<PrivateFileReference> WriteAsync(byte[] bytes, string purpose, CancellationToken ct)
        {
            var key = Guid.NewGuid().ToString("N"); values[key] = (purpose, bytes.ToArray());
            OnWrite?.Invoke();
            return Task.FromResult(new PrivateFileReference(key, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length));
        }
        public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
        {
            var value = values[reference.StorageKey]; Assert.Equal(value.Purpose, purpose); return Task.FromResult(value.Bytes.ToArray());
        }
    }
}
