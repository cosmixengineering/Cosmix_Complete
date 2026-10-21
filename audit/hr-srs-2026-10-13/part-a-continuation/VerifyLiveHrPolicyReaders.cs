#:project C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.FoundationPolicies;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Files;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
var checks=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
if(!OperatingSystem.IsWindows())throw new Exception("Only the selected Windows LocalDB operator is allowed");
var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Cosmix","PartAContinuation","Database20261013");
using var identity=WindowsIdentity.GetCurrent();
var metadataStart=new ProcessStartInfo("SqlLocalDB.exe"){CreateNoWindow=true,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};metadataStart.ArgumentList.Add("info");metadataStart.ArgumentList.Add("CosmixUat_d893653346");
using var metadata=Process.Start(metadataStart)??throw new Exception("Known existing metadata unavailable");
var output=await metadata.StandardOutput.ReadToEndAsync();var error=await metadata.StandardError.ReadToEndAsync();await metadata.WaitForExitAsync();Check(metadata.ExitCode==0,"known existing alias metadata available");
output+=error;
// Some native Windows console writers emit CR CR LF when stdout is redirected.
output=Regex.Replace(output,@"\r+\n","\n");
var owner=Regex.Match(output,@"(?im)^[\t ]*Owner:[\t ]*([^\r\n]+)\r?$").Groups[1].Value.Trim();
Check(owner.Equals(identity.Name,StringComparison.OrdinalIgnoreCase),"existing alias owner matches operator");
var pipe=Regex.Match(output,@"(?im)^[\t ]*Instance pipe name:[\t ]*([^\r\n]+)\r?$").Groups[1].Value.Trim();
Check(Regex.IsMatch(pipe,@"^np:\\\\.\\pipe\\LOCALDB#[A-Fa-f0-9]{8}\\tsql\\query$"),"only exact live local Windows pipe selected");
var connection=new SqlConnectionStringBuilder{DataSource=pipe,InitialCatalog="Cosmix_Testing",IntegratedSecurity=true,Encrypt=SqlConnectionEncryptOption.Optional,TrustServerCertificate=true,ConnectTimeout=5,Pooling=false};
using var db=new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(connection.ConnectionString).Options);
await db.Database.OpenConnectionAsync();
using(var command=db.Database.GetDbConnection().CreateCommand()){
 command.CommandText="SELECT DB_NAME(),ORIGINAL_LOGIN(),SUSER_SNAME(d.owner_sid),CONVERT(int,SERVERPROPERTY('IsLocalDB')),CONVERT(nvarchar(36),r.family_guid) FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id WHERE d.database_id=DB_ID()";
 using var reader=await command.ExecuteReaderAsync();Check(await reader.ReadAsync(),"actual selected identity row present");
 Check(reader.GetString(0)=="Cosmix_Testing"&&reader.GetString(1).Equals(identity.Name,StringComparison.OrdinalIgnoreCase)&&reader.GetString(2).Equals(identity.Name,StringComparison.OrdinalIgnoreCase)&&reader.GetInt32(3)==1,"actual SQL catalog/operator/owner/local identity verified");
 using var header=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,"known-backup-header.json")));
 Check(Guid.Parse(reader.GetString(4))==header.RootElement.GetProperty("FamilyGUID").GetGuid(),"actual selected SQL family matches verified retained backup");
}
var history=(await db.Database.GetAppliedMigrationsAsync()).ToArray();
Check(history.Length>=23&&history[22]=="20261013121357_CompleteHrFoundationPartA","actual selected SQL contains applied A23");
var sourceSettings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Cosmix","LocalTesting","settings.json");
using var settings=JsonDocument.Parse(File.ReadAllBytes(sourceSettings));
var files=new EncryptedPrivateFileStore(settings.RootElement.GetProperty("PrivateFiles").Deserialize<PrivateFileStorageOptions>()??throw new Exception("Existing private provider not configured"));
var taxonomy=new StaffTaxonomyResolver(new StaffTaxonomyRepository(db),files);var date=new DateOnly(2026,10,13);
var office=await taxonomy.ResolveAsync("OFFICE",date,default);var site=await taxonomy.ResolveAsync("SITE",date,default);
Check(office.Available&&office.Baseline&&office.Mapping!.WorkLocation==StaffWorkLocation.Office,"real SQL preserves confirmed OFFICE baseline without invented publication");
Check(site.Available&&site.Baseline&&site.Mapping!.WorkLocation==StaffWorkLocation.Site,"real SQL preserves confirmed SITE baseline without invented publication");
Check(!(await taxonomy.ResolveAsync("UNAPPROVED_SYNTHETIC_CODE",date,default)).Available,"real SQL cannot authorize an unconfigured category");
var policies=new HrFoundationPolicyReader(new FoundationPolicyRepository(db),files);
Check((await policies.GetFieldsAsync(date,default)).State=="none","real SQL has no published HR field authority to invent");
Check((await policies.GetLifecycleAsync(date,default)).State=="none","real SQL has no published new lifecycle authority to invent");
Check((await policies.GetRecordsAsync(date,default)).State=="none","real SQL has no published record authority to invent");
var fieldPolicy=new EmployeeFieldPolicy(new(),policies);
Check((await fieldPolicy.MissingFieldsAsync(new(),date,default)).Count==0,"real SQL absence retains empty configured field baseline");
try{await new EmployeeLifecyclePolicy(new(),policies).ValidateTransitionAsync(Cosmix.Domain.HR.Employees.EmployeeStatus.Terminated,Cosmix.Domain.HR.Employees.EmployeeStatus.Active,DateTimeOffset.UtcNow,null,default);throw new Exception("Unapproved rehire allowed");}
catch(AuthenticationFailure e)when(e.Code=="employee_rehire_requires_approved_employment_episode_policy"){Check(true,"rehire remains unavailable against actual selected configuration");}
Check(await db.UserAccounts.LongCountAsync()==5&&await db.Employees.LongCountAsync()==14,"actual selected account/employee inventory remains preserved");
var guardServices=new ServiceCollection();guardServices.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
guardServices.AddDbContext<CosmixDbContext>(options=>options.UseSqlServer(connection.ConnectionString));
using(var guardProvider=guardServices.BuildServiceProvider()){
 var localTesting=System.Reflection.Assembly.Load("Cosmix.Api").GetType("Cosmix.Api.Development.LocalTesting")??throw new Exception("Actual local guard unavailable");
 var verify=localTesting.GetMethod("VerifyDatabaseAsync",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)??throw new Exception("Actual local SQL guard unavailable");
 try{await (Task)verify.Invoke(null,[guardProvider])!;throw new Exception("Five-account target incorrectly accepted by the three-account local OTP guard");}
 catch(InvalidOperationException exception)when(exception.Message=="Local testing requires exactly the three synthetic fixture accounts. Never import real data."){
  Check(true,"actual local OTP host guard rejects five-account target without deletion/reseeding/guard relaxation");
 }
}
Console.WriteLine($"PASS {checks} actual selected SQL HR reader/default-policy checks; applied migration count={history.Length}; read-only queries, no account/provider/HTTP action");
