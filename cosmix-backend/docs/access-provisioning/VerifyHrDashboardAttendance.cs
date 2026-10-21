#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyHrDashboardAttendance.packages.lock.json
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Reports;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Dashboard;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;

// Offline synthetic inputs only: the production population rules, operation, day reader and aggregate execute.
var checks = 0;
void Check(bool result, string name) { if (!result) throw new Exception(name); checks++; }
async Task Reject(Func<Task> action, string code)
{
    try { await action(); throw new Exception("Expected " + code); }
    catch (AuthenticationFailure error) { Check(error.Code == code, code); }
}
var clock = new Clock();
var date = RosterSchedule.Today(clock.GetUtcNow());
var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
var identity = new AuthenticatedAccount(Guid.NewGuid(), ids[0], Guid.NewGuid(), 1, SessionClient.Web, true);
var current = new Current { Identity = identity };
var access = new Access();
access.Grants.Add(new(AttendanceReportPermissions.Read, false, Guid.NewGuid(), AccessScopeKind.All, null));
var journal = DispatchProxy.Create<ISchedulingJournalRepository, Journal>();
var transactions = DispatchProxy.Create<IAccessManagementRepository, Transactions>();
var files = new Files();
var operation = new AttendanceOperation(current, access, transactions, journal, null!, clock, files);
var population = new Population { Ids = ids };
var repository = new Summaries();
var policy = new Policies { PresentEmployee = ids[0] };
var rosters = new Rosters();
var summaries = new AttendanceSummaryService(operation, repository, rosters, policy, files);
var service = new HrDashboardAttendanceService(operation, population, summaries);
var context = new AuthRequestContext("127.0.0.1", "synthetic-hr-dashboard");
var start = clock.GetUtcNow().AddHours(-2);
repository.Sessions.Add(new(Guid.NewGuid(), 1, ids[0], date, start, start.AddHours(1), Guid.NewGuid(), null,
    clock.GetUtcNow(), null, correctionId: Guid.NewGuid()));
repository.Sessions.Add(new(Guid.NewGuid(), 1, ids[1], date, start, null, Guid.NewGuid(), null,
    clock.GetUtcNow(), null, correctionId: Guid.NewGuid()));
var result = await service.SummaryAsync(context, default);
Check(result.ActiveEmployees == 4 && result.WithAcceptedSessionEvidence == 2, "one employee per accepted duty evidence");
Check(result.OpenSessionEmployees == 1 && result.ClosedSessionOnlyEmployees == 1 && result.NoAcceptedSessionEvidenceEmployees == 2,
    "open closed and no-evidence buckets partition the population");
Check(result.ClassifiedEmployees == 1 && result.NotYetClassifiedEmployees == 3 && result.ClassifiedStatusCounts["Present"] == 1,
    "only explicit completed policy output classifies a day");
Check(result.ClassifiedStatusCounts["Absent"] == 0, "missing accepted evidence never becomes absence");
Check(result.TimeZone == "Asia/Karachi" && result.BusinessDate == new DateOnly(2026, 10, 20) &&
    result.GeneratedAtUtc == clock.GetUtcNow(), "request instant establishes Karachi date and UTC generation");
Check(population.Now == clock.GetUtcNow() && population.Date == result.BusinessDate, "same instant passed to population");
Check(result.CurrentSavedSummaryEmployees == 0 && result.UnsavedOrStaleSummaryEmployees == 4, "live evidence does not require saved summaries");
Check(((Transactions)(object)transactions).Calls == 1, "one authorization transaction with trusted day reads");
Check(((Journal)(object)journal).Audits == 1, "aggregate read is audited");
rosters.FailureCode = "employee_assignment_unavailable";
result = await service.SummaryAsync(context, default);
Check(result.ActiveEmployees == 4 && result.WithAcceptedSessionEvidence == 2 && result.OpenSessionEmployees == 1 &&
    result.NoAcceptedSessionEvidenceEmployees == 2, "missing assignment preserves accepted open and closed factual evidence");
