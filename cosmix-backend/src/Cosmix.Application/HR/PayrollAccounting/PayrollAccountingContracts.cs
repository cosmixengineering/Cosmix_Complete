using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollHandoff;

namespace Cosmix.Application.HR.PayrollAccounting;

public sealed record FinalizeObligationRequest(
    Guid BatchId,
    decimal? TotalGrossAmount = null,
    decimal? TotalNetAmount = null,
    string? Currency = null,
    string? ExpectedSourceFingerprint = null,
    string Reason = "");

public sealed record ObligationResponse(
    Guid Id,
    Guid BatchId,
    decimal TotalGrossAmount,
    decimal TotalNetAmount,
    string Currency,
    string State,
    DateTimeOffset? FinalizedAt,
    string? FinalizedByUserId,
    DateTimeOffset CreatedAt,
    string? SourceFingerprint = null,
    decimal TotalDeductions = 0,
    DateOnly? PeriodStart = null,
    DateOnly? PeriodEndExclusive = null);

public sealed record PayrollDeductionItem(
    string DebitAccountCode,
    string CreditAccountCode,
    decimal Amount,
    string Description);

public sealed record PostAccrualJournalRequest(
    Guid ObligationId,
    string? DebitAccountCode = null,
    string? CreditAccountCode = null,
    string Description = "Salary accrual",
    IReadOnlyList<PayrollDeductionItem>? Deductions = null,
    Guid? PostingProfileId = null,
    DateOnly? PostingDate = null,
    string Reason = "");

public sealed record PostStatutoryJournalRequest(
    Guid ObligationId,
    string? Scheme = null,
    string? DebitAccountCode = null,
    string? CreditAccountCode = null,
    decimal? Amount = null,
    string Description = "Employer statutory contribution",
    Guid? StatutoryContributionId = null,
    Guid? PostingProfileId = null,
    DateOnly? PostingDate = null,
    string Reason = "");

public sealed record BatchEmployeeEntitlementDto(
    Guid EmployeeId,
    decimal GrossAmount,
    decimal DeductionsAmount,
    decimal NetSalary);

public sealed record BatchEmployeeEntitlementResponse(
    Guid EmployeeId,
    decimal NetEntitlement,
    decimal DisbursedAmount,
    decimal RemainingBalance);

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
    string PostedByUserId,
    DateOnly? PostingDate = null,
    Guid? AccountingPeriodId = null,
    string? SourceKey = null,
    Guid? StatutoryContributionId = null);

public sealed record RecordSalaryPaymentRequest(
    Guid ObligationId,
    Guid AccrualJournalEntryId,
    string PaymentReference,
    string DebitAccountCode,
    string CreditAccountCode,
    decimal? Amount = null,
    Guid? EmployeeId = null,
    DateOnly? PostingDate = null,
    string Reason = "");

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
    DateTimeOffset CreatedAt,
    string? FailureReason = null,
    Guid? ReversalJournalEntryId = null,
    DateTimeOffset? ReversedAt = null,
    DateTimeOffset? ReversalRequestedAt = null,
    int Version = 1,
    Guid? BankExportId = null,
    string? CreatedByUserId = null,
    string? ReversalRequestedByUserId = null,
    bool ReversalIsFailure = false);

public sealed record ConfirmSalaryPaymentRequest(string Reason, string? Evidence = null, int ExpectedVersion = 0);
public sealed record RequestSalaryPaymentReversalRequest(string Reason, string Evidence, bool PaymentFailed, int ExpectedVersion);
public sealed record ApproveSalaryPaymentReversalRequest(string Reason, string Evidence, DateOnly PostingDate, int ExpectedVersion);

public interface IPayrollAccountingRepository
{
    Task<PayrollObligation?> GetObligationByIdAsync(Guid id, CancellationToken ct);
    Task<PayrollObligation?> GetObligationByBatchIdAsync(Guid batchId, CancellationToken ct);
    Task<IReadOnlyList<PayrollObligation>> ListObligationsAsync(int page, int pageSize, CancellationToken ct);
    /// <summary>Approved locked salary navigation, with exactly pageSize + 1 look-ahead rows at most.</summary>
    Task<IReadOnlyList<PayrollSettlementBatchSummary>> ListSettlementBatchesAsync(int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
    Task<PayrollJournalEntry?> GetJournalEntryByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PayrollJournalEntry>> GetJournalEntriesForObligationAsync(Guid obligationId, CancellationToken ct);
    Task<SalaryPayment?> GetSalaryPaymentByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SalaryPayment>> GetSalaryPaymentsForObligationAsync(Guid obligationId, CancellationToken ct);
    Task<IReadOnlyList<PayrollHandoffLine>> GetBatchLinesAsync(Guid batchId, CancellationToken ct);
    Task<PayrollHandoffBatch?> GetBatchByIdAsync(Guid batchId, CancellationToken ct);
    Task<Cosmix.Domain.HR.StatutoryContributions.StatutoryContribution?> GetStatutoryContributionByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Cosmix.Domain.HR.StatutoryContributions.StatutoryContribution>> GetApprovedStatutoryContributionsForMonthAsync(DateOnly month, CancellationToken ct);
    Task AddObligationAsync(PayrollObligation obligation, CancellationToken ct);
    Task AddJournalEntryAsync(PayrollJournalEntry entry, CancellationToken ct);
    Task AddSalaryPaymentAsync(SalaryPayment payment, CancellationToken ct);
    Task RefreshSalaryPaymentAsync(SalaryPayment payment, CancellationToken ct) => Task.CompletedTask;
    Task<PayrollBankFormatProfile?> GetBankFormatAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    Task<IReadOnlyList<PayrollBankFormatProfile>> ListBankFormatsAsync(CancellationToken ct) => throw new NotSupportedException();
    Task<PayrollBankExport?> GetBankExportAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    Task AddBankFormatAsync(PayrollBankFormatProfile profile, CancellationToken ct) => throw new NotSupportedException();
    Task AddBankExportAsync(PayrollBankExport export, CancellationToken ct) => throw new NotSupportedException();
    Task<IReadOnlyList<PayrollBankExport>> ListBankExportsAsync(Guid obligationId, CancellationToken ct) => throw new NotSupportedException();
    Task<PayrollBankExportRenewal?> LatestBankExportRenewalAsync(Guid exportId, CancellationToken ct) => throw new NotSupportedException();
    Task AddBankExportRenewalAsync(PayrollBankExportRenewal renewal, CancellationToken ct) => throw new NotSupportedException();
    Task AcquirePaymentLockAsync(Guid obligationId, Guid employeeId, CancellationToken ct) => Task.CompletedTask;
    Task AcquireStatutoryPostingLockAsync(Guid contributionId, CancellationToken ct) => Task.CompletedTask;
    Task<IReadOnlyList<Cosmix.Domain.HR.StatutoryContributions.StatutoryEmployeePosting>> ContributionPostingsAsync(Guid contributionId, CancellationToken ct)
        => throw new NotSupportedException();
    Task AddContributionPostingAsync(Cosmix.Domain.HR.StatutoryContributions.StatutoryEmployeePosting row, CancellationToken ct)
        => throw new NotSupportedException();
    Task SaveChangesAsync(CancellationToken ct);
}
