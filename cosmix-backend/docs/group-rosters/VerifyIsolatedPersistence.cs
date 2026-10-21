#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.HR.Organization;
using Cosmix.Infrastructure.Authentication.Bootstrap;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Files;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Approved isolated acceptance only. All database operations below are reads. No SaveChanges,
// migrations, account grants, private-file writes, cleanup, or plaintext snapshot output.
const string privateRoot = @"C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009";
if (args.Length == 1 && args[0] == "--help")
{
    Console.WriteLine("VerifyIsolatedPersistence --evidence <private completed HTTP evidence.json>; --help performs no file or database reads.");
    return;
}
if (args.Length != 2 || args[0] != "--evidence") throw new ArgumentException("Use --evidence followed by the private completed HTTP evidence path.");
string PrivatePath(string value)
{
    var full = Path.GetFullPath(value);
    if (!full.StartsWith(privateRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Path is outside the approved isolated acceptance directory.");
    PrivateBootstrapSnapshotStore.RequirePrivatePath(full, false);
    return full;
}
var checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    checks++;
}
JsonElement Property(JsonElement value, string name) => value.EnumerateObject()
    .Single(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)).Value;
Guid Id(JsonElement value) => value.GetGuid() is var id && id != Guid.Empty ? id : throw new InvalidOperationException("Empty fixture identity.");
Guid[] Ids(JsonElement value) => value.EnumerateArray().Select(Id).ToArray();
using var settings = JsonDocument.Parse(await File.ReadAllBytesAsync(PrivatePath(Path.Combine(privateRoot, "settings.json"))));
using var evidence = JsonDocument.Parse(await File.ReadAllBytesAsync(PrivatePath(args[1])));
var root = evidence.RootElement;
Check(Property(root, "database").GetString() == "Cosmix_Testing" && Property(root, "completed").GetBoolean(), "Completed testing evidence required.");
var runId = Property(root, "runId").GetString()!;
Check(Regex.IsMatch(runId, "^ACC[A-Z0-9]{6,18}$", RegexOptions.CultureInvariant), "Synthetic run prefix required.");
var siteId = Id(Property(root, "siteId"));
var employeeIds = Ids(Property(root, "employeeIds"));
Check(employeeIds.Length is >= 1 and <= 10 && employeeIds.Distinct().Count() == employeeIds.Length, "Bounded distinct staff required.");
var created = Property(root, "created");
var shiftIds = Ids(Property(created, "shiftIds"));
var initialVersionIds = Ids(Property(created, "initialVersionIds"));
var assignmentIds = Ids(Property(created, "assignmentIds"));
var temporaryIds = Ids(Property(created, "temporaryIds"));
var amendmentIds = Ids(Property(created, "amendmentIds"));
var publishedId = Id(Property(created, "publishedVersionId"));
Check(shiftIds.Length == 3 && initialVersionIds.Length == 3 && assignmentIds.Length == 2 && temporaryIds.Length == 3 && amendmentIds.Length == 3,
    "Unexpected fixture cardinality.");
