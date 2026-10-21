#:project C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.FoundationPolicies;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Employees;
var now=new DateTimeOffset(2026,10,13,0,0,0,TimeSpan.Zero);var today=new DateOnly(2026,10,13);var checks=0;
var employee=new Employee(now.AddDays(-3),"synthetic-doc-policy");
var rows=new List<EmployeeDocument>();
EmployeeDocument Doc(EmployeeDocumentType type,EmployeeDocumentPrivacy privacy=EmployeeDocumentPrivacy.Standard,DateOnly? expiry=null){
 var d=new EmployeeDocument(employee.Id,type,"synthetic-private","sample.pdf","application/pdf",10,new string('A',64),"synthetic-uploader",now.AddDays(-2),expiresOn:expiry,privacy:privacy);
 d.RecordScan(DocumentScanStatus.Clean,true,"synthetic-scanner",now.AddDays(-1));d.RecordVerification(DocumentVerificationStatus.Verified,"independent-verifier",null,now);rows.Add(d);return d;
}
var identity=Doc(EmployeeDocumentType.Identity);var photo=Doc(EmployeeDocumentType.ProfilePhoto);var contract=Doc(EmployeeDocumentType.Contract);
var published=new Policies(today);var policy=new EmployeeFieldPolicy(new(),published);var category="SITE";var categoryCalls=0;
var hiring=Proxy.Of<IEmployeeHiringRepository>((method,args)=>method.Name=="FindDocumentAsync"?Task.FromResult(rows.SingleOrDefault(d=>d.Id==(Guid)args![1]!)):throw new Exception(method.Name));
var repository=Proxy.Of<IEmployeeUpdateRepository>((m,a)=>throw new Exception(m.Name));var bank=Proxy.Of<IEmployeeBankRepository>((m,a)=>throw new Exception(m.Name));var files=Proxy.Of<IPrivateFileStore>((m,a)=>throw new Exception(m.Name));
var categories=Proxy.Of<IEmployeeDocumentRepository>((m,a)=>m.Name=="CurrentStaffCategoryCodeAsync"?Category(a!):throw new Exception(m.Name));
Task<string?> Category(object?[] args){if(args[0] is not Guid id || id!=employee.Id || args[1] is not DateOnly date || date!=today)throw new Exception("category query must use employee and current businessdate");categoryCalls++;return Task.FromResult<string?>(category);}
var constructor=typeof(EmployeeUpdateSections).GetConstructors().Single();
var sections=(EmployeeUpdateSections)constructor.Invoke(constructor.GetParameters().Select(p=>p.ParameterType.Name switch {nameof(IEmployeeUpdateRepository)=>(object)repository,nameof(IEmployeeHiringRepository)=>hiring,nameof(IPrivateFileStore)=>files,nameof(IEmployeeBankRepository)=>bank,nameof(EmployeeFieldPolicy)=>policy,nameof(IEmployeeDocumentRepository)=>categories,_=>throw new Exception(p.Name)}).ToArray());
Task Validate(params Guid[] ids)=>sections.ValidateAsync(employee,EmployeeChangeSection.Documents,new(Documents:new(ids)),today,now,default);
await Denied(()=>Validate(identity.Id,photo.Id),400,"published global Contract removal denied");
await Validate(identity.Id,photo.Id,contract.Id);Check(true,"selected verified required Contract accepted");
published.State="unavailable";await Denied(()=>Validate(identity.Id,photo.Id,contract.Id),503,"unavailable policy cannot bypass document manifest gate");published.State="effective";
published.Rules=new(){RequiredDocuments=[new("SITE",[EmployeeDocumentType.License])]};
await Denied(()=>Validate(identity.Id,photo.Id,contract.Id),400,"current Site category License required");
category="OFFICE";await Validate(identity.Id,photo.Id,contract.Id);Check(true,"unrelated category License excluded");category="SITE";
var privateLicense=Doc(EmployeeDocumentType.License,EmployeeDocumentPrivacy.Medical);await Denied(()=>Validate(identity.Id,photo.Id,privateLicense.Id),409,"medical privacy evidence cannot satisfy Standard manifest");
var expired=Doc(EmployeeDocumentType.License,expiry:today.AddDays(-1));await Denied(()=>Validate(identity.Id,photo.Id,expired.Id),409,"expired required evidence cannot satisfy publication");
var license=Doc(EmployeeDocumentType.License);await Validate(identity.Id,photo.Id,license.Id);Check(true,"current clean verified Site License accepted");
Check(categoryCalls>=5,"real section validation resolves dated current category");
Console.WriteLine($"PASS {checks} actual post-hire document policy write checks; synthetic grants/evidence, no SQL/provider");
void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
async Task Denied(Func<Task> action,int status,string name){try{await action();}catch(AuthenticationFailure e)when(e.StatusCode==status){checks++;return;}throw new Exception(name);}
public class Proxy:DispatchProxy {public Func<MethodInfo,object?[]?,object?> Handler=null!;public static T Of<T>(Func<MethodInfo,object?[]?,object?> handler)where T:class{var value=Create<T,Proxy>();((Proxy)(object)value).Handler=handler;return value;}protected override object?Invoke(MethodInfo?m,object?[]?a)=>Handler(m!,a);}
sealed class Policies(DateOnly today):IHrFoundationPolicyReader {
 public string State="effective";public HrFoundationFieldRules Rules=new(){RequiredDocuments=[new(null,[EmployeeDocumentType.Contract])]};
 readonly HrFoundationPolicyIdentity identity=new(Guid.NewGuid(),Guid.NewGuid(),1,"synthetic-doc-only",today,null);
 public Task<HrFoundationPolicyResolution<HrFoundationFieldRules>>GetFieldsAsync(DateOnly d,CancellationToken ct)=>Task.FromResult(new HrFoundationPolicyResolution<HrFoundationFieldRules>(State,Rules,identity,null));
 public Task<HrFoundationPolicyResolution<HrFoundationLifecycleRules>>GetLifecycleAsync(DateOnly d,CancellationToken ct)=>throw new Exception("unused");
 public Task<HrFoundationPolicyResolution<HrFoundationRecordRules>>GetRecordsAsync(DateOnly d,CancellationToken ct)=>throw new Exception("unused");
}
