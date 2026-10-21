using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.HR.StaffCategories;

namespace Cosmix.Application.Attendance.Rosters;

public sealed class OfficeRosterResolver(IOfficeRosterRepository offices, TimeProvider clock, IStaffTaxonomyResolver? taxonomy = null)
{
    public async Task<EffectiveRosterResponse> ResolveAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        RosterSchedule.ValidateDate(date);
        var membership = await offices.EmploymentAsync(employeeId, date, ct) ?? throw SchedulingOperation.Conflict("employee_assignment_unavailable");
        if (membership.EmployeeId != employeeId || membership.StaffCategory is null || membership.StaffCategoryId != membership.StaffCategory.Id)
            throw SchedulingOperation.Conflict("employee_assignment_history_conflict");
        var mapping = await RosterStaffTaxonomy.ResolveAsync(taxonomy, membership.StaffCategory.Code, date, date.AddDays(1), ct);
        StaffTaxonomyResolver.RequireMapping(mapping);
        if (!RosterStaffTaxonomy.IsOffice(mapping) || membership.BranchId is not Guid branchId || membership.SiteId.HasValue)
            throw SchedulingOperation.Conflict("employee_office_assignment_required");
        var branch = await offices.FindBranchAsync(branchId, ct) ?? throw SchedulingOperation.Conflict("office_assignment_reference_missing");
        if (!branch.IsActive && date >= RosterSchedule.Today(clock.GetUtcNow()))
            throw SchedulingOperation.Conflict("office_unavailable_on_business_date");
        var state = await offices.ReadAsync(branchId, ct);
        var result = OfficeRosterSchedule.Resolve(employeeId, date, state, membership);
        var next = RosterSchedule.OccupiedEndExclusive(result);
        if (!next.HasValue || next > date.AddDays(1))
        {
            var following = await offices.EmploymentAsync(employeeId, date.AddDays(1), ct);
            if (following is null || following.Id != membership.Id || following.BranchId != branchId || following.SiteId.HasValue ||
                following.StaffCategoryId != membership.StaffCategoryId || following.StaffCategory?.Code != membership.StaffCategory.Code)
                throw SchedulingOperation.Conflict("roster_office_relationship_changed");
        }
        var calendarId = OfficeRosterSchedule.AssignmentOn(state, date)?.HolidayCalendarId;
        var calendar = calendarId.HasValue ? await offices.FindCalendarAsync(calendarId.Value, ct) ??
            throw SchedulingOperation.Conflict("holiday_calendar_revision_missing") : null;
        var current = await offices.EmploymentAsync(employeeId, date, ct);
        if (current is null || current.Id != membership.Id || current.EmployeeId != employeeId ||
            current.BranchId != branchId || current.SiteId.HasValue || current.StaffCategoryId != membership.StaffCategoryId ||
            current.StaffCategory?.Code != membership.StaffCategory.Code)
            throw SchedulingOperation.Conflict("roster_office_relationship_changed");
        var finalMapping = await RosterStaffTaxonomy.ResolveAsync(taxonomy, membership.StaffCategory.Code, date,
            next ?? throw SchedulingOperation.Conflict("staff_taxonomy_schedule_interval_unavailable"), ct);
        if (!RosterStaffTaxonomy.Matches(membership.StaffCategory.Code, finalMapping, RosterStaffTaxonomy.Capture(mapping)))
            throw SchedulingOperation.Conflict("staff_taxonomy_schedule_source_changed");
        return result with { Holiday = RosterHolidayContext.Resolve(calendar, date), Taxonomy = RosterStaffTaxonomy.Capture(finalMapping) };
    }
}
