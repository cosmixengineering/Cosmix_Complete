#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Calculation;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Punches;

// In-memory source/serialization checks only. No SQL, provider, files, or authority defaults.
var checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
async Task Fails(Func<Task> action, string code)
{ checks++; try { await action(); } catch (AuthenticationFailure e) when (e.Code == code) { return; } throw new Exception(code); }
string Json(object value) => JsonSerializer.Serialize(value);
string Hash(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
void Legacy(object current, object old, string name)
{ Check(Json(current) == Json(old), name + " exact legacy JSON"); Check(Hash(current) == Hash(old), name + " exact legacy hash"); }
var date = new DateOnly(2026, 10, 12); var employee = Guid.NewGuid(); var site = Guid.NewGuid();
var start = new DateTimeOffset(2026, 10, 12, 5, 0, 0, TimeSpan.Zero);
var punch = new AttendanceCalculationPunch(Guid.NewGuid(), employee, PunchAction.CheckIn, date, start, site,
    Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), 1, PunchAssessmentOutcome.Accepted, 1);
Legacy(punch, new { punch.Id, punch.EmployeeId, punch.Action, punch.BusinessDate, punch.ReceivedAtUtc, punch.SiteId,
    punch.EmployeeAssignmentId, punch.ShiftVersionId, punch.SiteRosterAssignmentId, punch.TemporaryShiftChangeId, punch.HolidayCalendarId,
    punch.AssessmentId, punch.AssessmentRevision, punch.AssessmentOutcome, punch.LatestAssessmentCount }, "Site punch");
var session = new OvertimeSessionSource(Guid.NewGuid(), Guid.NewGuid(), 1, start, start.AddHours(9), site,
    punch.ShiftVersionId!.Value, null, punch.AssessmentId, Guid.NewGuid(), null, true);
Legacy(session, new { session.Id, session.SessionKey, session.Revision, session.StartsAtUtc, session.EndsAtUtc, session.SiteId,
    session.ShiftVersionId, session.PolicyVersionId, session.CheckInAssessmentId, session.CheckOutAssessmentId,
    session.CorrectionDecisionId, session.ValidDutyEvidence, session.CorrectionId }, "Site session");
var employment = new OvertimeEmploymentSource(Guid.NewGuid(), Guid.NewGuid(), "SITE", site, date.AddYears(-1), null);
Legacy(employment, new { employment.AssignmentId, employment.StaffCategoryId, employment.StaffCategory, employment.SiteId,
    employment.EffectiveFrom, employment.EffectiveTo }, "Site employment");
var jurisdictionDay = new AttendancePayrollJurisdictionDay(date, site);
Legacy(jurisdictionDay, new { jurisdictionDay.BusinessDate, jurisdictionDay.SiteId }, "Site jurisdiction request");
var jurisdiction = new AttendancePayrollJurisdictionContext(date, site, Guid.NewGuid(), "PK-PB", "configured");
Legacy(jurisdiction, new { jurisdiction.BusinessDate, jurisdiction.SiteId, jurisdiction.ConfigurationVersionId,
    jurisdiction.JurisdictionCode, jurisdiction.State }, "Site jurisdiction response");
var payrollDay = new AttendancePayrollDaySource(date, "unavailable", new('A', 64), null, null, null);
Legacy(payrollDay, new { payrollDay.BusinessDate, payrollDay.State, payrollDay.Fingerprint, payrollDay.Snapshot,
    payrollDay.Time, payrollDay.Compensation, payrollDay.Policy }, "Site payroll day");
for (var schema = 1; schema <= 3; schema++)
{
    var snapshot = new AttendanceCalculationSnapshot(schema, employee, date, "synthetic", new('A', 64), null!, employment,
        null!, null!, [session], [punch], [], [], null!, 0);
    Legacy(snapshot, new { snapshot.SchemaVersion, snapshot.EmployeeId, snapshot.BusinessDate, snapshot.CalculationVersion,
        snapshot.FactualFingerprint, snapshot.Roster, snapshot.Employment, snapshot.Policy, snapshot.Leave, snapshot.Sessions,
        snapshot.Punches, snapshot.Corrections, snapshot.Overtime, snapshot.TimeInput, snapshot.ApprovedOvertimeTicks }, $"schema-{schema} legacy shape");
}
var branch = Guid.NewGuid(); var rosterId = Guid.NewGuid(); var member = Guid.NewGuid();
var roster = new EffectiveRosterResponse(employee, date, "scheduled", RosterSchedule.TimeZoneId,
    AssignmentId: rosterId, ShiftVersionId: Guid.NewGuid(),
    Explanation: new("office", null, member, null, null, "synthetic") { OfficeRosterAssignmentId = rosterId },
    Holiday: new("configured", date, Guid.NewGuid(), "synthetic", 1, [])) { BranchId = branch };
var first = new OfficeBoundaryVersionResponse(Guid.NewGuid(), branch, 1, date.AddDays(-2), 24m, 67m, 100m, 20m,
    "PK-SD", "synthetic-approved-history", "synthetic", start.AddDays(-3));
