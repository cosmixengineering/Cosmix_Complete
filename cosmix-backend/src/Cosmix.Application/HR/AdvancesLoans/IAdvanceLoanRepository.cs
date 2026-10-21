using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.AdvancesLoans;

namespace Cosmix.Application.HR.AdvancesLoans;

public interface IAdvanceLoanRepository
{
    Task<AdvanceLoanRequest?> FindRequestAsync(Guid id, CancellationToken ct);
    Task<AdvanceLoanAgreement?> FindAgreementAsync(Guid id, CancellationToken ct);
    Task<AdvanceLoanAgreement?> FindAgreementWithDetailsAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<AdvanceLoanRequest>> ListRequestsForEmployeeAsync(Guid employeeId, CancellationToken ct);
    Task<IReadOnlyList<AdvanceLoanAgreement>> ListAgreementsForEmployeeAsync(Guid employeeId, CancellationToken ct);
    Task<(IReadOnlyList<AdvanceLoanRequest> Items, int TotalCount)> PageRequestsAsync(Guid? employeeId, ApprovalState? state, int skip, int limit, CancellationToken ct);
    Task<(IReadOnlyList<AdvanceLoanAgreement> Items, int TotalCount)> PageAgreementsAsync(Guid? employeeId, bool? activeOnly, int skip, int limit, CancellationToken ct);
    Task<bool> HasPendingRequestAsync(Guid employeeId, AdvanceLoanKind kind, CancellationToken ct);
    Task<RepaymentLedgerEntry?> FindLedgerEntryByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);
    Task<RepaymentLedgerEntry?> FindLedgerEntryAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<RepaymentLedgerEntry>> ListLedgerEntriesAsync(IReadOnlyCollection<Guid> agreementIds, CancellationToken ct);
    Task<decimal> GetTotalRepaidAmountAsync(Guid agreementId, CancellationToken ct);
    void AddRequest(AdvanceLoanRequest request, ApprovalRequest approval);
    void AddAgreement(AdvanceLoanAgreement agreement, ApprovalDecision decision, IEnumerable<RepaymentScheduleEntry> schedule);
    void AddLedgerEntry(RepaymentLedgerEntry entry);
    void AddDecision(ApprovalDecision decision);
    Task SaveChangesAsync(CancellationToken ct);
}
