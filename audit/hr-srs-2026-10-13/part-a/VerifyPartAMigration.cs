#:project C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using Cosmix.Application;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Approvals;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;
using Cosmix.Infrastructure;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.DataProtection;
using Cosmix.Api.Authentication;
using Cosmix.Api.Attendance;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.FaceVerification;
const string baseline="20261013101327_AddOfficeRosterHistory";
const string latest="20261013121357_CompleteHrFoundationPartA";
var count=0;
void Check(bool yes,string name){if(!yes)throw new Exception(name);count++;}
using var db=new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
 .UseSqlServer("Server=part-a-check.invalid;Database=OfflineOnly;Integrated Security=true;Encrypt=true")
 .AddInterceptors(new NoConnections()).Options);
Check(!db.Database.HasPendingModelChanges(),"Current model agrees with coordinated Part A snapshot");
var assembly=db.GetService<IMigrationsAssembly>();
Check(assembly.Migrations.Count==23 && assembly.Migrations.Keys.Last()==latest,"Only one new Part A migration follows retained Office22");
var migration=assembly.CreateMigration(assembly.Migrations[latest],db.Database.ProviderName!);
var tables=migration.UpOperations.OfType<CreateTableOperation>().ToArray();
Check(tables.Select(t=>t.Name).Order().SequenceEqual(new[]{"EmployeeArchiveChanges","EmployeeAssetHistory","EmployeeBankVerifications","EmployeeDocumentHistory"}),"Exactly four new HR history tables; no duplicate attendance/office/schema recreation");
Check(!migration.UpOperations.Any(o=>o is DropTableOperation or DropColumnOperation),"Forward migration preserves every existing table and column");
Check(tables.All(t=>t.Schema=="hr") && tables.SelectMany(t=>t.ForeignKeys).All(f=>f.OnDelete==ReferentialAction.Restrict),"History stays in HR with restrictive source relationships");
var columns=migration.UpOperations.OfType<AddColumnOperation>().ToArray();
Check(columns.Single(c=>c.Table=="EmployeeCertifications"&&c.Name=="Revision").DefaultValue is 1 &&
 columns.Single(c=>c.Table=="EmployeeCertifications"&&c.Name=="Status").DefaultValue is 1,"Retained certification rows default to initial active revision");
var operations=migration.UpOperations.ToList();
var projection=migration.UpOperations.OfType<SqlOperation>().Single(s=>s.Sql.Contains("COALESCE([ReturnedOn], [IssuedOn])"));
Check(projection.Sql.StartsWith("EXEC(N'",StringComparison.Ordinal),"New-column backfills compile dynamically after ALTER ADD in an idempotent SQL Server batch");
Check(operations.IndexOf(projection)<operations.FindIndex(o=>o is AddCheckConstraintOperation),"Derived-date initialization precedes every new check constraint");
Check(projection.Sql.Contains("change.[NewStatus] IN (3,4,5,6)") && projection.Sql.Contains("[workflow].[ApprovalDecisions]") &&
 projection.Sql.Contains("[hr].[EmployeeOnboardingDecisions]") && projection.Sql.Contains("[ActorUserId] = change.[ChangedByUserId]"),"Legacy marker requires exact independent inactive approval proof; no Active release is blessed");
foreach(var retainedAccess in new[]{"[attendance].[FaceEnrollments]","[auth].[RegisteredDevices]","[auth].[UserRoleAssignments]","[auth].[UserSessions]","[auth].[MobileOtpChallenges]"})
 Check(projection.Sql.Contains(retainedAccess),"Legacy marker refuses residual access projection: "+retainedAccess);
Check(projection.Sql.Contains("account.[Status] NOT IN (4,5)") && projection.Sql.Contains("account.[MobileAccessEnabled] = 1") && projection.Sql.Contains("account.[WebAccessEnabled] = 1"),"Legacy marker requires blocked account and disabled channels");
Check(migration.DownOperations.First() is SqlOperation guard && guard.Sql.Contains("downgrade refused"),"Downgrade refuses retained Part A evidence before any drops");
var model=db.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model;
var asset=model.FindEntityType(typeof(EmployeeAssetCustody))!;
Check(asset.GetIndexes().Any(i=>i.IsUnique && i.Properties.Select(p=>p.Name).SequenceEqual(new[]{"AssetReference"}) && i.GetFilter()=="[ReturnedAt] IS NULL"),"Original SQL active asset custody uniqueness is retained");
var employee=model.FindEntityType(typeof(Employee))!;
Check(employee.GetForeignKeys().Any(f=>f.Properties.Select(p=>p.Name).SequenceEqual(new[]{"LastAppliedStatusChangeId","Id"}) &&
 f.PrincipalEntityType.ClrType==typeof(EmployeeStatusChange) && f.DeleteBehavior==DeleteBehavior.Restrict),"Applied marker is tied to the same employee status row");
foreach(var type in new[]{typeof(EmployeeBankVerification),typeof(EmployeeDocumentHistory),typeof(EmployeeArchiveChange),typeof(EmployeeAssetHistory)})
 Check(typeof(Cosmix.Domain.Common.IAppendOnlyEntity).IsAssignableFrom(type),"New history is append-only: "+type.Name);
