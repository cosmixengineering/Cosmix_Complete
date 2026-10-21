using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Compensation;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class EmployeeCompensationRepository(CosmixDbContext db) : IEmployeeCompensationRepository
{
    public Task<bool> HasConflictingHistoryAsync(Guid employeeId, CancellationToken ct)
    {
        var history = db.CompensationRecords.AsNoTracking().Where(x => x.EmployeeId == employeeId);
        // Unique revisions and valid, decreasing predecessor links imply one connected revision-one root.
        return history.AnyAsync(record =>
            record.Revision < 1 ||
            (record.EffectiveTo != null && record.EffectiveTo <= record.EffectiveFrom) ||
            history.Any(other => other.Id != record.Id &&
                (other.Revision == record.Revision || other.EffectiveFrom == record.EffectiveFrom)) ||
            (record.Revision == 1 && record.SupersedesCompensationRecordId != null) ||
            (record.Revision > 1 && !history.Any(previous =>
                previous.Id == record.SupersedesCompensationRecordId && previous.Revision == record.Revision - 1 &&
                previous.EffectiveFrom < record.EffectiveFrom &&
                (previous.EffectiveTo == null || previous.EffectiveTo <= record.EffectiveFrom))) ||
            (record.Revision > 1 && record.ApprovalRequestId == null) ||
            (record.ApprovalRequestId != null && !db.ApprovalRequests.Any(request =>
                request.Id == record.ApprovalRequestId && request.SubjectId == employeeId &&
                request.RequestType == EmployeeCompensationService.RequestType && request.State == ApprovalState.Approved &&
                request.ClosedAt != null && request.ClosedAt >= request.RequestedAt && request.ClosedAt <= record.RecordedAt &&
                db.ApprovalDecisions.Count(decision => decision.ApprovalRequestId == request.Id) == 1 &&
                db.ApprovalDecisions.Any(decision => decision.ApprovalRequestId == request.Id &&
                    decision.Decision == ApprovalState.Approved && decision.ActorUserId == record.RecordedByUserId &&
                    decision.ActorUserId != request.RequesterUserId && decision.ActorUserId != "" &&
                    decision.DecidedAt == request.ClosedAt && decision.DecidedAt >= request.RequestedAt &&
                    decision.DecidedAt <= record.RecordedAt))), ct);
    }

    public async Task<CompensationWindow> WindowAsync(Guid employeeId, DateOnly asOf, CancellationToken ct)
    {
        var history = db.CompensationRecords.AsNoTracking().Where(x => x.EmployeeId == employeeId);
        // Pick the newest started version even when expired, so older open versions cannot become current again.
        var candidate = await history.Where(x => x.EffectiveFrom <= asOf)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Revision).ThenBy(x => x.Id)
            .FirstOrDefaultAsync(ct);
        var next = await history.Where(x => x.EffectiveFrom > asOf)
            .OrderBy(x => x.EffectiveFrom).ThenBy(x => x.Revision).ThenBy(x => x.Id)
            .FirstOrDefaultAsync(ct);
        var latest = await history.OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.Revision).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
        return new(candidate, next, latest);
    }

    public async Task<PagedResult<CompensationEntry>> HistoryAsync(Guid employeeId,
        CompensationPageQuery query, CancellationToken ct)
    {
        ValidatePage(query.Page, query.PageSize);
        var history = db.CompensationRecords.AsNoTracking().Where(x => x.EmployeeId == employeeId);
        var count = await history.CountAsync(ct);
        var rows = await history.OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Revision).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(record => new
            {
                Record = record,
                NextStart = history.Where(next => next.EffectiveFrom > record.EffectiveFrom)
                    .Select(next => (DateOnly?)next.EffectiveFrom).Min()
            }).ToArrayAsync(ct);
        return new(rows.Select(x =>
        {
            var record = x.Record;
            var end = record.EffectiveTo;
            if (x.NextStart.HasValue && (!end.HasValue || x.NextStart < end)) end = x.NextStart;
            return new CompensationEntry(record.Id, record.Revision, record.SupersedesCompensationRecordId,
                new(record.BaseAmount, record.GrossAmount, record.Currency, record.WageBasis, record.PaymentMethod),
                record.EffectiveFrom, end, record.EffectiveTo, record.ApprovalRequestId, record.RecordedByUserId, record.RecordedAt);
        }).ToArray(), count, query.Page, query.PageSize);
    }

    public Task<bool> HasPendingRequestAsync(Guid employeeId, CancellationToken ct) =>
        db.ApprovalRequests.AsNoTracking().AnyAsync(x => x.RequestType == EmployeeCompensationService.RequestType &&
            x.SubjectId == employeeId && x.State == ApprovalState.Pending, ct);

    public Task<ApprovalRequest?> FindRequestAsync(Guid employeeId, Guid requestId, CancellationToken ct) =>
        db.ApprovalRequests.SingleOrDefaultAsync(x => x.RequestType == EmployeeCompensationService.RequestType &&
            x.SubjectId == employeeId && x.Id == requestId, ct);

    public async Task<IReadOnlyList<ApprovalDecision>> DecisionsAsync(Guid requestId, CancellationToken ct) =>
        await db.ApprovalDecisions.AsNoTracking().Where(x => x.ApprovalRequestId == requestId &&
                x.Request.RequestType == EmployeeCompensationService.RequestType)
            .OrderBy(x => x.DecidedAt).ThenBy(x => x.Id).ToArrayAsync(ct);

    public async Task<PagedResult<CompensationRequestSummary>> RequestsAsync(Guid? employeeId,
        CompensationRequestQuery query, CancellationToken ct)
    {
        ValidatePage(query.Page, query.PageSize);
        var requests = db.ApprovalRequests.AsNoTracking()
            .Where(x => x.RequestType == EmployeeCompensationService.RequestType);
        if (employeeId.HasValue) requests = requests.Where(x => x.SubjectId == employeeId.Value);
        if (query.State.HasValue) requests = requests.Where(x => x.State == query.State.Value);
        var count = await requests.CountAsync(ct);
        var rows = await requests.OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new { x.Id, x.SubjectId, x.State, x.RequesterUserId, x.RequestedAt, x.ClosedAt, x.RowVersion })
            .ToArrayAsync(ct);
        return new(rows.Select(x => new CompensationRequestSummary(x.Id, x.SubjectId, x.State,
            x.RequesterUserId, x.RequestedAt, x.ClosedAt, Convert.ToBase64String(x.RowVersion))).ToArray(),
            count, query.Page, query.PageSize);
    }

    public void ExpectVersion(ApprovalRequest request, byte[] version)
    {
        if (!request.RowVersion.SequenceEqual(version)) throw EmployeeOperation.Conflict("compensation_request_version_conflict");
        db.Entry(request).Property(x => x.RowVersion).OriginalValue = version;
    }

    public void AddRequest(ApprovalRequest request) => db.ApprovalRequests.Add(request);
    public void AddDecision(ApprovalDecision decision) => db.ApprovalDecisions.Add(decision);
    public void AddCompensation(CompensationRecord record) => db.CompensationRecords.Add(record);

    private static void ValidatePage(int page, int pageSize)
    {
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 100)
            throw EmployeeOperation.Invalid("invalid_compensation_pagination");
    }
}
