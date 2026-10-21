using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.HR.StaffCategories;

namespace Cosmix.Application.Attendance.Offices;

public sealed class OfficeAttendanceSourceReader(IOfficeBoundaryRepository boundaries, IOfficeRosterRepository offices,
    IStaffTaxonomyResolver? taxonomy = null)
    : IOfficeAttendanceSourceReader
{
    public async Task<OfficeAttendanceContext> ReadAsync(Guid employeeId, DateOnly date,
        EffectiveRosterResponse roster, Guid? pinnedBoundaryId, CancellationToken ct)
    {
        if (roster.BranchId is not Guid branchId)
        {
            if (roster.Explanation?.Source == "office" || roster.Explanation?.OfficeRosterAssignmentId.HasValue == true)
                throw AttendanceOperation.Conflict("office_schedule_provenance_unavailable");
            return new("not_office", null, null);
        }
        if (roster.EmployeeId != employeeId || roster.BusinessDate != date || roster.SiteId.HasValue || branchId == Guid.Empty || pinnedBoundaryId == Guid.Empty)
            throw AttendanceOperation.Conflict("office_schedule_provenance_unavailable");
        if (!await CurrentMembershipMatchesAsync(employeeId, branchId, roster.Explanation?.MembershipRecordId, date, ct))
            throw AttendanceOperation.Conflict("office_membership_source_changed");
        await RequireTaxonomyMatchesAsync(employeeId, date, roster, ct);
        var rows = await boundaries.VersionsAsync(branchId, ct);
        OfficeBoundaryService.ValidateHistory(branchId, rows);
        var boundary = rows.Where(x => x.EffectiveFrom <= date).MaxBy(x => x.VersionNumber);
        // Recheck after history IO so a retained schedule never acquires a changed category publication.
        if (!await CurrentMembershipMatchesAsync(employeeId, branchId, roster.Explanation?.MembershipRecordId, date, ct))
            throw AttendanceOperation.Conflict("office_membership_source_changed");
        await RequireTaxonomyMatchesAsync(employeeId, date, roster, ct);
        if (pinnedBoundaryId.HasValue && boundary?.Id != pinnedBoundaryId)
            throw AttendanceOperation.Conflict("office_boundary_source_changed");
        if (boundary is null) return new("office_boundary_not_configured", null, null);
        return new("configured", OfficeAttendanceSource.Capture(employeeId, date, roster, boundary), boundary);
    }
    public async Task<bool> CurrentMembershipMatchesAsync(Guid employeeId, Guid branchId, Guid? membershipId,
        DateOnly receiptDate, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || branchId == Guid.Empty || membershipId is null || membershipId == Guid.Empty)
            return false;
        var membership = await offices.EmploymentAsync(employeeId, receiptDate, ct);
        if (membership is null || membership.EmployeeId != employeeId || membership.Id != membershipId ||
            membership.BranchId != branchId || membership.SiteId.HasValue || membership.StaffCategory is null ||
            membership.StaffCategoryId != membership.StaffCategory.Id) return false;
        var mapping = await RosterStaffTaxonomy.ResolveAsync(taxonomy, membership.StaffCategory.Code,
            receiptDate, receiptDate.AddDays(1), ct);
        var current = await offices.EmploymentAsync(employeeId, receiptDate, ct);
        return RosterStaffTaxonomy.IsOffice(mapping) && current is not null && current.EmployeeId == employeeId &&
            current.Id == membershipId && current.BranchId == branchId && current.SiteId is null &&
            current.StaffCategoryId == membership.StaffCategoryId && current.StaffCategory?.Code == membership.StaffCategory.Code;
    }

    private async Task RequireTaxonomyMatchesAsync(Guid employeeId, DateOnly date, EffectiveRosterResponse roster, CancellationToken ct)
    {
        var membership = await offices.EmploymentAsync(employeeId, date, ct);
        if (membership is null || membership.EmployeeId != employeeId || membership.Id != roster.Explanation?.MembershipRecordId ||
            membership.BranchId != roster.BranchId || membership.SiteId.HasValue || membership.StaffCategory is null ||
            membership.StaffCategoryId != membership.StaffCategory.Id) throw AttendanceOperation.Conflict("office_membership_source_changed");
        var mapping = await RosterStaffTaxonomy.ResolveAsync(taxonomy, membership.StaffCategory.Code, date,
            RosterSchedule.OccupiedEndExclusive(roster) ?? throw AttendanceOperation.Conflict("office_schedule_provenance_unavailable"), ct);
        var current = await offices.EmploymentAsync(employeeId, date, ct);
        if (current is null || current.EmployeeId != employeeId || current.Id != membership.Id ||
            current.BranchId != membership.BranchId || current.SiteId.HasValue || current.StaffCategoryId != membership.StaffCategoryId ||
            current.StaffCategory?.Code != membership.StaffCategory.Code) throw AttendanceOperation.Conflict("office_membership_source_changed");
        if (!RosterStaffTaxonomy.IsOffice(mapping) || !RosterStaffTaxonomy.Matches(membership.StaffCategory.Code, mapping, roster.Taxonomy))
            throw AttendanceOperation.Conflict("staff_taxonomy_schedule_source_changed");
    }
}
