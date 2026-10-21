#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:package Microsoft.Extensions.Configuration.Json@10.0.12
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Infrastructure.Authentication.Bootstrap;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

const string repository = @"C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend";
const string pipe = @"np:\\.\pipe\LOCALDB#842DD4FB\tsql\query";
const string server = @"Software-PC1\LOCALDB#842DD4FB";
const string owner = @"COSMIXRND\Huma.Khan";
const string root = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting";
const string baseline = "20261008074438_AddEmployeeChangeJournal";
const string final = "20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence";
const string officeMigration = "20261013101327_AddOfficeRosterHistory";
var mode = args.SingleOrDefault() ?? "--inspect";
Require(mode is "--inspect" or "--upgrade" or "--verify" or "--auth-evidence" or "--shift-evidence" or "--prepare-remaining-sql" or "--continue-reviewed-19-21" or "--office-preflight" or "--apply-reviewed-office", "Unknown mode.");
var continuation = mode == "--continue-reviewed-19-21";
var office = mode is "--office-preflight" or "--apply-reviewed-office";
Require(Path.GetFullPath(Directory.GetCurrentDirectory()).Equals(repository, StringComparison.OrdinalIgnoreCase), "Wrong checkout.");
if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Windows acceptance only.");
Require(OperatingSystem.IsWindows() && System.Security.Principal.WindowsIdentity.GetCurrent().Name == owner, "Wrong operator.");
var builder = new SqlConnectionStringBuilder
{
    DataSource = pipe, InitialCatalog = "Cosmix_Testing", IntegratedSecurity = true,
    Encrypt = true, TrustServerCertificate = true, Pooling = false, ConnectTimeout = 15,
    ConnectRetryCount = 0, ApplicationName = "Cosmix Existing Attendance Acceptance"
};
await using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(builder.ConnectionString).Options);
var migrationAssembly = db.GetService<IMigrationsAssembly>();
if (db.Database.HasPendingModelChanges())
{
    var snapshot = migrationAssembly.ModelSnapshot?.Model ?? throw new InvalidOperationException("Retained snapshot missing.");
    var initialized = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot, designTime: true);
    var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(
        initialized.GetRelationalModel(), db.GetService<IDesignTimeModel>().Model.GetRelationalModel());
    var missingTables = differences.OfType<CreateTableOperation>().Select(x => $"{x.Schema}.{x.Name}");
    throw new InvalidOperationException($"Model differs from retained snapshot; {differences.Count} pending schema operations. New tables: {string.Join(", ", missingTables)}. No SQL connection opened.");
}
var local = migrationAssembly.Migrations.ToArray();
Require(local.Length is 21 or 22 && local[11].Key == baseline && local[20].Key == final &&
    (local.Length == 21 || local[21].Key == officeMigration), "Unexpected source chain.");
Require(!office || local.Length == 22, "Reviewed office source migration is missing.");
Require(local.Length == 21 || mode is "--verify" or "--auth-evidence" or "--shift-evidence" || office,
    "Historical mutation modes cannot operate on an extended source chain.");
var operations = local.Skip(office ? 21 : 12).SelectMany(x => migrationAssembly.CreateMigration(x.Value, db.Database.ProviderName!).UpOperations).ToArray();
Require(!operations.Any(x => x is InsertDataOperation or UpdateDataOperation or DeleteDataOperation or DropTableOperation or DropColumnOperation),
    "Pending migration includes unreviewed business-data/destructive operations.");
if (office) Require(operations.Length == 6 && operations.All(x => x is CreateTableOperation or CreateIndexOperation),
    "Office migration differs from the reviewed additive six-operation range.");
var sql = db.GetService<IMigrator>().GenerateScript(office ? final : mode == "--upgrade" ? baseline : local[17].Key, office ? officeMigration : final);
var reviewedPath = office ? Path.Combine(repository, "docs", "sql", officeMigration + ".sql") :
    Path.Combine(repository, "docs", "attendance-completion", mode == "--upgrade"
        ? "Huma-Attendance-Pending-13-21.sql" : "Huma-Attendance-Remaining-19-21-20261013.sql");
