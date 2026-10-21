#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=B03VerifyOfficeEvidence.packages.lock.json
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.CarryForward;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;

// Actual pure evidence serializers and validators; no SQL, auth account, face or storage writes.
var checks = 0;
void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
void Conflict(Action action, string code)
{
    checks++;
    try { action(); }
    catch (TargetInvocationException e) when (e.InnerException is AuthenticationFailure a && a.Code == code) { return; }
    catch (AuthenticationFailure e) when (e.Code == code) { return; }
    throw new Exception("Expected " + code);
}
string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
var type = typeof(CorrectionSourceResponse).Assembly.GetType("Cosmix.Application.Attendance.Corrections.CorrectionSourceEvidence")!;
var capture = type.GetMethod("Capture")!; var matches = type.GetMethod("Matches")!;
var employee = Guid.NewGuid(); var date = new DateOnly(2026,9,8); var start = new DateTimeOffset(2026,9,8,16,0,0,TimeSpan.Zero);
var site = Guid.NewGuid(); var shift = Guid.NewGuid();
var roster = new EffectiveRosterResponse(employee,date,"scheduled",RosterSchedule.TimeZoneId,site,Guid.NewGuid(),Guid.NewGuid(),shift,1,
    start,start.AddHours(10),600,0,0,new("site",Guid.NewGuid(),Guid.NewGuid(),null,null,"site history"));
var raw = new RawPunchEvent(employee,PunchAction.CheckIn,Guid.NewGuid().ToString(),"synthetic-raw-key",new string('A',64),start,false,
    businessDate:date,siteId:site,shiftVersionId:shift);
var session = new WorkSession(Guid.NewGuid(),1,employee,date,start,start.AddHours(8),shift,null,start.AddHours(9),null,
    correctionId:Guid.NewGuid(),siteId:site);
IReadOnlyList<AttendanceResourceScope> scopes = [new([new(EmployeeId:employee,SiteId:site)])];
(CorrectionSourceResponse Source,IReadOnlyList<AttendanceResourceScope> Scopes) Capture(CorrectionSourceState state,
    EffectiveRosterResponse r, OfficeAttendanceSource? office = null) =>
    ((CorrectionSourceResponse,IReadOnlyList<AttendanceResourceScope>))capture.Invoke(null,[employee,date,state,r,scopes,office])!;
var oldSession = new { session.Id,session.SessionKey,session.Revision,session.StartsAtUtc,session.EndsAtUtc,session.CheckInPunchId,
    session.CheckOutPunchId,session.CorrectionId,session.SiteId,session.ShiftVersionId,session.PolicyVersionId,session.EndsContinuousDuty };
var oldPunch = new { raw.BusinessDate,raw.SiteId,raw.EmployeeAssignmentId,raw.ShiftVersionId,raw.GeofenceVersionId,
    raw.SiteRosterAssignmentId,raw.TemporaryShiftChangeId,raw.HolidayCalendarId,raw.IsOffline };
var source2 = Capture(new(raw,null,null,false,[session]),roster);
var oldSource2 = new { source2.Source.BusinessDate,source2.Source.RawPunchId,source2.Source.Action,source2.Source.OriginalTimeUtc,
    source2.Source.LatestAssessmentId,source2.Source.AssessmentOutcome,source2.Source.LatestSummaryId,source2.Source.SummaryRevision,
    source2.Source.SummaryStatus,source2.Source.PeriodLocked,source2.Source.RoutingStatus,source2.Source.SummaryCalculationState,
    source2.Source.HistoricalRoster,SessionRevisions=new[]{oldSession},OriginalPunchContext=oldPunch };
Check(source2.Source.SourceFingerprint == Hash(new {schema="correction-source-v2",employeeId=employee,source=oldSource2,originalResourceScopes=source2.Scopes}),
    "Site schema2 fingerprint matches exact pre-office typed byte contract");
var source3 = Capture(new(raw,null,null,false,[session],[new(session.Id,null,null,null)]),roster);
var oldSource3 = new { source3.Source.BusinessDate,source3.Source.RawPunchId,source3.Source.Action,source3.Source.OriginalTimeUtc,
    source3.Source.LatestAssessmentId,source3.Source.AssessmentOutcome,source3.Source.LatestSummaryId,source3.Source.SummaryRevision,
    source3.Source.SummaryStatus,source3.Source.PeriodLocked,source3.Source.RoutingStatus,source3.Source.SummaryCalculationState,
    source3.Source.HistoricalRoster,SessionRevisions=new[]{oldSession},OriginalPunchContext=oldPunch,source3.Source.DutyProofs };
Check(source3.Source.SourceFingerprint == Hash(new {schema="correction-source-v3",employeeId=employee,source=oldSource3,originalResourceScopes=source3.Scopes}),
    "Site schema3 fingerprint matches exact pre-office typed byte contract");
Check(!JsonSerializer.Serialize(source3.Source).Contains("OfficeSource") && !JsonSerializer.Serialize(source3.Source).Contains("BranchId"),
    "new null office fields do not alter Site serialized source bytes");
var siteCarry = new CarryForwardSession(session.SessionKey,session.Id,start,start.AddHours(8),site,shift,true,session.CorrectionId,null,null);
var oldCarry = new {siteCarry.SessionKey,siteCarry.SourceWorkSessionId,siteCarry.StartsAtUtc,siteCarry.EndsAtUtc,SiteId=site,
    siteCarry.ShiftVersionId,siteCarry.IsManual,siteCarry.OriginCorrectionId,siteCarry.CheckInPunchId,siteCarry.CheckOutPunchId};