var next = first with { Id = Guid.NewGuid(), VersionNumber = 2, EffectiveFrom = date.AddDays(1), JurisdictionCode = "PK-PB" };
var source = OfficeAttendanceSource.Capture(employee, date, roster, first)!;
var history = new List<OfficeBoundaryVersionResponse> { first, next }; var reads = 0; var siteReads = 0;
IReadOnlyList<AttendancePayrollJurisdictionDay> delegated = [];
var boundaries = SourceProxy.Make<IOfficeBoundaryRepository>((method, args) =>
{
    if (method.Name != "VersionsAsync" || (Guid)args[0]! != branch) throw new Exception("Unexpected branch/read or write");
    reads++; return Task.FromResult<IReadOnlyList<OfficeBoundaryVersionResponse>>(history.ToArray());
});
var sites = SourceProxy.Make<IAttendancePayrollJurisdictionReader>((_, args) =>
{
    siteReads++; delegated = ((IReadOnlyList<AttendancePayrollJurisdictionDay>)args[0]!).ToArray();
    if (delegated.Any(x => x.BranchId.HasValue || x.OfficeSource is not null)) throw new Exception("Office delegated to Site/HO fallback");
    return Task.FromResult<IReadOnlyList<AttendancePayrollJurisdictionContext>>(delegated.Select(x => jurisdiction with { BusinessDate = x.BusinessDate, SiteId = x.SiteId }).ToArray());
});
var reader = new OfficeAttendancePayrollJurisdictionReader(sites, boundaries);
var officeDay = new AttendancePayrollJurisdictionDay(date, null, branch, source);
var result = await reader.ReadAsync([officeDay, jurisdictionDay, officeDay with { BusinessDate = date.AddDays(-1),
    OfficeSource = source with { BusinessDate = date.AddDays(-1) } }], default);
Check(result.Count == 3 && reads == 1 && siteReads == 1 && delegated.SequenceEqual([jurisdictionDay]), "mixed sources read dated Office history once and delegate only actual Site");
Check(result.Single(x => x.SiteId == site) == jurisdiction, "Site passthrough unchanged");
Check(result.Where(x => x.BranchId == branch).All(x => x.SiteId is null && x.ConfigurationVersionId == first.Id &&
    x.JurisdictionCode == "PK-SD" && x.State == "configured"), "future Office boundary cannot replace the retained effective boundary");
Check(Json(result).Contains("BranchId") && !Json(result).Contains("ApprovalReference"), "public location metadata has Branch, no private approval detail");
foreach (var wrong in new[] { source with { OfficeGeofenceVersionId = Guid.NewGuid() }, source with { BoundaryVersionNumber = 2 },
    source with { JurisdictionCode = "PK-PB" } })
    Check((await reader.ReadAsync([officeDay with { OfficeSource = wrong }], default)).Single().State == "conflict", "wrong Office boundary/version/jurisdiction cannot be inferred");
var laterSource = OfficeAttendanceSource.Capture(employee, next.EffectiveFrom, roster with { BusinessDate = next.EffectiveFrom,
    Holiday = roster.Holiday! with { BusinessDate = next.EffectiveFrom } }, next)!;
Check((await reader.ReadAsync([new(next.EffectiveFrom, null, branch, laterSource)], default)).Single().ConfigurationVersionId == next.Id,
    "actual later history selects the later approved boundary");
Check((await reader.ReadAsync([officeDay with { BusinessDate = next.EffectiveFrom, OfficeSource = source with { BusinessDate = next.EffectiveFrom } }], default))
    .Single().State == "conflict", "stale Office boundary at a newer effective date fails closed");
var before = reads; var beforeSite = siteReads;
Check((await reader.ReadAsync([officeDay with { OfficeSource = null }], default)).Single() is { State: "unavailable", SiteId: null, ConfigurationVersionId: null, JurisdictionCode: null },
    "missing Office source has no HO jurisdiction fallback");
Check(reads == before && siteReads == beforeSite, "no read of unrelated location for missing proof");
history.Clear();
Check((await reader.ReadAsync([officeDay], default)).Single().State == "unavailable", "missing Office history remains unavailable");
history.Add(first with { BranchId = Guid.NewGuid() });
await Fails(() => reader.ReadAsync([officeDay], default), "office_boundary_history_unavailable");
history.Clear(); history.Add(first);
await Fails(() => reader.ReadAsync([officeDay with { SiteId = site }], default), "payroll_jurisdiction_contract_invalid");
await Fails(() => reader.ReadAsync([officeDay with { OfficeSource = source with { BranchId = Guid.NewGuid() } }], default), "payroll_jurisdiction_contract_invalid");
await Fails(() => reader.ReadAsync([officeDay with { BranchId = Guid.Empty }], default), "payroll_jurisdiction_contract_invalid");
await Fails(() => reader.ReadAsync(Enumerable.Repeat(officeDay, 367).ToArray(), default), "payroll_jurisdiction_contract_invalid");
Check((await reader.ReadAsync([], default)).Count == 0, "empty batch does not invent location");
var eligibility = await new UnavailableOvertimeEligibilityReader().ResolveAsync(employee, date,
    employment with { StaffCategory = "OFFICE", SiteId = null, BranchId = branch }, default);
Check(eligibility is { Status: "office_ineligible", Allowed: false }, "fixed OFFICE remains OT ineligible without taxonomy defaults");
await Fails(() => reader.ReadAsync([officeDay with { BusinessDate = default, OfficeSource = source with { BusinessDate = default } }], default),
    "payroll_jurisdiction_contract_invalid");
Console.WriteLine($"PASS: {checks} Office payroll history and exact legacy JSON/fingerprint checks; synthetic sources only.");

public class SourceProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, SourceProxy>(); ((SourceProxy)(object)value).Handler = handler; return value; }
}