if (mode == "--prepare-remaining-sql")
{
    Require(!File.Exists(reviewedPath), "Remaining script already exists; inspect rather than overwrite.");
    await using var prepared = new FileStream(reviewedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    await prepared.WriteAsync(System.Text.Encoding.UTF8.GetBytes(sql));
    Console.WriteLine("Prepared exact remaining 19-21 script offline; no SQL connection or execution.");
    return;
}
var reviewed = File.ReadAllText(reviewedPath);
Require(sql.Replace("\r\n", "\n") == reviewed.Replace("\r\n", "\n"), "Reviewed pending SQL differs from current migration source.");
Require(!Regex.IsMatch(sql, @"(?im)^\s*(?:USE\b|:|!)|\b(?:CREATE|ALTER|DROP)\s+DATABASE\b"), "SQL contains target switching.");
var batches = Regex.Split(sql, @"(?m)^GO\s*$").Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

await using var connection = new SqlConnection(builder.ConnectionString);
await connection.OpenAsync();
await VerifyIdentity();
var history = await Rows("SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;");
var expectedCount = office ? 21 : mode is "--verify" or "--auth-evidence" or "--shift-evidence" ? local.Length : mode == "--upgrade" ? 12 : 18;
Require(history.Count == expectedCount && history.Select(x => (string)x["MigrationId"]).SequenceEqual(local.Take(expectedCount).Select(x => x.Key)), "Actual history differs from the expected exact range.");
var accounts = await Rows("SELECT a.LoginName,e.FullLegalName,e.ApprovedLoginPhoneNumber FROM auth.UserAccounts a JOIN hr.Employees e ON e.Id=a.EmployeeId ORDER BY a.LoginName;");
var expectedAccounts = new[] { ("admin@example.invalid", "TEST ONLY Admin", "+12025550102"),
    ("hr@example.invalid", "TEST ONLY HR", "+12025550103"), ("superadmin@example.invalid", "TEST ONLY SuperAdmin", "+12025550101") };
Require(accounts.Count == 3 && accounts.Select(x => ((string)x["LoginName"], (string)x["FullLegalName"], (string)x["ApprovedLoginPhoneNumber"]))
    .SequenceEqual(expectedAccounts), "Unexpected or non-synthetic authentication account.");
var beforeCounts = await Counts();
var employeeProof = await ExistingIdentityProof();
Console.WriteLine($"Verified existing target {server}/Cosmix_Testing; {history.Count} applied migrations, 3 synthetic accounts, {beforeCounts["hr.Employees"]} employees; pending SQL matches current source.");
if (mode == "--apply-reviewed-office")
{
    var officeBackup = @"C:\Users\Huma.Khan\Cosmix_Testing_Attendance_20261013_B05.bak";
    var officeIntent = Path.Combine(root, "Attendance-Office-Migration-20261013-B05-intent.json");
    var officeResult = Path.Combine(root, "Attendance-Office-Migration-20261013-B05-result.json");
    Require(!File.Exists(officeBackup) && !File.Exists(officeIntent) && !File.Exists(officeResult), "Owned office attempt already exists; no automatic retry/overwrite.");
    Require(!beforeCounts.ContainsKey("attendance.OfficeRosterAssignments") && !beforeCounts.ContainsKey("attendance.OfficeRosterAssignmentEnds"),
        "Unrecorded office tables already exist; stop for reconciliation.");
    var physical = await Rows("SELECT physical_name AS Path FROM sys.database_files ORDER BY file_id;");
    Require(physical.Count == 2 && (string)physical[0]["Path"] == @"C:\Users\Huma.Khan\Cosmix_Testing.mdf" &&
        (string)physical[1]["Path"] == @"C:\Users\Huma.Khan\Cosmix_Testing_log.ldf", "Existing physical target changed.");
    for (var parent = Path.GetDirectoryName(officeBackup); parent is not null; parent = Path.GetDirectoryName(parent))
        Require((File.GetAttributes(parent) & FileAttributes.ReparsePoint) == 0, "Linked backup ancestry is forbidden.");
    await Command("BACKUP DATABASE [Cosmix_Testing] TO DISK=@path WITH COPY_ONLY,CHECKSUM,NOFORMAT,NOINIT,NAME=N'Cosmix Attendance 20261013 B05';", officeBackup);
    var officeHeader = await Rows("RESTORE HEADERONLY FROM DISK=@path;", officeBackup);
    Require(officeHeader.Count == 1 && (string)officeHeader[0]["DatabaseName"] == "Cosmix_Testing" &&
        (string)officeHeader[0]["BackupName"] == "Cosmix Attendance 20261013 B05" && (bool)officeHeader[0]["IsCopyOnly"] &&
        (bool)officeHeader[0]["HasBackupChecksums"] && !(bool)officeHeader[0]["IsDamaged"], "Office backup header verification failed.");
    await Command("RESTORE VERIFYONLY FROM DISK=@path WITH FILE=1,CHECKSUM;", officeBackup);
    var acl = new FileSecurity(); var sid = WindowsIdentity.GetCurrent().User!;
    acl.SetOwner(sid); acl.SetAccessRuleProtection(true, false);
    foreach (var value in new[] { sid.Value, "S-1-5-18", "S-1-5-32-544" })
        acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(value), FileSystemRights.FullControl, AccessControlType.Allow));
    new FileInfo(officeBackup).SetAccessControl(acl);
    string hash;
    await using (var file = File.OpenRead(officeBackup)) hash = Convert.ToHexString(await SHA256.HashDataAsync(file));
    await NewJson(officeIntent, new { Server = server, Database = "Cosmix_Testing", Owner = owner, Backup = officeBackup,
        BackupSha256 = hash, RestoreVerified = true, History = history, TableCounts = beforeCounts, IdentityProof = employeeProof,
        PendingSqlSha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(reviewed))),
        Approval = "Human request: complete Attendance locally using existing synthetic testing database; preserve all existing data.", AtUtc = DateTimeOffset.UtcNow });
    Require((await Counts()).OrderBy(x => x.Key).SequenceEqual(beforeCounts.OrderBy(x => x.Key)) && await ExistingIdentityProof() == employeeProof,
        "Existing records changed during office backup; stop before migration.");
    await VerifyIdentity();
    foreach (var batch in batches) await Command(batch);
    var completed = await Rows("SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;");
    var counts = await Counts();
    Require(completed.Count == 22 && completed.Select(x => (string)x["MigrationId"]).SequenceEqual(local.Select(x => x.Key)) &&
        completed.Take(21).Select(x => (string)x["ProductVersion"]).SequenceEqual(history.Select(x => (string)x["ProductVersion"])), "Office migration history mismatch.");
    Require(beforeCounts.Where(x => x.Key != "dbo.__EFMigrationsHistory").All(x => counts.TryGetValue(x.Key, out var value) && value == x.Value) &&
        counts.Count == beforeCounts.Count + 2 && counts["attendance.OfficeRosterAssignments"] == 0 && counts["attendance.OfficeRosterAssignmentEnds"] == 0 &&
        await ExistingIdentityProof() == employeeProof, "Office upgrade altered retained business records or seeded data.");
    await VerifyIdentity();
    await NewJson(officeResult, new { Server = server, Database = "Cosmix_Testing", AppliedMigrations = 22, Backup = officeBackup,
        BackupSha256 = hash, ExistingRowsPreserved = true, NewOfficeTablesEmpty = true, History = completed, AtUtc = DateTimeOffset.UtcNow });
    Console.WriteLine("PASS: reviewed migration 22 applied after verified backup; all retained business rows/identities preserved; two new office tables empty.");
    return;
}
if (mode == "--shift-evidence")
{
    var shifts = await Rows("SELECT Id,Code,Name FROM attendance.Shifts WHERE Code IN (N'ATTHUMA13B03',N'ATTHUMA13B04') ORDER BY Code;");
    Require(shifts.Count == 2 && shifts.Select(x => (string)x["Code"]).Distinct().Count() == 2, "Duplicate/missing synthetic shift.");
    var versions = await Rows("SELECT v.ShiftId,COUNT(*) AS Versions FROM attendance.ShiftVersions v JOIN attendance.Shifts s ON s.Id=v.ShiftId WHERE s.Code IN (N'ATTHUMA13B03',N'ATTHUMA13B04') GROUP BY v.ShiftId;");
    Require(versions.Count == 2 && versions.All(x => (int)x["Versions"] == 1), "Retry/rename duplicated schedule history.");
    var receipts = await Rows("SELECT ActorUserId,Operation,[Key],CompletedAt,ProtectedResultReference FROM workflow.IdempotencyReceipts WHERE [Key] LIKE N'huma-attendance-%b03-%' OR [Key] LIKE N'huma-attendance-b04-%' ORDER BY [Key];");
    Require(receipts.Count == 4 && receipts.All(x => x["CompletedAt"] is not DBNull && x["ProtectedResultReference"] is string) &&
        receipts.Count(x => (string)x["Key"] == "huma-attendance-b04-same-key") == 1 &&
        receipts.Count(x => ((string)x["Key"]).StartsWith("huma-attendance-b04-race-", StringComparison.Ordinal)) == 1 &&
        receipts.All(x => !((string)x["Key"]).EndsWith("-stale", StringComparison.Ordinal)), "Duplicate/loser scheduling receipt persisted.");
    var audits = await Rows("SELECT a.EventType,a.ActorUserId,a.TargetId,a.ProtectedChangesReference FROM audit.AuditEvents a JOIN attendance.Shifts s ON a.TargetId=CONVERT(varchar(36),s.Id) WHERE s.Code IN (N'ATTHUMA13B03',N'ATTHUMA13B04') AND a.EventType IN (N'shifts.create',N'shifts.update') ORDER BY a.OccurredAt;");
    Require(audits.Count == 4 && audits.All(x => x["ProtectedChangesReference"] is string), "Business writes lack their protected atomic audit evidence.");
    var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(root, "Attendance-Host-20261013-B01.json")).Build();
    var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<IConfiguration>(configuration); services.AddInfrastructure(configuration);
    using var provider = services.BuildServiceProvider();
    var files = provider.GetRequiredService<IPrivateFileStore>();
    var readCount = 0;
    foreach (var receipt in receipts)
    {
        var reference = JsonSerializer.Deserialize<PrivateFileReference>((string)receipt["ProtectedResultReference"])!;
        var purpose = $"attendance:scheduling:{Guid.Parse((string)receipt["ActorUserId"]):N}:{receipt["Operation"]}:result";
        var bytes = await files.ReadAsync(reference, purpose, CancellationToken.None);
        using var body = JsonDocument.Parse(bytes);
        Require(shifts.Any(x => (Guid)x["Id"] == body.RootElement.GetProperty("Id").GetGuid()), "Protected receipt source identity differs.");
        readCount++;
        if (readCount == 1)
        {
            var rejected = false;
            try { await files.ReadAsync(reference, purpose + ":wrong", CancellationToken.None); }
            catch (AuthenticationFailure failure) when (failure.StatusCode == 503) { rejected = true; }
            Require(rejected, "Wrong-purpose encrypted evidence read was accepted.");
        }
    }
    foreach (var audit in audits)
    {
        var reference = JsonSerializer.Deserialize<PrivateFileReference>((string)audit["ProtectedChangesReference"])!;
        var bytes = await files.ReadAsync(reference, $"attendance:scheduling:{Guid.Parse((string)audit["ActorUserId"]):N}:{audit["EventType"]}:audit", CancellationToken.None);
        using var body = JsonDocument.Parse(bytes);
        Require(body.RootElement.GetProperty("ResourceId").GetGuid() == Guid.Parse((string)audit["TargetId"]), "Protected audit identity differs.");
        readCount++;
    }
    await NewJson(Path.Combine(root, "Attendance-Shift-Concurrency-20261013-B04-sql-proof.json"), new { Server = server, Database = "Cosmix_Testing",
        Shifts = 2, ScheduleVersions = 2, CommittedReceipts = 4, BusinessAudits = 4, ProtectedFilesRead = readCount,
        WrongPurposeRejected = true, AtUtc = DateTimeOffset.UtcNow });
    Console.WriteLine("PASS: actual SQL has two unique shifts, two schedule versions, four receipts and four protected business audits; eight encrypted files read in a separate process, wrong-purpose read rejected.");
    return;
}
if (mode == "--auth-evidence")
{
    var webPath = PrivateBootstrapSnapshotStore.RequirePrivatePath(Path.Combine(root, "Attendance-Web-Acceptance-20261013-B02-result.json"), false);
    using var document = JsonDocument.Parse(await File.ReadAllTextAsync(webPath));
    var start = document.RootElement.GetProperty("StartedAtUtc").GetDateTimeOffset();
    var end = document.RootElement.GetProperty("FinishedAtUtc").GetDateTimeOffset();
    await using var query = new SqlCommand("""
        SELECT a.LoginName,COUNT(*) AS Sessions,
            SUM(CASE WHEN s.Client=1 AND c.Purpose=N'WebLogin' AND c.ConsumedAt=s.CreatedAt AND d.Status=1 THEN 1 ELSE 0 END) AS VerifiedOtpSessions,
            SUM(CASE WHEN s.RevokedAt IS NOT NULL THEN 1 ELSE 0 END) AS RevokedSessions
        FROM auth.UserSessions s JOIN auth.UserAccounts a ON a.Id=s.UserAccountId
        LEFT JOIN auth.MobileOtpChallenges c ON c.Id=s.SmsOtpChallengeId
        LEFT JOIN auth.OtpDeliveries d ON d.ChallengeId=c.Id
        WHERE s.CreatedAt>=@start AND s.CreatedAt<=@end GROUP BY a.LoginName ORDER BY a.LoginName;
        """, connection);
    query.Parameters.Add("@start", SqlDbType.DateTimeOffset).Value = start;
    query.Parameters.Add("@end", SqlDbType.DateTimeOffset).Value = end;
    var evidence = new List<object>();
    await using (var reader = await query.ExecuteReaderAsync())
    {
        var i = 0;
        while (await reader.ReadAsync())
        {
            Require(i < 3 && reader.GetString(0) == expectedAccounts[i].Item1 && reader.GetInt32(1) == 1 &&
                reader.GetInt32(2) == 1 && reader.GetInt32(3) == 1, "Persisted MFA/session/revocation evidence differs from HTTP run.");
            evidence.Add(new { Actor = expectedAccounts[i].Item1.Split('@')[0], Sessions = 1, VerifiedOtpSessions = 1, RevokedSessions = 1 });
            i++;
        }
        Require(i == 3, "Missing persisted synthetic MFA sessions.");
    }
    var indexes = await Rows("SELECT COUNT(*) AS VerifiedIndexes FROM sys.indexes WHERE object_id=OBJECT_ID('hr.LeaveRequests') AND is_unique=1 AND has_filter=1 AND name IN ('IX_LeaveRequests_EmployeeId_SubmissionKey','IX_LeaveRequests_PreviousRequestId');");
    Require((int)indexes[0]["VerifiedIndexes"] == 2, "Real filtered leave index protections are missing.");
    await NewJson(Path.Combine(root, "Attendance-Web-Acceptance-20261013-B02-sql-proof.json"), new { Server = server, Database = "Cosmix_Testing",
        Start = start, End = end, Evidence = evidence, FilteredLeaveIndexes = 2, AtUtc = DateTimeOffset.UtcNow });
    Console.WriteLine("PASS: real SQL persistence confirms exactly three OTP-backed web sessions and three revocations for B02; both unique filtered leave indexes verified.");
    return;
}
if (mode is not ("--upgrade" or "--continue-reviewed-19-21")) { Console.WriteLine("Read-only SQL inspection passed; no migration or fixture write."); return; }

