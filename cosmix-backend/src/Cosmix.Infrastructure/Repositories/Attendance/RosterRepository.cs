using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.Authentication.Scopes;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

/// <summary>Called inside SchedulingOperation's serializable authorization/write transaction.</summary>
public sealed class RosterRepository(CosmixDbContext db, IEmployeeLifecycleRepository lifecycle,
    TimeProvider clock) : IRosterRepository
{
    private const int MaximumTimelineRows = 2048;
    private const int MaximumResourceSlices = 8192;

    public Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken ct) =>
        db.Employees.SingleOrDefaultAsync(x => x.Id == employeeId, ct);

    public async Task<IReadOnlyList<EmployeeShiftAssignment>> AssignmentsAsync(Guid employeeId, CancellationToken ct) =>
        Bounded(await db.EmployeeShiftAssignments.Include(x => x.Shift).Where(x => x.EmployeeId == employeeId)
            .OrderBy(x => x.EffectiveFrom).ThenBy(x => x.Id).Take(MaximumTimelineRows + 1).ToArrayAsync(ct));

    public Task<Shift?> FindShiftAsync(Guid shiftId, CancellationToken ct) =>
        db.Shifts.SingleOrDefaultAsync(x => x.Id == shiftId, ct);

    public async Task<IReadOnlyList<ShiftVersion>> VersionsAsync(Guid shiftId, CancellationToken ct) =>
        Bounded(await db.ShiftVersions.AsNoTracking().Where(x => x.ShiftId == shiftId)
            .OrderBy(x => x.EffectiveFrom).ThenBy(x => x.VersionNumber).Take(MaximumTimelineRows + 1).ToArrayAsync(ct));

    public async Task<IReadOnlyList<SchedulingResourceScope>> ResourcesAsync(Guid employeeId, DateOnly from,
        DateOnly? to, CancellationToken ct)
    {
        ValidateInterval(from, to);
        var employment = await EmploymentAsync(employeeId, from, to, ct);
        var teams = await TeamsAsync(employeeId, from, to, ct);
        var projects = await ProjectsAsync(employeeId, from, to, ct);
        var boundaries = new SortedSet<DateOnly> { from };
        foreach (var row in employment) AddBoundaries(boundaries, row.EffectiveFrom, row.EffectiveTo, from, to);
        foreach (var row in teams) AddBoundaries(boundaries, row.EffectiveFrom,
            Earlier(row.EffectiveTo, row.TerminatedEffectiveTo), from, to);
        foreach (var row in projects) AddBoundaries(boundaries, row.EffectiveFrom,
            Earlier(row.EffectiveTo, row.TerminatedEffectiveTo), from, to);

        var result = new List<SchedulingResourceScope>();
        var resourceCount = 0;
        foreach (var date in boundaries)
        {
            var matches = employment.Where(x => Effective(x.EffectiveFrom, x.EffectiveTo, date)).Take(2).ToArray();
            if (matches.Length > 1) throw SchedulingOperation.Conflict("employee_assignment_history_conflict");
            var assignment = matches.SingleOrDefault();
            var teamIds = teams.Where(x => x.Team.IsActive && x.IsEffectiveOn(date)).Select(x => (Guid?)x.TeamId)
                .Distinct().DefaultIfEmpty(null).ToArray();
            var projectIds = projects.Where(x => x.Project.IsActive && x.IsEffectiveOn(date)).Select(x => (Guid?)x.ProjectId)
                .Distinct().DefaultIfEmpty(null).ToArray();
            var alternatives = new List<AccessResource>();
            foreach (var teamId in teamIds)
            foreach (var projectId in projectIds)
            {
                alternatives.Add(new(employeeId, assignment?.DepartmentId, teamId, assignment?.SiteId, projectId));
                if (++resourceCount > MaximumResourceSlices) throw SchedulingOperation.Conflict("roster_scope_timeline_limit");
            }
            result.Add(new(alternatives));
        }
        return result.ToArray();
    }

    public async Task ValidateEligibilityAsync(Guid employeeId, DateOnly from, DateOnly? to, Guid? siteId,
        DateTimeOffset now, CancellationToken ct)
    {
        ValidateInterval(from, to);
        var employee = await FindEmployeeAsync(employeeId, ct) ?? throw SchedulingOperation.Missing();
        var zone = BusinessZone();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        if (employee.Status != EmployeeStatus.Active || employee.JoinedOn is not DateOnly joined ||
            joined > today || joined > from || !await lifecycle.HasApprovedEmploymentAsync(employee, ct))
            throw SchedulingOperation.Conflict("employee_not_approved_active");

        var statuses = Bounded(await db.EmployeeStatusChanges.AsNoTracking().Where(x => x.EmployeeId == employeeId)
            .OrderBy(x => x.EffectiveAt).ThenBy(x => x.Id).Take(MaximumTimelineRows + 1).ToArrayAsync(ct));
        var startInstant = Midnight(from, zone);
        var endInstant = to.HasValue ? Midnight(to.Value, zone) : (DateTimeOffset?)null;
        var approvalIds = statuses.Where(x => x.ApprovalRequestId.HasValue)
            .Select(x => x.ApprovalRequestId!.Value).Distinct().ToArray();
        var approvals = await db.ApprovalRequests.AsNoTracking().Where(x => approvalIds.Contains(x.Id) &&
                x.SubjectId == employeeId && x.RequestType == EmployeeLifecycleService.RequestType &&
                x.State == ApprovalState.Approved && x.ClosedAt != null &&
                db.ApprovalDecisions.Any(d => d.ApprovalRequestId == x.Id && d.Decision == ApprovalState.Approved &&
                    d.DecidedAt == x.ClosedAt && d.ActorUserId != x.RequesterUserId))
            .Select(x => new { x.Id, x.ClosedAt }).ToArrayAsync(ct);
        var approved = approvals.ToDictionary(x => x.Id, x => x.ClosedAt!.Value);
        var state = EmployeeStatus.Active;
        var currentState = EmployeeStatus.Active;
        var startState = EmployeeStatus.Active;
        DateTimeOffset? lastEffective = null;
        foreach (var change in statuses)
        {
            // Validate existing provenance before using either current or future status as authority.
            if (change.EffectiveAt == lastEffective || change.PreviousStatus != state ||
                change.NewStatus is EmployeeStatus.Draft or EmployeeStatus.PendingApproval ||
                !Enum.IsDefined(change.NewStatus) || change.NewStatus == change.PreviousStatus ||
                change.EffectiveAt < change.RecordedAt || change.ApprovalRequestId is not Guid approvalId ||
                !approved.TryGetValue(approvalId, out var approvedAt) || approvedAt > change.EffectiveAt)
                throw SchedulingOperation.Conflict("employee_lifecycle_history_conflict");
            state = change.NewStatus;
            lastEffective = change.EffectiveAt;
            if (change.EffectiveAt <= now) currentState = state;
            if (change.EffectiveAt <= startInstant) startState = state;
            if (change.EffectiveAt >= startInstant && (!endInstant.HasValue || change.EffectiveAt < endInstant.Value) &&
                state != EmployeeStatus.Active)
                throw SchedulingOperation.Conflict("employee_ineligible_during_roster");
        }
        if (currentState != EmployeeStatus.Active || startState != EmployeeStatus.Active)
            throw SchedulingOperation.Conflict("employee_not_approved_active");

        var employment = await EmploymentAsync(employeeId, from, to, ct);
        var cursor = from;
        var covered = false;
        foreach (var assignment in employment)
        {
            var start = assignment.EffectiveFrom < from ? from : assignment.EffectiveFrom;
            if (covered || start != cursor)
                throw SchedulingOperation.Conflict("employee_assignment_coverage_conflict");
            var end = Earlier(assignment.EffectiveTo, to);
            if (assignment.StaffCategory is null || !assignment.StaffCategory.IsActive ||
                assignment.StaffCategory.Code is not ("OFFICE" or "SITE"))
                throw SchedulingOperation.Conflict("employee_staff_category_unavailable");
            if (assignment.StaffCategory.Code == "SITE" &&
                (assignment.BranchId.HasValue || !assignment.SiteId.HasValue || !siteId.HasValue))
                throw SchedulingOperation.Conflict("employee_site_assignment_required");
            if (assignment.StaffCategory.Code == "OFFICE" &&
                (!assignment.BranchId.HasValue || assignment.Branch is null || !assignment.Branch.IsActive))
                throw SchedulingOperation.Conflict("employee_office_assignment_required");
            if (assignment.SiteId != siteId)
                throw SchedulingOperation.Conflict("roster_site_does_not_match_employment");
            if (siteId.HasValue)
            {
                var site = assignment.Site;
                if (site is null || !site.IsActive || site.StartsOn > start ||
                    (site.EndsOn.HasValue && (!end.HasValue || end.Value.DayNumber - 1 > site.EndsOn.Value.DayNumber)))
                    throw SchedulingOperation.Conflict("site_unavailable_during_roster");
            }
            if (!end.HasValue || end == to) covered = true;
            else cursor = end.Value;
        }
        if (!covered) throw SchedulingOperation.Conflict("employee_assignment_coverage_conflict");
    }

    public async Task<Guid?> EffectiveSiteAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        if (date == default) throw SchedulingOperation.Invalid("invalid_business_date");
        var rows = await db.EmployeeAssignments.AsNoTracking().Where(x => x.EmployeeId == employeeId &&
                x.EffectiveFrom <= date && (x.EffectiveTo == null || date < x.EffectiveTo))
            .Select(x => new { x.SiteId, Category = x.StaffCategory.Code,
                SiteActive = x.Site != null && x.Site.IsActive,
                StartsOn = x.Site != null ? x.Site.StartsOn : null,
                EndsOn = x.Site != null ? x.Site.EndsOn : null }).Take(2).ToArrayAsync(ct);
        if (rows.Length > 1) throw SchedulingOperation.Conflict("employee_assignment_history_conflict");
        if (rows.Length == 0) throw SchedulingOperation.Conflict("employee_assignment_unavailable");
        var row = rows[0];
        if (row.Category is not ("SITE" or "OFFICE"))
            throw SchedulingOperation.Conflict("employee_staff_category_unavailable");
        if (row.Category == "SITE" && !row.SiteId.HasValue)
            throw SchedulingOperation.Conflict("employee_site_assignment_required");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), BusinessZone()).DateTime);
        // Current deactivation cannot erase a historical roster. Effective site dates still apply.
        if (row.SiteId.HasValue && (row.StartsOn > date || row.EndsOn < date || (!row.SiteActive && date >= today)))
            throw SchedulingOperation.Conflict("site_unavailable_on_business_date");
        return row.SiteId;
    }

    public Task<bool> HasLockedPeriodAsync(DateOnly from, DateOnly? to, CancellationToken ct)
    {
        ValidateInterval(from, to);
        return db.Set<AttendancePeriodLock>().AnyAsync(x => x.IsLocked && x.PeriodEndExclusive > from &&
            (!to.HasValue || x.PeriodStart < to.Value), ct);
    }

    public Task<bool> HasUnsupportedScheduleAsync(Guid employeeId, DateOnly date, CancellationToken ct) =>
        HasUnsupportedScheduleInRangeAsync(employeeId, date, date == DateOnly.MaxValue ? null : date.AddDays(1), ct);

    public async Task<bool> HasUnsupportedScheduleInRangeAsync(Guid employeeId, DateOnly from,
        DateOnly? to, CancellationToken ct)
    {
        ValidateInterval(from, to);
        if (await db.Set<TemporaryShiftChangeEmployee>().AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId &&
            x.Change.AttendanceDate >= from && (!to.HasValue || x.Change.AttendanceDate < to.Value), ct)) return true;

        var employment = await EmploymentAsync(employeeId, from, to, ct);
        var teams = await TeamsAsync(employeeId, from, to, ct);
        var teamIds = teams.Select(x => x.TeamId).Distinct().ToArray();
        var departments = employment.Select(x => x.DepartmentId).Distinct().ToArray();
        var categories = employment.Select(x => x.StaffCategoryId).Distinct().ToArray();
        var sites = employment.Where(x => x.SiteId.HasValue).Select(x => x.SiteId!.Value).Distinct().ToArray();
        var populations = Bounded(await db.Set<ShiftPopulationAssignment>().AsNoTracking()
            .Where(x => (x.EffectiveTo == null || from < x.EffectiveTo) && (!to.HasValue || x.EffectiveFrom < to.Value) &&
                ((x.TeamId != null && teamIds.Contains(x.TeamId.Value)) ||
                 (x.DepartmentId != null && departments.Contains(x.DepartmentId.Value)) ||
                 (x.StaffCategoryId != null && categories.Contains(x.StaffCategoryId.Value)) ||
                 (x.SiteId != null && sites.Contains(x.SiteId.Value))))
            .OrderBy(x => x.Id).Take(MaximumTimelineRows + 1).ToArrayAsync(ct));
        var ids = populations.Select(x => x.Id).ToArray();
        var ends = Bounded(await db.Set<ShiftPopulationAssignmentEnd>().AsNoTracking()
            .Where(x => ids.Contains(x.ShiftPopulationAssignmentId)).OrderBy(x => x.Id)
            .Take(MaximumTimelineRows + 1).ToArrayAsync(ct));
        // Identify historical candidates without inventing approval authority or precedence.
        // Keep rejecting unsupported records until the site workflow can validate their approvals.
        return PopulationRosterTimeline.IntersectsRange(employeeId, from, to, employment, teams, populations, ends);
    }

    public void Add(EmployeeShiftAssignment assignment) => db.EmployeeShiftAssignments.Add(assignment);

    public void ExpectVersion(EmployeeShiftAssignment assignment, byte[] version)
    {
        if (!assignment.RowVersion.SequenceEqual(version)) throw SchedulingOperation.Conflict("roster_version_conflict");
        db.Entry(assignment).Property(x => x.RowVersion).OriginalValue = version;
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private async Task<EmployeeAssignment[]> EmploymentAsync(Guid employeeId, DateOnly from, DateOnly? to, CancellationToken ct) =>
        Bounded(await db.EmployeeAssignments.AsNoTracking().Include(x => x.StaffCategory).Include(x => x.Branch).Include(x => x.Site)
            .Where(x => x.EmployeeId == employeeId && (x.EffectiveTo == null || from < x.EffectiveTo) &&
                (!to.HasValue || x.EffectiveFrom < to.Value)).OrderBy(x => x.EffectiveFrom).ThenBy(x => x.Id)
            .Take(MaximumTimelineRows + 1).ToArrayAsync(ct));

    private async Task<TeamMember[]> TeamsAsync(Guid employeeId, DateOnly from, DateOnly? to, CancellationToken ct) =>
        Bounded(await db.Set<TeamMember>().AsNoTracking().Include(x => x.Team).Where(x => x.EmployeeId == employeeId &&
            (x.EffectiveTo == null || from < x.EffectiveTo) && (x.TerminatedEffectiveTo == null || from < x.TerminatedEffectiveTo) &&
            (!to.HasValue || x.EffectiveFrom < to.Value)).OrderBy(x => x.EffectiveFrom).ThenBy(x => x.Id)
            .Take(MaximumTimelineRows + 1).ToArrayAsync(ct));

    private async Task<ProjectMember[]> ProjectsAsync(Guid employeeId, DateOnly from, DateOnly? to, CancellationToken ct) =>
        Bounded(await db.Set<ProjectMember>().AsNoTracking().Include(x => x.Project).Where(x => x.EmployeeId == employeeId &&
            (x.EffectiveTo == null || from < x.EffectiveTo) && (x.TerminatedEffectiveTo == null || from < x.TerminatedEffectiveTo) &&
            (!to.HasValue || x.EffectiveFrom < to.Value)).OrderBy(x => x.EffectiveFrom).ThenBy(x => x.Id)
            .Take(MaximumTimelineRows + 1).ToArrayAsync(ct));

    private static T[] Bounded<T>(T[] rows) => rows.Length <= MaximumTimelineRows ? rows :
        throw SchedulingOperation.Conflict("roster_timeline_limit");

    private static void ValidateInterval(DateOnly from, DateOnly? to)
    {
        if (from == default || to <= from) throw SchedulingOperation.Invalid("invalid_roster_interval");
    }

    private static DateOnly? Earlier(DateOnly? first, DateOnly? second) =>
        !first.HasValue ? second : !second.HasValue ? first : first < second ? first : second;

    private static bool Effective(DateOnly from, DateOnly? to, DateOnly date) => from <= date && (!to.HasValue || date < to);

    private static void AddBoundaries(SortedSet<DateOnly> dates, DateOnly start, DateOnly? end, DateOnly from, DateOnly? to)
    {
        if (start > from && (!to.HasValue || start < to.Value)) dates.Add(start);
        if (end.HasValue && end > from && (!to.HasValue || end < to.Value)) dates.Add(end.Value);
    }

    private static TimeZoneInfo BusinessZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(RosterSchedule.TimeZoneId); }
        catch (Exception error) when (error is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new AuthenticationFailure("business_timezone_not_configured", 503); }
    }

    private static DateTimeOffset Midnight(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
            throw SchedulingOperation.Invalid("invalid_business_date_boundary");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }
}
