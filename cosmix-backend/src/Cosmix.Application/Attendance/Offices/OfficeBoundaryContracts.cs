using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.HR.Branches;
using Cosmix.Domain.HR.CompanySettings;

namespace Cosmix.Application.Attendance.Offices;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateOfficeBoundaryRequest(DateOnly EffectiveFrom,
    [property: JsonRequired] decimal Latitude, [property: JsonRequired] decimal Longitude,
    [property: JsonRequired] decimal RadiusMeters, [property: JsonRequired] decimal MaximumAccuracyMeters,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedBranchVersion,
    [property: JsonRequired] int ExpectedLatestVersionNumber,
    [property: JsonRequired] bool ManagementApproved,
    [Required, StringLength(10)] string JurisdictionCode,
    [Required, StringLength(500)] string ApprovalReference,
    [Required, StringLength(500)] string Reason);

public sealed record OfficeBoundaryVersionResponse(Guid Id, Guid BranchId, int VersionNumber,
    DateOnly EffectiveFrom, decimal Latitude, decimal Longitude, decimal RadiusMeters,
    decimal MaximumAccuracyMeters, string JurisdictionCode, string ApprovalReference,
    string RecordedBy, DateTimeOffset RecordedAtUtc);
public sealed record OfficeBoundaryConfigurationResponse(Guid BranchId, DateOnly BusinessDate,
    string Status, string BranchVersion, int LatestVersionNumber, OfficeBoundaryVersionResponse? Boundary);

public sealed record OfficeAttendanceSource(DateOnly BusinessDate, Guid EmployeeId, Guid BranchId,
    Guid OfficeRosterAssignmentId, Guid EmployeeAssignmentId, Guid ShiftVersionId,
    Guid? HolidayCalendarId, Guid OfficeGeofenceVersionId, int BoundaryVersionNumber,
    string JurisdictionCode)
{
    public static OfficeAttendanceSource? Capture(Guid employeeId, DateOnly date,
        EffectiveRosterResponse roster, OfficeBoundaryVersionResponse boundary)
    {
        if (roster.BranchId is null)
        {
            if (roster.Explanation?.Source == "office" || roster.Explanation?.OfficeRosterAssignmentId.HasValue == true)
                throw AttendanceOperation.Conflict("office_schedule_provenance_unavailable");
            return null;
        }
        if (employeeId == Guid.Empty || date == default || roster.EmployeeId != employeeId || roster.BusinessDate != date ||
            roster.TimeZone != RosterSchedule.TimeZoneId || roster.Status is not ("scheduled" or "rest_day") ||
            roster.BranchId == Guid.Empty || roster.SiteId.HasValue || roster.Explanation?.Source != "office" ||
            roster.Explanation.SiteAssignmentId.HasValue || roster.Explanation.TemporaryChangeId.HasValue ||
            roster.Explanation.OfficeRosterAssignmentId is not Guid officeRoster || officeRoster == Guid.Empty ||
            roster.AssignmentId != officeRoster || roster.Explanation.MembershipRecordId is not Guid membership || membership == Guid.Empty ||
            roster.ShiftVersionId is not Guid shift || shift == Guid.Empty || roster.Holiday is null || roster.Holiday.BusinessDate != date ||
            boundary.Id == Guid.Empty || boundary.BranchId != roster.BranchId || boundary.VersionNumber < 1 ||
            boundary.EffectiveFrom == default || boundary.EffectiveFrom > date ||
            !OfficeBoundaryService.ValidGeometry(boundary.Latitude, boundary.Longitude, boundary.RadiusMeters, boundary.MaximumAccuracyMeters) ||
            !CompanyJurisdictions.Codes.Contains(boundary.JurisdictionCode, StringComparer.Ordinal) ||
            string.IsNullOrWhiteSpace(boundary.ApprovalReference))
            throw AttendanceOperation.Conflict("office_schedule_provenance_unavailable");
        return new(date, employeeId, roster.BranchId.Value, officeRoster, membership, shift,
            roster.Holiday.CalendarId, boundary.Id, boundary.VersionNumber, boundary.JurisdictionCode);
    }
}

public sealed record OfficeAttendanceContext(string Status, OfficeAttendanceSource? Source,
    OfficeBoundaryVersionResponse? Boundary);

/// <summary>Bounded append-only office boundary history; caller owns the existing authorization transaction.</summary>
public interface IOfficeBoundaryRepository
{
    Task<Branch?> FindBranchAsync(Guid branchId, CancellationToken ct);
    Task<IReadOnlyList<OfficeBoundaryVersionResponse>> VersionsAsync(Guid branchId, CancellationToken ct);
    Task<bool> AnyLockedFromAsync(DateOnly date, CancellationToken ct);
    Task AddAsync(OfficeBoundaryVersionResponse version, CancellationToken ct);
}

/// <summary>Internal source binding only. Never accept this source from the mobile request.</summary>
public interface IOfficeAttendanceSourceReader
{
    Task<OfficeAttendanceContext> ReadAsync(Guid employeeId, DateOnly date,
        EffectiveRosterResponse roster, Guid? pinnedBoundaryId, CancellationToken ct);
    Task<bool> CurrentMembershipMatchesAsync(Guid employeeId, Guid branchId, Guid? membershipId,
        DateOnly receiptDate, CancellationToken ct);
}