PrivateBootstrapSnapshotStore.RequirePrivatePath(root, true);
var backup = @"C:\Users\Huma.Khan\Cosmix_Testing_Attendance_20261013_B01.bak";
var originalIntent = Path.Combine(root, "Attendance-SQL-Upgrade-20261013-B01-intent.json");
var intent = continuation ? Path.Combine(root, "Attendance-SQL-Continuation-19-21-20261013-B01-intent.json") : originalIntent;
var result = Path.Combine(root, continuation ? "Attendance-SQL-Continuation-19-21-20261013-B01-result.json" : "Attendance-SQL-Upgrade-20261013-B01-result.json");
Require((continuation ? File.Exists(backup) && File.Exists(originalIntent) : !File.Exists(backup)) && !File.Exists(intent) && !File.Exists(result),
    "Owned attempt state differs; no automatic retry or overwrite.");
var physicalFiles = await Rows("SELECT physical_name AS Path FROM sys.database_files ORDER BY file_id;");
Require(physicalFiles.Count == 2 && (string)physicalFiles[0]["Path"] == @"C:\Users\Huma.Khan\Cosmix_Testing.mdf" &&
    (string)physicalFiles[1]["Path"] == @"C:\Users\Huma.Khan\Cosmix_Testing_log.ldf", "Existing physical files differ from inspected ownership.");
