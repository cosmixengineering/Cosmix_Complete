#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;

// Offline review only. The migration owner prepares generated SQL separately; this runner cannot apply it.
const string baseline="20261013121357_CompleteHrFoundationPartA";
if(args.Any(x=>x!="--write-reviewed-sql"))throw new Exception("Unsupported offline review argument.");
var guard=new NoMigrationConnections();
using var db=new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=migration-review.invalid;Database=NeverConnect;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
var checks=0;
void Check(bool ok,string name) { if(!ok)throw new Exception(name);checks++; }
var assembly=db.GetService<IMigrationsAssembly>();
var chain=assembly.Migrations.ToArray();
Check(chain.Length==24 && chain[^2].Key==baseline && chain[^1].Key.EndsWith("_AddOfficeCaptureValidationAndOfflineZoneClaims",StringComparison.Ordinal),
    "Expected one later independent B24 migration after frozen A23");
Check(!db.Database.HasPendingModelChanges(),"Current model equals final B24 snapshot");
var previous=assembly.CreateMigration(chain[^2].Value,db.Database.ProviderName!);
var current=assembly.CreateMigration(chain[^1].Value,db.Database.ProviderName!);
var initializer=db.GetService<IModelRuntimeInitializer>();
var from=initializer.Initialize(previous.TargetModel,designTime:true);
var to=initializer.Initialize(current.TargetModel,designTime:true);
var expected=db.GetService<IMigrationsModelDiffer>().GetDifferences(from.GetRelationalModel(),to.GetRelationalModel());
Check(expected.Count==current.UpOperations.Count && expected.Select(x=>x.GetType()).SequenceEqual(current.UpOperations.Select(x=>x.GetType())),
    "Generated operations start from actual frozen A23 target, not an older or partially compiled snapshot");
var operations=current.UpOperations;
Check(!operations.Any(x=>x is DropTableOperation or DropColumnOperation or DeleteDataOperation or UpdateDataOperation or InsertDataOperation),
    "B migration has no destructive table/column operations or automatic data/permission seeds");
var tables=operations.OfType<CreateTableOperation>().Select(x=>$"{x.Schema}.{x.Name}").Order().ToArray();
Check(tables.SequenceEqual(new[] { "attendance.CaptureValidationFinalEvidence","attendance.CaptureValidationPolicyVersions","attendance.OfficeGeofenceVersions" }.Order()),
    "Only approved capture policy/final evidence/office boundary tables are added");
var columns=operations.OfType<AddColumnOperation>().Select(x=>$"{x.Table}.{x.Name}").Order().ToArray();
Check(columns.SequenceEqual(new[] {
    "RawPunchEvents.BranchId","RawPunchEvents.OfficeRosterAssignmentId","RawPunchEvents.OfficeGeofenceVersionId",
    "WorkSessions.BranchId","WorkSessions.OfficeRosterAssignmentId","WorkSessions.EmployeeAssignmentId",
    "WorkSessions.OfficeGeofenceVersionId","WorkSessions.HolidayCalendarId",
    "ZoneEvents.WrittenClaimPrivateReference","ZoneEvents.WrittenClaimExpiresAtUtc"
}.Order()),"Exact nullable office/claim delta; no unrelated HR columns");
Check(operations.OfType<AddColumnOperation>().All(x=>x.IsNullable),"Existing raw/session/zone rows remain representable without fabricated backfill");
Check(!operations.OfType<SqlOperation>().Any(),"No opaque B raw-SQL backfill or bypass");
var script=db.GetService<IMigrator>().GenerateScript(baseline,chain[^1].Key,MigrationsSqlGenerationOptions.Idempotent);
Check(script.Contains(chain[^1].Key) && !script.Contains("CREATE DATABASE",StringComparison.OrdinalIgnoreCase) &&
    !script.Contains("DROP DATABASE",StringComparison.OrdinalIgnoreCase) && !script.Contains("USE [",StringComparison.OrdinalIgnoreCase),
    "Incremental SQL has no target-switching/database creation or destructive database operation");
Check(script.Contains("[WrittenClaimPrivateReference]") && script.Contains("[OfficeGeofenceVersions]") && script.Contains("[CaptureValidationPolicyVersions]"),
    "Generated SQL includes actual approved model delta");
var down=db.GetService<IMigrator>().GenerateScript(chain[^1].Key,baseline);
Check(down.Contains("B24 downgrade refused") && down.IndexOf("B24 downgrade refused",StringComparison.Ordinal)<down.IndexOf("DROP TABLE",StringComparison.Ordinal),
    "Downgrade refuses loss of retained Attendance evidence before any destructive operation");
var sqlPath=Path.Combine("docs","sql",chain[^1].Key+".sql");
if(args.Contains("--write-reviewed-sql")) {
    if(File.Exists(sqlPath))Check(File.ReadAllText(sqlPath).Replace("\r\n","\n")==script.Replace("\r\n","\n"),"Existing generated SQL equals current reviewed output; never overwritten");
    else {
        using var file=new FileStream(sqlPath,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        file.Write(Encoding.UTF8.GetBytes(script));
    }
}
var frozen=new Dictionary<string,string> {
    ["src/Cosmix.Infrastructure/Database/Migrations/20261013121357_CompleteHrFoundationPartA.cs"]="377D2ED05117EF50B2A6F37F2E0FAFF64E15B00E42FDDAA0066D5689AAD04AA9",
    ["src/Cosmix.Infrastructure/Database/Migrations/20261013121357_CompleteHrFoundationPartA.Designer.cs"]="D17BFEFB4FD0A490FA623268667AD0B9404AF7393ADA04A63C08785CBDA3E02C",
    ["docs/sql/20261013121357_CompleteHrFoundationPartA.sql"]="787DB2C00217F6F9026CAA3AC56D9E5053CE71AA8925AE79304308133F8A1EF8"
};
foreach(var entry in frozen)Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(entry.Key)))==entry.Value,"Frozen file preserved: "+entry.Key);
Check(guard.Attempts==0,"No SQL connection while inspecting actual migrations/model/generated script");
Console.WriteLine($"PASS: {checks} B24 model/migration/source-preservation checks; {operations.Count} operations. Zero SQL connections/writes.");
Console.WriteLine("Reviewed later migration: "+chain[^1].Key);
if(File.Exists(sqlPath))Console.WriteLine("SQL SHA256: "+Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sqlPath))));
sealed class NoMigrationConnections:DbConnectionInterceptor {
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection,ConnectionEventData data,InterceptionResult result)
    {Attempts++;throw new Exception("SQL execution forbidden in B migration review");}
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,ConnectionEventData data,
        InterceptionResult result,CancellationToken ct=default)
    {Attempts++;throw new Exception("SQL execution forbidden in B migration review");}
}
