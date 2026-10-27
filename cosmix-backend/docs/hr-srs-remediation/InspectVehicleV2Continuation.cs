using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.Files;
using Microsoft.Data.SqlClient;

// Fixed synthetic corpus only. No EF context, host, transaction, or SQL write API.
const string root = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261026V2";
const string repo = @"C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend";
const string marker = "TEST-HR-V2-20261026";
const string serviceId = "f31f46a9-3bf3-4bd4-b34e-61125bc27e11";
const string hr = "8475187f-bf3b-4f9c-a3ed-4bac32253005";
const string admin = "94a83b62-5b56-4450-9175-2a7a9e3261bf";
const string employee = "35d19922-70a8-4c80-8578-e1029360e9fc";
var started = DateTimeOffset.UtcNow;
string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
JsonElement Json(string text) { using var doc = JsonDocument.Parse(text); return doc.RootElement.Clone(); }
string Q(string identifier) => "[" + identifier.Replace("]", "]]") + "]";
void NoLinks(string path)
{
    for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "Linked path refused.");
}
SortedDictionary<string, object> Inventory()
{
    NoLinks(root);
    var rows = new SortedDictionary<string, object>(StringComparer.Ordinal);
    void Walk(string directory)
    {
        foreach (var item in new DirectoryInfo(directory).EnumerateFileSystemInfos().OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            Require((item.Attributes & FileAttributes.ReparsePoint) == 0, "Linked old-root member refused.");
            var relative = Path.GetRelativePath(root, item.FullName);
            if (item is DirectoryInfo child) { rows.Add(relative, new { Directory = true, Attributes = (int)item.Attributes }); Walk(child.FullName); }
            else { var file = (FileInfo)item; rows.Add(relative, new { Directory = false, file.Length, Attributes = (int)file.Attributes, Sha256 = Hash(File.ReadAllBytes(file.FullName)) }); }
        }
    }
    Walk(root); return rows;
}
var oldBefore = Inventory();
System.Runtime.InteropServices.NativeLibrary.Load(@"D:\CosmixVerification\hr-backend-20261026\v2-final-build02\bin\Cosmix.Api\release\runtimes\win-x64\native\Microsoft.Data.SqlClient.SNI.dll");
await using var connection = new SqlConnection(@"Server=np:\\.\pipe\LOCALDB#8C03C2BC\tsql\query;Database=Cosmix_Testing;Integrated Security=true;Encrypt=false;Pooling=false;Connect Timeout=30");
await connection.OpenAsync();
async Task<JsonElement> Select(string sql)
{
    Require(sql.StartsWith("SELECT ", StringComparison.Ordinal), "Only SELECT is supported.");
    await using var command = connection.CreateCommand(); command.CommandText = sql; command.CommandTimeout = 30;
    await using var reader = await command.ExecuteReaderAsync();
    var text = new StringBuilder(); while (await reader.ReadAsync()) text.Append(reader.GetString(0));
    return Json(text.Length == 0 ? "[]" : text.ToString());
}
async Task<JsonElement> Identity() => await Select("""
SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS Server, DB_NAME() AS Catalog,
 CONVERT(int,SERVERPROPERTY('ProcessID')) AS Pid, ORIGINAL_LOGIN() AS Actor,
 SUSER_SNAME(d.owner_sid) AS Owner,r.family_guid AS Family,d.state_desc AS State,d.user_access_desc AS Access,d.is_read_only AS IsReadOnly,
 (SELECT COUNT(*) FROM sys.triggers WHERE is_disabled=0 AND is_ms_shipped=0) AS ActiveTriggers,
 (SELECT COUNT(*) FROM sys.synonyms) AS Synonyms,
 (SELECT COUNT(*) FROM sys.sql_expression_dependencies WHERE referenced_server_name IS NOT NULL OR referenced_database_name IS NOT NULL) AS ExternalDependencies
 FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id
 WHERE d.database_id=DB_ID() FOR JSON PATH,INCLUDE_NULL_VALUES
""");
var target = (await Identity()).EnumerateArray().Single();
Require(target.GetProperty("Server").GetString() == @"Software-PC1\LOCALDB#8C03C2BC" && target.GetProperty("Catalog").GetString() == "Cosmix_Testing" &&
    target.GetProperty("Pid").GetInt32() == 22112 && target.GetProperty("Actor").GetString() == @"COSMIXRND\Huma.Khan" &&
    target.GetProperty("Owner").GetString() == @"COSMIXRND\Huma.Khan" && target.GetProperty("Family").GetGuid() == Guid.Parse("D9C34627-18A2-41CF-A804-821E39516303") &&
    target.GetProperty("State").GetString() == "ONLINE" && target.GetProperty("Access").GetString() == "MULTI_USER" && !target.GetProperty("IsReadOnly").GetBoolean() &&
    new[] { "ActiveTriggers", "Synonyms", "ExternalDependencies" }.All(x => target.GetProperty(x).GetInt32() == 0), "Wrong SQL target or dependencies.");