for (var parent = Path.GetDirectoryName(backup); parent is not null; parent = Path.GetDirectoryName(parent))
    Require((File.GetAttributes(parent) & FileAttributes.ReparsePoint) == 0, "Linked backup ancestry is forbidden.");
if (!continuation) await Command("BACKUP DATABASE [Cosmix_Testing] TO DISK=@path WITH COPY_ONLY,CHECKSUM,NOFORMAT,NOINIT,NAME=N'Cosmix Attendance 20261013 B01';", backup);
var header = await Rows("RESTORE HEADERONLY FROM DISK=@path;", backup);
Require(header.Count == 1 && (string)header[0]["DatabaseName"] == "Cosmix_Testing" &&
    (string)header[0]["BackupName"] == "Cosmix Attendance 20261013 B01" && (bool)header[0]["IsCopyOnly"] &&
    (bool)header[0]["HasBackupChecksums"] && !(bool)header[0]["IsDamaged"], "Backup header verification failed.");
await Command("RESTORE VERIFYONLY FROM DISK=@path WITH FILE=1,CHECKSUM;", backup);
// Protect only this newly created backup; existing data files/directories and key ACLs are unchanged.
var backupAcl = new FileSecurity();
var currentSid = WindowsIdentity.GetCurrent().User!;
backupAcl.SetOwner(currentSid);
backupAcl.SetAccessRuleProtection(true, false);
foreach (var sid in new[] { currentSid.Value, "S-1-5-18", "S-1-5-32-544" })
    backupAcl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid), FileSystemRights.FullControl, AccessControlType.Allow));
