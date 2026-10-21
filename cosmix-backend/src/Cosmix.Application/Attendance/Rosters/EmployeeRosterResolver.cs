using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.HR.StaffCategories;

namespace Cosmix.Application.Attendance.Rosters;

public sealed class EmployeeRosterResolver(IOfficeRosterRepository offices, OfficeRosterResolver office, SiteRosterResolver site,
    IStaffTaxonomyResolver? taxonomy = null)
    : IIndividualRosterResolver
{
    public async Task<EffectiveRosterResponse> ResolveAsync(Guid employeeId, DateOnly businessDate, CancellationToken ct)
    {
        RosterSchedule.ValidateDate(businessDate);
        if (employeeId == Guid.Empty) throw SchedulingOperation.Invalid("invalid_employee_id");
        var membership = await offices.EmploymentAsync(employeeId, businessDate, ct) ?? throw SchedulingOperation.Conflict("employee_assignment_unavailable");
        if (membership.EmployeeId != employeeId || membership.StaffCategory is null || membership.StaffCategoryId != membership.StaffCategory.Id)
            throw SchedulingOperation.Conflict("employee_assignment_history_conflict");
        var mapping = await RosterStaffTaxonomy.ResolveAsync(taxonomy, membership.StaffCategory.Code,
            businessDate, businessDate.AddDays(1), ct);
        StaffTaxonomyResolver.RequireMapping(mapping);
        return mapping.Mapping!.ScheduleBasis switch
        {
            StaffScheduleBasis.Site when RosterStaffTaxonomy.IsSite(mapping) && membership.SiteId.HasValue && !membership.BranchId.HasValue => await site.ResolveAsync(employeeId, businessDate, ct),
            StaffScheduleBasis.Office when RosterStaffTaxonomy.IsOffice(mapping) && membership.BranchId.HasValue && !membership.SiteId.HasValue => await office.ResolveAsync(employeeId, businessDate, ct),
            _ => throw SchedulingOperation.Conflict("employee_assignment_location_conflict")
        };
    }
}
