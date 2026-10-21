#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Text;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

const string output = @"C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend\docs\attendance-a02\A02-FullSchema.sql";
var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=never-open.invalid;Database=NeverOpen;Integrated Security=true")
    .AddInterceptors(guard).Options);
var ids = db.Database.GetMigrations().ToArray();
if (ids.Length != 24 || ids[^1] != "20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims" || db.Database.HasPendingModelChanges())
    throw new Exception("A02 requires the reviewed exact 24-migration model.");
var sql = db.GetService<IMigrator>().GenerateScript("0", ids[^1], MigrationsSqlGenerationOptions.Default);
if (guard.Attempts != 0) throw new Exception("Offline generation attempted SQL.");
File.WriteAllText(output, sql, new UTF8Encoding(false));
Console.WriteLine($"PASS: generated current 24-migration A02 schema; zero connections. {output}");

sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Attempts++; throw new Exception("SQL forbidden during generation."); }
}