if (!continuation) new FileInfo(backup).SetAccessControl(backupAcl);
var retainedAcl = new FileInfo(backup).GetAccessControl();
Require(retainedAcl.GetOwner(typeof(SecurityIdentifier))?.Value == currentSid.Value, "Backup owner mismatch.");
foreach (FileSystemAccessRule rule in retainedAcl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
    Require(rule.AccessControlType != AccessControlType.Allow ||
        new[] { currentSid.Value, "S-1-5-18", "S-1-5-32-544" }.Contains(rule.IdentityReference.Value), "Backup permissions are not private.");
string backupHash;
await using (var backupFile = File.OpenRead(backup)) backupHash = Convert.ToHexString(await SHA256.HashDataAsync(backupFile));
if (continuation)
{
    PrivateBootstrapSnapshotStore.RequirePrivatePath(originalIntent, false);
    using var retained = JsonDocument.Parse(await File.ReadAllTextAsync(originalIntent));
    var proof = retained.RootElement;
    Require(proof.GetProperty("Server").GetString() == server && proof.GetProperty("Database").GetString() == "Cosmix_Testing" &&
        proof.GetProperty("RestoreVerified").GetBoolean() && proof.GetProperty("BackupSha256").GetString() == backupHash &&
        proof.GetProperty("IdentityProof").GetString() == employeeProof, "Retained backup/original identity evidence mismatch.");
    var originalCounts = proof.GetProperty("TableCounts").EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetInt64());
    Require(originalCounts.Where(x => x.Key != "dbo.__EFMigrationsHistory").All(x => beforeCounts.TryGetValue(x.Key, out var count) && count == x.Value) &&
        beforeCounts.Where(x => !originalCounts.ContainsKey(x.Key)).All(x => x.Value == 0), "Partial upgrade changed business data.");
    Require((await Rows("SELECT COL_LENGTH('hr.LeaveRequests','SubmissionKey') AS SubmissionKey,COL_LENGTH('hr.LeaveRequests','PreviousRequestId') AS PreviousRequestId;"))
        .All(x => x.Values.All(y => y is DBNull)), "Migration 19 has unrecorded partial columns; explicit reconciliation required.");
}
await NewJson(intent, new { Source = pipe, Server = server, Owner = owner, Database = "Cosmix_Testing", Backup = backup,
    BackupSha256 = backupHash, RestoreVerified = true, History = history, TableCounts = beforeCounts, IdentityProof = employeeProof,
    PendingSqlSha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(reviewed))), Batches = batches.Length,
    Approval = "Human instruction 13 October: use existing Cosmix_Testing synthetic data for tests; preserve existing data.", AtUtc = DateTimeOffset.UtcNow });