Check(result.ClassifiedEmployees == 0 && result.NotYetClassifiedEmployees == 4 && result.ClassifiedStatusCounts["Absent"] == 0,
    "missing assignment leaves policy classification undetermined");
await Reject(() => summaries.LoadAuthorizedDayAsync(ids[0], date, default), "employee_assignment_unavailable");
population.Ids = Enumerable.Range(0, 12).Select(_ => Guid.NewGuid()).ToArray();
result = await service.SummaryAsync(context, default);
Check(result.ActiveEmployees == 12 && result.WithAcceptedSessionEvidence == 0 && result.NoAcceptedSessionEvidenceEmployees == 12 &&
    result.NotYetClassifiedEmployees == 12, "unassigned population without accepted sessions reports factual zero of twelve");
population.Ids = ids;
repository.Locked = true;
await Reject(() => service.SummaryAsync(context, default), "locked_attendance_summary_unavailable");
repository.Locked = false;
rosters.FailureCode = "employee_assignment_history_conflict";
await Reject(() => service.SummaryAsync(context, default), "employee_assignment_history_conflict");
rosters.FailureCode = null;
var day = await summaries.LoadAuthorizedDayAsync(ids[0], date, default);
var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { day.Facts, day.Roster, day.Policy, day.ExceptionCount })));
repository.Saved[ids[0]] = new(ids[0], date, 1, AttendanceDayStatus.Present, policy.PolicyId, null, "synthetic-policy", fingerprint,
    0, day.Facts.WorkedMinutes, 0, 0, 0, 0, clock.GetUtcNow());
Check((await service.SummaryAsync(context, default)).CurrentSavedSummaryEmployees == 1, "matching saved summary is counted");
repository.Sessions.Add(new(Guid.NewGuid(), 1, ids[0], date, start.AddMinutes(75), start.AddMinutes(90), Guid.NewGuid(), null,
    clock.GetUtcNow(), null, correctionId: Guid.NewGuid()));
Check((await service.SummaryAsync(context, default)).CurrentSavedSummaryEmployees == 0, "new live evidence makes saved summary stale");
population.Ids = [];
result = await service.SummaryAsync(context, default);
Check(result.ActiveEmployees == 0 && result.NoAcceptedSessionEvidenceEmployees == 0 && result.ClassifiedEmployees == 0,
    "verified empty population yields genuine zero");
population.Ids = [ids[0], ids[0]];
await Reject(() => service.SummaryAsync(context, default), "hr_dashboard_population_conflict");
population.Ids = ids;
repository.Unavailable = ids[1];
await Reject(() => service.SummaryAsync(context, default), "synthetic_evidence_unavailable");
repository.Unavailable = null;
access.Grants.Clear();
access.Grants.Add(new(AttendanceReportPermissions.Read, false, Guid.NewGuid(), AccessScopeKind.Employee, ids[0]));
var beforeDenied = population.Reads;
await Reject(() => service.SummaryAsync(context, default), "access_denied");
Check(population.Reads == beforeDenied, "employee-only permission denied before population discovery");
access.Grants.Clear();
access.Grants.Add(new(AttendanceReportPermissions.Read, false, Guid.NewGuid(), AccessScopeKind.All, null));
current.Identity = identity with { MfaSatisfied = false };
await Reject(() => service.SummaryAsync(context, default), "access_denied");
Check(population.Reads == beforeDenied, "missing MFA denied before population discovery");
current.Identity = identity with { Client = SessionClient.Mobile };
await Reject(() => service.SummaryAsync(context, default), "access_denied");
Check(population.Reads == beforeDenied, "mobile session denied before population discovery");
current.Identity = identity;
((Journal)(object)journal).Valid = false;
await Reject(() => service.SummaryAsync(context, default), "invalid_authentication");
((Journal)(object)journal).Valid = true;
using (var canceled = new CancellationTokenSource())
{
    canceled.Cancel();
    try { await service.SummaryAsync(context, canceled.Token); throw new Exception("Expected cancellation"); }
    catch (OperationCanceledException) { checks++; }
}

