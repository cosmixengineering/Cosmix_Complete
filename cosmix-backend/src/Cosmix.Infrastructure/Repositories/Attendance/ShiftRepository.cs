using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class ShiftRepository(CosmixDbContext db, IOfficeRosterRepository offices) : IShiftRepository
{
    public async Task<PagedResult<ShiftSummaryResponse>> ListAsync(ShiftListRequest request, CancellationToken ct)
    {
        var query = db.Shifts.AsNoTracking();
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
        var rows = await ordered.ThenBy(x => x.Id).Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize).ToListAsync(ct);
        return new(rows.Select(x => new ShiftSummaryResponse(x.Id, x.Code, x.Name, x.IsActive,
            Convert.ToBase64String(x.RowVersion))).ToArray(), count, request.Page, request.PageSize);
    }

    public async Task<ShiftResponse?> GetAsync(Guid id, CancellationToken ct)
    {
        var shift = await db.Shifts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (shift is null) return null;
        var versions = await db.ShiftVersions.AsNoTracking().Where(x => x.ShiftId == id)
            .OrderBy(x => x.EffectiveFrom).ToListAsync(ct);
        return Map(shift, versions);
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) =>
        db.Shifts.AnyAsync(x => x.Code == code, ct);

    public Task<bool> HasLockedPeriodAsync(DateOnly from, CancellationToken ct) =>
        db.AttendancePeriodLocks.AnyAsync(x => x.IsLocked && x.PeriodEndExclusive > from, ct);

    public async Task<bool> HasCurrentOrFutureUseAsync(Guid id, DateOnly today, CancellationToken ct)
    {
        if (await offices.HasCurrentOrFutureUseAsync(id, today, ct)) return true;
        if (await db.EmployeeShiftAssignments.AnyAsync(x => x.ShiftId == id &&
                (x.EffectiveTo == null || x.EffectiveTo > today), ct) ||
            await HasGroupOrTemporaryUseAsync(id, today, ct)) return true;

        // A yesterday assignment can still own a duty window after midnight today.
        var yesterday = today.AddDays(-1);
        var previous = await db.ShiftVersions.AsNoTracking().Where(x => x.ShiftId == id && x.EffectiveFrom <= yesterday)
            .OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
        if (previous is not null && previous.EndDayOffset == 1 && previous.EndTime > TimeOnly.MinValue &&
            Applies(previous.ApplicableWeekdays, yesterday.DayOfWeek))
        {
            if (await db.EmployeeShiftAssignments.AnyAsync(x => x.ShiftId == id && x.EffectiveFrom <= yesterday &&
                    x.EffectiveTo == today, ct) || await HasPopulationAsync(id, yesterday, ct)) return true;
        }
        var carryover = from change in db.TemporaryShiftChanges
                      where change.AttendanceDate == yesterday
                      join version in db.ShiftVersions on change.ReplacementShiftVersionId equals version.Id
                      where version.ShiftId == id && version.EndDayOffset == 1 && version.EndTime > TimeOnly.MinValue
                      select change;
        if (!await carryover.AnyAsync(ct)) return false;
        await new SiteRosterRepository(db).RequireDirectSchemaAsync(ct);
        return await carryover.AnyAsync(x => !db.Set<TemporaryShiftChangeAmendment>().Any(a => a.OriginalChangeId == x.Id), ct);
    }

    public async Task<bool> HasGroupOrTemporaryUseAsync(Guid id, DateOnly from, CancellationToken ct)
    {
        if (await HasPopulationAsync(id, from, ct)) return true;
        var changes = db.TemporaryShiftChanges.Where(x => x.AttendanceDate >= from &&
            db.ShiftVersions.Any(v => v.ShiftId == id &&
                (v.Id == x.OriginalShiftVersionId || v.Id == x.ReplacementShiftVersionId)));
        if (!await changes.AnyAsync(ct)) return false;
        await new SiteRosterRepository(db).RequireDirectSchemaAsync(ct);
        return await changes.AnyAsync(x => !db.Set<TemporaryShiftChangeAmendment>().Any(a => a.OriginalChangeId == x.Id), ct);
    }

    private Task<bool> HasPopulationAsync(Guid id, DateOnly from, CancellationToken ct) =>
        db.ShiftPopulationAssignments.AnyAsync(x => x.ShiftId == id &&
            (x.EffectiveTo == null || x.EffectiveTo > from) &&
            !db.ShiftPopulationAssignmentEnds.Any(e => e.ShiftPopulationAssignmentId == x.Id && e.EffectiveTo <= from), ct);

    public async Task<IReadOnlyList<Guid>> GetPublicationSiteIdsAsync(Guid id, DateOnly from, CancellationToken ct)
    {
        var affected = PublicationAssignments(id, from);
        if (await affected.AnyAsync(x => x.PopulationKind != ShiftPopulationKind.Site || x.SiteId == null ||
                x.ApprovalRequestId != null || db.ShiftPopulationAssignmentEnds.Any(e =>
                    e.ShiftPopulationAssignmentId == x.Id && e.ApprovalRequestId != null), ct))
            throw SchedulingOperation.Conflict("legacy_site_roster_requires_review");
        var ids = await PublicationSiteIds(id, from).ToArrayAsync(ct);
        if (ids.Length > 128) throw SchedulingOperation.Conflict("shift_version_impact_too_large");
        return ids;
    }

    public async Task<IReadOnlyList<EmployeeAssignment>> GetSiteMembershipImpactAsync(Guid siteId,
        DateOnly from, DateOnly? to, CancellationToken ct)
    {
        var rows = await PublicationMemberships(siteId, from, to).ToArrayAsync(ct);
        if (rows.Length > 2000) throw SchedulingOperation.Conflict("shift_version_impact_too_large");
        return rows;
    }

    private IQueryable<ShiftPopulationAssignment> PublicationAssignments(Guid id, DateOnly from) =>
        db.ShiftPopulationAssignments.Where(x => x.ShiftId == id &&
            (x.EffectiveTo == null || x.EffectiveTo > from) &&
            !db.ShiftPopulationAssignmentEnds.Any(e => e.ShiftPopulationAssignmentId == x.Id && e.EffectiveTo <= from));

    private IQueryable<Guid> PublicationSiteIds(Guid id, DateOnly from) =>
        PublicationAssignments(id, from).Select(x => x.SiteId!.Value).Distinct().OrderBy(x => x).Take(129);

    private IQueryable<EmployeeAssignment> PublicationMemberships(Guid siteId, DateOnly from, DateOnly? to)
    {
        var employeeIds = db.EmployeeAssignments.Where(x => x.SiteId == siteId &&
            (!to.HasValue || x.EffectiveFrom < to) && (x.EffectiveTo == null || x.EffectiveTo > from))
            .Select(x => x.EmployeeId);
        // Include other-site and office rows too: otherwise a competing/overlapping HR assignment
        // could be hidden and the proposed schedule could carry across an invalid transfer.
        return db.EmployeeAssignments.AsNoTracking().Where(x => employeeIds.Contains(x.EmployeeId) &&
                (!to.HasValue || x.EffectiveFrom < to) && (x.EffectiveTo == null || x.EffectiveTo > from))
            .OrderBy(x => x.EmployeeId).ThenBy(x => x.EffectiveFrom).ThenBy(x => x.Id).Take(2001);
    }

    public async Task<ShiftRosterImpact> GetVersionImpactAsync(Guid id, DateOnly from, CancellationToken ct)
    {
        var employees = db.EmployeeShiftAssignments.Where(x => x.ShiftId == id &&
            (x.EffectiveTo == null || x.EffectiveTo > from)).Select(x => x.EmployeeId);
        var previousDate = from.AddDays(-1);
        var hasTemporaryChanges = await db.TemporaryShiftChangeEmployees.AnyAsync(x =>
            employees.Contains(x.EmployeeId) && x.Change.AttendanceDate >= previousDate, ct);
        var assignments = await db.EmployeeShiftAssignments.AsNoTracking()
            .Where(x => employees.Contains(x.EmployeeId) && (x.EffectiveTo == null || x.EffectiveTo > previousDate))
            .OrderBy(x => x.EmployeeId).ThenBy(x => x.EffectiveFrom).Take(5001).ToListAsync(ct);
        if (assignments.Count > 5000) throw SchedulingOperation.Conflict("shift_version_impact_too_large");
        var shiftIds = assignments.Select(x => x.ShiftId).Distinct().ToArray();
        var versions = await db.ShiftVersions.AsNoTracking().Where(x => shiftIds.Contains(x.ShiftId))
            .OrderBy(x => x.ShiftId).ThenBy(x => x.EffectiveFrom).Take(10001).ToListAsync(ct);
        if (versions.Count > 10000) throw SchedulingOperation.Conflict("shift_version_impact_too_large");
        return new(assignments, versions, hasTemporaryChanges);
    }

    public async Task<ShiftResponse> CreateAsync(Shift shift, ShiftVersion version, CancellationToken ct)
    {
        db.Shifts.Add(shift);
        db.ShiftVersions.Add(version);
        await db.SaveChangesAsync(ct);
        return Map(shift, [version]);
    }

    public async Task<ShiftResponse> UpdateNameAsync(Guid id, string name, byte[] expectedVersion, CancellationToken ct)
    {
        var shift = await TrackedAsync(id, expectedVersion, ct);
        shift.UpdateName(name);
        // Even a retried semantic no-op consumes the expected version when it has a new idempotency key.
        db.Entry(shift).Property(x => x.Name).IsModified = true;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct) ?? throw SchedulingOperation.Missing();
    }

    public async Task<ShiftResponse> SetStatusAsync(Guid id, bool active, byte[] expectedVersion, CancellationToken ct)
    {
        var shift = await TrackedAsync(id, expectedVersion, ct);
        shift.SetActive(active);
        db.Entry(shift).Property(x => x.IsActive).IsModified = true;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct) ?? throw SchedulingOperation.Missing();
    }

    public async Task<ShiftResponse> AppendVersionAsync(Guid id, ShiftVersion version, byte[] expectedVersion, CancellationToken ct)
    {
        var shift = await TrackedAsync(id, expectedVersion, ct);
        db.ShiftVersions.Add(version);
        // The parent concurrency token covers its immutable child schedule collection too.
        db.Entry(shift).Property(x => x.Name).IsModified = true;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct) ?? throw SchedulingOperation.Missing();
    }

    private async Task<Shift> TrackedAsync(Guid id, byte[] expectedVersion, CancellationToken ct)
    {
        var shift = await db.Shifts.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw SchedulingOperation.Missing();
        SchedulingOperation.ExpectVersion(Convert.ToBase64String(shift.RowVersion), Convert.ToBase64String(expectedVersion));
        db.Entry(shift).Property(x => x.RowVersion).OriginalValue = expectedVersion;
        return shift;
    }

    private static bool Applies(ShiftWeekdays days, DayOfWeek day) =>
        (days & (ShiftWeekdays)(1 << (((int)day + 6) % 7))) != 0;

    private static ShiftResponse Map(Shift shift, IReadOnlyList<ShiftVersion> versions) =>
        new(shift.Id, shift.Code, shift.Name, shift.IsActive, Convert.ToBase64String(shift.RowVersion),
            versions.Select((x, index) => new ShiftVersionResponse(x.Id, x.VersionNumber, x.EffectiveFrom,
                index + 1 < versions.Count ? versions[index + 1].EffectiveFrom : null, x.StartTime, x.EndTime,
                x.EndDayOffset, x.ScheduledMinutes, x.PaidBreakMinutes, x.UnpaidBreakMinutes, x.ApplicableWeekdays,
                x.MissingCheckoutReminderAfterMinutes)).ToArray());
}
