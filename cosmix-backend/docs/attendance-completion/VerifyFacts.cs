#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using System.Security.Cryptography;
using System.Text.Json;

var employee = Guid.NewGuid(); var date = new DateOnly(2026, 10, 10); var checks = 0;
var start = new DateTimeOffset(2026, 10, 10, 15, 0, 0, TimeSpan.Zero);
WorkSession Session(int revision, Guid key, DateTimeOffset from, DateTimeOffset? to)
{
    RawPunchEvent Punch(PunchAction action, DateTimeOffset at) => new(employee, action, "synthetic-device",
        Guid.NewGuid().ToString("N"), new string('A', 64), at, false);
    return new(key, revision, employee, date, from, to, Guid.NewGuid(), null, to ?? from,
        Punch(PunchAction.CheckIn, from), to.HasValue ? Punch(PunchAction.CheckOut, to.Value) : null);
}
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
void Denied(Action action, string code)
{ try { action(); throw new Exception("Expected " + code); } catch(AuthenticationFailure ex) { Check(ex.Code == code, code); } }
var one = Session(1, Guid.NewGuid(), start, start.AddHours(9));
var result = AttendanceFacts.Calculate(employee, date, [one]);
Check(result.WorkedSeconds == 9 * 3600 && result.WorkedMinutes == 540, "overnight elapsed time, no automatic unpaid-break deduction");
Check(result.OpenSessions == 0 && result.SessionIds.Single() == one.Id, "source session revision retained");
var key = Guid.NewGuid(); var open = Session(1, key, start, null); var closed = Session(2, key, start, start.AddHours(2));
result = AttendanceFacts.Calculate(employee, date, [open, closed]);
Check(result.WorkedMinutes == 120 && result.OpenSessions == 0 && result.SessionIds.Single() == closed.Id, "latest revision counted once");
var shortA = Session(1, Guid.NewGuid(), start, start.AddSeconds(35));
var shortB = Session(1, Guid.NewGuid(), start.AddMinutes(1), start.AddMinutes(1).AddSeconds(35));
result = AttendanceFacts.Calculate(employee, date, [shortB, shortA]);
Check(result.WorkedSeconds == 70 && result.WorkedMinutes == 1, "round only after summing factual seconds");
Check(result.InputFingerprint == AttendanceFacts.Calculate(employee, date, [shortA, shortB]).InputFingerprint, "input ordering stable");
Check(AttendanceFacts.Calculate(employee, date, [open]).WorkedSeconds == 0, "open duration is not fabricated");
Check(AttendanceFacts.Calculate(employee, date, []).SessionIds.Count == 0, "no sessions is no evidence, not absence");
Denied(() => AttendanceFacts.Calculate(Guid.NewGuid(), date, [one]), "attendance_session_identity_conflict");
Denied(() => AttendanceFacts.Calculate(employee, date.AddDays(1), [one]), "attendance_session_identity_conflict");
Denied(() => AttendanceFacts.Calculate(employee, date, [one, open]), "attendance_session_overlap");
Denied(() => AttendanceFacts.Calculate(employee, date, [open, Session(1, key, start, null)]), "attendance_session_revision_conflict");
var legacyFingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
{
    employeeId = employee, date, calculation = AttendanceFacts.CalculationVersion,
    sessions = new[] { new { one.Id, one.SessionKey, one.Revision, one.StartsAtUtc, one.EndsAtUtc,
        one.CheckInPunchId, one.CheckOutPunchId, one.CorrectionId, one.SiteId, one.ShiftVersionId, one.PolicyVersionId } }
})));
Check(AttendanceFacts.Calculate(employee, date, [one]).InputFingerprint == legacyFingerprint,
    "legacy Site factual JSON and fingerprint remain byte-for-byte compatible");
var branch = Guid.NewGuid(); var officeRoster = Guid.NewGuid(); var membership = Guid.NewGuid();
var boundary = Guid.NewGuid(); var calendar = Guid.NewGuid(); var officeShift = Guid.NewGuid();
RawPunchEvent OfficePunch(PunchAction action, DateTimeOffset at) => new(employee, action, "synthetic-device",
    Guid.NewGuid().ToString("N"), new string('A', 64), at, false, employeeAssignmentId: membership,
    shiftVersionId: officeShift, businessDate: date, holidayCalendarId: calendar, branchId: branch,
    officeRosterAssignmentId: officeRoster, officeGeofenceVersionId: boundary);
var officeIn = OfficePunch(PunchAction.CheckIn, start); var officeOut = OfficePunch(PunchAction.CheckOut, start.AddHours(9));
var office = new WorkSession(Guid.NewGuid(), 1, employee, date, start, start.AddHours(9), officeShift, null, start.AddHours(9),
    officeIn, officeOut, branchId: branch, officeRosterAssignmentId: officeRoster, employeeAssignmentId: membership,
    officeGeofenceVersionId: boundary, holidayCalendarId: calendar);
var officeFacts = AttendanceFacts.Calculate(employee, date, [office]);
typeof(WorkSession).GetProperty("OfficeGeofenceVersionId")!.SetValue(office, Guid.NewGuid());
Check(AttendanceFacts.Calculate(employee, date, [office]).InputFingerprint != officeFacts.InputFingerprint,
    "B01 RED: changed retained Office boundary must change factual fingerprint at identical times");
typeof(WorkSession).GetProperty("OfficeGeofenceVersionId")!.SetValue(office, boundary);
foreach (var name in new[] { "BranchId", "OfficeRosterAssignmentId", "EmployeeAssignmentId", "HolidayCalendarId" })
{
    var property = typeof(WorkSession).GetProperty(name)!; var prior = property.GetValue(office);
    property.SetValue(office, Guid.NewGuid());
    Check(AttendanceFacts.Calculate(employee, date, [office]).InputFingerprint != officeFacts.InputFingerprint,
        "Office factual fingerprint binds " + name);
    property.SetValue(office, prior);
}
Console.WriteLine($"PASS {checks} factual calculation checks. In-memory domain input; no SQL/provider acceptance.");