HrDashboardEmploymentEvidence Evidence(Guid employee, EmployeeStatus cached = EmployeeStatus.Active,
    bool approved = true, Guid? marker = null, params HrDashboardStatusEvidence[] changes) =>
    new(employee, new DateOnly(2026, 10, 1), cached, OnboardingStatus.Approved, false, approved, marker, changes);
var active = Evidence(ids[0]);
var suspended = new EmployeeStatusChange(ids[1], EmployeeStatus.Active, EmployeeStatus.Suspended,
    start, "synthetic", Guid.NewGuid().ToString(), start, Guid.NewGuid());
var released = new EmployeeStatusChange(ids[1], EmployeeStatus.Suspended, EmployeeStatus.Active,
    start.AddMinutes(30), "synthetic", Guid.NewGuid().ToString(), start.AddMinutes(30), Guid.NewGuid(), "synthetic-approved-policy");
var suspendedEvidence = Evidence(ids[1], changes: [new(suspended, true)]);
var releasedEvidence = Evidence(ids[1], marker: released.Id, changes: [new(suspended, true), new(released, true)]);
var selected = HrDashboardPopulationRules.ActiveEmployeeIds(clock.GetUtcNow(), date, [active, suspendedEvidence]);
Check(selected.SequenceEqual([ids[0]]), "effective suspension excludes stale cached Active");
Check(HrDashboardPopulationRules.ActiveEmployeeIds(clock.GetUtcNow(), date, [releasedEvidence]).SequenceEqual([ids[1]]),
    "verified reconciled release restores the denominator");
Check(HrDashboardPopulationRules.ActiveEmployeeIds(clock.GetUtcNow(), date,
    [active with { IsArchived = true }, active with { EmployeeId = ids[1], JoinedOn = date.AddDays(1) }]).Count == 0,
    "archived and future join dates excluded");
await Reject(() => Task.FromResult(HrDashboardPopulationRules.ActiveEmployeeIds(clock.GetUtcNow(), date,
    [active with { ApprovedEmployment = false }])), "hr_dashboard_employment_provenance_conflict");
await Reject(() => Task.FromResult(HrDashboardPopulationRules.ActiveEmployeeIds(clock.GetUtcNow(), date,
    [releasedEvidence with { LastAppliedStatusChangeId = suspended.Id }])), "hr_dashboard_lifecycle_reconciliation_required");
await Reject(() => Task.FromResult(HrDashboardPopulationRules.ActiveEmployeeIds(clock.GetUtcNow(), date,
    [suspendedEvidence with { Statuses = [new(suspended, false)] }])), "hr_dashboard_lifecycle_provenance_conflict");
var duplicateTime = new EmployeeStatusChange(ids[1], EmployeeStatus.Suspended, EmployeeStatus.Active,
    start, "synthetic", Guid.NewGuid().ToString(), start, Guid.NewGuid(), "synthetic-approved-policy");
await Reject(() => Task.FromResult(HrDashboardPopulationRules.ActiveEmployeeIds(clock.GetUtcNow(), date,
    [Evidence(ids[1], changes: [new(suspended, true), new(duplicateTime, true)])])), "hr_dashboard_lifecycle_provenance_conflict");
Check(RosterSchedule.Today(new(2026, 10, 19, 18, 59, 59, TimeSpan.Zero)) == new DateOnly(2026, 10, 19) &&
    RosterSchedule.Today(new(2026, 10, 19, 19, 0, 0, TimeSpan.Zero)) == new DateOnly(2026, 10, 20), "Karachi midnight boundary");
Console.WriteLine($"HR dashboard attendance: {checks} offline checks passed. No SQL, storage provider or HTTP acceptance exercised.");

public sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 19, 19, 30, 0, TimeSpan.Zero); }
public sealed class Current : ICurrentAuthentication { public required AuthenticatedAccount Identity; public AuthenticatedAccount Require() => Identity; }
public sealed class Access : IAccountAccessReader
{
    public List<PermissionGrant> Grants = [];
    public Task<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid id, DateTimeOffset now, CancellationToken ct) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray());
}
public sealed class Population : IHrDashboardPopulationRepository
{
    public IReadOnlyList<Guid> Ids = []; public DateTimeOffset Now; public DateOnly Date; public int Reads;
    public Task<IReadOnlyList<Guid>> ActiveEmployeeIdsAsync(DateTimeOffset now, DateOnly date, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); Reads++; Now = now; Date = date; return Task.FromResult(Ids); }
}
public sealed class Summaries : IAttendanceSummaryRepository
{
    public List<WorkSession> Sessions = []; public Dictionary<Guid, DailyAttendanceSummary> Saved = []; public Guid? Unavailable; public bool Locked;
    public Task<IReadOnlyList<AccessResource>> ResourcesAsync(Guid id, DateOnly from, DateOnly to, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<WorkSession>> SessionsAsync(Guid id, DateOnly date, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); if (id == Unavailable) throw AttendanceOperation.Conflict("synthetic_evidence_unavailable"); return Task.FromResult<IReadOnlyList<WorkSession>>(Sessions.Where(x => x.EmployeeId == id && x.AttendanceDate == date).ToArray()); }
    public Task<int> ExceptionCountAsync(Guid id, DateOnly date, CancellationToken ct) => Task.FromResult(0);
    public Task<bool> IsLockedAsync(DateOnly date, CancellationToken ct) => Task.FromResult(Locked);
    public Task<DailyAttendanceSummary?> LatestAsync(Guid id, DateOnly date, CancellationToken ct) => Task.FromResult(Saved.GetValueOrDefault(id));
    public Task<PagedResult<DailyAttendanceSummary>> HistoryAsync(Guid id, DateOnly date, int page, int size, CancellationToken ct) => throw new NotSupportedException();
    public void Add(DailyAttendanceSummary summary, IReadOnlyList<Cosmix.Domain.Attendance.DailySummaries.AttendanceSummarySession> sessions) => throw new NotSupportedException();
}
public sealed class Rosters : IIndividualRosterResolver
{
    public string? FailureCode;
    public Task<EffectiveRosterResponse> ResolveAsync(Guid id, DateOnly date, CancellationToken ct) =>
        FailureCode is { } code ? throw AttendanceOperation.Conflict(code) : Task.FromResult(new EffectiveRosterResponse(id, date, "unassigned", RosterSchedule.TimeZoneId));
}
public sealed class Policies : IAttendancePolicyCalculator
{
    public Guid PresentEmployee; public Guid PolicyId = Guid.NewGuid();
    public Task<AttendancePolicyResult> CalculateAsync(Guid id, DateOnly date, FactualAttendance facts, EffectiveRosterResponse roster, CancellationToken ct) =>
        Task.FromResult(id == PresentEmployee ? new AttendancePolicyResult("calculated", PolicyId, "synthetic-policy", 0, 0, 0, AttendanceDayStatus.Present, []) :
            new AttendancePolicyResult("policy_configuration_required", null, null, null, null, null, null, []));
}
public sealed class Files : IPrivateFileStore
{
    public Task<PrivateFileReference> WriteAsync(byte[] bytes, string purpose, CancellationToken ct) => throw new NotSupportedException();
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct) => throw new NotSupportedException();
}
public class Journal : DispatchProxy
{
    public bool Valid = true; public int Audits;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
    { "IsCurrentSessionAsync" => Task.FromResult(Valid), "AddAudit" => Count(), "SaveChangesAsync" => Task.CompletedTask, _ => throw new NotSupportedException(method?.Name) };
    private object? Count() { Audits++; return null; }
}
public class Transactions : DispatchProxy
{
    public int Calls;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name == "InTransactionAsync") { Calls++; return ((Delegate)args![0]!).DynamicInvoke(args[1]); }
        if (method?.Name == "GetAssignmentRoleCodeAsync") return Task.FromResult<string?>("synthetic-hr");
        throw new NotSupportedException(method?.Name);
    }
}
