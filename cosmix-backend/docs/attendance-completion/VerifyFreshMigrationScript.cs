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

if (args.Any(x => x != "--prepare-reviewed-sql")) throw new Exception("Unsupported offline preparation argument.");
var checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
const string final = "20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence";
var guard = new NoFreshConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer(@"Server=np:\\.\pipe\LOCALDB#ABCDEF12\tsql\query;Database=Cosmix_Testing;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
Check(!db.Database.HasPendingModelChanges(), "Current model equals the preserved migration snapshot");
var assembly = db.GetService<IMigrationsAssembly>();
var migrations = assembly.Migrations.ToArray();
Check(migrations.Length == 21 && migrations[^1].Key == final, "Exact reviewed 21-migration chain");
var operations = migrations.SelectMany(x => assembly.CreateMigration(x.Value, db.Database.ProviderName!).UpOperations).ToArray();
Check(!operations.Any(x => x is InsertDataOperation or UpdateDataOperation or DeleteDataOperation), "Migrations do not seed business/authentication rows");
var raw = operations.OfType<SqlOperation>().ToArray();
Check(raw.Length == 12, "Exact forward raw-SQL inventory includes deferred leave-index compilation");
Check(raw.All(x => !x.SuppressTransaction), "No raw migration operation escapes its migration transaction");
var sql = db.GetService<IMigrator>().GenerateScript(Migration.InitialDatabase, final);
Check(sql.Contains("EXEC(N'CREATE UNIQUE INDEX [IX_LeaveRequests_EmployeeId_SubmissionKey]") &&
    sql.Contains("EXEC(N'CREATE UNIQUE INDEX [IX_LeaveRequests_PreviousRequestId]"),
    "Filtered leave indexes compile after new columns exist, not during the outer ALTER batch");
Check(sql.Contains("CREATE TABLE [auth].[UserAccounts]") && sql.Contains("CREATE TABLE [attendance].[AttendancePayrollAttestations]"),
    "From-zero artifact includes initial auth and latest attendance evidence tables");
Check(sql.Split("INSERT INTO [__EFMigrationsHistory]", StringSplitOptions.None).Length - 1 == 21,
    "Full artifact records each migration once");
Check(migrations.All(x => sql.Contains(x.Key)), "Every exact local migration identity is retained in the artifact");
var path = Path.GetFullPath(Path.Combine("docs", "sql", final + ".fresh-isolated-index-fix-20261013.sql"));
if (args.Contains("--prepare-reviewed-sql"))
{
    if (File.Exists(path))
        Check(File.ReadAllText(path).Replace("\r\n", "\n") == sql.Replace("\r\n", "\n"), "Existing generated artifact is identical, never overwritten");
    else
    {
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await file.WriteAsync(Encoding.UTF8.GetBytes(sql));
        checks++;
    }
}
else
{
    Check(File.Exists(path), "Prepare the separate reviewed from-zero artifact explicitly before approval");
    Check(File.ReadAllText(path).Replace("\r\n", "\n") == sql.Replace("\r\n", "\n"), "Saved fresh artifact equals actual generated full chain");
}
Check(guard.Attempts == 0, "No SQL connection opened while preparing/reviewing migrations");
Console.WriteLine($"PASS: {checks} full-chain preparation checks; {migrations.Length} migrations; no SQL execution.");
Console.WriteLine($"Artifact SHA256: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}");

sealed class NoFreshConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Attempts++; throw new Exception("SQL connection forbidden during fresh packet preparation"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data,
        InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("SQL connection forbidden during fresh packet preparation"); }
}
