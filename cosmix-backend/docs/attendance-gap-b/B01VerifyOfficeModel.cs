#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;

// Source/model contract checks only. No accounts, SQL, host or biometric provider.
var checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); checks++; }
foreach (var field in new[] { "BranchId", "OfficeRosterAssignmentId", "OfficeGeofenceVersionId" })
    Check(typeof(RawPunchEvent).GetProperty(field)?.PropertyType == typeof(Guid?), "office raw evidence retains " + field);
foreach (var field in new[] { "BranchId", "OfficeRosterAssignmentId", "EmployeeAssignmentId", "OfficeGeofenceVersionId", "HolidayCalendarId" })
    Check(typeof(WorkSession).GetProperty(field)?.PropertyType == typeof(Guid?), "office session revision retains " + field);
var employee = Guid.NewGuid(); var branch = Guid.NewGuid(); var roster = Guid.NewGuid(); var assignment = Guid.NewGuid();
var boundary = Guid.NewGuid(); var shift = Guid.NewGuid(); var calendar = Guid.NewGuid();
var date = new DateOnly(2026,10,14); var now = new DateTimeOffset(2026,10,14,4,0,0,TimeSpan.Zero);
RawPunchEvent Raw(DateOnly day, Guid version) => new(employee,PunchAction.CheckIn,Guid.NewGuid().ToString("D"),
    "synthetic-office-model",new string('A',64),now,false,employeeAssignmentId:assignment,shiftVersionId:version,
    businessDate:day,holidayCalendarId:calendar,branchId:branch,officeRosterAssignmentId:roster,officeGeofenceVersionId:boundary);
WorkSession Session(RawPunchEvent raw) => new(Guid.NewGuid(),1,employee,date,now,null,shift,null,now,raw,
    branchId:branch,officeRosterAssignmentId:roster,employeeAssignmentId:assignment,
    officeGeofenceVersionId:boundary,holidayCalendarId:calendar);
Check(Session(Raw(date,shift)).BranchId == branch,"exact office raw-to-duty source accepted");
void Reject(Action action,string name) { checks++; try { action(); } catch(ArgumentException) { return; } throw new Exception(name); }
Reject(() => Session(Raw(date.AddDays(-1),shift)),"office raw business date cannot be relabelled as another accepted duty day");
Reject(() => Session(Raw(date,Guid.NewGuid())),"office accepted duty cannot replace its raw shift");
Reject(() => new RawPunchEvent(employee,PunchAction.CheckIn,Guid.NewGuid().ToString("D"),"mixed-office-site",new string('B',64),now,false,
    siteId:Guid.NewGuid(),businessDate:date,branchId:branch),"no fake site on office raw evidence");
Reject(() => new WorkSession(Guid.NewGuid(),1,employee,date,now,now.AddHours(1),shift,null,now,null,
    correctionId:Guid.NewGuid(),branchId:branch),"manual office duty still needs exact office roster and membership and boundary");
Console.WriteLine($"PASS: {checks} office source shape/domain checks; no SQL/HTTP/face acceptance.");
