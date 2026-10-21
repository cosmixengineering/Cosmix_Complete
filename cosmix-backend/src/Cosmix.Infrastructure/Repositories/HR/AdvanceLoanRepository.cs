using Cosmix.Application.HR.AdvancesLoans;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.AdvancesLoans;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class AdvanceLoanRepository(CosmixDbContext db) : IAdvanceLoanRepository
{
    public Task<AdvanceLoanRequest?> FindRequestAsync(Guid id, CancellationToken ct) =>
        db.AdvanceLoanRequests
            .Include(r => r.Employee)
            .Include(r => r.ApprovalRequest)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<AdvanceLoanAgreement?> FindAgreementAsync(Guid id, CancellationToken ct) =>
        db.AdvanceLoanAgreements
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<AdvanceLoanAgreement?> FindAgreementWithDetailsAsync(Guid id, CancellationToken ct) =>
        db.AdvanceLoanAgreements
            .Include(a => a.AdvanceLoanRequest)
                .ThenInclude(r => r.Employee)
            .Include(a => a.AdvanceLoanRequest)
                .ThenInclude(r => r.ApprovalRequest)
            .Include(a => a.Schedule)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<AdvanceLoanRequest>> ListRequestsForEmployeeAsync(Guid employeeId, CancellationToken ct) =>
        await db.AdvanceLoanRequests
            .AsNoTracking()
            .Include(r => r.Employee)
            .Include(r => r.ApprovalRequest)
            .Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.CreatedAt)
            .ToArrayAsync(ct);

    public async Task<IReadOnlyList<AdvanceLoanAgreement>> ListAgreementsForEmployeeAsync(Guid employeeId, CancellationToken ct) =>
        await db.AdvanceLoanAgreements
            .AsNoTracking()
            .Include(a => a.AdvanceLoanRequest)
                .ThenInclude(r => r.Employee)
            .Include(a => a.Schedule)
            .Where(a => a.AdvanceLoanRequest.EmployeeId == employeeId)
            .OrderByDescending(a => a.RecordedAt)
            .ToArrayAsync(ct);

    public async Task<(IReadOnlyList<AdvanceLoanRequest> Items, int TotalCount)> PageRequestsAsync(
        Guid? employeeId,
        ApprovalState? state,
        int skip,
        int limit,
        CancellationToken ct)
    {
        var query = db.AdvanceLoanRequests
            .AsNoTracking()
            .Include(r => r.Employee)
            .Include(r => r.ApprovalRequest)
            .AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(r => r.EmployeeId == employeeId.Value);

        if (state.HasValue)
            query = query.Where(r => r.ApprovalRequest != null && r.ApprovalRequest.State == state.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Skip(skip)
            .Take(limit)
            .ToArrayAsync(ct);

        return (items, total);
    }

    public async Task<(IReadOnlyList<AdvanceLoanAgreement> Items, int TotalCount)> PageAgreementsAsync(
        Guid? employeeId,
        bool? activeOnly,
        int skip,
        int limit,
        CancellationToken ct)
    {
        var query = db.AdvanceLoanAgreements
            .AsNoTracking()
            .Include(a => a.AdvanceLoanRequest)
                .ThenInclude(r => r.Employee)
            .Include(a => a.Schedule)
            .AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(a => a.AdvanceLoanRequest.EmployeeId == employeeId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.RecordedAt)
            .ThenBy(a => a.Id)
            .Skip(skip)
            .Take(limit)
            .ToArrayAsync(ct);

        return (items, total);
    }

    public Task<bool> HasPendingRequestAsync(Guid employeeId, AdvanceLoanKind kind, CancellationToken ct) =>
        db.AdvanceLoanRequests
            .AsNoTracking()
            .AnyAsync(r => r.EmployeeId == employeeId &&
                           r.Kind == kind &&
                           r.ApprovalRequest != null &&
                           r.ApprovalRequest.State == ApprovalState.Pending, ct);

    public Task<RepaymentLedgerEntry?> FindLedgerEntryByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) =>
        db.RepaymentLedgerEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.IdempotencyKey == idempotencyKey, ct);

    public Task<RepaymentLedgerEntry?> FindLedgerEntryAsync(Guid id, CancellationToken ct) =>
        db.RepaymentLedgerEntries
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<RepaymentLedgerEntry>> ListLedgerEntriesAsync(
        IReadOnlyCollection<Guid> agreementIds, CancellationToken ct)
    {
        if (agreementIds.Count == 0) return [];
        return await db.RepaymentLedgerEntries
            .AsNoTracking()
            .Where(e => agreementIds.Contains(e.AdvanceLoanAgreementId))
            .OrderBy(e => e.RecordedAt)
            .ThenBy(e => e.Id)
            .ToArrayAsync(ct);
    }

    public async Task<decimal> GetTotalRepaidAmountAsync(Guid agreementId, CancellationToken ct)
    {
        var entries = await db.RepaymentLedgerEntries
            .AsNoTracking()
            .Where(e => e.AdvanceLoanAgreementId == agreementId)
            .Select(e => new { e.Kind, e.Amount })
            .ToArrayAsync(ct);

        var payments = entries.Where(e => e.Kind != RepaymentEntryKind.Reversal).Sum(e => e.Amount);
        var reversals = entries.Where(e => e.Kind == RepaymentEntryKind.Reversal).Sum(e => Math.Abs(e.Amount));

        return Math.Max(0m, payments - reversals);
    }

    public void AddRequest(AdvanceLoanRequest request, ApprovalRequest approval)
    {
        db.AdvanceLoanRequests.Add(request);
        db.ApprovalRequests.Add(approval);
    }

    public void AddAgreement(AdvanceLoanAgreement agreement, ApprovalDecision decision, IEnumerable<RepaymentScheduleEntry> schedule)
    {
        db.ApprovalDecisions.Add(decision);
        db.AdvanceLoanAgreements.Add(agreement);
        db.RepaymentScheduleEntries.AddRange(schedule);
    }

    public void AddLedgerEntry(RepaymentLedgerEntry entry) =>
        db.RepaymentLedgerEntries.Add(entry);

    public void AddDecision(ApprovalDecision decision) =>
        db.ApprovalDecisions.Add(decision);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);
}