Console.WriteLine("COPY_ONLY checksummed backup passed HEADERONLY and RESTORE VERIFYONLY; retained privately.");
Require((await Counts()).OrderBy(x => x.Key).SequenceEqual(beforeCounts.OrderBy(x => x.Key)) && await ExistingIdentityProof() == employeeProof,
    "Source changed during backup; stop before migration.");
await VerifyIdentity();
foreach (var batch in batches) await Command(batch);
var afterHistory = await Rows("SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;");
Require(afterHistory.Count == 21 && afterHistory.Select(x => (string)x["MigrationId"]).SequenceEqual(local.Select(x => x.Key)), "Post-migration history mismatch.");
Require(afterHistory.Take(expectedCount).Select(x => (string)x["ProductVersion"]).SequenceEqual(history.Select(x => (string)x["ProductVersion"])), "Applied history was changed.");
var afterCounts = await Counts();
Require(beforeCounts.Where(x => x.Key != "dbo.__EFMigrationsHistory").All(x => afterCounts.TryGetValue(x.Key, out var count) && count == x.Value), "Existing table rows changed during schema upgrade.");
Require(afterCounts.Where(x => !beforeCounts.ContainsKey(x.Key)).All(x => x.Value == 0), "Migration unexpectedly seeded a new business table.");
Require(await ExistingIdentityProof() == employeeProof, "Existing employee/account identity or version changed.");
await VerifyIdentity();
await NewJson(result, new { Server = server, Database = "Cosmix_Testing", AppliedMigrations = 21, Backup = backup, BackupSha256 = backupHash,
    ExistingRowsPreserved = true, NewBusinessTablesEmpty = true, IdentityProof = employeeProof, History = afterHistory, TableCounts = afterCounts,
    CompletedBatches = batches.Length, AtUtc = DateTimeOffset.UtcNow });
