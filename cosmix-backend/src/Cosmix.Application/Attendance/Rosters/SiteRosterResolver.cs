using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.HR.StaffCategories;

namespace Cosmix.Application.Attendance.Rosters;

/// <summary>Runtime resolver for a dated Site mapping; legacy individual records never override a site.</summary>
public sealed class SiteRosterResolver(ISiteRosterRepository sites, IRosterRepository employees,
    IOfficeRosterRepository? offices = null, IStaffTaxonomyResolver? taxonomy = null) : IIndividualRosterResolver
{
    public async Task<EffectiveRosterResponse> ResolveAsync(Guid employeeId, DateOnly businessDate, CancellationToken ct)
    {
        RosterSchedule.ValidateDate(businessDate);
        if (employeeId == Guid.Empty) throw SchedulingOperation.Invalid("invalid_employee_id");
        if (await employees.FindEmployeeAsync(employeeId, ct) is null) throw SchedulingOperation.Missing();
        var siteId = await employees.EffectiveSiteAsync(employeeId, businessDate, ct);
        if (!siteId.HasValue)
            return new(employeeId, businessDate, "site_not_assigned", RosterSchedule.TimeZoneId,
                Explanation: new("site_not_assigned", null, null, null, null, SiteRosterSchedule.Rule),
                Holiday: RosterHolidayContext.Resolve(null, businessDate));
        var memberships = await sites.MembershipsAsync(employeeId, businessDate, businessDate.AddDays(1), ct);
        var matching = memberships.Where(x => x.EmployeeId == employeeId && x.EffectiveFrom <= businessDate &&
            (!x.EffectiveTo.HasValue || businessDate < x.EffectiveTo)).ToArray();
        if (matching.Length != 1 || matching[0].SiteId != siteId)
            throw SchedulingOperation.Conflict("employee_assignment_history_conflict");
        var membership = offices is null ? null : await offices.EmploymentAsync(employeeId, businessDate, ct);
        StaffTaxonomyResolution? mapping = null;
        if (offices is not null || taxonomy is not null)
        {
            if (membership is null || membership.Id != matching[0].Id || membership.EmployeeId != employeeId ||
                membership.SiteId != siteId || membership.BranchId.HasValue || membership.StaffCategory is null ||
                membership.StaffCategoryId != membership.StaffCategory.Id)
                throw SchedulingOperation.Conflict("employee_assignment_history_conflict");
            mapping = await RosterStaffTaxonomy.ResolveAsync(taxonomy, membership.StaffCategory.Code,
                businessDate, businessDate.AddDays(1), ct);
            StaffTaxonomyResolver.RequireMapping(mapping);
            if (!RosterStaffTaxonomy.IsSite(mapping)) throw SchedulingOperation.Conflict("employee_site_assignment_required");
        }
        var state = await sites.ReadAsync(siteId.Value, ct);
        var result = SiteRosterSchedule.Resolve(employeeId, businessDate, state);
        if (result.Explanation?.TemporaryChangeId is Guid changeId)
        {
            var change = state.TemporaryChanges.Single(x => x.Id == changeId);
            var staff = await sites.StaffAsync(siteId.Value, businessDate, ct);
            var ids = staff.Select(x => x.EmployeeId).Order().ToArray();
            if (ids.Distinct().Count() != ids.Length || !ids.Contains(employeeId) ||
                !ids.SequenceEqual(change.Employees.Select(x => x.EmployeeId).Order()))
                throw SchedulingOperation.Conflict("temporary_change_staff_changed");
        }
        var endExclusive = RosterSchedule.OccupiedEndExclusive(result);
        if (endExclusive is null || endExclusive > businessDate.AddDays(1))
        {
            var nextSite = await employees.EffectiveSiteAsync(employeeId, businessDate.AddDays(1), ct);
            if (nextSite != siteId) throw SchedulingOperation.Conflict("roster_site_relationship_changed");
            if (membership is not null)
            {
                var following = await offices!.EmploymentAsync(employeeId, businessDate.AddDays(1), ct);
                if (following is null || following.Id != membership.Id || following.SiteId != siteId || following.BranchId.HasValue ||
                    following.StaffCategoryId != membership.StaffCategoryId || following.StaffCategory?.Code != membership.StaffCategory.Code)
                    throw SchedulingOperation.Conflict("roster_site_relationship_changed");
            }
        }
        var assignment = SiteRosterSchedule.AssignmentOn(state, businessDate);
        var calendar = assignment?.HolidayCalendarId is Guid calendarId
            ? await sites.FindCalendarAsync(calendarId, ct) ?? throw SchedulingOperation.Conflict("holiday_calendar_revision_missing")
            : null;
        RosterStaffTaxonomySource? source = null;
        if (membership is not null)
        {
            var current = await offices!.EmploymentAsync(employeeId, businessDate, ct);
            if (current is null || current.Id != membership.Id || current.EmployeeId != employeeId || current.SiteId != siteId ||
                current.BranchId.HasValue || current.StaffCategoryId != membership.StaffCategoryId ||
                current.StaffCategory?.Code != membership.StaffCategory.Code ||
                await employees.EffectiveSiteAsync(employeeId, businessDate, ct) != siteId)
                throw SchedulingOperation.Conflict("roster_site_relationship_changed");
            var finalMapping = await RosterStaffTaxonomy.ResolveAsync(taxonomy, membership.StaffCategory.Code, businessDate,
                endExclusive ?? throw SchedulingOperation.Conflict("staff_taxonomy_schedule_interval_unavailable"), ct);
            if (!RosterStaffTaxonomy.Matches(membership.StaffCategory.Code, finalMapping, RosterStaffTaxonomy.Capture(mapping!)))
                throw SchedulingOperation.Conflict("staff_taxonomy_schedule_source_changed");
            source = RosterStaffTaxonomy.Capture(finalMapping);
        }
        return result with
        {
            Explanation = result.Explanation! with { MembershipRecordId = matching[0].Id },
            Holiday = RosterHolidayContext.Resolve(calendar, businessDate), Taxonomy = source
        };
    }
}