var databaseFiles = await Select("SELECT physical_name,state,type FROM sys.database_files ORDER BY file_id FOR JSON PATH");
Require(databaseFiles.GetArrayLength() == 2 && databaseFiles[0].GetProperty("physical_name").GetString() == @"C:\Users\Huma.Khan\Cosmix_Testing.mdf" &&
    databaseFiles[1].GetProperty("physical_name").GetString() == @"C:\Users\Huma.Khan\Cosmix_Testing_log.ldf" &&
    databaseFiles.EnumerateArray().All(x => x.GetProperty("state").GetInt32() == 0), "Wrong database files.");
var transport = await Select("SELECT net_transport FROM sys.dm_exec_connections WHERE session_id=@@SPID FOR JSON PATH");
Require(transport[0].GetProperty("net_transport").GetString() == "Named pipe", "Wrong transport.");
var migrations = await Select("SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId FOR JSON PATH");
var sourceMigrations = Directory.GetFiles(Path.Combine(repo, "src/Cosmix.Infrastructure/Database/Migrations"), "*.cs")
    .Select(Path.GetFileNameWithoutExtension).Where(x => Regex.IsMatch(x!, @"^\d{14}_[^.]+$")).OrderBy(x => x, StringComparer.Ordinal).ToArray();
Require(sourceMigrations.Length == 26 && migrations.EnumerateArray().Select(x => x.GetProperty("MigrationId").GetString()).SequenceEqual(sourceMigrations), "Exact 26 migrations differ.");

// Complete row hashes include every column. No secret-bearing non-corpus values leave SQL.
var tables = await Select("SELECT s.name AS [Schema],t.name AS [Table],t.object_id AS ObjectId FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name FOR JSON PATH");
async Task<SortedDictionary<string, JsonElement>> Memberships()
{
    var result = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
    foreach (var table in tables.EnumerateArray())
    {
        var schema = table.GetProperty("Schema").GetString()!; var name = table.GetProperty("Table").GetString()!;
        var pk = await Select($"SELECT c.name FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id={table.GetProperty("ObjectId").GetInt32()} AND i.is_primary_key=1 ORDER BY ic.key_ordinal FOR JSON PATH");
        Require(pk.GetArrayLength() > 0, "Missing primary key.");
        var columns = string.Join(",", pk.EnumerateArray().Select(x => "t." + Q(x.GetProperty("name").GetString()!)));
        var members = await Select($"SELECT JSON_QUERY((SELECT {columns} FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)) AS [Key],CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT t.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)),2) AS Sha256 FROM {Q(schema)}.{Q(name)} t ORDER BY {columns} FOR JSON PATH");
        result.Add(schema + "." + name, members);
    }
    return result;
}
var before = await Memberships();
var vehicle = (await Select($"SELECT * FROM hr.Vehicles WHERE RegistrationNumber='{marker}' FOR JSON PATH,INCLUDE_NULL_VALUES")).EnumerateArray().Single();
var vehicleId = vehicle.GetProperty("Id").GetGuid();
Require(vehicleId == Guid.Parse("2349b065-e018-4a1c-9917-0808ea4e634b"), "Unexpected vehicle.");
var services = await Select("SELECT * FROM hr.VehicleServiceRecords ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES");
Require(services.GetArrayLength() == 1 && services[0].GetProperty("Id").GetGuid() == Guid.Parse(serviceId), "Expected exactly one maintenance record.");
var service = services[0]; var headerId = service.GetProperty("ApprovalRequestId").GetGuid();
Require(service.GetProperty("VehicleId").GetGuid() == vehicleId && service.GetProperty("EmployeeId").GetGuid() == Guid.Parse(employee), "Wrong pending binding.");
var header = (await Select($"SELECT * FROM workflow.ApprovalRequests WHERE Id='{headerId:D}' FOR JSON PATH,INCLUDE_NULL_VALUES")).EnumerateArray().Single();
Require(header.GetProperty("State").GetInt32() == 0, "Pending service already transitioned.");
var decisions = await Select($"SELECT * FROM workflow.ApprovalDecisions WHERE ApprovalRequestId='{headerId:D}' FOR JSON PATH,INCLUDE_NULL_VALUES");
Require(decisions.GetArrayLength() == 0, "Unexpected service decision.");
var receipts = await Select($"SELECT * FROM workflow.IdempotencyReceipts WHERE [Key] LIKE '{marker}%' ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES");
Require(receipts.GetArrayLength() == 2, "Unexpected V2 receipt population.");
var receipt = receipts.EnumerateArray().Single(x => x.GetProperty("Key").GetString() == marker + "-service-Tuning-2026-09-17");
Require(receipt.GetProperty("ActorUserId").GetString() == hr && receipt.GetProperty("Operation").GetString() == "vehicle.service.submit" &&
    receipt.GetProperty("CompletedAt").ValueKind == JsonValueKind.String && receipt.GetProperty("ExpiresAt").GetDateTimeOffset() > started, "Missing or expired completed submission receipt.");