var from = DateOnly.ParseExact(Property(root, "effectiveFrom").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
var businessDate = DateOnly.ParseExact(Property(root, "businessDate").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
var connectionString = Property(Property(settings.RootElement, "ConnectionStrings"), "CosmixDatabase").GetString()!;
var connection = new SqlConnectionStringBuilder(connectionString);
Check(connection.InitialCatalog == "Cosmix_Testing" && connection.IntegratedSecurity &&
    string.Equals(connection.DataSource, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase) &&
    string.IsNullOrEmpty(connection.UserID) && string.IsNullOrEmpty(connection.Password), "Exact local testing SQL target required.");
var fileOptions = JsonSerializer.Deserialize<PrivateFileStorageOptions>(Property(settings.RootElement, "PrivateFiles").GetRawText(),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
Check(Path.GetFullPath(fileOptions.Directory).StartsWith(privateRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
    "API private objects must use the approved isolated root.");
PrivatePath(fileOptions.KeyFile);
var files = new EncryptedPrivateFileStore(fileOptions);
await using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(connectionString).Options);
db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
await db.Database.OpenConnectionAsync();
await using (var command = db.Database.GetDbConnection().CreateCommand())
{
    command.CommandText = "SELECT DB_NAME()";
    Check((string?)await command.ExecuteScalarAsync() == "Cosmix_Testing", "Actual SQL catalog mismatch.");
}
await new SiteRosterRepository(db).RequireDirectSchemaAsync(default);
checks++;

var shifts = await db.Shifts.Where(x => shiftIds.Contains(x.Id)).ToArrayAsync();
Check(shifts.Length == 3 && shifts.All(x => x.Code.StartsWith(runId + ".", StringComparison.Ordinal)), "Unexpected persisted shift identities.");
var versions = await db.ShiftVersions.Where(x => shiftIds.Contains(x.ShiftId)).ToArrayAsync();
Check(versions.Length == 4 && versions.Count(x => initialVersionIds.Contains(x.Id) && x.VersionNumber == 1 && x.EffectiveFrom == from) == 3,
    "Initial versions must remain immutable and publication append exactly one version.");
Check(versions.Single(x => x.Id == publishedId).VersionNumber == 2 && versions.Single(x => x.Id == publishedId).EffectiveFrom == from.AddDays(1),
    "Published version has the wrong identity/date.");
var assignments = await db.ShiftPopulationAssignments.Where(x => x.SiteId == siteId).ToArrayAsync();
Check(assignments.Length == 2 && assignments.All(x => assignmentIds.Contains(x.Id) && x.EffectiveTo is null && x.ApprovalRequestId is null),
    "Original assignment intervals must remain append-only and direct.");
var ends = await db.ShiftPopulationAssignmentEnds.Where(x => assignmentIds.Contains(x.ShiftPopulationAssignmentId)).ToArrayAsync();
Check(ends.Length == 2 && ends.All(x => x.ApprovalRequestId is null), "Expected two direct immutable endings.");
Check(ends.Single(x => x.ShiftPopulationAssignmentId == assignmentIds[0]).EffectiveTo == businessDate.AddDays(2) &&
    ends.Single(x => x.ShiftPopulationAssignmentId == assignmentIds[1]).EffectiveTo == businessDate.AddDays(4), "Ending dates are incorrect.");
var changes = await db.TemporaryShiftChanges.Where(x => x.SiteId == siteId).Include(x => x.Employees).AsSplitQuery().ToArrayAsync();
Check(changes.Length == 3 && changes.All(x => temporaryIds.Contains(x.Id) && x.AttendanceDate == businessDate &&
    x.OriginalShiftVersionId == initialVersionIds[0] && x.ApprovalRequestId is null), "Temporary original identities were lost or rebound.");
foreach (var change in changes)
    Check(change.Employees.Select(x => x.EmployeeId).Order().SequenceEqual(employeeIds.Order()), "Actual SQL manifest differs from exact affected staff.");
var amendments = await db.Set<TemporaryShiftChangeAmendment>().Where(x => temporaryIds.Contains(x.OriginalChangeId)).ToArrayAsync();
Check(amendments.Length == 3 && amendments.All(x => amendmentIds.Contains(x.Id)), "Amendment count/identity mismatch.");
Check(amendments.Single(x => x.Id == amendmentIds[0]).OriginalChangeId == temporaryIds[0] &&
    amendments.Single(x => x.Id == amendmentIds[0]).ReplacementChangeId == temporaryIds[1], "Supersession link is incorrect.");
Check(amendments.Single(x => x.Id == amendmentIds[1]).OriginalChangeId == temporaryIds[1] &&
    amendments.Single(x => x.Id == amendmentIds[1]).ReplacementChangeId is null &&
    amendments.Single(x => x.Id == amendmentIds[2]).OriginalChangeId == temporaryIds[2] &&
    amendments.Single(x => x.Id == amendmentIds[2]).ReplacementChangeId is null, "Cancellation links are incorrect.");

var expectedReceipts = Property(root, "receipts").EnumerateArray().Select(x => x.Clone()).ToArray();
Check(expectedReceipts.Length is 12 or 13, "Unexpected successful write evidence count.");
var keys = expectedReceipts.Select(x => Property(x, "key").GetString()!).ToArray();
Check(keys.Distinct().Count() == keys.Length && keys.All(x => x.StartsWith(runId + ".", StringComparison.Ordinal) &&
    Regex.IsMatch(x, "^[A-Za-z0-9_.:-]{8,100}$", RegexOptions.CultureInvariant)), "Receipt keys must stay within the synthetic run.");
var receipts = await db.IdempotencyReceipts.Where(x => keys.Contains(x.Key)).ToArrayAsync();
Check(receipts.Length == keys.Length && receipts.All(x => x.CompletedAt.HasValue && x.ProtectedResultReference is not null),
    "Successful writes need one completed SQL receipt each.");
var rejected = new[] { ".stale", ".no-csrf", ".blocked-version", ".geofence-denied", ".geofence-stale" }.Select(x => runId + x).ToArray();
Check(!await db.IdempotencyReceipts.AnyAsync(x => rejected.Contains(x.Key)), "Rejected writes must leave no retry receipt.");
var auditReads = 0;
foreach (var expected in expectedReceipts)
{
    var receipt = receipts.Single(x => x.Key == Property(expected, "key").GetString());
    var expectedBody = Property(expected, "expected");
    if (Property(expected, "clientKind").GetString() == "Admin")
    {
        Check(receipt.ResultCode == "SiteGeofence", "Geofence receipt uses the wrong result contract.");
        var changeId = Guid.ParseExact(receipt.ProtectedResultReference!, "N");
        var journal = await db.Set<OrganizationChange>().SingleAsync(x => x.Id == changeId);
        using var stored = JsonDocument.Parse(journal.AfterJson);
        Check(Equal(stored.RootElement, expectedBody), "Geofence SQL journal differs from the real HTTP result.");
        continue;
    }
    var actor = Guid.Parse(receipt.ActorUserId);
    using var protectedResult = await ReadProtected(receipt.ProtectedResultReference!, $"attendance:scheduling:{actor:N}:{receipt.Operation}:result");
    var result = protectedResult.RootElement;
    if (receipt.ResultCode == "attendance-scheduling-scoped-v2")
    {
        var requirements = Property(result, "Requirements").EnumerateArray().ToArray();
        Check(requirements.Length == 1 && Property(requirements[0], "Permission").GetString() == "attendance.rosters.manage",
            "Publication did not preserve original roster authority.");
        var scopes = Property(requirements[0], "Resources").EnumerateArray().ToArray();
        Check(scopes.Length == 1 && Id(Property(Property(scopes[0], "Alternatives")[0], "SiteId")) == siteId,
            "Publication original affected-site proof is missing.");
        result = Property(result, "Result");
    }
    else Check(receipt.ResultCode == "attendance-scheduling-v1", "Unknown scheduling receipt contract.");
    Check(Equal(result, expectedBody), "Actual encrypted receipt differs from the accepted HTTP response.");
    var path = Property(expected, "path").GetString()!;
    Guid target;
    if (path.StartsWith("/api/v1/hr/shifts", StringComparison.Ordinal)) target = Id(Property(expectedBody, "Id"));
    else if (path.EndsWith("/supersede", StringComparison.Ordinal) || path.EndsWith("/cancel", StringComparison.Ordinal))
        target = Id(Property(Property(expectedBody, "Amendment"), "Id"));
    else if (path.EndsWith("/temporary-shift-changes", StringComparison.Ordinal)) target = Id(Property(Property(expectedBody, "Change"), "Id"));
    else target = Id(Property(Property(expectedBody, "Assignment"), "Id"));
    var targetText = target.ToString();
    var audits = await db.AuditEvents.Where(x => x.EventType == receipt.Operation && x.TargetId == targetText &&
        x.ActorUserId == receipt.ActorUserId).Take(2).ToArrayAsync();
    Check(audits.Length == 1 && audits[0].ProtectedChangesReference is not null, "Expected one original protected SQL audit for the successful write.");
    using var audit = await ReadProtected(audits[0].ProtectedChangesReference!, $"attendance:scheduling:{actor:N}:{receipt.Operation}:audit");
    Check(Id(Property(audit.RootElement, "ResourceId")) == target && Equal(Property(audit.RootElement, "After"), expectedBody),
        "Actual decrypted audit has the wrong resource/result.");
    auditReads++;
}
Check(auditReads == 12, "Every original scheduling write needs independently decrypted audit evidence.");
foreach (var change in changes)
{
    var incoming = amendments.SingleOrDefault(x => x.ReplacementChangeId == change.Id);
    var purpose = incoming is null ? $"attendance:site-temporary:{siteId:N}:{businessDate:yyyyMMdd}" :
        $"attendance:site-temporary-amendment:{siteId:N}:{incoming.OriginalChangeId:N}";
    using var proof = await ReadProtected(change.WrittenEvidenceReference, purpose);
    Check(Ids(Property(proof.RootElement, "AffectedEmployeeIds")).Order().SequenceEqual(employeeIds.Order()),
        "Written temporary evidence must retain the real affected staff.");
}
foreach (var amendment in amendments)
{
    using var proof = await ReadProtected(amendment.WrittenEvidenceReference,
        $"attendance:site-temporary-amendment:{siteId:N}:{amendment.OriginalChangeId:N}");
    Check(Ids(Property(proof.RootElement, "PreviousAffectedEmployeeIds")).Order().SequenceEqual(employeeIds.Order()) &&
        Ids(Property(proof.RootElement, "AffectedEmployeeIds")).Order().SequenceEqual(employeeIds.Order()),
        "Amendment written proof must preserve previous and actual manifests.");
}
Console.WriteLine($"PASS {checks} actual SQL/private-object persistence checks; {auditReads} protected scheduling audits decrypted. Read-only, no secret or plaintext evidence output.");

async Task<JsonDocument> ReadProtected(string serialized, string purpose)
{
    var reference = JsonSerializer.Deserialize<PrivateFileReference>(serialized) ?? throw new InvalidOperationException("Missing protected reference.");
    return JsonDocument.Parse(await files.ReadAsync(reference, purpose, default));
}
bool Equal(JsonElement first, JsonElement second) => JsonSerializer.Serialize(Normalize(first)) == JsonSerializer.Serialize(Normalize(second));
object? Normalize(JsonElement value) => value.ValueKind switch
{
    JsonValueKind.Object => value.EnumerateObject().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(x => x.Name.ToLowerInvariant(), x => Normalize(x.Value)),
    JsonValueKind.Array => value.EnumerateArray().Select(Normalize).ToArray(),
    JsonValueKind.String => NormalizeText(value.GetString()!),
    JsonValueKind.Number => value.GetDecimal(),
    JsonValueKind.True => true,
    JsonValueKind.False => false,
    JsonValueKind.Null => null,
    _ => throw new InvalidOperationException("Unexpected evidence JSON value.")
};
string NormalizeText(string text) => text.Contains('T') && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
    DateTimeStyles.RoundtripKind, out var instant) ? instant.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) : text;
