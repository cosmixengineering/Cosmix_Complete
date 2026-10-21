#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Punches.CaptureValidation;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Common;
using Cosmix.Infrastructure.Repositories.Attendance;
using System.Text.Json;

var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=b02-verification.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
var domain = typeof(Cosmix.Domain.Attendance.Punches.RawPunchEvent).Assembly;
var policyType = domain.GetType("Cosmix.Domain.Attendance.Punches.CaptureValidationPolicyVersion");
var evidenceType = domain.GetType("Cosmix.Domain.Attendance.Punches.CaptureValidationFinalEvidence");
if (policyType is null || evidenceType is null || db.Model.FindEntityType(policyType) is null || db.Model.FindEntityType(evidenceType) is null)
    throw new Exception("B02 approved policy and explicit final evidence must have registered append-only EF mappings after the freeze.");
var infrastructure = typeof(CosmixDbContext).Assembly;
var policyRepository = infrastructure.GetType("Cosmix.Infrastructure.Repositories.Attendance.CaptureValidationPolicyRepository");
var evidenceRepository = infrastructure.GetType("Cosmix.Infrastructure.Repositories.Attendance.CaptureValidationEvidenceRepository");
if (policyRepository?.GetMethod("TimelineQuery", BindingFlags.NonPublic | BindingFlags.Instance) is null ||
    evidenceRepository?.GetMethod("RecentQuery", BindingFlags.NonPublic | BindingFlags.Instance) is null ||
    evidenceRepository.GetMethod("PunchQuery", BindingFlags.NonPublic | BindingFlags.Instance) is null)
    throw new Exception("B02 repositories require bounded SQL-translatable company timeline and employee/raw scoped evidence queries.");
var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
var policyRepo = new CaptureValidationPolicyRepository(db);
var evidenceRepo = new CaptureValidationEvidenceRepository(db);
string Sql(object repo, string name, params object?[] args) => ((IQueryable)repo.GetType()
    .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(repo, args)!).ToQueryString();
void Refuse(Action action, string name)
{
    try { action(); }
    catch (TargetInvocationException e) when (e.InnerException is AuthenticationFailure or ArgumentException or InvalidOperationException) { checks++; return; }
    catch (Exception e) when (e is AuthenticationFailure or ArgumentException or InvalidOperationException) { checks++; return; }
    throw new Exception(name);
}
var employee = Guid.NewGuid(); var punchId = Guid.NewGuid();
var now = new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
var timeline = Sql(policyRepo, "TimelineQuery");
Check(timeline.Contains("TOP(") && timeline.Contains("513") && timeline.Contains("ORDER BY") && timeline.Contains("[Revision]"),
    "Global timeline is deterministically ordered and bounded with an overflow sentinel");
var recent = Sql(evidenceRepo, "RecentQuery", employee, now.AddSeconds(-120), now);
Check(recent.Contains(employee.ToString()) && recent.Contains("[EmployeeId] =") && recent.Contains("[ReceivedAtUtc] >=") && recent.Contains("[ReceivedAtUtc] <="),
    "Recent raw query applies employee and inclusive server receipt range predicates");
Check(recent.Contains("TOP(") && recent.Contains("513") && recent.Contains("[ReceivedAtUtc] DESC") && recent.Contains("[Id]"),
    "Recent query has bounded deterministic server chronology ordering");
Check(recent.Contains("PunchAssessments") && recent.Contains("[Revision] DESC") && recent.Contains("[RawPunchEventId]") &&
    recent.Split("[EmployeeId]", StringSplitOptions.None).Length >= 4,
    "Each latest assessment is constrained to both raw and employee without loading full assessment history");
Check(!recent.Contains("[Action] =") && !recent.Contains("[Action] IN"), "Intervening accepted opposite actions are retained for legitimate break cycles");
var opening = Sql(evidenceRepo, "PunchQuery", employee, punchId);
Check(opening.Contains(employee.ToString()) && opening.Contains(punchId.ToString()) && opening.Contains("[Id] =") &&
    opening.Contains("TOP(") && opening.Contains("2"), "Exact opening lookup pins employee/raw ID and bounds ambiguity");
var finalQuery = Sql(evidenceRepo, "FinalQuery", employee, punchId);
Check(finalQuery.Contains(employee.ToString()) && finalQuery.Contains(punchId.ToString()) && finalQuery.Contains("TOP(") && finalQuery.Contains("2"),
    "Final evidence lookup is employee/raw scoped and bounded");
