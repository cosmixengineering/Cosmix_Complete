using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

/// <summary>Called only inside EmployeeOperation's serializable current-authorization transaction.</summary>
public sealed class EmployeeRecordRepository(CosmixDbContext db) : IEmployeeRecordRepository
{
    public Task<EmployeeAssetCustody?> AssetAsync(Guid id, CancellationToken ct) => db.Set<EmployeeAssetCustody>().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<EmployeeAssetCustody?> LatestAssetAsync(string reference, CancellationToken ct)
    {
        var rows = db.Set<EmployeeAssetCustody>();
        var open = await rows.Where(x => x.AssetReference == reference && x.ReturnedAt == null).Take(2).ToArrayAsync(ct);
        if (open.Length > 1) throw EmployeeOperation.Conflict("asset_custody_history_requires_review");
        return open.SingleOrDefault() ?? await rows.Where(x => x.AssetReference == reference).OrderByDescending(x => x.ReturnedAt).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
    }
    public async Task<IReadOnlyList<EmployeeAssetHistory>> AssetHistoryAsync(Guid employeeId, Guid custodyId, CancellationToken ct) => await db.Set<EmployeeAssetHistory>().AsNoTracking().Where(x => x.EmployeeId == employeeId && x.EmployeeAssetCustodyId == custodyId).OrderBy(x => x.Revision).ToArrayAsync(ct);
    public Task<PagedResult<EmployeeAssetCustody>> AssetsAsync(Guid employeeId, bool history, DateTimeOffset asOf, EmployeeRecordQuery query, CancellationToken ct)
    {
        var all = db.Set<EmployeeAssetCustody>().AsNoTracking();
        var rows = all.Where(x => x.EmployeeId == employeeId);
        if (!history) rows = rows.Where(x => x.IssuedAt <= asOf && (x.ReturnedAt == null || x.ReturnedAt > asOf));
        return PageAsync(rows.OrderByDescending(x => x.IssuedAt).ThenBy(x => x.Id), query, ct);
    }
    public Task<EmployeeSimAssignment?> SimAsync(Guid id, CancellationToken ct) => db.Set<EmployeeSimAssignment>().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> SimSuccessorAsync(Guid id, CancellationToken ct) => db.Set<EmployeeSimAssignment>().AnyAsync(x => x.SupersedesEmployeeSimAssignmentId == id, ct);
    public async Task<IReadOnlyList<EmployeeSimAssignment>> LatestSimsAsync(string reference, string phone, DateOnly asOf, CancellationToken ct)
    {
        var rows = db.Set<EmployeeSimAssignment>();
        return await rows.Where(x => (x.SimReference == reference || x.PhoneNumber == phone) &&
            (x.Status != CompanySimStatus.Returned || x.ReturnedOn > asOf) &&
            !rows.Any(n => n.SupersedesEmployeeSimAssignmentId == x.Id)).OrderByDescending(x => x.EffectiveOn)
            .ThenByDescending(x => x.RecordedAt).ThenByDescending(x => x.Revision).Take(2).ToArrayAsync(ct);
    }
    public Task<PagedResult<EmployeeSimAssignment>> SimsAsync(Guid employeeId, bool history, DateOnly asOf, EmployeeRecordQuery query, CancellationToken ct)
    {
        var all = db.Set<EmployeeSimAssignment>().AsNoTracking();
        var rows = all.Where(x => x.EmployeeId == employeeId);
        if (!history) rows = rows.Where(x => x.EffectiveOn <= asOf && x.Status != CompanySimStatus.Returned &&
            !all.Any(n => n.SupersedesEmployeeSimAssignmentId == x.Id && n.EffectiveOn <= asOf));
        return PageAsync(rows.OrderByDescending(x => x.EffectiveOn).ThenByDescending(x => x.Revision).ThenBy(x => x.Id), query, ct);
    }
    public Task<EmployeeCertification?> CertificationAsync(Guid id, CancellationToken ct) => db.Set<EmployeeCertification>().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> CertificationSuccessorAsync(Guid id, CancellationToken ct) => db.Set<EmployeeCertification>().AnyAsync(x => x.SupersedesEmployeeCertificationId == id, ct);
    public Task<bool> ActiveCertificationAsync(Guid employeeId, string type, string number, string issuer, CancellationToken ct)
    {
        var rows = db.Set<EmployeeCertification>();
        // A revoked credential remains a terminal current identity. A fresh Issue cannot
        // revive the same number/issuer/proof beside its retained revoked tip.
        return rows.AnyAsync(x => x.EmployeeId == employeeId && x.CertificateType == type && x.CertificateNumber == number &&
            x.Issuer == issuer && !rows.Any(n => n.SupersedesEmployeeCertificationId == x.Id), ct);
    }
    public Task<PagedResult<EmployeeCertification>> CertificationsAsync(Guid employeeId, bool history, DateOnly asOf, EmployeeRecordQuery query, CancellationToken ct)
    {
        var all = db.Set<EmployeeCertification>().AsNoTracking();
        var rows = all.Where(x => x.EmployeeId == employeeId);
        if (!history) rows = rows.Where(x => x.EffectiveOn <= asOf && !all.Any(n => n.SupersedesEmployeeCertificationId == x.Id && n.EffectiveOn <= asOf));
        return PageAsync(rows.OrderByDescending(x => x.EffectiveOn).ThenByDescending(x => x.Revision).ThenBy(x => x.Id), query, ct);
    }
    public Task<bool> HasPendingAsync(Guid employeeId, string requestType, CancellationToken ct) => db.ApprovalRequests.AnyAsync(x => x.SubjectId == employeeId && x.RequestType == requestType && x.State == ApprovalState.Pending, ct);
    public Task<ApprovalRequest?> RequestAsync(Guid employeeId, string requestType, Guid id, CancellationToken ct) => db.ApprovalRequests.SingleOrDefaultAsync(x => x.SubjectId == employeeId && x.RequestType == requestType && x.Id == id, ct);
    public async Task<PagedResult<EmployeeRecordRequestSummary>> RequestsAsync(Guid? employeeId, string type, EmployeeRecordQuery query, CancellationToken ct)
    {
        var rows = db.ApprovalRequests.AsNoTracking().Where(x => x.RequestType == type);
        if (employeeId.HasValue) rows = rows.Where(x => x.SubjectId == employeeId);
        if (query.State.HasValue) rows = rows.Where(x => x.State == query.State);
        var count = await rows.CountAsync(ct);
        var page = await rows.OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return new(page.Select(x => new EmployeeRecordRequestSummary(x.Id, x.SubjectId, x.State, x.RequesterUserId,
            x.RequestedAt, x.ClosedAt, Convert.ToBase64String(x.RowVersion))).ToArray(), count, query.Page, query.PageSize);
    }
    public async Task<IReadOnlyList<ApprovalDecision>> DecisionsAsync(Guid requestId, CancellationToken ct) => await db.ApprovalDecisions.AsNoTracking().Where(x => x.ApprovalRequestId == requestId).OrderBy(x => x.DecidedAt).ThenBy(x => x.Id).ToArrayAsync(ct);
    public void ExpectVersion(ApprovalRequest request, byte[] version)
    {
        if (!request.RowVersion.SequenceEqual(version)) throw EmployeeOperation.Conflict("employee_record_request_version_conflict");
        db.Entry(request).Property(x => x.RowVersion).OriginalValue = version.ToArray();
    }
    public void Add(EmployeeAssetCustody row) => db.Set<EmployeeAssetCustody>().Add(row);
    public void Add(EmployeeAssetHistory row) => db.Set<EmployeeAssetHistory>().Add(row);
    public void Add(EmployeeSimAssignment row) => db.Set<EmployeeSimAssignment>().Add(row);
    public void Add(EmployeeCertification row) => db.Set<EmployeeCertification>().Add(row);
    public void Add(ApprovalRequest row) => db.ApprovalRequests.Add(row);
    public void Add(ApprovalDecision row) => db.ApprovalDecisions.Add(row);
    private static async Task<PagedResult<T>> PageAsync<T>(IOrderedQueryable<T> rows, EmployeeRecordQuery query, CancellationToken ct) where T : class =>
        new(await rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct), await rows.CountAsync(ct), query.Page, query.PageSize);
}
