#:project C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.FoundationPolicies;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Employees;

var date=new DateOnly(2026,10,13);var now=new DateTimeOffset(2026,10,13,9,0,0,TimeSpan.Zero);var checks=0;
var identity=new HrFoundationPolicyIdentity(Guid.NewGuid(),Guid.NewGuid(),1,"synthetic-only",date,null);
var reader=new SyntheticPolicies { Fields=new("effective",new(){RequireGender=true,GenderValues=["SYNTHETIC"]},identity,null) };
var policy=new EmployeeFieldPolicy(new(),reader);
Check((await policy.MissingFieldsAsync(new(),date,default)).Contains("gender"),"published required field enforced");
await Failure(()=>policy.ValidateDraftAsync(new(){Gender="OTHER"},date,default),400,"controlled list enforced");
await policy.ValidateDraftAsync(new(){Gender="SYNTHETIC"},date,default);Check(true,"configured choice accepted");
var configured=new EmployeeFieldPolicyOptions {ApprovedVersion="synthetic-config",EffectiveFrom=date,RequireGender=true,GenderValues=["CONFIG"]};
reader.Fields=new("none",null,null,null);
Check((await new EmployeeFieldPolicy(configured,reader).MissingFieldsAsync(new(),date,default)).Contains("gender"),"absence preserves configured baseline");
reader.Fields=new("unavailable",null,null,"expired");
await Failure(()=>new EmployeeFieldPolicy(configured,reader).MissingFieldsAsync(new(),date,default),503,"expired publication cannot revive configured fallback");
foreach(var malformed in new[]{identity with {VersionId=Guid.Empty},identity with {EffectiveFrom=date.AddDays(1)},identity with {EffectiveTo=date},identity with {Number=0}}){
 reader.Fields=new("effective",new(),malformed,null);
 await Failure(()=>policy.MissingFieldsAsync(new(),date,default),503,"invalid publication identity/window denied");
}
reader.Fields=new("effective",new(){RequiredDocuments=[new(null,[EmployeeDocumentType.Contract]),new("SITE",[EmployeeDocumentType.License,EmployeeDocumentType.Contract])]},identity,null);
Check((await policy.RequiredDocumentTypesAsync("SITE",date,default)).Order().SequenceEqual(new[]{EmployeeDocumentType.Contract,EmployeeDocumentType.License}.Order()),"global and category requirements combined once");
Check((await policy.RequiredDocumentTypesAsync("OFFICE",date,default)).SequenceEqual([EmployeeDocumentType.Contract]),"other category requirement excluded");
reader.Fields=new("effective",new(){RequiredDocuments=[new("SITE",null!)]},identity,null);
await Failure(()=>policy.RequiredDocumentTypesAsync("SITE",date,default),503,"malformed document rules denied safely");
reader.Lifecycle=new("effective",new(true,true),identity,null);
var lifecycle=new EmployeeLifecyclePolicy(new(),reader);
var token=await lifecycle.ValidateTransitionAsync(EmployeeStatus.Active,EmployeeStatus.Retired,now,null,default);
reader.Lifecycle=new("effective",new(true,true),identity with {VersionId=Guid.NewGuid(),Number=2},null);
Check(token!=await lifecycle.ValidateTransitionAsync(EmployeeStatus.Active,EmployeeStatus.Retired,now,null,default),"same human label does not hide new lifecycle version");
reader.FutureLifecycle=new("effective",new(true,true),identity with {VersionId=Guid.NewGuid(),EffectiveFrom=date.AddDays(1)},null);
await Failure(()=>lifecycle.ValidateTransitionAsync(EmployeeStatus.Suspended,EmployeeStatus.Active,now,now.AddDays(1),default),409,"future policy switch requires fresh review");
reader.Lifecycle=new("unavailable",null,null,"invalid evidence");
await Failure(()=>lifecycle.ValidateTransitionAsync(EmployeeStatus.Active,EmployeeStatus.Retired,now,null,default),503,"invalid lifecycle evidence denies retirement");
Check(await lifecycle.ValidateTransitionAsync(EmployeeStatus.Active,EmployeeStatus.Suspended,now,null,default)==null,"established suspension baseline retained");
await Failure(()=>lifecycle.ValidateTransitionAsync(EmployeeStatus.Terminated,EmployeeStatus.Active,now,null,default),503,"rehire stays denied without confirmed episode policy");
var evaluate=typeof(EmployeeDocumentCompleteness).GetMethod("Evaluate")!;
var arguments=new object?[]{Guid.NewGuid(),Array.Empty<EmployeeDocument>(),Array.Empty<Guid>(),Array.Empty<EmployeeCertificationRequirement>(),date,30,false};
var result=(EmployeeDocumentCompletenessResponse)evaluate.Invoke(null,evaluate.GetParameters().Length==8 ? [..arguments,new[]{EmployeeDocumentType.Degree}] : arguments)!;
Check(result.RequiredDocuments.Any(x=>x.Type==EmployeeDocumentType.Degree && x.IsMissing),"completeness metadata includes published required Degree");
Check(result.ActivationBlockers.Contains("documents.required.Degree"),"published document has actionable blocker");
Console.WriteLine($"PASS {checks} persisted HR policy consumer checks (synthetic rules; no database/provider)");
void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
async Task Failure(Func<Task> action,int status,string name){try{await action();}catch(AuthenticationFailure e)when(e.StatusCode==status){checks++;return;}throw new Exception(name);}
sealed class SyntheticPolicies:IHrFoundationPolicyReader {
 public HrFoundationPolicyResolution<HrFoundationFieldRules> Fields=new("none",null,null,null);
 public HrFoundationPolicyResolution<HrFoundationLifecycleRules> Lifecycle=new("none",null,null,null);
 public HrFoundationPolicyResolution<HrFoundationLifecycleRules>? FutureLifecycle;
 public Task<HrFoundationPolicyResolution<HrFoundationFieldRules>> GetFieldsAsync(DateOnly date,CancellationToken ct)=>Task.FromResult(Fields);
 public Task<HrFoundationPolicyResolution<HrFoundationLifecycleRules>> GetLifecycleAsync(DateOnly date,CancellationToken ct)=>Task.FromResult(date>new DateOnly(2026,10,13)?FutureLifecycle??Lifecycle:Lifecycle);
 public Task<HrFoundationPolicyResolution<HrFoundationRecordRules>> GetRecordsAsync(DateOnly date,CancellationToken ct)=>Task.FromResult(new HrFoundationPolicyResolution<HrFoundationRecordRules>("none",null,null,null));
}