Require(!receipts.EnumerateArray().Any(x => x.GetProperty("Operation").GetString() == "vehicle.service.approve"), "Approval receipt exists.");
var proposalTypes = new[] { "vehicle.trip.attribution", "vehicle.trip.attribution-correction", "vehicle.fuel.reconciliation", "vehicle.monthly-adjustment" };
var proposals = await Select("SELECT Id,RequestType,SubjectId,State FROM workflow.ApprovalRequests WHERE RequestType IN('vehicle.trip.attribution','vehicle.trip.attribution-correction','vehicle.fuel.reconciliation','vehicle.monthly-adjustment') ORDER BY Id FOR JSON PATH");
Require(proposals.GetArrayLength() == 0, "Unexpected vehicle proposals.");
var approvalReceipts = await Select("SELECT Id FROM workflow.IdempotencyReceipts WHERE Operation='vehicle.service.approve' FOR JSON PATH");
Require(approvalReceipts.GetArrayLength() == 0, "Service approval receipt exists outside the old key namespace.");
string FixtureId(int n) => $"26102602-{n:0000}-4000-8000-{n:000000000000}";
var sourceTables = new[] { "hr.Policies", "hr.PolicyVersions", "hr.PolicyVersions", "hr.PolicyVersions", "hr.VehicleCustodies", "hr.VehicleTrips", "hr.VehicleMileageEntries", "hr.FuelClaims", "workflow.ApprovalRequests", "workflow.ApprovalRequests", "workflow.ApprovalDecisions", "workflow.ApprovalDecisions" };
var sources = new List<object>(); var roots = new List<JsonElement> { vehicle, service, header, receipts };
for (var i = 1; i <= 12; i++)
{
    var table = sourceTables[i - 1]; var row = (await Select($"SELECT * FROM {table} WHERE Id='{FixtureId(i)}' FOR JSON PATH,INCLUDE_NULL_VALUES")).EnumerateArray().Single();
    sources.Add(new { Table = table, Id = FixtureId(i), Values = row, RowSha256 = before[table].EnumerateArray().Single(x => x.GetProperty("Key").GetProperty("Id").GetGuid() == Guid.Parse(FixtureId(i))).GetProperty("Sha256").GetString() });
    roots.Add(row);
}
Require(before["hr.VehicleTrips"].GetArrayLength() == 1 && before["hr.VehicleMileageEntries"].GetArrayLength() == 1 && before["hr.FuelClaims"].GetArrayLength() == 1, "Unexpected vehicle source additions.");
var audits = await Select($"SELECT Id,EventType,ActorUserId,ActorRoleCode,TargetType,TargetId,OccurredAt,EmployeeScopeId,ProtectedChangesReference FROM audit.AuditEvents WHERE TargetId IN('{serviceId}','{vehicleId:D}') ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES");
roots.Add(audits);
Console.WriteLine($"Vehicle={vehicleId:D}; service={serviceId}; header={headerId:D}; state=Pending; serviceVersion={service.GetProperty("RowVersion").GetString()}; headerVersion={header.GetProperty("RowVersion").GetString()}");
Console.WriteLine($"SubmissionReceipt={receipt.GetProperty("Id").GetString()}; key={receipt.GetProperty("Key").GetString()}; expires={receipt.GetProperty("ExpiresAt").GetString()}; sourceRows=12; serviceDecisions=0; approvalReceipts=0; proposals=0.");

