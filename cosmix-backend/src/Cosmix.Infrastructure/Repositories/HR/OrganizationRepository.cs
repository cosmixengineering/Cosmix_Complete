using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Branches;
using Cosmix.Domain.HR.Departments;
using Cosmix.Domain.HR.Designations;
using Cosmix.Domain.HR.EmploymentTypes;
using Cosmix.Domain.HR.Holidays;
using Cosmix.Domain.HR.Organization;
using Cosmix.Domain.HR.StaffCategories;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class OrganizationRepository(CosmixDbContext db) : IOrganizationRepository
{
    public async Task<PagedResult<OrganizationMasterResponse>> ListAsync(OrganizationMasterKind kind,
        OrganizationListRequest request, CancellationToken ct)
    {
        var query = MasterQuery(kind);
        if (request.IsActive.HasValue) query = query.Where(x => x.IsActive == request.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.Code.Contains(search) || x.Name.Contains(search));
        }
        var count = await query.CountAsync(ct);
        var ordered = request.SortBy == "name"
            ? request.Descending ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name)
            : request.Descending ? query.OrderByDescending(x => x.Code) : query.OrderBy(x => x.Code);
        var rows = await ordered.ThenBy(x => x.Id).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        return new(rows.Select(Map).ToArray(), count, request.Page, request.PageSize);
    }

    public async Task<OrganizationMasterResponse?> GetAsync(OrganizationMasterKind kind, Guid id, CancellationToken ct)
    {
        var row = await MasterQuery(kind).SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : Map(row);
    }

    public async Task<OrganizationMasterResponse> CreateAsync(OrganizationMasterKind kind,
        CreateOrganizationMasterRequest request, string? cityName, CancellationToken ct)
    {
        object entity = kind switch
        {
            OrganizationMasterKind.Department => new Department(request.Code, request.Name),
            OrganizationMasterKind.Designation => new Designation(request.Code, request.Name),
            OrganizationMasterKind.Branch => new Branch(request.Code, request.Name, cityName, request.CityId),
            OrganizationMasterKind.City => new City(request.Code, request.Name, request.Province),
            OrganizationMasterKind.StaffCategory => new StaffCategory(request.Code, request.Name),
            OrganizationMasterKind.EmploymentType => new EmploymentType(request.Code, request.Name),
            _ => throw OrganizationOperation.Invalid()
        };
        db.Add(entity);
        await db.SaveChangesAsync(ct);
        return await GetAsync(kind, (Guid)db.Entry(entity).Property("Id").CurrentValue!, ct)
            ?? throw OrganizationOperation.Missing();
    }

    public async Task<OrganizationMasterResponse> UpdateAsync(OrganizationMasterKind kind, Guid id,
        UpdateOrganizationMasterRequest request, string? cityName, CancellationToken ct)
    {
        var entity = await FindTrackedAsync(kind, id, ct);
        ExpectVersion(entity, request.ExpectedVersion);
        switch (entity)
        {
            case Department value: value.Update(request.Name); break;
            case Designation value: value.Update(request.Name); break;
            case Branch value: value.Update(request.Name, request.CityId, cityName); break;
            case City value: value.Update(request.Name, request.Province); break;
            case StaffCategory value: value.Update(request.Name); break;
            case EmploymentType value: value.Update(request.Name); break;
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(kind, id, ct) ?? throw OrganizationOperation.Missing();
    }

    public async Task<OrganizationMasterResponse> SetStatusAsync(OrganizationMasterKind kind, Guid id,
        SetOrganizationStatusRequest request, CancellationToken ct)
    {
        var entity = await FindTrackedAsync(kind, id, ct);
        ExpectVersion(entity, request.ExpectedVersion);
        var active = request.IsActive!.Value;
        switch (entity)
        {
            case Department value: value.SetActive(active); break;
            case Designation value: value.SetActive(active); break;
            case Branch value: value.SetActive(active); break;
            case City value: value.SetActive(active); break;
            case StaffCategory value: value.SetActive(active); break;
            case EmploymentType value: value.SetActive(active); break;
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(kind, id, ct) ?? throw OrganizationOperation.Missing();
    }

    private async Task<object> FindTrackedAsync(OrganizationMasterKind kind, Guid id, CancellationToken ct) =>
        (kind switch
        {
            OrganizationMasterKind.Department => (object?)await db.Departments.SingleOrDefaultAsync(x => x.Id == id, ct),
            OrganizationMasterKind.Designation => await db.Designations.SingleOrDefaultAsync(x => x.Id == id, ct),
            OrganizationMasterKind.Branch => await db.Branches.SingleOrDefaultAsync(x => x.Id == id, ct),
            OrganizationMasterKind.City => await db.Cities.SingleOrDefaultAsync(x => x.Id == id, ct),
            OrganizationMasterKind.StaffCategory => await db.StaffCategories.SingleOrDefaultAsync(x => x.Id == id, ct),
            OrganizationMasterKind.EmploymentType => await db.EmploymentTypes.SingleOrDefaultAsync(x => x.Id == id, ct),
            _ => throw OrganizationOperation.Invalid()
        }) ?? throw OrganizationOperation.Missing();

    private void ExpectVersion(object entity, string expected)
    {
        var property = db.Entry(entity).Property("RowVersion");
        OrganizationOperation.ExpectVersion(Convert.ToBase64String((byte[])property.CurrentValue!), expected);
        property.OriginalValue = OrganizationOperation.Version(expected);
    }

    // Projection and sorting stay in SQL; no domain IQueryable escapes Infrastructure.
    private IQueryable<MasterRow> MasterQuery(OrganizationMasterKind kind) => kind switch
    {
        OrganizationMasterKind.Department => db.Departments.AsNoTracking().Select(x => new MasterRow { Id = x.Id, Code = x.Code, Name = x.Name, IsActive = x.IsActive, RowVersion = x.RowVersion }),
        OrganizationMasterKind.Designation => db.Designations.AsNoTracking().Select(x => new MasterRow { Id = x.Id, Code = x.Code, Name = x.Name, IsActive = x.IsActive, RowVersion = x.RowVersion }),
        OrganizationMasterKind.Branch => db.Branches.AsNoTracking().Select(x => new MasterRow { Id = x.Id, Code = x.Code, Name = x.Name, IsActive = x.IsActive, RowVersion = x.RowVersion, CityId = x.CityId, CityName = x.CityReference == null ? x.City : x.CityReference.Name }),
        OrganizationMasterKind.City => db.Cities.AsNoTracking().Select(x => new MasterRow { Id = x.Id, Code = x.Code, Name = x.Name, IsActive = x.IsActive, RowVersion = x.RowVersion, Province = x.Province }),
        OrganizationMasterKind.StaffCategory => db.StaffCategories.AsNoTracking().Select(x => new MasterRow { Id = x.Id, Code = x.Code, Name = x.Name, IsActive = x.IsActive, RowVersion = x.RowVersion }),
        OrganizationMasterKind.EmploymentType => db.EmploymentTypes.AsNoTracking().Select(x => new MasterRow { Id = x.Id, Code = x.Code, Name = x.Name, IsActive = x.IsActive, RowVersion = x.RowVersion }),
        _ => throw OrganizationOperation.Invalid()
    };
    private sealed class MasterRow
    {
        public Guid Id { get; init; }
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public bool IsActive { get; init; }
        public byte[] RowVersion { get; init; } = [];
        public Guid? CityId { get; init; }
        public string? CityName { get; init; }
        public string? Province { get; init; }
    }
    private static OrganizationMasterResponse Map(MasterRow row) => new(row.Id, row.Code, row.Name, row.IsActive,
        Convert.ToBase64String(row.RowVersion), row.CityId, row.CityName, row.Province);

    public Task<IdempotencyReceipt?> FindReceiptAsync(string actor, string operation, string key, CancellationToken ct) =>
        db.IdempotencyReceipts.SingleOrDefaultAsync(x => x.ActorUserId == actor && x.Operation == operation && x.Key == key, ct);
    public Task<OrganizationChange?> FindChangeAsync(Guid id, CancellationToken ct) =>
        db.Set<OrganizationChange>().AsNoTracking().Include(x => x.AuditEvent).SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<PagedResult<OrganizationChangeResponse>> HistoryAsync(string resource, Guid id, OrganizationListRequest request, CancellationToken ct)
    {
        var query = db.Set<OrganizationChange>().AsNoTracking().Where(x => x.Resource == resource && x.ResourceId == id);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.AuditEvent.OccurredAt).ThenByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new OrganizationChangeResponse(x.Id, x.Operation, x.Reason, x.AuditEvent.OccurredAt,
                x.AuditEvent.ActorUserId, x.AuditEvent.ActorRoleCode, x.BeforeJson, x.AfterJson)).ToArrayAsync(ct);
        return new(rows, count, request.Page, request.PageSize);
    }
    public void AddChange(OrganizationChange change, AuditEvent audit, IdempotencyReceipt receipt)
    {
        db.AuditEvents.Add(audit); db.Set<OrganizationChange>().Add(change); db.IdempotencyReceipts.Add(receipt);
    }
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<PagedResult<HolidayCalendarSummary>> ListCalendarsAsync(OrganizationListRequest request, bool includeHistory, CancellationToken ct)
    {
        var query = db.HolidayCalendars.AsNoTracking();
        if (!includeHistory) query = query.Where(x => !db.HolidayCalendars.Any(next => next.SupersedesCalendarId == x.Id));
        if (request.IsActive.HasValue) query = query.Where(x => x.IsActive == request.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.Code.Contains(search) || x.Name.Contains(search));
        }
        var count = await query.CountAsync(ct);
        var ordered = request.SortBy == "name"
            ? request.Descending ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name)
            : request.Descending ? query.OrderByDescending(x => x.Code) : query.OrderBy(x => x.Code);
        var rows = await ordered.ThenByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Revision).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        return new(rows.Select(x => HolidayCalendarService.Map(x).Calendar).ToArray(), count, request.Page, request.PageSize);
    }
    public Task<HolidayCalendar?> FindCalendarAsync(Guid id, CancellationToken ct) =>
        db.HolidayCalendars.AsNoTracking().Include(x => x.Holidays).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> HasSuccessorAsync(Guid id, CancellationToken ct) => db.HolidayCalendars.AnyAsync(x => x.SupersedesCalendarId == id, ct);
    public Task<bool> CalendarOverlapsAsync(string code, DateOnly from, DateOnly? to, CancellationToken ct) =>
        db.HolidayCalendars.AnyAsync(x => x.Code == code && (!x.EffectiveTo.HasValue || from < x.EffectiveTo) &&
            (!to.HasValue || x.EffectiveFrom < to), ct);
    public async Task<HolidayCalendarResponse> AddCalendarAsync(HolidayCalendar calendar, CancellationToken ct)
    {
        db.HolidayCalendars.Add(calendar);
        await db.SaveChangesAsync(ct);
        return HolidayCalendarService.Map(calendar);
    }
}
