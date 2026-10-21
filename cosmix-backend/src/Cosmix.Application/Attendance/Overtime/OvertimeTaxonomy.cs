using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.HR.StaffCategories;

namespace Cosmix.Application.Attendance.Overtime;

/// <summary>Taxonomy is a dated source discriminator, never an overtime award or permission.</summary>
internal static class OvertimeTaxonomy
{
    public static OvertimeEligibility? Restriction(StaffTaxonomyResolution resolution)
    {
        if (!resolution.Available || resolution.Mapping is null) return new("category_unavailable", null, null);
        _ = RosterStaffTaxonomy.Capture(resolution); // Reject an asserted effective/baseline state without exact source proof.
        if (RosterStaffTaxonomy.IsOffice(resolution)) return new("office_ineligible", null, false);
        if (!RosterStaffTaxonomy.IsSite(resolution)) return new("category_unavailable", null, null);
        return resolution.Mapping.OutsideOvertimeInheritance ? null : new("category_ineligible", null, false);
    }

    public static DateOnly EndExclusive(OvertimeSourceSnapshot source)
    {
        var end = source.Source.BusinessDate.AddDays(1);
        foreach (var at in source.Source.Sessions.Where(x => x.EndsAtUtc.HasValue).Select(x => x.EndsAtUtc!.Value)
            .Concat(source.Source.Roster.EndsAt.HasValue ? [source.Source.Roster.EndsAt.Value] : []))
        {
            var occupiedEnd = RosterSchedule.Today(at.AddTicks(-1)).AddDays(1);
            if (occupiedEnd > end) end = occupiedEnd;
        }
        return end;
    }

    public static void ValidateRetained(OvertimeSourceSnapshot snapshot)
    {
        var source = snapshot.Source; var employment = snapshot.Employment;
        if (employment.AssignmentId == Guid.Empty || employment.StaffCategoryId == Guid.Empty ||
            employment.StaffCategory != source.StaffCategory || employment.EffectiveFrom > source.BusinessDate ||
            employment.EffectiveTo <= source.BusinessDate || source.Roster.EmployeeId != source.EmployeeId ||
            source.Roster.BusinessDate != source.BusinessDate)
            throw AttendanceOperation.Conflict("overtime_taxonomy_source_conflict");
        // Exact legacy sources retain their original bytes. Any published/broader source must carry
        // its real version/fingerprint; a bare category string cannot inherit Site eligibility.
        var pin = source.Roster.Taxonomy;
        if (pin is null)
        {
            if (source.StaffCategory is not ("OFFICE" or "SITE"))
                throw AttendanceOperation.Conflict("overtime_category_unavailable");
            return;
        }
        var office = pin.WorkLocation == StaffWorkLocation.Office;
        if (source.Roster.TimeZone != RosterSchedule.TimeZoneId || source.Roster.Explanation?.MembershipRecordId != employment.AssignmentId ||
            !RosterStaffTaxonomy.Valid(pin, employment.StaffCategory,
            office ? StaffWorkLocation.Office : StaffWorkLocation.Site,
            office ? StaffScheduleBasis.Office : StaffScheduleBasis.Site) ||
            (office ? employment.SiteId.HasValue || source.SiteId.HasValue || source.Roster.SiteId.HasValue ||
                employment.BranchId is null || source.Roster.BranchId != employment.BranchId :
                employment.BranchId.HasValue || source.Roster.BranchId.HasValue || employment.SiteId is null ||
                source.SiteId != employment.SiteId || source.Roster.SiteId != employment.SiteId))
            throw AttendanceOperation.Conflict("overtime_taxonomy_source_conflict");
    }

    public static bool Office(OvertimeSourceResponse source) => source.Roster.Taxonomy is { } pin
        ? RosterStaffTaxonomy.Valid(pin, source.StaffCategory, StaffWorkLocation.Office, StaffScheduleBasis.Office)
        : source.StaffCategory == "OFFICE";

    public static bool Outside(OvertimeSourceResponse source) => source.Roster.Taxonomy is { } pin
        ? RosterStaffTaxonomy.Valid(pin, source.StaffCategory, StaffWorkLocation.Site, StaffScheduleBasis.Site) && pin.OutsideOvertimeInheritance
        : source.StaffCategory == "SITE";
}