using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "config/host.json")));
var options = settings.RootElement.GetProperty("PrivateFiles").Deserialize<PrivateFileStorageOptions>()!;
Require(options.Directory == root + @"\files" && options.KeyFile == root + @"\keys\files.key" && options.KeyReference == "hr-vehicle-v2", "Wrong private store.");
var files = new EncryptedPrivateFileStore(options);
var purposes = new HashSet<string>(StringComparer.Ordinal) { "test:vehicle:v2:corpus", $"vehicle:trip:{Guid.Parse(FixtureId(6)):N}", $"vehicle:mileage:{Guid.Parse(FixtureId(6)):N}", $"vehicle:fuel:{Guid.Parse(FixtureId(6)):N}", $"vehicle:fuel:claim:{Guid.Parse(FixtureId(8)):N}", $"vehicle:service:{vehicleId:N}" };
foreach (var r in receipts.EnumerateArray())
{
    Require(r.GetProperty("ActorUserId").GetString() is hr or admin, "Unexpected receipt actor.");
    Require(r.GetProperty("Operation").GetString() is "vehicle.service.submit" or "vehicle.create", "Unexpected receipt operation.");
    foreach (var kind in new[] { "result", "audit" }) purposes.Add($"attendance:operation:{Guid.Parse(r.GetProperty("ActorUserId").GetString()!):N}:{r.GetProperty("Operation").GetString()}:{kind}");
}
var pending = new Queue<PrivateFileReference>(); var references = new Dictionary<string, PrivateFileReference>(StringComparer.Ordinal);
void Discover(JsonElement value)
{
    if (value.ValueKind == JsonValueKind.Object)
    {
        if (value.TryGetProperty("StorageKey", out _) && value.TryGetProperty("ObjectVersion", out _))
        {
            var reference = value.Deserialize<PrivateFileReference>()!;
            Require(reference.KeyReference == "hr-vehicle-v2", "Unexpected evidence key label.");
            if (references.TryGetValue(reference.StorageKey, out var previous)) Require(previous == reference, "Conflicting retained reference.");
            else { references.Add(reference.StorageKey, reference); pending.Enqueue(reference); }
        }
        else foreach (var property in value.EnumerateObject()) Discover(property.Value);
    }
    else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) Discover(item);
    else if (value.ValueKind == JsonValueKind.String && value.GetString()!.TrimStart().StartsWith('{'))
    { try { var nested = Json(value.GetString()!); Discover(nested); } catch (JsonException) { } }
}
foreach (var value in roots) Discover(value);
var allowlist = new List<object>(); JsonElement? savedRequest = null; JsonElement? savedResult = null;
while (pending.TryDequeue(out var reference))
{
    byte[]? plaintext = null; string? authenticatedPurpose = null;
    foreach (var purpose in purposes.OrderBy(x => x, StringComparer.Ordinal))
    {
        try { plaintext = await files.ReadAsync(reference, purpose, CancellationToken.None); authenticatedPurpose = purpose; break; }
        catch (Cosmix.Application.Authentication.AuthenticationFailure ex) when (ex.Code == "private_file_storage_unavailable") { }
    }
    Require(plaintext is not null && authenticatedPurpose is not null, "Reachable evidence did not authenticate with a source-derived purpose.");
    try
    {
        var document = Json(Encoding.UTF8.GetString(plaintext!)); Discover(document);
        if (authenticatedPurpose == $"attendance:operation:{Guid.Parse(hr):N}:vehicle.service.submit:audit") savedRequest = document.GetProperty("request").Clone();
        if (authenticatedPurpose == $"attendance:operation:{Guid.Parse(hr):N}:vehicle.service.submit:result") savedResult = document.Clone();
        var path = Path.Combine(options.Directory, reference.StorageKey); NoLinks(path);
        allowlist.Add(new { Reference = reference, Purpose = authenticatedPurpose, CiphertextLength = new FileInfo(path).Length, CiphertextSha256 = Hash(File.ReadAllBytes(path)) });
    }
    finally { CryptographicOperations.ZeroMemory(plaintext!); }
}
Require(savedRequest.HasValue && savedResult.HasValue, "Submission request/result evidence unavailable.");
Require(Hash(Encoding.UTF8.GetBytes(savedRequest!.Value.GetRawText())) == receipt.GetProperty("RequestSha256").GetString(), "Saved request does not match receipt digest.");
var payload = savedRequest.Value.GetProperty("payload");
var body = payload.EnumerateObject().ToDictionary(x => char.ToLowerInvariant(x.Name[0]) + x.Name[1..], x => x.Value.Clone(), StringComparer.Ordinal);
var sourceFiles = Directory.GetFiles(options.Directory).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToArray();
var orphans = sourceFiles.Where(x => !references.ContainsKey(x!)).ToArray();
var after = await Memberships();
Require(JsonSerializer.Serialize(before) == JsonSerializer.Serialize(after), "SQL membership/value drift during inspection.");
Require(JsonElement.DeepEquals(target, (await Identity()).EnumerateArray().Single()), "SQL identity changed.");
var oldAfter = Inventory(); Require(JsonSerializer.Serialize(oldBefore) == JsonSerializer.Serialize(oldAfter), "Old-root membership/bytes/attributes changed.");
using var failure = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "results/failure.json")));
var failedApproval = failure.RootElement.GetProperty("HttpResults").EnumerateArray().Single(x => x.GetProperty("Path").GetString() == $"/api/v1/hr/vehicles/maintenance/{serviceId}/approve").Clone();
var report = new
{
    Schema = "vehicle-v2-continuation-readonly-v1", StartedUtc = started, CompletedUtc = DateTimeOffset.UtcNow,
    Target = target, DatabaseFiles = databaseFiles, Migrations = migrations,
    Vehicle = vehicle, PendingService = service, PendingHeader = header, SubmissionReceipt = receipt,
    SavedApplicationRequest = savedRequest, ReplayHttpBody = body,
    RequestBodyProvenance = "Authenticated audit request matches saved receipt SHA256; HTTP property ordering/whitespace was not retained.",
    ApprovalKey = marker + "-service-Tuning-2026-09-17-approve", ApprovalBody = new { }, FailedApproval = failedApproval,
    ApprovalKeyProvenance = "Original runner deterministic suffix; HTTP log retains route/status, not key/body bytes.",
    ServiceDecisionCount = decisions.GetArrayLength(), ApprovalReceiptCount = 0, PendingMaintenanceCount = 1,
    ProposalTable = "workflow.ApprovalRequests", ProposalTypes = proposalTypes, ProposalCount = proposals.GetArrayLength(), Sources = sources, Receipts = receipts, BusinessAudits = audits,
    OldReferencePurposeAllowlist = allowlist, ReachableObjectCount = references.Count, EncryptedDirectoryMembers = sourceFiles,
    UnreferencedObjects = orphans, OldRootInventory = oldBefore, OldRootUnchanged = true,
    SqlTableMemberships = before, SqlTableCount = before.Count, SqlRowsUnchangedDuringInspection = true,
    RowHashEncoding = "SQL HASHBYTES SHA2_256 over UTF-16LE FOR JSON PATH INCLUDE_NULL_VALUES WITHOUT_ARRAY_WRAPPER, physical column order; all columns included.",
    DatabaseWrites = false, PrivateWrites = false, HostStarted = false,
    Limitations = new[] { "Two equal SELECT passes, not a transaction-wide snapshot.", "No earlier original-run values are inferred; this manifest establishes the continuation baseline only.", "No auth cookies, tokens, keys, credentials or decrypted result/snapshot corpus exported.", "No ACL verification claim; private store validates paths during authenticated reads." }
};
var destination = Path.Combine(@"D:\CosmixVerification", "vehicle-v2-continuation-" + Guid.NewGuid().ToString("N") + ".json");
NoLinks(Path.GetDirectoryName(destination)!);
var bytes = JsonSerializer.SerializeToUtf8Bytes(report, new JsonSerializerOptions { WriteIndented = true });
await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { await output.WriteAsync(bytes); output.Flush(true); }
Console.WriteLine($"Manifest={destination}; SHA256={Hash(bytes)}; tables={before.Count}; migrations={migrations.GetArrayLength()}; reachableObjects={references.Count}; encryptedMembers={sourceFiles.Length}; unreferenced={orphans.Length}; SQL/private writes=0.");