Console.WriteLine($"PASS: migrations {expectedCount + 1}-21 applied; all existing business row counts and employee/account identities preserved; no fixtures seeded.");

async Task VerifyIdentity()
{
    var rows = await Rows("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS ServerName,DB_NAME() AS Catalog,ORIGINAL_LOGIN() AS OperatorName,SUSER_SNAME(d.owner_sid) AS OwnerName,d.is_read_only AS ReadOnly,(SELECT COUNT(*) FROM sys.synonyms) AS Synonyms,(SELECT COUNT(*) FROM sys.sql_expression_dependencies WHERE referenced_server_name IS NOT NULL OR referenced_database_name IS NOT NULL) AS ExternalDependencies FROM sys.databases d WHERE d.database_id=DB_ID();");
    Require(rows.Count == 1 && (string)rows[0]["ServerName"] == server && (string)rows[0]["Catalog"] == "Cosmix_Testing" &&
        (string)rows[0]["OperatorName"] == owner && (string)rows[0]["OwnerName"] == owner && !(bool)rows[0]["ReadOnly"] &&
        (int)rows[0]["Synonyms"] == 0 && (int)rows[0]["ExternalDependencies"] == 0, "Existing target identity/indirect dependency mismatch.");
}
async Task<List<Dictionary<string, object>>> Rows(string text, string? path = null)
{
    await using var command = new SqlCommand(text, connection) { CommandTimeout = 180 };
    if (path is not null) command.Parameters.Add("@path", SqlDbType.NVarChar, 1024).Value = path;
    await using var reader = await command.ExecuteReaderAsync();
    var rows = new List<Dictionary<string, object>>();
    while (await reader.ReadAsync())
    {
        var row = new Dictionary<string, object>();
        for (var i = 0; i < reader.FieldCount; i++) row.Add(reader.GetName(i), reader.GetValue(i));
        rows.Add(row);
    }
    return rows;
}
async Task Command(string text, string? path = null)
{
    await using var command = new SqlCommand(text, connection) { CommandTimeout = 180 };
    if (path is not null) command.Parameters.Add("@path", SqlDbType.NVarChar, 1024).Value = path;
    await command.ExecuteNonQueryAsync();
}
async Task<Dictionary<string, long>> Counts()
{
    var tables = await Rows("SELECT s.name AS SchemaName,t.name AS TableName FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name;");
    var counts = new Dictionary<string, long>();
    foreach (var table in tables)
    {
        var schema = (string)table["SchemaName"]; var name = (string)table["TableName"];
        var rows = await Rows("SELECT COUNT_BIG(*) AS Count FROM [" + schema.Replace("]", "]]") + "].[" + name.Replace("]", "]]") + "];");
        counts.Add(schema + "." + name, (long)rows[0]["Count"]);
    }
    return counts;
}
async Task<string> ExistingIdentityProof()
{
    var rows = await Rows("SELECT CONVERT(varchar(36),Id) AS Id,CONVERT(varchar(18),RowVersion,1) AS Version,Status,OnboardingStatus FROM hr.Employees ORDER BY Id;");
    var accounts = await Rows("SELECT CONVERT(varchar(36),Id) AS Id,CONVERT(varchar(36),EmployeeId) AS EmployeeId,CONVERT(varchar(18),RowVersion,1) AS Version FROM auth.UserAccounts ORDER BY Id;");
    return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Employees = rows, Accounts = accounts })));
}
async Task NewJson(string path, object value)
{
    await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    await JsonSerializer.SerializeAsync(file, value, new JsonSerializerOptions { WriteIndented = true });
    await file.FlushAsync();
    PrivateBootstrapSnapshotStore.RequirePrivatePath(path, false);
}
static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
