#:project C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Data.Common;
using System.Reflection;
using Cosmix.Application;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.FoundationPolicies;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Infrastructure;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Calculation;
var checks=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
using var db=new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
 .UseSqlServer("Server=continuation-composition.invalid;Database=OfflineOnly;Integrated Security=true;Encrypt=true")
 .AddInterceptors(new NoConnections()).Options);
Check(!db.Database.HasPendingModelChanges(),"current model and shared coordinated snapshot agree");
var graph=db.GetService<IMigrationsAssembly>().Migrations.Keys.ToArray();
Check(graph.Length==24&&graph[22]=="20261013121357_CompleteHrFoundationPartA"&&graph[23]=="20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims","frozen A23 precedes separate B24");
Check(db.Model.GetEntityTypes().All(t=>t.ClrType.Namespace!="Cosmix.Domain.HR.EmploymentEpisodes"),"unapproved rehire types remain outside deployed EF model");
var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
 ["ConnectionStrings:CosmixDatabase"]="Server=continuation-composition.invalid;Database=OfflineOnly;Integrated Security=true;Encrypt=true",
 ["MobileAuthentication:Enabled"]="false",["WebAuthentication:Enabled"]="false",
 ["Jwt:Issuer"]="Continuation.Offline",["Jwt:Audience"]="Continuation.Offline",
 ["Jwt:SigningKey"]=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
}).Build();
var services=new ServiceCollection();services.AddLogging();services.AddSingleton<IConfiguration>(configuration);
var environment=new EnvironmentStub();services.AddSingleton<IHostEnvironment>(environment);
services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
services.AddSingleton<IAttendanceCursorProtector,AttendanceCursorProtector>();
services.AddSingleton<IFaceCaptureProtector,FaceCaptureProtector>();
services.AddApplication();services.AddInfrastructure(configuration);services.AddApiAuthentication(configuration,environment);
services.Replace(ServiceDescriptor.Singleton<IPrivateFileStore,NoFiles>());
using var provider=services.BuildServiceProvider(new ServiceProviderOptions{ValidateScopes=true});using var scope=provider.CreateScope();
foreach(var type in new[]{typeof(EmployeeHiringService),typeof(EmployeeApprovalService),typeof(EmployeeApprovalMaterializer),typeof(EmployeeUpdateService),typeof(EmployeeUpdateSections),typeof(EmployeeSiteAssignmentService),typeof(EmployeeDocumentService),typeof(EmployeeFieldPolicy),typeof(EmployeeRecordService),typeof(EmployeeLifecyclePolicy),typeof(EmployeeLifecycleService),typeof(EmployeeLifecycleProcessor),typeof(StaffTaxonomyAuthority),typeof(StaffTaxonomyOperation),typeof(StaffTaxonomyService),typeof(IStaffTaxonomyResolver),typeof(HrFoundationPolicyService),typeof(HrFoundationPolicyOperation),typeof(IHrFoundationPolicyReader),typeof(IStaffTaxonomyRepository),typeof(IHrFoundationPolicyRepository),typeof(IEmployeeDocumentRepository)})
 Check(scope.ServiceProvider.GetRequiredService(type)is not null,"deployed DI resolves "+type.Name);
var reader=scope.ServiceProvider.GetRequiredService<IHrFoundationPolicyReader>();var fieldPolicy=scope.ServiceProvider.GetRequiredService<EmployeeFieldPolicy>();
Check(ReferenceEquals(reader,scope.ServiceProvider.GetRequiredService<HrFoundationPolicyReader>()),"reader interface uses same scoped deployed reader");
Check(fieldPolicy.GetType().GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Any(f=>ReferenceEquals(f.GetValue(fieldPolicy),reader)),"real field validator receives persisted reader rather than optional-null compatibility path");
var sections=scope.ServiceProvider.GetRequiredService<EmployeeUpdateSections>();
Check(sections.GetType().GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Any(f=>ReferenceEquals(f.GetValue(sections),fieldPolicy)),"actual post-hire manifest validation receives deployed field policy");
Check(sections.GetType().GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Any(f=>f.GetValue(sections)is IEmployeeDocumentRepository),"actual post-hire validation receives dated category repository");
var taxonomy=scope.ServiceProvider.GetRequiredService<IStaffTaxonomyResolver>();
foreach(var type in new[]{typeof(IIndividualRosterResolver),typeof(OfficeRosterResolver),typeof(SiteRosterResolver),
 typeof(IOfficeAttendanceSourceReader),typeof(OvertimeSources),typeof(OvertimeEligibilityService),typeof(IAttendanceCalculationSnapshotProducer)}){
 var consumer=scope.ServiceProvider.GetRequiredService(type);
 Check(consumer.GetType().GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Any(f=>ReferenceEquals(f.GetValue(consumer),taxonomy)),
  "deployed dated taxonomy injected in "+type.Name);
}
Check(scope.ServiceProvider.GetRequiredService<StaffTaxonomyAuthorityOptions>().ApprovedVersion is null&&scope.ServiceProvider.GetRequiredService<HrFoundationPolicyAuthorityOptions>().ApprovedVersion is null,"no invented publication authority enabled by default");
Check(scope.ServiceProvider.GetRequiredService<EmployeeLifecycleOptions>().ApprovedVersion is null&&scope.ServiceProvider.GetRequiredService<EmployeeRecordPolicyOptions>().ApprovedVersion is null,"new lifecycle/record rules remain gated without actual approved values");
var catalog=Cosmix.Application.Authentication.Access.AccessPermissionCatalog.All;
Check(catalog.Select(p=>p.Code).Distinct(StringComparer.Ordinal).Count()==catalog.Count,"shared permission registry has unique codes");
foreach(var code in new[]{StaffTaxonomyPermissions.Read,StaffTaxonomyPermissions.Request,StaffTaxonomyPermissions.Review}
    .Concat(HrFoundationPolicyPermissions.Definitions.Select(permission=>permission.Code)))
    Check(catalog.Any(permission=>permission.Code==code),"dedicated permission registered "+code);
Console.WriteLine($"PASS {checks} current continuation model/DI/default-authority checks; no SQL, private file, provider or account operation");
sealed class NoConnections:DbConnectionInterceptor {
 public override InterceptionResult ConnectionOpening(DbConnection c,ConnectionEventData e,InterceptionResult r)=>throw new Exception("Offline check cannot open SQL");
 public override ValueTask<InterceptionResult>ConnectionOpeningAsync(DbConnection c,ConnectionEventData e,InterceptionResult r,CancellationToken ct=default)=>throw new Exception("Offline check cannot open SQL");
}
sealed class NoFiles:IPrivateFileStore {
 public Task<PrivateFileReference>WriteAsync(byte[] b,string p,CancellationToken ct)=>throw new Exception("Offline check cannot write files");
 public Task<byte[]>ReadAsync(PrivateFileReference r,string p,CancellationToken ct)=>throw new Exception("Offline check cannot read files");
}
sealed class EnvironmentStub:IHostEnvironment {
 public string EnvironmentName{get;set;}=Environments.Development;public string ApplicationName{get;set;}="Continuation.Offline";public string ContentRootPath{get;set;}="";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
}
