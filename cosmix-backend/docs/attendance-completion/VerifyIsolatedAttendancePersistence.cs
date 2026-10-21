#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Globalization;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Authentication;
using Cosmix.Infrastructure.Authentication.Bootstrap;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Files;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Retained, machine-specific acceptance evidence. This verifier executes SELECTs and private reads only.
// It does not start/stop a host, save entities, apply migrations, create files, grant access or clean up.
// The caller separately verifies that the owned acceptance host has stopped before execution.
const string privateRoot = @"C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009";
const string baselineRun = "ACC202610098E9C6465";
const string runId = "ATT20261012R45B3DDA713";
const string targetMigration = "20261012082833_RetainZoneWarningTransitions";
const string operation = "attendance.summary.calculate";
var businessDate = new DateOnly(2026, 10, 9);
if (!args.SequenceEqual(new[] { "--execute" }))
{
    Console.WriteLine("Use --execute only after the fixed isolated web continuation completes or reaches its recorded stop and its owned host stops. Help performs no file or SQL reads.");
    if (!args.SequenceEqual(new[] { "--help" })) Environment.ExitCode = 2;
    return;
}
var checks = 0;
var stage = "private provenance";
try
{
    PrivatePath(privateRoot, true);
    var runRoot = PrivatePath(Path.Combine(privateRoot, "runs", runId), true);
    using var fixtureDocument = await ReadJson(Path.Combine(privateRoot, "attendance-manifest.json"));
    using var settingsDocument = await ReadJson(Path.Combine(privateRoot, "settings.json"));
    using var revocationDocument = await ReadJson(Path.Combine(privateRoot, "revocation-evidence.json"));
    using var startedDocument = await ReadJson(Path.Combine(runRoot, "web-execution-started.json"));
    using var schemaDocument = await ReadJson(Path.Combine(runRoot, "schema-result.json"));
    var fixture = fixtureDocument.RootElement;
    var settings = settingsDocument.RootElement;
    var revocation = revocationDocument.RootElement;
    Check(Text(fixture, "RunId") == baselineRun && Text(fixture, "Database") == "Cosmix_Testing" &&
        Property(fixture, "Committed").GetBoolean(), "retained fixture provenance");
    Check(Text(startedDocument.RootElement, "RunId") == runId, "original web run provenance");
    Check(Text(schemaDocument.RootElement, "RunId") == runId &&
        Text(schemaDocument.RootElement, "Database") == "Cosmix_Testing" &&
        Text(schemaDocument.RootElement, "TargetMigration") == targetMigration &&
        !Property(schemaDocument.RootElement, "FixturesChanged").GetBoolean() &&
        !Property(schemaDocument.RootElement, "PermissionsChanged").GetBoolean() &&
        !Property(schemaDocument.RootElement, "KeysChanged").GetBoolean(), "completed schema provenance");
    var employeeId = Id(fixture, "EmployeeId");
    var adminId = Id(fixture, "AdminAccountId");
    var superId = Id(fixture, "SuperAdminAccountId");
    var hrId = Id(fixture, "HrAccountId");
    var siteId = Id(fixture, "SiteId");
    Check(new[] { adminId, superId, hrId }.Distinct().Count() == 3 && Id(fixture, "EmployeeAccountId") == hrId,
        "distinct retained synthetic identities");
    Check(Text(revocation, "RunId") == baselineRun && Text(revocation, "Action") == "RevokeAdminRosterOnly" &&
        Id(revocation, "AccountId") == adminId && Property(revocation, "OtherGrantsRetained").GetBoolean(),
        "retained revocation provenance");

    var originalJournal = await ReadJournal(Path.Combine(runRoot, "web-journal.jsonl"));
    var completedJournal = await ReadJournal(Path.Combine(runRoot, "web-continuation-1", "web-journal.jsonl"));
    var webCompleted = completedJournal.Count(x => Text(x, "Kind") == "Completed") == 1 &&
        Text(completedJournal[^1], "Kind") == "Completed" && !completedJournal.Any(x => Text(x, "Kind") == "Stopped");
    var completed = Property(completedJournal[^1], "Metadata");
    var recordedPartialStop = completedJournal.Count(x => Text(x, "Kind") == "Stopped") == 1 &&
        !completedJournal.Any(x => Text(x, "Kind") == "Completed") && Text(completedJournal[^1], "Kind") == "Stopped" &&
        Property(completed, "Passed").GetInt32() == 39 && completedJournal.Count(x => Text(x, "Kind") == "SummarySaved") == 1;
    Check(webCompleted || recordedPartialStop, "completed or exact recorded partial continuation journal");
    if (webCompleted)
        Check(Property(completed, "Passed").GetInt32() > 0 && Property(completed, "NewRoles").GetInt32() == 3 &&
            Property(completed, "NewAssignments").GetInt32() == 3 && Property(completed, "SummaryRevisions").GetInt32() == 1 &&
            !Property(completed, "WorkerEnabled").GetBoolean() && !Property(completed, "ManualDutyAccepted").GetBoolean() &&
            !Property(completed, "MonthLocked").GetBoolean() && !Property(completed, "CarryForwardAccepted").GetBoolean(),
            "completed factual-only envelope");
    var journal = originalJournal.Concat(completedJournal).ToArray();
    var createdRoles = journal.Where(x => Text(x, "Kind") == "RoleCreated").ToArray();
    var assignedRoles = journal.Where(x => Text(x, "Kind") == "Assigned").ToArray();
    var savedSummaries = journal.Where(x => Text(x, "Kind") == "SummarySaved").ToArray();
    Check(createdRoles.Length == 3 && assignedRoles.Length == 3 && savedSummaries.Length == 1,
        "exact journal write cardinalities");
    var saved = Property(savedSummaries[0], "Metadata");
    var summaryId = Id(saved, "Id");
    Check(Id(saved, "EmployeeId") == employeeId && Date(saved, "BusinessDate") == businessDate &&
        Property(saved, "Revision").GetInt32() == 1, "journal summary binding");

    stage = "fixed read-only configuration";
    Check(Property(Property(settings, "LocalTesting"), "Enabled").GetBoolean() &&
        Path.GetFullPath(Text(Property(settings, "LocalTesting"), "PrivateDirectory")) == privateRoot &&
        !Enabled(settings, "AttendanceJobs", "Enabled") && !Enabled(settings, "AttendanceZones", "Enabled") &&
        !Enabled(settings, "AttendanceZones", "RetentionEnabled") && !Enabled(settings, "ZoneLocationStorage", "Enabled"),
        "guarded local configuration");
    var sms = Property(Property(settings, "MobileAuthentication"), "Sms");
    Check(Text(sms, "Provider") == "LocalTesting" && string.IsNullOrEmpty(Text(sms, "Endpoint")) &&
        string.IsNullOrEmpty(Text(sms, "BearerToken")), "local-only SMS configuration");
    var connectionString = Text(Property(settings, "ConnectionStrings"), "CosmixDatabase");
    var target = new SqlConnectionStringBuilder(connectionString);
    Check(target.InitialCatalog == "Cosmix_Testing" && target.IntegratedSecurity &&
        target.DataSource.Equals(@"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase) &&
        string.IsNullOrEmpty(target.AttachDBFilename) && string.IsNullOrEmpty(target.UserID) && string.IsNullOrEmpty(target.Password),
        "exact local SQL target");
    var fileOptions = Property(settings, "PrivateFiles").Deserialize<PrivateFileStorageOptions>(
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException();
    Check(PrivatePath(fileOptions.Directory, true) == Path.Combine(privateRoot, "files") &&
        PrivatePath(fileOptions.KeyFile) == Path.Combine(privateRoot, "keys", "files.key"), "retained object and key paths");
    foreach (var keyFile in fileOptions.DecryptionKeyFiles.Values) PrivatePath(keyFile);
    var files = new EncryptedPrivateFileStore(fileOptions);
    await using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
        .UseSqlServer(connectionString).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        .AddInterceptors(new SelectOnlyInterceptor()).Options);
    db.ChangeTracker.AutoDetectChangesEnabled = false;
    await db.Database.OpenConnectionAsync();
    await using (var command = db.Database.GetDbConnection().CreateCommand())
    {
        command.CommandText = "SELECT DB_NAME(), CONVERT(int, SERVERPROPERTY('IsLocalDB'))";
        await using var reader = await command.ExecuteReaderAsync();
        Check(await reader.ReadAsync() && reader.GetString(0) == "Cosmix_Testing" && reader.GetInt32(1) == 1,
            "actual local SQL catalog");
    }
    // Read the already verified history directly; EF's history helper can issue non-query setup.
    var migrations = await db.Database.SqlQueryRaw<string>(
        "SELECT [MigrationId] AS [Value] FROM [dbo].[__EFMigrationsHistory] ORDER BY [MigrationId]").ToArrayAsync();
    Check(migrations.Length == 17 && migrations[^1] == targetMigration &&
        migrations.SequenceEqual(db.Database.GetMigrations()), "exact complete migration chain");

    stage = "synthetic identities and access";
    var accounts = await db.UserAccounts.Select(x => new { x.Id, x.EmployeeId, x.LoginName, x.Employee.FullLegalName }).Take(4).ToArrayAsync();
    Check(accounts.Length == 3 && await db.Employees.CountAsync() == 3, "exact synthetic account and employee counts");
    foreach (var expected in new[] { (superId, "superadmin@example.invalid", "TEST ONLY SuperAdmin"),
        (adminId, "admin@example.invalid", "TEST ONLY Admin"), (hrId, "hr@example.invalid", "TEST ONLY HR") })
        Check(accounts.Count(x => x.Id == expected.Item1 && x.LoginName == expected.Item2 && x.FullLegalName == expected.Item3) == 1,
            "retained synthetic account identity");
    Check(accounts.Single(x => x.Id == hrId).EmployeeId == employeeId, "server-derived HR employee");
    var definitions = new[] {
        new ExpectedRole("EMPLOYEE", adminId, AccessScopeKind.Employee, employeeId,
            ["attendance.corrections.submit", "attendance.corrections.review", "attendance.corrections.decide",
             "attendance.summaries.read", "attendance.summaries.calculate", "attendance.reports.read", "attendance.reports.export"]),
        new ExpectedRole("MONTH", adminId, AccessScopeKind.All, null,
            ["attendance.month-locks.read", "attendance.month-locks.finalize"]),
        new ExpectedRole("SELF", hrId, AccessScopeKind.Self, employeeId,
            ["attendance.corrections.self", "attendance.summaries.self"])
    };
    var prefix = runId + "_";
    var roles = await db.Roles.Where(x => x.Code.StartsWith(prefix)).Take(4).ToArrayAsync();
    Check(roles.Length == 3 && roles.All(x => x.IsActive && !x.IsSystemRole && x.CreatedByUserId == superId),
        "exact three new custom roles");
    var roleIds = roles.Select(x => x.Id).ToArray();
    var assignments = await db.UserRoleAssignments.Where(x => roleIds.Contains(x.RoleId)).Take(4).ToArrayAsync();
    var permissions = await db.RolePermissions.Where(x => roleIds.Contains(x.RoleId))
        .Select(x => new { x.RoleId, x.Permission.Code, x.RevokedAt, x.GrantedByUserId }).Take(12).ToArrayAsync();
    Check(assignments.Length == 3 && permissions.Length == 11 &&
        permissions.All(x => x.RevokedAt == null && x.GrantedByUserId == superId), "exact assignments and eleven role permissions");
    foreach (var definition in definitions)
    {
        var role = roles.Single(x => x.Code == prefix + definition.Suffix);
        var created = Property(createdRoles.Single(x => Text(x, "Label") == definition.Suffix), "Metadata");
        var assigned = Property(assignedRoles.Single(x => Text(x, "Label") == definition.Suffix), "Metadata");
        Check(Id(created, "RoleId") == role.Id && Text(created, "Code") == role.Code && Id(assigned, "RoleId") == role.Id &&
            Id(assigned, "AccountId") == definition.AccountId && Property(assigned, "Scope").GetInt32() == (int)definition.Scope,
            "SQL role matches original HTTP journal");
        Check(permissions.Where(x => x.RoleId == role.Id).Select(x => x.Code).Order(StringComparer.Ordinal)
            .SequenceEqual(definition.Permissions.Order(StringComparer.Ordinal)), "exact role permission set");
        var assignment = assignments.Single(x => x.RoleId == role.Id);
        Check(assignment.Id == Id(assigned, "AssignmentId") && assignment.UserAccountId == definition.AccountId &&
            assignment.EmployeeId == accounts.Single(x => x.Id == definition.AccountId).EmployeeId &&
            assignment.AssignedByUserId == superId && assignment.RevokedAt == null && assignment.EffectiveTo == null &&
            assignment.EffectiveFrom <= DateTimeOffset.UtcNow, "exact retained active assignment");
        var scopes = await db.UserAccessScopes.Where(x => x.UserRoleAssignmentId == assignment.Id).Take(2).ToArrayAsync();
        Check(scopes.Length == 1 && scopes[0].Kind == definition.Scope && scopes[0].EmployeeId == definition.EmployeeScope &&
            scopes[0].DepartmentId == null && scopes[0].TeamId == null && scopes[0].SiteId == null && scopes[0].ProjectId == null,
            "exact role-assignment scope");
        Check(definition.Scope == AccessScopeKind.Employee ? Id(assigned, "TargetId") == employeeId :
            Property(assigned, "TargetId").ValueKind == JsonValueKind.Null, "journal scope target");
    }
    var revokedId = Id(revocation, "AssignmentId");
    var prior = await db.UserRoleAssignments.SingleAsync(x => x.Id == revokedId);
    Check(prior.UserAccountId == adminId && prior.RoleId == Id(revocation, "RoleId") && prior.RevokedByUserId == superId &&
        prior.RevokedAt == Property(revocation, "RevokedAt").GetDateTimeOffset(), "old revocation remains unchanged");
    var priorRole = await db.Roles.SingleAsync(x => x.Id == prior.RoleId);
    var priorScopes = await db.UserAccessScopes.Where(x => x.UserRoleAssignmentId == prior.Id).Take(2).ToArrayAsync();
    Check(priorRole.Code == baselineRun + "_ROSTER_SITE" && priorScopes.Length == 1 &&
        priorScopes[0].Kind == AccessScopeKind.Site && priorScopes[0].SiteId == siteId, "old revoked SiteA grant binding");

    stage = "summary SQL and protected evidence";
    var summaries = await db.DailyAttendanceSummaries.Take(2).ToArrayAsync();
    Check(summaries.Length == 1, "exactly one factual summary");
    var summary = summaries[0];
    Check(summary.Id == summaryId && summary.EmployeeId == employeeId && summary.AttendanceDate == businessDate &&
        summary.Revision == 1 && summary.SupersedesSummaryId == null && summary.Status == AttendanceDayStatus.Unverified &&
        summary.PolicyVersionId == null && summary.PayableMinutes == null && summary.LateMinutes == null &&
        summary.OvertimeMinutes == null && summary.WorkedMinutes == 0 && summary.ExceptionCount == 0 &&
        summary.CalculationVersion == AttendanceFacts.CalculationVersion && summary.InputEvidenceReference != null,
        "immutable factual-only summary SQL");
    var expectedRevision = AttendanceSummaryService.Map(summary);
    var summaryKey = runId + ":summary";
    var receipts = await db.IdempotencyReceipts.Where(x => x.Key.StartsWith(runId + ":")).Take(2).ToArrayAsync();
    Check(receipts.Length == 1, "one receipt with no rejected-operation receipts");
    var receipt = receipts[0];
    Check(receipt.Key == summaryKey && receipt.Operation == operation && receipt.ActorUserId == adminId.ToString() &&
        receipt.CompletedAt.HasValue && receipt.CompletedAt >= receipt.CreatedAt && receipt.CompletedAt < receipt.ExpiresAt &&
        receipt.ResultCode == "attendance-operation-v2" && receipt.ProtectedResultReference != null,
        "completed exact summary receipt");
    var summaryText = summaryId.ToString();
    var audits = await db.AuditEvents.Where(x => x.EventType == operation && x.TargetId == summaryText).Take(2).ToArrayAsync();
    Check(audits.Length == 1 && audits[0].ActorUserId == adminId.ToString() && audits[0].EmployeeScopeId == employeeId &&
        audits[0].TargetType == "Attendance" && audits[0].ActorRoleCode == prefix + "EMPLOYEE" &&
        audits[0].ProtectedChangesReference != null, "one original protected summary audit");
    await ReadProtected(summary.InputEvidenceReference!, $"attendance:summary:{employeeId:N}:{businessDate:yyyy-MM-dd}", value =>
    {
        var input = value.Deserialize<AttendanceDayResponse>() ?? throw new InvalidOperationException();
        Check(input.EmployeeId == employeeId && input.BusinessDate == businessDate && !input.IsLocked && input.LatestSavedRevision == null &&
            input.Facts.WorkedSeconds == 0 && input.Facts.WorkedMinutes == 0 && input.Facts.OpenSessions == 0 &&
            input.Facts.SessionIds.Count == 0 && input.Policy.Status == "policy_integration_unavailable" &&
            input.Policy.PolicyVersionId == null && input.Policy.PayableMinutes == null && input.Policy.LateMinutes == null &&
            input.Policy.OvertimeMinutes == null && input.Policy.DayStatus == null &&
            input.Roster.ShiftVersionId == summary.ShiftVersionId && (input.Roster.ConfiguredScheduledMinutes ?? 0) == summary.ScheduledMinutes &&
            AttendanceSummaryService.IsSavedRevisionCurrent(input with { LatestSavedRevision = expectedRevision }),
            "decrypted input identity facts and fingerprint");
    });
    await ReadProtected(receipt.ProtectedResultReference!, $"attendance:operation:{adminId:N}:{operation}:result", value =>
    {
        Check(Id(value, "ResourceId") == summaryId && Property(value, "Result").Deserialize<AttendanceSummaryRevision>() == expectedRevision &&
            !Property(value, "RequiresAll").GetBoolean() && Property(value, "Resources").GetArrayLength() == 0,
            "decrypted result summary binding");
        CheckScopes(Property(value, "ResourceScopes"), employeeId);
    });
    await ReadProtected(audits[0].ProtectedChangesReference!, $"attendance:operation:{adminId:N}:{operation}:audit", value =>
    {
        var request = Property(value, "request");
        var payload = Property(request, "payload");
        Check(Text(value, "operation") == operation && Id(value, "ResourceId") == summaryId &&
            Property(value, "Before").ValueKind == JsonValueKind.Null &&
            Property(value, "After").Deserialize<AttendanceSummaryRevision>() == expectedRevision &&
            Id(request, "employeeId") == employeeId && Text(request, "permission") == "attendance.summaries.calculate" &&
            Property(request, "DeviceId").ValueKind == JsonValueKind.Null && Date(payload, "BusinessDate") == businessDate &&
            Property(payload, "ExpectedRevision").GetInt32() == 0 && Text(payload, "Reason").StartsWith(runId, StringComparison.Ordinal) &&
            Text(payload, "Reason") == Text(request, "reason"), "decrypted audit employee date and result binding");
        var requestBytes = JsonSerializer.SerializeToUtf8Bytes(request);
        try { Check(Convert.ToHexString(SHA256.HashData(requestBytes)) == receipt.RequestSha256, "audit request matches receipt digest"); }
        finally { CryptographicOperations.ZeroMemory(requestBytes); }
        CheckScopes(Property(value, "resourceScopes"), employeeId);
    });
    Check(summary.InputEvidenceReference != receipt.ProtectedResultReference && summary.InputEvidenceReference != audits[0].ProtectedChangesReference &&
        receipt.ProtectedResultReference != audits[0].ProtectedChangesReference, "three separate protected evidence references");

    stage = "absence of unauthorized attendance writes";
    Check(!await db.RawPunchEvents.AnyAsync(), "no raw punches");
    Check(!await db.WorkSessions.AnyAsync(), "no duty sessions");
    Check(!await db.AttendanceCorrections.AnyAsync(), "no corrections");
    Check(!await db.AttendanceCarryForwardAdjustments.AnyAsync(), "no carry-forward adjustments");
    Check(!await db.AttendancePeriodLocks.AnyAsync() && !await db.AttendancePeriodLockEvents.AnyAsync() &&
        !await db.AttendancePeriodSummaries.AnyAsync(), "no period locks or frozen period summaries");
    Check(!await db.AttendanceSummarySessions.AnyAsync() && !await db.AttendanceCalculationLines.AnyAsync(), "no invented session or pay lines");
    Check(!await db.OutboxMessages.AnyAsync() && !await db.ReportExportJobs.AnyAsync(), "no outbox or export jobs");
    Check(!await db.DutyMonitoringWindows.AnyAsync(), "no duty monitoring windows");
    Check(!db.ChangeTracker.Entries().Any(), "no tracked database entities");
    Console.WriteLine($"PASS {checks} isolated attendance SQL/private-evidence checks; three protected objects independently decrypted. Read-only; no secret or plaintext evidence output.");
    Console.WriteLine(webCompleted ? "The bounded HTTP journal is completed; broader attendance acceptance is not implied." :
        "The HTTP continuation remains partial: 39 recorded checks, one factual summary, then stopped at month preview. This persistence result does not complete HTTP acceptance.");

    async Task ReadProtected(string serialized, string purpose, Action<JsonElement> verify)
    {
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(serialized) ?? throw new InvalidOperationException();
        PrivatePath(Path.Combine(fileOptions.Directory, reference.StorageKey));
        var bytes = await files.ReadAsync(reference, purpose, default);
        try { using var document = JsonDocument.Parse(bytes); verify(document.RootElement); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
catch (Exception error)
{
    // Parser, SQL and storage exception messages can contain private input. Never print them.
    Console.Error.WriteLine($"FAIL during {stage}; private details suppressed. No repair or cleanup was attempted.");
    Console.Error.WriteLine($"Exception type: {error.GetType().Name}; SQL number: {(error is SqlException sql ? sql.Number : 0)}.");
    Console.Error.WriteLine(error.StackTrace?.Split('\n').FirstOrDefault(x => x.Contains("VerifyIsolatedAttendancePersistence.cs", StringComparison.Ordinal))?.Trim());
    Environment.ExitCode = 1;
}

void Check(bool condition, string label)
{
    if (!condition) { stage = label; throw new InvalidOperationException(); }
    checks++;
}
void CheckScopes(JsonElement value, Guid employeeId)
{
    var scopes = value.Deserialize<AttendanceResourceScope[]>() ?? throw new InvalidOperationException();
    Check(AttendanceScopeEvaluation.IsValid(scopes) &&
        scopes.All(x => x.Alternatives.All(y => y.EmployeeId == employeeId)), "protected historical employee scopes");
}
JsonElement Property(JsonElement value, string name) => value.EnumerateObject()
    .Single(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
string Text(JsonElement value, string name) => Property(value, name).GetString() ?? "";
Guid Id(JsonElement value, string name) => Property(value, name).GetGuid() is var id && id != Guid.Empty ? id : throw new InvalidOperationException();
DateOnly Date(JsonElement value, string name) => DateOnly.ParseExact(Text(value, name), "yyyy-MM-dd", CultureInfo.InvariantCulture);
bool Enabled(JsonElement value, string section, string name) => value.TryGetProperty(section, out var nested) &&
    nested.TryGetProperty(name, out var enabled) && enabled.ValueKind == JsonValueKind.True;
string PrivatePath(string path, bool directory = false)
{
    var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    if (!full.Equals(privateRoot, StringComparison.OrdinalIgnoreCase) &&
        !full.StartsWith(privateRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
    PrivateBootstrapSnapshotStore.RequirePrivatePath(full, directory);
    if (OperatingSystem.IsWindows())
    {
        FileSystemSecurity security = directory ? new DirectoryInfo(full).GetAccessControl() : new FileInfo(full).GetAccessControl();
        if (new RawSecurityDescriptor(security.GetSecurityDescriptorBinaryForm(), 0).DiscretionaryAcl is null)
            throw new InvalidOperationException();
    }
    return full;
}
async Task<byte[]> ReadPrivate(string path)
{
    await using var stream = new FileStream(PrivatePath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
    if (stream.Length is < 1 or > 1048576) throw new InvalidOperationException();
    var bytes = new byte[checked((int)stream.Length)];
    try { await stream.ReadExactlyAsync(bytes); return bytes; }
    catch { CryptographicOperations.ZeroMemory(bytes); throw; }
}
async Task<JsonDocument> ReadJson(string path)
{
    var bytes = await ReadPrivate(path);
    try { return JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes)); }
    finally { CryptographicOperations.ZeroMemory(bytes); }
}
async Task<JsonElement[]> ReadJournal(string path)
{
    var bytes = await ReadPrivate(path);
    try
    {
        var lines = System.Text.Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length is < 1 or > 2048) throw new InvalidOperationException();
        return lines.Select(line => { using var document = JsonDocument.Parse(line); return document.RootElement.Clone(); }).ToArray();
    }
    finally { CryptographicOperations.ZeroMemory(bytes); }
}
sealed record ExpectedRole(string Suffix, Guid AccountId, AccessScopeKind Scope, Guid? EmployeeScope, string[] Permissions);
sealed class SelectOnlyInterceptor : DbCommandInterceptor
{
    private static void RequireSelect(DbCommand command)
    {
        if (!command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
    }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result)
    { RequireSelect(command); return result; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { RequireSelect(command); return ValueTask.FromResult(result); }
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData data, InterceptionResult<object> result)
    { RequireSelect(command); return result; }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<object> result, CancellationToken cancellationToken = default)
    { RequireSelect(command); return ValueTask.FromResult(result); }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData data, InterceptionResult<int> result) => throw new InvalidOperationException();
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<int> result, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
}