Refuse(() => Sql(evidenceRepo, "RecentQuery", Guid.Empty, now.AddMinutes(-1), now), "Unscoped recent query must fail before SQL");
Refuse(() => Sql(evidenceRepo, "RecentQuery", employee, now, now.AddTicks(-1)), "Reversed range must fail before SQL");
Refuse(() => Sql(evidenceRepo, "RecentQuery", employee, now.ToOffset(TimeSpan.FromHours(5)), now), "Non-UTC range must fail before SQL");
Refuse(() => Sql(evidenceRepo, "PunchQuery", employee, Guid.Empty), "Missing raw reference must fail before SQL");
Refuse(() => Sql(evidenceRepo, "FinalQuery", Guid.Empty, punchId), "Unscoped final lookup must fail before SQL");
var model = db.GetService<IDesignTimeModel>().Model;
var policyModel = model.FindEntityType(policyType)!; var evidenceModel = model.FindEntityType(evidenceType)!;
Check(typeof(IAppendOnlyEntity).IsAssignableFrom(policyType) && typeof(IAppendOnlyEntity).IsAssignableFrom(evidenceType), "Both models use actual append-only save protection");
Check(policyModel.GetIndexes().Any(x => x.IsUnique && x.Properties.SingleOrDefault()?.Name == "Revision"), "One unique global policy revision timeline");
Check(evidenceModel.GetIndexes().Any(x => x.IsUnique && x.Properties.SingleOrDefault()?.Name == "RawPunchEventId"), "Only one final artifact per raw event");
Check(evidenceModel.GetForeignKeys().Any(x => x.PrincipalEntityType.ClrType == typeof(RawPunchEvent) &&
    x.Properties.Select(p => p.Name).SequenceEqual(new[] { "EmployeeId", "RawPunchEventId" })), "Final artifact raw relation binds employee, not raw ID alone");
Check(evidenceModel.GetForeignKeys().Any(x => x.PrincipalEntityType.ClrType == policyType &&
    x.Properties.Select(p => p.Name).SequenceEqual(new[] { "PolicyVersionId", "PolicyRevision", "SourceFingerprint" })), "Final policy FK retains exact version/revision/source hash");
Check(evidenceModel.GetForeignKeys().All(x => x.DeleteBehavior == DeleteBehavior.Restrict) &&
    evidenceModel.GetForeignKeys().All(x => x.PrincipalEntityType.ClrType != typeof(Cosmix.Domain.HR.Policies.PolicyVersion)),
    "Restrict deletion; capture policy has no HR financial-policy relation");
Check(evidenceModel.GetCheckConstraints().Any(x => x.Sql.Contains("[EvaluatedAtUtc] <= [AssessedAtUtc]")) &&
    evidenceModel.GetCheckConstraints().Any(x => x.Sql.Contains("[IsOffline]")), "SQL model protects final chronology and offline Unverified");
Check(evidenceModel.GetCheckConstraints().Any(x => x.Sql.Contains("[PolicyRevision] IS NOT NULL") && x.Sql.Contains("[SourceFingerprint] IS NOT NULL")),
    "Nullable composite policy FK cannot bypass the all-or-none source constraint through SQL UNKNOWN");
var validate = evidenceRepo.GetType().GetMethod("ValidateRows", BindingFlags.NonPublic | BindingFlags.Static)!;
RawPunchEvent Raw(Guid who) => new(who, PunchAction.CheckOut, Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("N"), new string('A', 64), now, false);
void Rows(IReadOnlyList<RecentCaptureEvidence> rows) => validate.Invoke(null, [rows, employee, now.AddMinutes(-1), now]);
var raw = Raw(employee);
Rows([new(raw, null)]); Check(true, "Actual repository source validator accepts a correctly scoped immutable raw");
Refuse(() => Rows([new(Raw(Guid.NewGuid()), null)]), "Wrong employee must fail immediately");
typeof(RawPunchEvent).GetProperty("Action")!.SetValue(raw, (PunchAction)999);
Refuse(() => Rows([new(raw, null)]), "Wrong action must fail immediately");
typeof(RawPunchEvent).GetProperty("Action")!.SetValue(raw, PunchAction.CheckOut);
var assessment = new PunchAssessment(raw, 1, PunchAssessmentOutcome.Rejected, "SYNTHETIC_REJECTION", null, "synthetic-b02", now);
typeof(PunchAssessment).GetProperty("RawPunchEventId")!.SetValue(assessment, Guid.NewGuid());
Refuse(() => Rows([new(raw, assessment)]), "Wrong assessment reference must fail immediately");
var disabled = new CaptureValidationSettings(new(false, null), new(false, null), new(false, null, null, null), new(false, null, null));
var entry = new CaptureValidationPolicyEntry(Guid.NewGuid(), 1, null, now.AddHours(1), now, "synthetic-admin", "SYNTHETIC-APPROVAL", disabled,
    "Synthetic approved disabled policy", "", "synthetic-protected-reference");
