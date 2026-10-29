using System.Text.Json;
using System.Reflection;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.AdvancesLoans;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.PayrollHandoff;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollSalaryFrozenEvidenceTests
{
    [Fact]
    public async Task SQL_decimal_scale_does_not_invalidate_existing_frozen_salary_fingerprint()
    {
        var (batch, files) = await FixtureAsync();
        var retainedFingerprint = batch.FrozenFingerprint;
        var before = JsonSerializer.Serialize(PayrollFrozenBatchReader.Lines(batch));
        // Match materialization from the production SQL decimal(...,2) column.
        typeof(PayrollHandoffLine).GetProperty(nameof(PayrollHandoffLine.Amount))!
            .SetValue(Assert.Single(batch.Lines), 100.00m);
        Assert.NotEqual(before, JsonSerializer.Serialize(PayrollFrozenBatchReader.Lines(batch)));
        var result = await PayrollFrozenBatchReader.ValidateAsync(batch, null!, files, false, default);
        Assert.Equal(retainedFingerprint, result.Fingerprint);
        Assert.Equal(100m, Assert.Single(result.Employees).NetPay);
    }

    [Theory]
    [InlineData(nameof(PayrollHandoffLine.Amount))]
    [InlineData(nameof(PayrollHandoffLine.SourceId))]
    [InlineData(nameof(PayrollHandoffLine.TraceId))]
    [InlineData(nameof(PayrollHandoffLine.ApprovalRequestId))]
    public async Task Scale_tolerant_frozen_validation_still_rejects_changed_money_or_source_identity(string field)
    {
        var (batch, files) = await FixtureAsync();
        var line = Assert.Single(batch.Lines);
        object changed = field switch
        {
            nameof(PayrollHandoffLine.Amount) => 100.01m,
            nameof(PayrollHandoffLine.TraceId) => "different-trace",
            _ => Guid.NewGuid()
        };
        typeof(PayrollHandoffLine).GetProperty(field)!.SetValue(line, changed);
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() =>
            PayrollFrozenBatchReader.ValidateAsync(batch, null!, files, false, default));
        Assert.Equal("salary_frozen_evidence_invalid", failure.Code);
    }

    [Theory]
    [InlineData(PayrollComponentKind.LoanRepayment)]
    [InlineData(PayrollComponentKind.SalaryAdjustment)]
    public async Task Repeated_loan_or_component_source_cannot_reduce_net_pay_twice(PayrollComponentKind component)
    {
        var (batch, files) = await FixtureAsync(repeatedDeduction: component);
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() => PayrollFrozenBatchReader.ValidateAsync(batch, null!, files, false, default));
        Assert.Equal("salary_frozen_evidence_invalid", failure.Code);
    }
    [Fact]
    public async Task Loan_posting_revalidates_full_salary_evidence_before_binding_a_frozen_installment()
    {
        var (batch, files) = await FixtureAsync(duplicateBase: true);
        batch.ReviewSalary(Guid.NewGuid(), Guid.NewGuid().ToString(), batch.PreparedAt.AddMinutes(1), true);
        batch.Lock(Guid.NewGuid(), Guid.NewGuid().ToString(), batch.PreparedAt.AddMinutes(2));
        var at = batch.PreparedAt.AddHours(1);
        var actor = new AttendanceActor(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true), "SyntheticFinance", at, null);
        IReadOnlyList<PermissionGrant> grants = new[] { PayrollHandoffPermissions.Read, PayrollHandoffPermissions.Process, AdvanceLoanPermissions.Repay }
            .Select(p => new PermissionGrant(p, true, Guid.NewGuid(), AccessScopeKind.All, null)
            { AuthorityTimingKnown = true, ValidFromUtc = batch.PreparedAt }).ToArray();
        var access = Proxy<IAccountAccessReader>((_, _) => Task.FromResult(grants));
        var repository = Proxy<IPayrollHandoffRepository>((method, _) => method.Name == "FindBatchAsync"
            ? Task.FromResult<PayrollHandoffBatch?>(batch) : throw new InvalidOperationException(method.Name));
        var loans = Proxy<IAdvanceLoanRepository>((method, _) => throw new InvalidOperationException(method.Name));
        var service = new PayrollHandoffService(null!, repository, null!, null!, null!, null!, access, files, loans: loans);
        var line = batch.Lines.First();
        var request = new ConfirmPayrollLoanPostingRequest(line.EmployeeId, Guid.NewGuid(), line.SourceId, 1, 100m, "PKR", at,
            "SYNTHETIC", "RECEIPT", "LINE", "synthetic-evidence", new('A', 64), "Synthetic posted receipt", "Synthetic test");
        var bind = typeof(PayrollHandoffService).GetMethod("BindLoanPostingAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(async () =>
            await (Task)bind.Invoke(service, [batch.Id, line.Id, request, actor, CancellationToken.None, true])!);
        Assert.Equal("salary_frozen_evidence_invalid", failure.Code);
    }
    [Fact]
    public async Task Duplicate_base_sources_cannot_form_a_complete_salary()
    {
        var (batch, files) = await FixtureAsync(duplicateBase: true);
        await Assert.ThrowsAnyAsync<Exception>(() => PayrollFrozenBatchReader.ValidateAsync(batch, null!, files, false, default));
    }

    [Fact]
    public async Task Foreign_batch_line_cannot_form_a_complete_salary()
    {
        var (batch, files) = await FixtureAsync(foreignBatch: true);
        await Assert.ThrowsAnyAsync<Exception>(() => PayrollFrozenBatchReader.ValidateAsync(batch, null!, files, false, default));
    }

    [Fact]
    public async Task Frozen_lines_cannot_change_after_the_salary_proof_is_written()
    {
        var (batch, files) = await FixtureAsync();
        batch.Lines.Clear();
        await Assert.ThrowsAnyAsync<Exception>(() => PayrollFrozenBatchReader.ValidateAsync(batch, null!, files, false, default));
    }

    [Fact]
    public async Task Prepared_salary_is_readable_but_cannot_be_used_as_approved_salary()
    {
        var (batch, files) = await FixtureAsync();
        var result = await PayrollFrozenBatchReader.ValidateAsync(batch, null!, files, false, default);
        Assert.Equal(100m, Assert.Single(result.Employees).NetPay);
        await Assert.ThrowsAnyAsync<Exception>(() => PayrollFrozenBatchReader.ValidateAsync(batch, null!, files, true, default));
    }

    internal static async Task<(PayrollHandoffBatch, MemoryFiles)> FixtureAsync(bool duplicateBase = false, bool foreignBatch = false,
        PayrollComponentKind? repeatedDeduction = null)
    {
        var actor = Guid.NewGuid(); var employee = Guid.NewGuid(); var files = new MemoryFiles();
        var batch = new PayrollHandoffBatch("SYNTHETIC", new(2026, 10, 1), new(2026, 11, 1), "PKR", actor.ToString(),
            new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
        for (var i = 0; i < (duplicateBase ? 2 : 1); i++)
        {
            var source = Guid.NewGuid(); var locked = Guid.NewGuid();
            var proof = new PayrollFrozenLineEvidence(2, batch.Id, employee, PayrollComponentKind.BaseCompensation.ToString(),
                "attendance.payroll-salary.base", source, 1, locked, 100m, "PKR", null, null, "synthetic-source", actor, batch.PreparedAt,
                new { BasePay = 100m });
            var reference = JsonSerializer.Serialize(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(proof),
                $"hr:payroll-handoff:{batch.Id:N}:line", default));
            batch.Lines.Add(new(foreignBatch ? Guid.NewGuid() : batch.Id, employee, PayrollComponentKind.BaseCompensation,
                "attendance.payroll-salary.base", source, 1, locked, 100m, "PKR", null, null, reference, $"line-{i}"));
        }
        if (repeatedDeduction is PayrollComponentKind deduction)
        {
            var source = Guid.NewGuid(); var locked = batch.Lines.First().AttendancePeriodLockEventId;
            var sourceType = deduction == PayrollComponentKind.LoanRepayment ? "hr.advance-loan.schedule" : "hr.payroll-salary.component";
            for (var i = 0; i < 2; i++)
            {
                var proof = new PayrollFrozenLineEvidence(2, batch.Id, employee, deduction.ToString(), sourceType,
                    source, 1, locked, -10m, "PKR", null, null, "synthetic-source", actor, batch.PreparedAt);
                var reference = JsonSerializer.Serialize(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(proof),
                    $"hr:payroll-handoff:{batch.Id:N}:line", default));
                batch.Lines.Add(new(batch.Id, employee, deduction, sourceType, source, 1, locked, -10m, "PKR", null, null,
                    reference, $"deduction-{i}"));
            }
        }
        var lines = PayrollFrozenBatchReader.Lines(batch); var fingerprint = PayrollFrozenBatchReader.Fingerprint(batch, lines);
        var salaryProof = new PayrollFrozenEvidence(2, batch.Id, "salary-v2", batch.PeriodStart, batch.PeriodEnd, "PKR", fingerprint,
            lines, PayrollFrozenBatchReader.Totals(lines), batch.Lines.ToDictionary(x => x.Id, x => x.CalculationPrivateReference),
            actor.ToString(), batch.PreparedAt);
        var salaryReference = JsonSerializer.Serialize(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(salaryProof),
            PayrollFrozenBatchReader.Purpose(batch.Id), default));
        batch.SetSalaryEvidence(new('A', 64), fingerprint, salaryReference);
        return (batch, files);
    }

    internal sealed class MemoryFiles : IPrivateFileStore
    {
        private readonly Dictionary<string, (string Purpose, byte[] Bytes)> contents = [];
        public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
        {
            var key = Guid.NewGuid().ToString(); contents.Add(key, (purpose, content));
            return Task.FromResult(new PrivateFileReference(key, "1", new('A', 64), "synthetic", content.Length));
        }
        public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
        {
            var item = contents[reference.StorageKey];
            Assert.Equal(item.Purpose, purpose);
            return Task.FromResult(item.Bytes);
        }
    }

    private static T Proxy<T>(Func<MethodInfo, object?[], object?> callback) where T : class
    {
        var value = DispatchProxy.Create<T, CallbackProxy>(); ((CallbackProxy)(object)value).Callback = callback; return value;
    }
    public class CallbackProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Callback = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Callback(method!, args ?? []);
    }
}
