namespace Cosmix.Application.HR.PayrollAccounting;

/// <summary>Read-only report navigation; contains no banking, employee or posting-control data.</summary>
public sealed record PayrollSettlementBatchSummary(Guid BatchId, string BatchReference, DateOnly PeriodStart,
    DateOnly PeriodEndExclusive, string Currency);
public sealed record PayrollSettlementBatchPage(IReadOnlyList<PayrollSettlementBatchSummary> Items,
    int Page, int PageSize, bool HasMore);
