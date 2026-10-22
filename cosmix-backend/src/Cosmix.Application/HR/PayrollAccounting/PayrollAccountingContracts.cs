using Cosmix.Domain.HR.PayrollAccounting;

namespace Cosmix.Application.HR.PayrollAccounting;

public sealed record FinalizeObligationRequest(
    Guid BatchId,
    decimal TotalGrossAmount,
    decimal TotalNetAmount,
    string Currency);

public sealed record ObligationResponse(
    Guid Id,
    Guid BatchId,
    decimal TotalGrossAmount,
    decimal TotalNetAmount,
    string Currency,
    string State,
    DateTimeOffset? FinalizedAt,
    string? FinalizedByUserId,
    DateTimeOffset CreatedAt);

public sealed record PostAccrualJournalRequest(
    Guid ObligationId,
    string DebitAccountCode,
    string CreditAccountCode,
    string Description);

public sealed record JournalEntryResponse(
    Guid Id,
    Guid ObligationId,
    string Kind,
    string DebitAccountCode,
    string CreditAccountCode,
    decimal Amount,
    string Currency,
    string Description,
    DateTimeOffset PostedAt,
    string PostedByUserId);

public sealed record RecordSalaryPaymentRequest(
    Guid ObligationId,
    Guid AccrualJournalEntryId,
    string PaymentReference,
    string DebitAccountCode,
    string CreditAccountCode,
    decimal? Amount = null,
    Guid? EmployeeId = null);

public sealed record SalaryPaymentResponse(
    Guid Id,
    Guid ObligationId,
    Guid? EmployeeId,
    decimal Amount,
    string State,
    string PaymentReference,
    Guid? PaymentJournalEntryId,
    DateTimeOffset? ConfirmedAt,
    string? ConfirmedByUserId,
    DateTimeOffset CreatedAt);

public interface IPayrollAccountingRepository
{
    Task<PayrollObligation?> GetObligationByIdAsync(Guid id, CancellationToken ct);
    Task<PayrollObligation?> GetObligationByBatchIdAsync(Guid batchId, CancellationToken ct);
    Task<IReadOnlyList<PayrollObligation>> ListObligationsAsync(int page, int pageSize, CancellationToken ct);
    Task<PayrollJournalEntry?> GetJournalEntryByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PayrollJournalEntry>> GetJournalEntriesForObligationAsync(Guid obligationId, CancellationToken ct);
    Task<SalaryPayment?> GetSalaryPaymentByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SalaryPayment>> GetSalaryPaymentsForObligationAsync(Guid obligationId, CancellationToken ct);
    Task AddObligationAsync(PayrollObligation obligation, CancellationToken ct);
    Task AddJournalEntryAsync(PayrollJournalEntry entry, CancellationToken ct);
    Task AddSalaryPaymentAsync(SalaryPayment payment, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
