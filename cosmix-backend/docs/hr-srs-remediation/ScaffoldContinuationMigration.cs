using System.Data.Common;
using System.Reflection;
using System.Security.Cryptography;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;

// Reviewed source generation only. No application host, database connection, seed or upgrade.
const string root = @"C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend";
const string baseline = "20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims";
const string name = "AddFinancialAdjustmentsAndOutstationDeployments";
if (!Path.GetFullPath(Directory.GetCurrentDirectory()).Equals(root, StringComparison.OrdinalIgnoreCase))
    throw new Exception("Wrong checkout; no generated file writes");
var directory = Path.Combine(root, "src", "Cosmix.Infrastructure", "Database", "Migrations");
var snapshot = Path.Combine(directory, "CosmixDbContextModelSnapshot.cs");
if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(snapshot))) != "D12E3C2783B08C1021C587137C3B56B1E838B2EC4ECE41323A002279B539D78E")
    throw new Exception("Reviewed 24-migration snapshot changed; no generated file writes");
if (Directory.EnumerateFiles(directory, "*" + name + "*.cs").Any())
    throw new Exception("Migration already exists; never overwrite or rerun");
var priorFiles = Directory.EnumerateFiles(directory, "*.cs").Where(x => x != snapshot)
    .ToDictionary(x => x, x => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x))));
var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=source-scaffold.invalid;Database=NeverConnect;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
var migrations = db.GetService<IMigrationsAssembly>();
if (migrations.Migrations.Count != 24 || migrations.Migrations.Keys.Last() != baseline)
    throw new Exception("Reviewed compiled migration baseline changed");
var migration = migrations.CreateMigration(migrations.Migrations[baseline], db.Database.ProviderName!);
var before = db.GetService<IModelRuntimeInitializer>().Initialize(migration.TargetModel, designTime: true);
var after = db.GetService<IDesignTimeModel>().Model;
var delta = db.GetService<IMigrationsModelDiffer>().GetDifferences(before.GetRelationalModel(), after.GetRelationalModel());
var expected = new[] { "AttendancePayrollFinancialAdjustments", "AttendancePayrollFinancialResolutions",
    "DeploymentHomeBreakRequests", "OutstationDeployments" }.Order().ToArray();
if (delta.Count != 21 || delta.Any(x => x is not (CreateTableOperation or CreateIndexOperation)) ||
    !delta.OfType<CreateTableOperation>().Select(x => x.Name).Order().SequenceEqual(expected) ||
    delta.OfType<CreateIndexOperation>().Count() != 17 ||
    delta.OfType<CreateIndexOperation>().Any(x => !expected.Contains(x.Table)))
    throw new Exception("Model differs from reviewed additive 4-table/17-index scope; no generated file writes");
var services = new ServiceCollection();
services.AddDbContextDesignTimeServices(db);
var providerAssembly = Assembly.Load(db.Database.ProviderName!);
var attribute = providerAssembly.GetCustomAttribute<DesignTimeProviderServicesAttribute>()
    ?? throw new Exception("Provider design services unavailable");
var type = providerAssembly.GetType(attribute.TypeName, throwOnError: true)!;
((IDesignTimeServices)Activator.CreateInstance(type)!).ConfigureDesignTimeServices(services);
services.AddEntityFrameworkDesignTimeServices();
using var provider = services.BuildServiceProvider();
var scaffolder = provider.GetRequiredService<IMigrationsScaffolder>();
var generated = scaffolder.ScaffoldMigration(name, "Cosmix.Infrastructure", "Database.Migrations", "C#", dryRun: true);
if (guard.Attempts != 0) throw new Exception("Unexpected connection attempt; no file writes");
var files = scaffolder.Save(Path.Combine(root, "src", "Cosmix.Infrastructure"), generated, directory, dryRun: false);
foreach (var prior in priorFiles)
    if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(prior.Key))) != prior.Value)
        throw new Exception("An existing migration source changed");
Console.WriteLine($"Prepared {generated.MigrationId}; preserved {priorFiles.Count} earlier source files.");
Console.WriteLine(files.MigrationFile);
Console.WriteLine(files.MetadataFile);
Console.WriteLine(files.SnapshotFile);
Console.WriteLine("Source-only EF scaffold: 4 new tables, 17 indexes. Zero SQL connections, hosts, seeds or applied migrations.");

sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts { get; private set; }
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    { Attempts++; throw new Exception("SQL forbidden in source-only migration scaffold"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    { Attempts++; throw new Exception("SQL forbidden in source-only migration scaffold"); }
}
