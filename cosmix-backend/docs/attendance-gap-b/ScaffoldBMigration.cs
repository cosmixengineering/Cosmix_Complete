#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:package Microsoft.EntityFrameworkCore.Design@10.0.12
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using System.Security.Cryptography;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

// Actual EF generator, source files only. No host startup/configuration, account, key or database writes.
if(!args.SequenceEqual(new[]{"--write-frozen-b24"}))throw new Exception("Explicit frozen B24 source-scaffold flag required.");
const string root=@"C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend";
const string baseline="20261013121357_CompleteHrFoundationPartA";
const string migrationName="AddOfficeCaptureValidationAndOfflineZoneClaims";
if(!Path.GetFullPath(Directory.GetCurrentDirectory()).Equals(root,StringComparison.OrdinalIgnoreCase))throw new Exception("Wrong source checkout.");
var directory=Path.Combine(root,"src","Cosmix.Infrastructure","Database","Migrations");
var snapshot=Path.Combine(directory,"CosmixDbContextModelSnapshot.cs");
if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(snapshot)))!="BFE33267F048C1DC4720EC56262C4C63940BA0CACC1B4298EB41EB471BC23308")
    throw new Exception("A23 frozen snapshot drift; no generated file writes.");
if(Directory.EnumerateFiles(directory,"*"+migrationName+"*.cs").Any())throw new Exception("B24 already exists; never rerun/overwrite scaffolding.");
var guard=new NoScaffoldConnections();
using var db=new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=scaffold-only.invalid;Database=NeverConnect;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
var migrations=db.GetService<IMigrationsAssembly>();
if(migrations.Migrations.Count!=23 || migrations.Migrations.Keys.Last()!=baseline)throw new Exception("Exact compiled A23 baseline required.");
var prior=migrations.CreateMigration(migrations.Migrations[baseline],db.Database.ProviderName!);
var before=db.GetService<IModelRuntimeInitializer>().Initialize(prior.TargetModel,designTime:true);
var after=db.GetService<IDesignTimeModel>().Model;
var delta=db.GetService<IMigrationsModelDiffer>().GetDifferences(before.GetRelationalModel(),after.GetRelationalModel());
if(delta.Any(x=>x is DropTableOperation or DropColumnOperation or InsertDataOperation or UpdateDataOperation or DeleteDataOperation or SqlOperation))
    throw new Exception("Unreviewed destructive/seed/raw-SQL delta; no file writes.");
var tables=delta.OfType<CreateTableOperation>().Select(x=>x.Name).Order().ToArray();
if(!tables.SequenceEqual(new[]{"CaptureValidationFinalEvidence","CaptureValidationPolicyVersions","OfficeGeofenceVersions"}.Order()) ||
    delta.OfType<AddColumnOperation>().Count()!=10 || delta.OfType<AddColumnOperation>().Any(x=>!x.IsNullable))
    throw new Exception("Model differs from frozen B scope; no generated file writes.");
var services=new ServiceCollection();
services.AddDbContextDesignTimeServices(db);
var providerAssembly=Assembly.Load(db.Database.ProviderName!);
var attribute=providerAssembly.GetCustomAttribute<DesignTimeProviderServicesAttribute>() ?? throw new Exception("Provider design services unavailable.");
var type=providerAssembly.GetType(attribute.TypeName,throwOnError:true)!;
((IDesignTimeServices)Activator.CreateInstance(type)!).ConfigureDesignTimeServices(services);
services.AddEntityFrameworkDesignTimeServices();
using var provider=services.BuildServiceProvider();
var scaffolder=provider.GetRequiredService<IMigrationsScaffolder>();
var generated=scaffolder.ScaffoldMigration(migrationName,"Cosmix.Infrastructure","Database.Migrations","C#",dryRun:true);
if(guard.Attempts!=0)throw new Exception("Unexpected SQL attempt; no file writes.");
var files=scaffolder.Save(Path.Combine(root,"src","Cosmix.Infrastructure"),generated,directory,dryRun:false);
Console.WriteLine("Generated frozen B migration: "+generated.MigrationId);
Console.WriteLine(files.MigrationFile);
Console.WriteLine(files.MetadataFile);
Console.WriteLine(files.SnapshotFile);
Console.WriteLine("Source scaffold only. No API host, SQL connection, fixture, credential or provider write.");
sealed class NoScaffoldConnections:DbConnectionInterceptor {
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection,ConnectionEventData data,InterceptionResult result)
    {Attempts++;throw new Exception("Database forbidden in source scaffold");}
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,ConnectionEventData data,
        InterceptionResult result,CancellationToken ct=default)
    {Attempts++;throw new Exception("Database forbidden in source scaffold");}
}