Check(JsonSerializer.Serialize(siteCarry) == JsonSerializer.Serialize(oldCarry), "Site carry-forward session retains exact legacy JSON bytes");

var boundary = new OfficeBoundaryVersionResponse(Guid.NewGuid(),Guid.NewGuid(),1,new(2026,9,1),24m,67m,100m,20m,"PK-SD",
    "SYNTHETIC TEST ONLY","synthetic",new(2026,9,1,0,0,0,TimeSpan.Zero));
var officeRoster = roster with { SiteId=null,BranchId=boundary.BranchId,
    Explanation=new("office",null,Guid.NewGuid(),null,null,"office history"){OfficeRosterAssignmentId=roster.AssignmentId},
    Holiday=new("configured",date,Guid.NewGuid(),"SYNTHETIC",1,[]) };
var office = OfficeAttendanceSource.Capture(employee,date,officeRoster,boundary)!;
var officeSession = new WorkSession(Guid.NewGuid(),1,employee,date,start,start.AddHours(8),shift,null,start.AddHours(9),null,
    correctionId:Guid.NewGuid(),branchId:office.BranchId,officeRosterAssignmentId:office.OfficeRosterAssignmentId,
    employeeAssignmentId:office.EmployeeAssignmentId,officeGeofenceVersionId:office.OfficeGeofenceVersionId,holidayCalendarId:office.HolidayCalendarId);
var officeState = new CorrectionSourceState(null,null,null,false,[officeSession],[new(officeSession.Id,null,null,null)]);
var officeSource = Capture(officeState,officeRoster,office).Source;
var proposal = new CreateCorrectionRequest(date,AttendanceCorrectionKind.ManualSession,null,null,null,start,start.AddHours(8),null,
    "Synthetic documented duty","SYNTHETIC TEST ONLY",officeSource.SourceFingerprint);
var snapshot = new CorrectionEvidenceSnapshot(4,Guid.NewGuid(),employee,Guid.NewGuid(),proposal,officeSource,scopes);
bool Matches(CorrectionEvidenceSnapshot value) => (bool)matches.Invoke(null,[value])!;
Check(Matches(snapshot), "office schema4 protected snapshot validates retained source");
Check(!Matches(snapshot with {SchemaVersion=3}), "office sources cannot be downgraded into legacy Site schema3");
Check(!Matches(snapshot with {Source=officeSource with {OfficeSource=office with {OfficeGeofenceVersionId=Guid.NewGuid()}}}),
    "changed office boundary identity invalidates protected source fingerprint");
Check(!Matches(snapshot with {Source=officeSource with {SessionRevisions=officeSource.SessionRevisions!.Select(x=>x with {BranchId=Guid.NewGuid()}).ToArray()}}),
    "changed pinned session branch invalidates source evidence");
Check(!Matches(snapshot with {Source=officeSource with {OfficeSource=null}}), "missing office evidence is not an empty validated source");
Conflict(()=>Capture(officeState,officeRoster),"correction_office_source_unavailable");
Conflict(()=>Capture(officeState,officeRoster,office with {HolidayCalendarId=Guid.NewGuid()}),"correction_office_source_unavailable");
Conflict(()=>Capture(officeState,officeRoster,office with {EmployeeAssignmentId=Guid.NewGuid()}),"correction_office_source_unavailable");
Conflict(()=>Capture(officeState,officeRoster,office with {OfficeRosterAssignmentId=Guid.NewGuid()}),"correction_office_source_unavailable");
Conflict(()=>Capture(new(null,null,null,false,[session],[new(session.Id,null,null,null)]),officeRoster,office),"correction_office_source_unavailable");
Conflict(()=>Capture(officeState,roster),"correction_office_source_unavailable");
var officeCarry = siteCarry with {SiteId=null,OfficeSource=office};
Check(CarryForwardRules.TotalTicks([officeCarry])==TimeSpan.FromHours(8).Ticks,"office factual duration uses explicit retained source");
Conflict(()=>CarryForwardRules.TotalTicks([officeCarry with {SiteId=site}]),"carry_forward_interpretation_unavailable");
Conflict(()=>CarryForwardRules.TotalTicks([officeCarry with {OfficeSource=null}]),"carry_forward_interpretation_unavailable");
Conflict(()=>CarryForwardRules.TotalTicks([officeCarry with {OfficeSource=office with {OfficeGeofenceVersionId=Guid.Empty}}]),"carry_forward_interpretation_unavailable");
Conflict(()=>CarryForwardRules.TotalTicks([officeCarry with {OfficeSource=office with {JurisdictionCode="UNVERIFIED"}}]),"carry_forward_interpretation_unavailable");
Check(CarryForwardEvidence.InterpretationFingerprint([officeCarry]) != CarryForwardEvidence.InterpretationFingerprint([officeCarry with {OfficeSource=office with {HolidayCalendarId=Guid.NewGuid()}}]),
    "office calendar changes are bound into carry-forward interpretation digest");
Console.WriteLine($"B03 OFFICE evidence checks passed: {checks}. Pure source and legacy-byte checks only.");