entry = entry with { SourceFingerprint = CaptureValidationPolicyRules.SourceFingerprint(entry) };
policyRepo.Add(entry);
var trackedPolicy = db.ChangeTracker.Entries<CaptureValidationPolicyVersion>().Single().Entity;
Check(trackedPolicy.SourceFingerprint == entry.SourceFingerprint && trackedPolicy.ApprovalReference == entry.ApprovalReference,
    "Actual repository stages exact approved policy metadata without saving or SQL");
db.Entry(trackedPolicy).State = EntityState.Unchanged;
db.Entry(trackedPolicy).Property(x => x.Reason).CurrentValue = "Attempted mutation";
Refuse(() => db.SaveChanges(), "Actual context must refuse policy mutation before opening any connection");
db.ChangeTracker.Clear();
var final = new CaptureValidationFinalEvidence(Guid.NewGuid(), raw.Id, employee, PunchAction.CheckOut, false, raw.RegisteredDeviceId,
    raw.RequestSha256, now, now, now, PunchAssessmentOutcome.QueuedForReview, "CAPTURE_POLICY_NOT_CONFIGURED", PunchAssessmentOutcome.QueuedForReview,
    "CAPTURE_POLICY_NOT_CONFIGURED", null, null, null, "[\"CAPTURE_POLICY_NOT_CONFIGURED\"]", "[]", null);
db.Attach(final); db.Remove(final);
Refuse(() => db.SaveChanges(), "Actual context must refuse final evidence deletion before opening any connection");
db.ChangeTracker.Clear();
Refuse(() => new CaptureValidationFinalEvidence(Guid.NewGuid(), raw.Id, employee, PunchAction.CheckOut, true, raw.RegisteredDeviceId,
    raw.RequestSha256, now, now, now, PunchAssessmentOutcome.Unverified, "OFFLINE_REQUIRES_REVIEW", PunchAssessmentOutcome.Accepted,
    "CAPTURE_VALIDATED", entry.Id, 1, entry.SourceFingerprint, "[\"CAPTURE_VALIDATED\"]", "[]", null),
    "Offline final acceptance must be refused by the actual domain constructor");
Refuse(() => new CaptureValidationFinalEvidence(Guid.NewGuid(), raw.Id, employee, PunchAction.CheckOut, false, raw.RegisteredDeviceId,
    raw.RequestSha256, now, now, now, PunchAssessmentOutcome.Accepted, "CAPTURE_VALIDATED", PunchAssessmentOutcome.Accepted,
    "CAPTURE_VALIDATED", null, null, null, "[\"CAPTURE_VALIDATED\"]", "[]", null),
    "Missing capture policy cannot create an accepted final domain artifact");
Refuse(() => new CaptureValidationFinalEvidence(Guid.NewGuid(), raw.Id, employee, PunchAction.CheckOut, false, raw.RegisteredDeviceId,
    raw.RequestSha256, now, now.AddMinutes(3), now, PunchAssessmentOutcome.Accepted, "CAPTURE_VALIDATED", PunchAssessmentOutcome.Rejected,
    "FACE_PROOF_EXPIRED", entry.Id, 1, entry.SourceFingerprint, "[\"FACE_PROOF_EXPIRED\"]", "[]", null),
    "Final domain artifact cannot backdate the parent's assessment before evaluated IO completion");
Check(guard.Attempts == 0, "Zero attempted sync/async database connections; SQL was translated only");
Console.WriteLine($"PASS: {checks} B02 bounded query/model/source/append-only checks; zero database connections or commands.");

sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Attempts++; throw new Exception("Database access forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data,
        InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("Database access forbidden"); }
}