var script=db.GetService<IMigrator>().GenerateScript(baseline,latest,MigrationsSqlGenerationOptions.Idempotent);
Check(!script.Contains("CREATE TABLE [attendance].[OfficeRosterAssignments]") && !script.Contains("DROP TABLE") && !script.Contains("DELETE FROM"),"Upgrade SQL does not recreate office schedules or remove source data");
Check(script.Contains("IF NOT EXISTS") && script.Contains(latest),"Upgrade SQL retains idempotent migration history gates");
Check(script.Contains("EXEC(N'UPDATE [hr].[EmployeeCertifications] SET [EffectiveOn] = [IssuedOn];"),"Generated idempotent script keeps the derived-column UPDATE inside dynamic SQL");
var down=db.GetService<IMigrator>().GenerateScript(latest,baseline);
Check(down.IndexOf("downgrade refused",StringComparison.Ordinal)<down.IndexOf("DROP TABLE",StringComparison.Ordinal),"SQL preservation refusal precedes destructive downgrade");
if(args.Contains("--write-sql")) {
 File.WriteAllText("docs/sql/"+latest+".sql",script,new System.Text.UTF8Encoding(false));
 File.WriteAllText("../audit/hr-srs-2026-10-13/part-a/"+latest+".review-only-down.sql",down,new System.Text.UTF8Encoding(false));
}
// Real registration graph, synthetic ephemeral settings and no files/SQL/provider operations.
var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
 ["ConnectionStrings:CosmixDatabase"]="Server=part-a-composition.invalid;Database=OfflineOnly;Integrated Security=true;Encrypt=true",
 ["MobileAuthentication:Enabled"]="false",["WebAuthentication:Enabled"]="false",
 ["Jwt:Issuer"]="PartA.Offline",["Jwt:Audience"]="PartA.Offline",
 ["Jwt:SigningKey"]=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
}).Build();
var services=new ServiceCollection();services.AddLogging();services.AddSingleton<IConfiguration>(configuration);
var environment=new EnvironmentStub();services.AddSingleton<IHostEnvironment>(environment);
services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
services.AddSingleton<IAttendanceCursorProtector,AttendanceCursorProtector>();
services.AddSingleton<IFaceCaptureProtector,FaceCaptureProtector>();
services.AddApplication();services.AddInfrastructure(configuration);services.AddApiAuthentication(configuration,environment);
services.Replace(ServiceDescriptor.Singleton<IPrivateFileStore,NoFiles>());
using var provider=services.BuildServiceProvider(new ServiceProviderOptions{ValidateScopes=true});
using var scope=provider.CreateScope();
foreach(var type in new[]{typeof(ApprovalRoutingService),typeof(IApprovalRouteResolver),typeof(IApprovalAuthority),typeof(EmployeeBankService),
 typeof(EmployeeFieldPolicy),typeof(EmployeeLifecyclePolicy),typeof(EmployeeLifecycleService),typeof(EmployeeLifecycleProcessor),
 typeof(EmployeeDocumentService),typeof(EmployeeRecordService)})
 Check(scope.ServiceProvider.GetRequiredService(type)is not null,"Part A registration resolves: "+type.Name);
Check(!scope.ServiceProvider.GetRequiredService<EmployeeLifecycleOptions>().AllowRetirement &&
 !scope.ServiceProvider.GetRequiredService<EmployeeLifecycleOptions>().AllowSuspensionRelease &&
 scope.ServiceProvider.GetRequiredService<EmployeeRecordPolicyOptions>().ApprovedVersion is null,"Missing approved new policies remain gated");
Check(Cosmix.Application.Authentication.Access.AccessPermissionCatalog.All.Select(p=>p.Code).Distinct(StringComparer.Ordinal).Count()==
 Cosmix.Application.Authentication.Access.AccessPermissionCatalog.All.Count,"Permission registry has no duplicate definitions");
Console.WriteLine($"PASS {count} Part A offline model/migration/composition checks; no SQL connection, provider, file or account mutation");
sealed class NoConnections:DbConnectionInterceptor {
 public override InterceptionResult ConnectionOpening(DbConnection c,ConnectionEventData e,InterceptionResult r)=>throw new Exception("SQL access prohibited in offline check");
 public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c,ConnectionEventData e,InterceptionResult r,CancellationToken ct=default)=>throw new Exception("SQL access prohibited in offline check");
}
sealed class NoFiles:IPrivateFileStore {
 public Task<PrivateFileReference> WriteAsync(byte[] b,string purpose,CancellationToken ct)=>throw new Exception("Private IO prohibited in composition check");
 public Task<byte[]> ReadAsync(PrivateFileReference r,string purpose,CancellationToken ct)=>throw new Exception("Private IO prohibited in composition check");
}
sealed class EnvironmentStub:IHostEnvironment {
 public string EnvironmentName{get;set;}=Environments.Development;
 public string ApplicationName{get;set;}="PartA.Offline";
 public string ContentRootPath{get;set;}="";
 public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
}
