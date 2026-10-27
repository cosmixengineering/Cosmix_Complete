using System.Text.Json;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.Files;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.HR.VehicleFuel;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Reflection;

// Fixed, synthetic V2 diagnostic. SELECT and authenticated evidence reads only.
const string root = @"C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261026V2";
System.Runtime.InteropServices.NativeLibrary.Load(@"D:\CosmixVerification\hr-backend-20261026\v2-final-build02\bin\Cosmix.Api\release\runtimes\win-x64\native\Microsoft.Data.SqlClient.SNI.dll");
await using var connection = new SqlConnection(@"Server=np:\\.\pipe\LOCALDB#8C03C2BC\tsql\query;Database=Cosmix_Testing;Integrated Security=true;Encrypt=false;Pooling=false;Connect Timeout=5");
await connection.OpenAsync();
await using var command = connection.CreateCommand();
command.CommandText = "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),DB_NAME(),ORIGINAL_LOGIN(),CONVERT(varchar(36),family_guid) FROM sys.database_recovery_status WHERE database_id=DB_ID()";
await using (var identity = await command.ExecuteReaderAsync())
{
    if (!await identity.ReadAsync() || identity.GetString(0) != @"Software-PC1\LOCALDB#8C03C2BC" ||
        identity.GetString(1) != "Cosmix_Testing" || identity.GetString(2) != @"COSMIXRND\Huma.Khan" ||
        !identity.GetString(3).Equals("D9C34627-18A2-41CF-A804-821E39516303", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Wrong diagnostic target.");
}
command.CommandText = """
    SELECT s.VehicleId,s.EligibilityCalculationPrivateReference,s.OdometerKm,s.ApprovedOutstationKm,
      s.ClaimedAmount,a.State
    FROM hr.VehicleServiceRecords s JOIN hr.Vehicles v ON v.Id=s.VehicleId
    JOIN workflow.ApprovalRequests a ON a.Id=s.ApprovalRequestId
    WHERE s.Id='f31f46a9-3bf3-4bd4-b34e-61125bc27e11'
      AND s.EmployeeId='35d19922-70a8-4c80-8578-e1029360e9fc'
      AND v.RegistrationNumber='TEST-HR-V2-20261026'
    """;
Guid vehicle;
string serialized;
var values = new Dictionary<string, decimal>();
await using (var row = await command.ExecuteReaderAsync())
{
    if (!await row.ReadAsync() || row.GetInt32(5) != 0) throw new Exception("Expected pending synthetic service.");
    vehicle = row.GetGuid(0); serialized = row.GetString(1);
    values.Add("OdometerKm", row.GetDecimal(2));
    values.Add("ApprovedOutstationKm", row.GetDecimal(3));
    values.Add("ClaimedAmount", row.GetDecimal(4));
    if (await row.ReadAsync()) throw new Exception("Ambiguous synthetic record.");
}
using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(root + @"\config\host.json"));
var options = settings.RootElement.GetProperty("PrivateFiles").Deserialize<PrivateFileStorageOptions>()!;
if (options.Directory != root + @"\files" || options.KeyFile != root + @"\keys\files.key" || options.KeyReference != "hr-vehicle-v2")
    throw new Exception("Wrong private storage.");
var files = new EncryptedPrivateFileStore(options);
var reference = JsonSerializer.Deserialize<PrivateFileReference>(serialized)!;
using var evidence = JsonDocument.Parse(await files.ReadAsync(reference, $"vehicle:service:{vehicle:N}", CancellationToken.None));
foreach (var (name, value) in values)
{
    var retained = evidence.RootElement.GetProperty(name);
    Console.WriteLine($"{name}: retained={retained.GetRawText()}, SQL={JsonSerializer.Serialize(value)}, numericEqual={retained.GetDecimal() == value}");
}
await using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer(connection).AddInterceptors(new RefuseSave()).Options);
var repository = new VehicleFuelRepository(db);
var record = await repository.FindServiceRecordAsync(Guid.Parse("f31f46a9-3bf3-4bd4-b34e-61125bc27e11"), CancellationToken.None)
    ?? throw new Exception("Synthetic service disappeared.");
var operation = new AttendanceOperation(null!, null!, null!, null!, null!, TimeProvider.System, files);
var service = new VehicleFuelService(operation, repository, files, null!, null!, null!, null!, TimeProvider.System);
async Task<object> Evaluate(string method, params object?[] arguments)
{
    var task = (Task)typeof(VehicleFuelService).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(service, arguments)!;
    await task;
    return task.GetType().GetProperty("Result")!.GetValue(task)!;
}
var retainedObject = await Evaluate("ReadServiceEligibilityAsync", record, CancellationToken.None);
using var retainedJson = JsonDocument.Parse(JsonSerializer.Serialize(retainedObject));
var currentObject = await Evaluate("ServiceEligibilityAsync", record.EmployeeId,
    new SubmitServiceRecordRequest(record.VehicleId, record.ServiceKind, record.ServiceDate, record.OdometerKm,
        retainedJson.RootElement.GetProperty("ClaimedOutstationKm").GetDecimal(), record.AuthorizedWorkshopReference,
        record.EvidencePrivateReference, record.ClaimedAmount, record.Currency),
    "Motorcycle", CancellationToken.None, record.PolicyVersionId, null, null, null);
var currentJson = JsonSerializer.SerializeToElement(currentObject);
Console.WriteLine($"Real SQL eligibility: textEqual={JsonSerializer.Serialize(retainedObject) == JsonSerializer.Serialize(currentObject)}, structuralEqual={JsonElement.DeepEquals(retainedJson.RootElement, currentJson)}");
if (!JsonElement.DeepEquals(retainedJson.RootElement, currentJson)) throw new Exception("Other eligibility differences remain.");
var comparison = typeof(VehicleFuelService).GetMethod("SameServiceEligibility", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new Exception("Corrected comparison is missing from this build.");
if (comparison.Invoke(null, new[] { retainedObject, currentObject }) is not true)
    throw new Exception("Corrected comparison rejected the retained SQL evidence.");
Console.WriteLine("Corrected production comparison accepted numerically identical retained SQL evidence.");
Console.WriteLine("SELECT/private-read diagnostic completed; no SQL or private-file writes.");

sealed class RefuseSave : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData data, InterceptionResult<int> result) =>
        throw new InvalidOperationException("Diagnostic writes forbidden.");
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default) =>
        throw new InvalidOperationException("Diagnostic writes forbidden.");
}
