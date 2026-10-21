#:project ../../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using Cosmix.Application.Attendance.Reminders;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.HR.Employees;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Actual SQL Server query translation only: interceptors forbid every connection attempt.
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=not-used.invalid;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true")
    .AddInterceptors(guard).Options);
var repository = new MissingCheckoutReminderRepository(db);
IQueryable<T> Query<T>(string name, params object?[] args) =>
    (IQueryable<T>)typeof(MissingCheckoutReminderRepository).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(repository, args)!;
var employee = Guid.NewGuid(); var date = new DateOnly(2026, 10, 12);
var now = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
var version = new ShiftVersion(Guid.NewGuid(), 1, date, new(9, 0), new(18, 0), 0, 540, 0, 0, ShiftWeekdays.All, 30);
var session = new WorkSession(Guid.NewGuid(), 1, employee, date, now.AddHours(-8), null, version.Id, null,
    now, null, correctionId: Guid.NewGuid(), siteId: Guid.NewGuid());
var candidates = Query<MissingCheckoutReminderCandidate>("CandidateQuery", 15L, now).Take(25).ToQueryString();
Check(candidates.Contains("TOP(") && candidates.Contains("25"), "candidate source remains bounded");
Check(candidates.Contains("[EmployeeNumber] >") && candidates.Contains("ORDER BY"), "stable employee-number sweep keyset");
Check(candidates.Contains("[MissingCheckoutReminderAfterMinutes] IS NOT NULL"), "unconfigured legacy versions skipped in SQL");
Check(candidates.Contains("[ShiftVersionId]") && candidates.Contains("ShiftVersions"), "candidate joins exact retained shift version");
Check(candidates.Contains("NOT EXISTS") && candidates.Contains("[SessionKey]") && candidates.Contains("[Revision] >"), "only latest duty revisions enter candidate scan");
Check(!candidates.Contains("COUNT("), "candidate query does not compute hidden totals");
var open = Query<WorkSession>("LatestOpenQuery", now).Where(s => s.EmployeeId == employee).Take(2).ToQueryString();
Check(open.Contains("[EndsAtUtc] IS NULL") && open.Contains("[StartsAtUtc] <="), "only already-started open duty");
Check(open.Contains("[EmployeeId] =") && open.Contains("TOP("), "per-employee cardinality bounded for conflict detection");
var accepted = Query<RawPunchEvent>("AcceptedOpeningQuery", employee, Guid.NewGuid(), session).ToQueryString();
Check(accepted.Contains("[Action] =") && accepted.Contains("[EmployeeId] =") && accepted.Contains("[Id] ="), "accepted source pins employee and Check In identity");
Check(accepted.Contains("[SiteId] =") && accepted.Contains("[ShiftVersionId] =") && accepted.Contains("[BusinessDate] ="), "original site, date, and version retained through transfers");
Check(accepted.Contains("[ReceivedAtUtc] ="), "manual start-time substitution cannot become accepted-device source");
Check(accepted.Contains("PunchAssessments") && accepted.Contains("[Outcome]") && accepted.Contains("NOT EXISTS") && accepted.Contains("[Revision] >"), "only current accepted assessment supports reminder");
var officeSession = new WorkSession(Guid.NewGuid(),1,employee,date,now.AddHours(-8),null,version.Id,null,now,null,
    correctionId:Guid.NewGuid(),branchId:Guid.NewGuid(),officeRosterAssignmentId:Guid.NewGuid(),
    employeeAssignmentId:Guid.NewGuid(),officeGeofenceVersionId:Guid.NewGuid(),holidayCalendarId:Guid.NewGuid());
var officeOpening=Query<RawPunchEvent>("AcceptedOpeningQuery",employee,Guid.NewGuid(),officeSession).ToQueryString();
foreach(var pin in new[] { "BranchId","OfficeRosterAssignmentId","EmployeeAssignmentId","OfficeGeofenceVersionId","HolidayCalendarId" })
    Check(officeOpening.Contains("["+pin+"] ="),"office reminder original source predicate: " + pin);
var account = Query<Guid>("CurrentAccountsQuery", employee, date, now).Take(2).ToQueryString();
Check(account.Contains("[EmployeeId] =") && account.Contains("TOP("), "recipient is current employee account with conflict bound");
Check(account.Contains("[MobileAccessEnabled]") && account.Contains("[WebAccessEnabled]") && account.Contains("[Status] ="), "disabled accounts cannot receive reminder");
Check(account.Contains("[LockedUntil] IS NULL") && account.Contains("[LockedUntil] <="), "currently locked accounts excluded");
Check(account.Contains("[JoinedOn]") && account.Contains("[OnboardingStatus]") && account.Contains("[ApprovedOnboardingDecisionId]") && account.Contains("[ApprovalDecisionId]"), "approved employment provenance required");
var statuses = Query<EmployeeStatusChange>("EffectiveStatusesQuery", employee, now).Take(2).ToQueryString();
Check(statuses.Contains("[EmployeeId] =") && statuses.Contains("[EffectiveAt] <=") && statuses.Contains("ORDER BY") && statuses.Contains("DESC"), "effective status history ignores future records and selects latest");
Check(statuses.Contains("TOP("), "duplicate effective status detection is bounded");
var hint = Query<bool>("ExistingIdentityQuery", session.SessionKey).ToQueryString();
Check(hint.Contains("READUNCOMMITTED") && hint.Contains("EXISTS") && hint.Contains("AS [Value]"), "deduplication probe reads existence only without reversing dispatch row locks");
Check(hint.Contains("@p0") && hint.Contains("@p1") && hint.Contains("Latin1_General_100_BIN2"), "deduplication identity is parameterized and exact-case with sentinel");
Check(hint.Contains("[MessageType] =") && hint.Contains("[DeduplicationKey] ="), "deduplication probe also exposes unique-index equality predicates");
Check(!hint.Contains("EncryptedPayloadReference") && !hint.Contains("Recipient") && !hint.Contains("[State]"), "hint cannot provide payload, recipient, authorization or delivery status");
Check(db.ChangeTracker.Entries().Count() == 0 && guard.Attempts == 0, "SQL translation stages no changes and attempts no connection");
Console.WriteLine($"Missing checkout reminders: {checks} SQL translation checks passed. No connection, SQL execution, or database writes.");

sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Attempts++; throw new Exception("Database access forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data,
        InterceptionResult result, CancellationToken cancellationToken = default)
    { Attempts++; throw new Exception("Database access forbidden"); }
}
