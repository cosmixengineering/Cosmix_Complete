#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;

// Actual source validation, no SQL/provider/employee accounts.
var checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); checks++; }
var method = typeof(OfficeAttendanceSource).GetMethod("Capture", BindingFlags.Static | BindingFlags.Public);
Check(method is not null, "office provenance is validated rather than just defining a DTO");
var capture = (Func<Guid, DateOnly, EffectiveRosterResponse, OfficeBoundaryVersionResponse, OfficeAttendanceSource?>)
    method!.CreateDelegate(typeof(Func<Guid, DateOnly, EffectiveRosterResponse, OfficeBoundaryVersionResponse, OfficeAttendanceSource?>));
var employee = Guid.NewGuid(); var branch = Guid.NewGuid(); var assignment = Guid.NewGuid();
var membership = Guid.NewGuid(); var shift = Guid.NewGuid(); var date = new DateOnly(2026, 10, 14);
var roster = new EffectiveRosterResponse(employee, date, "scheduled", RosterSchedule.TimeZoneId,
    AssignmentId: assignment, ShiftVersionId: shift,
    Explanation: new("office", null, membership, null, null, "office") { OfficeRosterAssignmentId = assignment },
    Holiday: RosterHolidayContext.Resolve(null, date)) { BranchId = branch };
var boundary = new OfficeBoundaryVersionResponse(Guid.NewGuid(), branch, 1, date, 24m, 67m, 100m, 20m,
    "PK-SD", "SYNTHETIC configuration only", "synthetic", new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero));
var source = capture(employee, date, roster, boundary)!;
Check(source.BranchId == branch && source.OfficeRosterAssignmentId == assignment && source.EmployeeAssignmentId == membership &&
    source.ShiftVersionId == shift && source.OfficeGeofenceVersionId == boundary.Id && source.JurisdictionCode == boundary.JurisdictionCode,
    "real office, schedule, membership and boundary sources are retained separately from site");
void Deny(EffectiveRosterResponse candidate, OfficeBoundaryVersionResponse evidence, string name)
{
    try { capture(employee, date, candidate, evidence); }
    catch (AuthenticationFailure e) when (e.StatusCode == 409) { checks++; return; }
    throw new Exception("FAIL: " + name);
}
Deny(roster with { SiteId = Guid.NewGuid() }, boundary, "office cannot masquerade as site");
Deny(roster with { EmployeeId = Guid.NewGuid() }, boundary, "employee is server-bound");
Deny(roster with { BusinessDate = date.AddDays(1) }, boundary, "business date is pinned");
Deny(roster with { AssignmentId = Guid.NewGuid() }, boundary, "office roster reference must match explanation");
Deny(roster with { Explanation = roster.Explanation! with { MembershipRecordId = null } }, boundary, "membership cannot be inferred");
Deny(roster with { Holiday = null }, boundary, "missing calendar source differs from explicit not-configured context");
Deny(roster, boundary with { BranchId = Guid.NewGuid() }, "boundary belongs to exact office");
Deny(roster, boundary with { EffectiveFrom = date.AddDays(1) }, "future boundary cannot validate earlier duty");
Deny(roster, boundary with { Id = Guid.Empty }, "missing boundary identity is unavailable");
Deny(roster, boundary with { JurisdictionCode = "unknown" }, "jurisdiction is explicit and known, not Head Office inference");
Deny(roster, boundary with { RadiusMeters = 0 }, "invalid stored boundary cannot validate duty");
Deny(roster, boundary with { Latitude = 24.12345678m }, "stored geometry precision remains exact");
Deny(roster with { Explanation = roster.Explanation! with { SiteAssignmentId = Guid.NewGuid() } }, boundary, "office cannot retain a fabricated site roster");
Check(capture(employee, date, roster with { BranchId = null, SiteId = Guid.NewGuid(),
    Explanation = new("site", Guid.NewGuid(), membership, null, null, "site") }, boundary) is null,
    "site source does not acquire office evidence");
Console.WriteLine($"PASS: {checks} office provenance checks; no SQL/HTTP/face acceptance.");
