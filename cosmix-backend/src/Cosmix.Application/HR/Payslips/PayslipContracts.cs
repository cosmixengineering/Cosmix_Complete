namespace Cosmix.Application.HR.Payslips;

public sealed record PayslipDocument(string FileName, string ContentType, string Base64, string Sha256);
public sealed record PayslipSummary(Guid BatchId, string BatchReference, DateOnly PeriodStart, DateOnly PeriodEndExclusive,
    string Currency, decimal GrossAmount, decimal DeductionAmount, decimal NetAmount, string PayrollState, string PaymentState, string Version);
public sealed record PayslipPage(IReadOnlyList<PayslipSummary> Items, int Page, int PageSize, bool HasMore);
public sealed record PayslipBatchIdentity(Guid Id, string Reference, string Version, DateTimeOffset? LockedAt);
public sealed record PayslipEmployeeIdentity(Guid Id, long Number, string Name);
public interface IPayslipRepository
{
    Task<IReadOnlyList<Guid>> OwnBatchIdsAsync(Guid employeeId, int page, int pageSize, CancellationToken ct);
    Task<bool> ContainsEmployeeAsync(Guid batchId, Guid employeeId, CancellationToken ct);
    Task<PayslipBatchIdentity?> BatchAsync(Guid batchId, CancellationToken ct);
    Task<PayslipEmployeeIdentity?> EmployeeAsync(Guid employeeId, CancellationToken ct);
    Task<decimal> ConfirmedPaymentsAsync(Guid batchId, Guid employeeId, CancellationToken ct);
}
