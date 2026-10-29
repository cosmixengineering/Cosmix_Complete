using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Application.HR.Payslips;
using Xunit;

namespace Cosmix.Tests;
public sealed class PayrollPayslipTests
{
    [Theory]
    [InlineData("pdf")]
    [InlineData("xlsx")]
    public async Task Export_contains_own_frozen_breakdown_and_exact_totals(string format)
    {
        var fixture = new Fixture();
        var file = await fixture.Service.DocumentInTransactionAsync(fixture.Batch, fixture.Employee, format, default);
        var bytes = Convert.FromBase64String(file.Base64);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), file.Sha256);
        Assert.InRange(bytes.Length, 100, 1024 * 1024);
        string content;
        if (format == "pdf")
        {
            content = Encoding.ASCII.GetString(bytes); Assert.StartsWith("%PDF-", content);
        }
        else
        {
            using var zip = new ZipArchive(new MemoryStream(bytes));
            content = string.Join("\n", zip.Entries.Where(x => x.FullName.EndsWith(".xml")).Select(x => { using var reader = new StreamReader(x.Open()); return reader.ReadToEnd(); }));
        }
        Assert.Contains("31000.00", content); Assert.Contains("1000.00", content); Assert.Contains("30000.00", content);
        Assert.DoesNotContain("999999.00", content);
    }
    [Fact]
    public async Task Foreign_employee_denied_before_frozen_batch_is_read()
    {
        var fixture = new Fixture();
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Service.DocumentInTransactionAsync(fixture.Batch, Guid.NewGuid(), "pdf", default));
        Assert.Equal(404, failure.StatusCode); Assert.Equal(0, fixture.Reads);
    }
    [Theory]
    [InlineData(0, "Unpaid")]
    public async Task Zero_only_legacy_fixture_can_report_unpaid_without_claiming_a_transfer(decimal paid, string expected)
    {
        var fixture = new Fixture { Paid = paid };
        var page = await fixture.Service.OwnPageInTransactionAsync(fixture.Employee, 1, 20, default);
        Assert.Equal(expected, Assert.Single(page.Items).PaymentState);
    }
    [Theory]
    [InlineData(10000)]
    [InlineData(30000)]
    public async Task Raw_confirmed_sum_cannot_label_payslip_paid_without_retained_proof_validation(decimal paid)
    {
        var fixture = new Fixture { Paid = paid };
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Service.OwnPageInTransactionAsync(fixture.Employee, 1, 20, default));
        Assert.Equal("payslip_payment_evidence_validator_required", failure.Code);
    }
    [Fact]
    public async Task Inconsistent_frozen_totals_and_overpayment_fail_closed()
    {
        var fixture = new Fixture { InvalidTotal = true };
        await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Service.DocumentInTransactionAsync(fixture.Batch, fixture.Employee, "pdf", default));
        fixture.InvalidTotal = false; fixture.Paid = 30001;
        await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Service.OwnPageInTransactionAsync(fixture.Employee, 1, 20, default));
    }
    private sealed class Fixture : IPayslipRepository, IPayrollFrozenBatchReader
    {
        public Guid Employee { get; } = Guid.NewGuid();
        public Guid Batch { get; } = Guid.NewGuid();
        public int Reads { get; private set; }
        public decimal Paid { get; set; }
        public bool InvalidTotal { get; set; }
        public PayslipService Service => new(null!, this, this, TimeProvider.System);
        public Task<IReadOnlyList<Guid>> OwnBatchIdsAsync(Guid employeeId, int page, int pageSize, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>(employeeId == Employee ? [Batch] : []);
        public Task<bool> ContainsEmployeeAsync(Guid batchId, Guid employeeId, CancellationToken ct) => Task.FromResult(batchId == Batch && employeeId == Employee);
        public Task<PayslipBatchIdentity?> BatchAsync(Guid batchId, CancellationToken ct) => Task.FromResult<PayslipBatchIdentity?>(new(Batch, "TEST-SALARY-202610", "AAAAAAAAAAE=", DateTimeOffset.UtcNow));
        public Task<PayslipEmployeeIdentity?> EmployeeAsync(Guid employeeId, CancellationToken ct) => Task.FromResult<PayslipEmployeeIdentity?>(new(Employee, 123, "Synthetic employee"));
        public Task<decimal> ConfirmedPaymentsAsync(Guid batchId, Guid employeeId, CancellationToken ct) => Task.FromResult(Paid);
        public Task<PayrollFrozenBatch> ReadAsync(Guid batchId, CancellationToken ct)
        {
            Reads++;
            PayrollHandoffLineResponse Line(Guid employee, string component, decimal amount) => new(Guid.NewGuid(), employee,
                component, "test", Guid.NewGuid(), 1, Guid.NewGuid(), amount, "PKR", null, null, "trace");
            return Task.FromResult(new PayrollFrozenBatch(Batch, "salary-v2", new(2026, 10, 1), new(2026, 11, 1), "PKR", new string('A', 64),
                [Line(Employee, "BaseCompensation", 31000), Line(Employee, "AttendanceDeduction", -1000), Line(Guid.NewGuid(), "BaseCompensation", 999999)],
                [new(Employee, 31000, 1000, InvalidTotal ? 31000 : 30000)], "private"));
        }
    }
}
