#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyCapturePersistenceModel.packages.lock.json
using System.Reflection;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;

// EF metadata/tracked-write guards only. No connection is opened and no SQL is executed.
var checks = 0;
void Check(bool valid, string name) { checks++; if (!valid) throw new Exception(name); }
var options = new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer("Server=not-used.invalid;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true").Options;
CosmixDbContext Context() => new(options);
void Validate(CosmixDbContext context)
{
    try { typeof(CosmixDbContext).GetMethod("ValidateTrackedWrites", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(context, []); }
    catch (TargetInvocationException e) when (e.InnerException is not null) { throw e.InnerException; }
}
void Deny(Action action, string name)
{
    checks++;
    try { action(); } catch (InvalidOperationException) { return; }
    throw new Exception(name);
}
void Set(object entity, string property, object? value) => entity.GetType().GetProperty(property)!.SetValue(entity, value);
var now = new DateTimeOffset(2026, 10, 9, 4, 0, 0, TimeSpan.Zero);
var account = (UserAccount)Activator.CreateInstance(typeof(UserAccount), true)!;
Set(account, "Id", Guid.NewGuid()); Set(account, "EmployeeId", Guid.NewGuid()); Set(account, "Status", AccountStatus.Active);
var device = new RegisteredDevice(account.Id, "synthetic-hash", "android", "Synthetic", now.AddDays(-1)); device.MarkVerified(now.AddDays(-1));
FaceEnrollment Enrollment() => new(account.EmployeeId, "protected-template", "key-ref", "synthetic", "1", "actor", device.Id.ToString(), "Synthetic", now.AddHours(-1), "synthetic");
FaceVerificationAttempt Attempt(FaceEnrollment enrollment) => new(enrollment, account, device, "punch-key-001", FaceService.Digest("synthetic-secret"), "synthetic", "1", now, now.AddMinutes(2), "synthetic");
RawPunchEvent Raw() => new(account.EmployeeId, PunchAction.CheckIn, device.Id.ToString("D"), "punch-key-001", new string('A', 64), now.AddSeconds(20), false, businessDate: new(2026, 10, 9));
using (var db = Context())
{
    Check(db.Model.FindEntityType(typeof(WorkSession))!.FindProperty("PolicyVersionId")!.IsNullable, "factual sessions have optional policy");
    Check(db.Model.FindEntityType(typeof(PunchAssessment))!.FindProperty("PolicyVersionId")!.IsNullable, "factual assessment has optional policy");
    var model = db.Model.FindEntityType(typeof(RawPunchEvent))!;
    foreach (var property in new[] { "BusinessDate", "SiteRosterAssignmentId", "TemporaryShiftChangeId", "HolidayCalendarId" })
        Check(model.FindProperty(property) is { IsNullable: true }, "legacy-compatible capture pin " + property);
    foreach (var property in new[] { "SiteRosterAssignmentId", "TemporaryShiftChangeId", "HolidayCalendarId" })
        Check(model.GetForeignKeys().Any(x => x.Properties.Any(p => p.Name == property) && x.DeleteBehavior == DeleteBehavior.Restrict), "pinned history restrict FK " + property);
    var repository = new AttendanceCaptureRepository(db);
    var query = (IQueryable<RawPunchEvent>)typeof(AttendanceCaptureRepository).GetMethod("HistoryQuery", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(repository, [account.EmployeeId, new AttendanceHistoryRequest(new(2026, 10, 9), new(2026, 10, 10))])!;
    var sql = query.ToQueryString();
    Check(sql.Contains("[BusinessDate] IS NULL", StringComparison.Ordinal) && sql.Contains("[ReceivedAtUtc] >=", StringComparison.Ordinal), "legacy date fallback translated into scoped SQL");
    Check(sql.Contains("2026-10-08T19:00:00", StringComparison.Ordinal) && sql.Contains("2026-10-09T19:00:00", StringComparison.Ordinal), "legacy received-date bounds use Pakistan midnight");
    var faceHistory = new FaceEnrollmentHistoryRepository(db);
    IQueryable<Cosmix.Domain.Common.Audit.AuditEvent> HistoryQuery(AttendanceCursorPosition? position) =>
        (IQueryable<Cosmix.Domain.Common.Audit.AuditEvent>)typeof(FaceEnrollmentHistoryRepository)
            .GetMethod("CandidateQuery", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(faceHistory, [account.EmployeeId, now.AddDays(-1), now.AddDays(1), position, 101])!;
    var firstPageSql = HistoryQuery(null).ToQueryString();
    Check(firstPageSql.Contains("[EmployeeScopeId] =", StringComparison.Ordinal) && firstPageSql.Contains("attendance.face.enrollment.started", StringComparison.Ordinal),
        "enrollment history filters employee and immutable start event in SQL");
    Check(firstPageSql.Contains("TOP(", StringComparison.Ordinal) && firstPageSql.Contains("ORDER BY", StringComparison.Ordinal) &&
        firstPageSql.Contains("[OccurredAt] DESC", StringComparison.Ordinal) && firstPageSql.Contains("[Id] DESC", StringComparison.Ordinal),
        "enrollment history bounded deterministic ordering stays server-side");
    Check(firstPageSql.Contains("[OccurredAt] >=", StringComparison.Ordinal) && firstPageSql.Contains("[OccurredAt] <", StringComparison.Ordinal),
        "enrollment history inclusive/exclusive UTC date bounds translated");
    var continuationSql = HistoryQuery(new(now, Guid.NewGuid())).ToQueryString();
    Check(continuationSql.Contains("[Id] <", StringComparison.Ordinal) && continuationSql.Contains("[OccurredAt] =", StringComparison.Ordinal),
        "timestamp and SQL GUID tie-break continuation translates without client filtering");
    Check(!continuationSql.Contains("COUNT(", StringComparison.Ordinal), "restricted enrollment candidates do not produce a total-count query");
}
using (var db = Context())
{
    var row = Enrollment(); db.Attach(row); row.Revoke("actor", "Synthetic revoke", now); Validate(db); checks++;
}
using (var db = Context())
{
    var row = Enrollment(); db.Attach(row); Set(row, "EncryptedTemplateReference", "different-reference");
    Deny(() => Validate(db), "template history cannot be changed");
}
using (var db = Context())
{
    var row = Enrollment(); db.Attach(row); db.Remove(row);
    Deny(() => Validate(db), "enrollment cannot be deleted");
}
using (var db = Context())
{
    var row = Attempt(Enrollment()); db.Attach(row); row.RecordProviderResult(true, true, true, "VERIFIED", now.AddSeconds(5));
    Validate(db); checks++;
}
using (var db = Context())
{
    var row = Attempt(Enrollment()); db.Attach(row); row.Expire(now.AddMinutes(2)); Validate(db); checks++;
}
using (var db = Context())
{
    var row = Attempt(Enrollment()); db.Attach(row); Set(row, "PunchIdempotencyKey", "different-key");
    Deny(() => Validate(db), "attempt punch binding immutable");
}
using (var db = Context())
{
    var row = Attempt(Enrollment()); row.RecordProviderResult(false, false, false, "UNAVAILABLE", now.AddSeconds(5)); db.Attach(row);
    Set(row, "Outcome", FaceVerificationOutcome.Passed); Set(row, "LivenessPassed", true); Set(row, "MatchPassed", true);
    Deny(() => Validate(db), "terminal failure cannot become success");
}
using (var db = Context())
{
    var enrollment = Enrollment(); var row = Attempt(enrollment); row.RecordProviderResult(true, true, true, "VERIFIED", now.AddSeconds(5)); db.Attach(row);
    var raw = Raw(); row.Consume(raw, enrollment, account, device, now.AddSeconds(20));
    Deny(() => Validate(db), "consumption without new raw evidence is denied");
    db.RawPunchEvents.Add(raw); Validate(db); checks++;
}
using (var db = Context())
{
    var row = Raw(); db.Attach(row); Set(row, "ReceivedAtUtc", now.AddHours(1));
    Deny(() => Validate(db), "raw evidence time immutable");
}
using (var db = Context())
{
    var raw = Raw(); var row = new WorkSession(Guid.NewGuid(), 1, account.EmployeeId, new(2026, 10, 9), raw.ReceivedAtUtc, null,
        Guid.NewGuid(), null, now.AddSeconds(20), raw); db.Attach(row); Set(row, "StartsAtUtc", now);
    Deny(() => Validate(db), "session revision immutable");
}
void ValidateAuditOnly(FaceEnrollmentJournalRepository repository)
{
    try { typeof(FaceEnrollmentJournalRepository).GetMethod("ValidateAuditOnly", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(repository, []); }
    catch (TargetInvocationException e) when (e.InnerException is not null) { throw e.InnerException; }
}
using (var db = Context())
{
    var repository = new FaceEnrollmentJournalRepository(db, null!);
    db.FaceEnrollments.Add(Enrollment());
    Deny(() => ValidateAuditOnly(repository), "audit-only finalizer cannot add a business enrollment before save");
}
using (var db = Context())
{
    var repository = new FaceEnrollmentJournalRepository(db, null!);
    var row = Enrollment(); db.Attach(row); row.Revoke("actor", "Synthetic", now);
    Deny(() => ValidateAuditOnly(repository), "audit-only finalizer cannot modify an enrollment before save");
}
using (var db = Context())
{
    var repository = new FaceEnrollmentJournalRepository(db, null!);
    db.IdempotencyReceipts.Add(new("actor", FaceEnrollmentJournal.TerminalOperation, "synthetic-terminal", new string('A', 64), now, now.AddHours(24)));
    ValidateAuditOnly(repository); checks++;
}
Console.WriteLine($"Capture EF metadata/tracked-guard checks passed: {checks}; no SQL connection opened.");
