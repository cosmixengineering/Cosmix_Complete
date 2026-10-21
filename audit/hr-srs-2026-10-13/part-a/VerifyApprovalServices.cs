#:project C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Approvals;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Application.HR.Employees;

var checks=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name); checks++;}
async Task Failure(string code,Func<Task> action){try{await action();throw new Exception("Expected "+code);}catch(AuthenticationFailure e){Check(e.Code==code,e.Code);}}
var clock=new TestClock();
var actor=new AuthenticatedAccount(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Web,true);
var current=Proxy.Create<ICurrentAuthentication>((m,a)=>actor);
var grants=new List<PermissionGrant>();
void Grant(string p)=>grants.Add(new(p,true,Guid.NewGuid(),AccessScopeKind.All,null));
foreach(var p in new[]{ApprovalPermissions.Read,ApprovalPermissions.Manage,ApprovalPermissions.Delegate,ApprovalPermissions.Review})Grant(p);
var target=Account(Guid.NewGuid());
var source=Account(actor.AccountId,actor.EmployeeId);
var isSuperAdmin=true; var employment=true;
var authorization=Proxy.Create<IAccessManagementRepository>((m,a)=>m.Name switch{
 "InTransactionAsync"=>((Delegate)a![0]!).DynamicInvoke(a[1]),
 "IsCurrentAdministratorAsync"=>Task.FromResult(true),
 "IsCurrentSuperAdminAsync"=>Task.FromResult(isSuperAdmin),
 "GetAssignmentRoleCodeAsync"=>Task.FromResult<string?>("SYNTHETIC"),
 "ScopeTargetExistsAsync"=>Task.FromResult(true),
 "IsEmployeeActiveForAccessAsync"=>Task.FromResult(employment),
 "FindAccountAsync"=>Task.FromResult<UserAccount?>((Guid)a![0]! == target.Id ? target : source),
 _=>throw new Exception("Unexpected auth method "+m.Name)});
var grantByAccount=new Dictionary<Guid,List<PermissionGrant>>{{actor.AccountId,grants}};
var reader=Proxy.Create<IAccountAccessReader>((m,a)=>Task.FromResult<IReadOnlyList<PermissionGrant>>(grantByAccount.TryGetValue((Guid)a![0]!,out var rows)?rows.ToArray():Array.Empty<PermissionGrant>()));
var repo=new MemoryRepository(); var files=new MemoryFiles();
var service=new ApprovalRoutingService(current,reader,authorization,repo,files,clock);
var requestContext=new AuthRequestContext("synthetic","synthetic-correlation");
var date=DateOnly.FromDateTime(clock.GetUtcNow().DateTime);
var publication=new PublishApprovalRouteRequest("CLAIM_STAGE_1",0,"allowance",ApprovalPermissions.Review,1,date,null,"Synthetic approved authority fixture");
await Failure("invalid_approval_reason",()=>service.PublishAsync(publication with{Reason=" "},"test-publish-invalid",requestContext,default));
isSuperAdmin=false;
await Failure("access_denied",()=>service.PublishAsync(publication,"test-publish-role",requestContext,default));
isSuperAdmin=true;
var published=await service.PublishAsync(publication,"test-publish-first",requestContext,default);
var replay=await service.PublishAsync(publication,"test-publish-first",requestContext,default);
Check(published.Id==replay.Id && repo.Rules.Count==1,"Identical route replay does not duplicate rule");
await Failure("idempotency_key_reused",()=>service.PublishAsync(publication with{Reason="Changed"},"test-publish-first",requestContext,default));
await Failure("approval_route_version_conflict",()=>service.PublishAsync(publication,"test-publish-new-key",requestContext,default));
await Failure("fixed_approval_route",()=>service.PublishAsync(publication with{RequestType="leave"},"test-publish-fixed",requestContext,default));
var delegationInput=new GrantApprovalDelegationRequest(target.Id,"allowance",ApprovalPermissions.Review,AccessScopeKind.Employee,Guid.NewGuid(),clock.GetUtcNow().AddHours(1),clock.GetUtcNow().AddHours(4),"Synthetic explicit delegation");
await Failure("invalid_approval_delegation",()=>service.GrantAsync(delegationInput with{DelegateAccountId=actor.AccountId},"test-delegate-self",requestContext,default));
await Failure("invalid_approval_delegation",()=>service.GrantAsync(delegationInput with{ScopeKind=AccessScopeKind.Self},"test-delegate-scope",requestContext,default));
var delegated=await service.GrantAsync(delegationInput,"test-delegate-first",requestContext,default);
clock.Advance(TimeSpan.FromHours(2));
var delegatedReplay=await service.GrantAsync(delegationInput,"test-delegate-first",requestContext,default);
Check(delegated.Id==delegatedReplay.Id && repo.Delegations.Count==1,"Retry inside active window survives elapsed start time");
var reviewGrant=grants.Single(g=>g.Permission==ApprovalPermissions.Review);grants.Remove(reviewGrant);
await Failure("access_denied",()=>service.GrantAsync(delegationInput,"test-delegate-first",requestContext,default));
grants.Add(reviewGrant);
var revoke=new RevokeApprovalDelegationRequest(delegated.Version,"Synthetic revocation");
await Failure("approval_delegation_version_conflict",()=>service.RevokeAsync(delegated.Id,revoke with{ExpectedVersion=Convert.ToBase64String(new byte[]{1,0,0,0,0,0,0,0})},"test-revoke-stale",requestContext,default));
var revoked=await service.RevokeAsync(delegated.Id,revoke,"test-revoke-first",requestContext,default);
Check(revoked.RevokedAt.HasValue,"Delegation revoked with retained reason");
Check((await service.RevokeAsync(delegated.Id,revoke,"test-revoke-first",requestContext,default)).Id==revoked.Id,"Revoke replay retains original result");
Check(repo.Delegations[0].RevocationReason==revoke.Reason,"Revocation reason retained");
var authority=new ApprovalAuthorityService(current,reader,authorization,repo,new ApprovalRouteResolver(),clock);
var beneficiary=Guid.NewGuid();
var routingContext=new ApprovalRoutingContext("allowance",date);
var route=new ApprovalRouteResolver().Resolve(routingContext,repo.Rules);
await Failure("access_denied",()=>authority.RequireAsync(routingContext,route.Fingerprint,published.Id,1,actor.AccountId.ToString(),beneficiary,new(EmployeeId:beneficiary),default));
await Failure("access_denied",()=>authority.RequireAsync(routingContext,route.Fingerprint,published.Id,1,Guid.NewGuid().ToString(),actor.EmployeeId,new(EmployeeId:actor.EmployeeId),default));
await Failure("approval_route_changed",()=>authority.RequireAsync(routingContext,"changed",published.Id,1,Guid.NewGuid().ToString(),beneficiary,new(EmployeeId:beneficiary),default));
var allowed=await authority.RequireAsync(routingContext,route.Fingerprint,published.Id,1,Guid.NewGuid().ToString(),beneficiary,new(EmployeeId:beneficiary),default);
Check(allowed.RouteRuleId==published.Id && allowed.DelegationId is null,"Direct scoped review carries route identity");
grants.Remove(reviewGrant);
await Failure("access_denied",()=>authority.RequireAsync(routingContext,route.Fingerprint,published.Id,1,Guid.NewGuid().ToString(),beneficiary,new(EmployeeId:beneficiary),default));
Check(repo.Audits.Count>0 && repo.Receipts.Count==3,"Transactional audit/receipt metadata recorded");
var hiringResult=new EmployeeWriteResult(Guid.NewGuid(),"AAAAAAAAAAA=",OnboardingStatus.Approved,
    SubmissionId:Guid.NewGuid(),DecisionId:Guid.NewGuid(),AccountId:Guid.NewGuid());
Check(JsonSerializer.Serialize(hiringResult).Length>500,"New DTO fields reproduce hiring receipt overflow with ordinary serializer");
var receiptOptions=(JsonSerializerOptions)typeof(EmployeeOperation).GetField("ReceiptJsonOptions",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
var compact=JsonSerializer.Serialize(hiringResult,receiptOptions);
var hiringReceipt=new IdempotencyReceipt(actor.AccountId.ToString(),"synthetic-hiring","receipt-key-001",new string('A',64),clock.GetUtcNow(),clock.GetUtcNow().AddHours(24));
hiringReceipt.Complete("employee-hiring",compact,clock.GetUtcNow());
Check(compact.Length<=500 && JsonSerializer.Deserialize<EmployeeWriteResult>(compact)==hiringResult,"Compact receipt fits and retains every actual hiring result ID");
var regressionFailures=new List<string>();
async Task RegressionFailure(string code,string name,Func<Task> action){
 try{await action();regressionFailures.Add(name+": expected "+code+", operation succeeded");}
 catch(AuthenticationFailure e){if(e.Code==code)checks++;else regressionFailures.Add(name+": expected "+code+", got "+e.Code);}
}
async Task RegressionSuccess(string name,Func<Task> action){
 try{await action();checks++;}catch(Exception e){regressionFailures.Add(name+": "+e.Message);}
}
grants.Add(reviewGrant);
const string stage2Permission="approvals.stages.2.review";
var rolePermissionGrant=new PermissionGrant(AccessPermissionCatalog.RolePermissionsManage,true,Guid.NewGuid(),AccessScopeKind.All,null);
var administratorType=typeof(AccessManagementService).GetNestedType("Administrator",BindingFlags.NonPublic)!;
var configurationActor=Activator.CreateInstance(administratorType,actor,new PermissionGrant[]{rolePermissionGrant},rolePermissionGrant.RoleAssignmentId,"Admin",clock.GetUtcNow())!;
var configurePermission=typeof(AccessManagementService).GetMethod("RequireRoleConfiguration",BindingFlags.NonPublic|BindingFlags.Static)!;
foreach(var permission in new[]{"approvals.stages.1.review",stage2Permission,"approvals.stages.20.review"})
 await RegressionSuccess("Fixed administrator can configure registered stage permission without personally owning it",()=>{configurePermission.Invoke(null,new[]{configurationActor,permission});return Task.CompletedTask;});
await RegressionFailure("invalid_approval_route","Stage two cannot reuse stage-one permission",()=>service.PublishAsync(publication with{Code="WRONG_STAGE",RequestType="advance",Stage=2},"test-wrong-stage-001",requestContext,default));
await RegressionFailure("invalid_approval_route","Stage one cannot select stage-two permission",()=>service.PublishAsync(publication with{Code="WRONG_PERMISSION",RequestType="advance",ApproverPermission=stage2Permission},"test-wrong-perm-001",requestContext,default));
await RegressionFailure("invalid_approval_route","Generic review permission is unsupported",()=>service.PublishAsync(publication with{Code="OLD_GENERIC",RequestType="advance",ApproverPermission="approvals.requests.review"},"test-generic-perm-001",requestContext,default));
await RegressionFailure("invalid_approval_route","Unsupported stage rejected",()=>service.PublishAsync(publication with{Code="STAGE_21",RequestType="advance",Stage=21,ApproverPermission="approvals.stages.21.review"},"test-stage-21-001",requestContext,default));
var invalidAmounts=new (string Code,decimal? Min,decimal? Max)[]{
 ("MIN_SCALE",100.005m,null),("MAX_SCALE",null,100.005m),
 ("MIN_ZERO_SCALE",100.010m,null),("MAX_ZERO_SCALE",null,100.000m),
 ("MIN_RANGE",10000000000000000m,null),("MAX_RANGE",null,10000000000000000m),
 ("MIN_NEGATIVE",-0.01m,null),("MAX_NEGATIVE",null,-0.01m)};
foreach(var item in invalidAmounts){
 var before=(repo.Rules.Count,repo.Receipts.Count,repo.Audits.Count,files.Count);
 await RegressionFailure("invalid_approval_route",item.Code+" rejected",()=>service.PublishAsync(publication with{Code=item.Code,RequestType="fuel",MinimumAmount=item.Min,MaximumAmount=item.Max,Currency="PKR"},"test-amount-"+item.Code,requestContext,default));
 if(before==(repo.Rules.Count,repo.Receipts.Count,repo.Audits.Count,files.Count))checks++;else regressionFailures.Add(item.Code+": rejected amount left rule/receipt/audit/private evidence");
}
await RegressionSuccess("Supported exact amount boundary remains unchanged",async()=>{
 var exact=await service.PublishAsync(publication with{Code="EXACT_AMOUNT",RequestType="fuel",MinimumAmount=0m,MaximumAmount=9999999999999999.99m,Currency="PKR"},"test-amount-exact-001",requestContext,default);
 Check(exact.MinimumAmount==0m && exact.MaximumAmount==9999999999999999.99m,"Exact decimal(18,2) bounds retained");
});
var loanOne=await service.PublishAsync(publication with{Code="LOAN_STAGE_ONE",RequestType="loan"},"test-loan-stage1-001",requestContext,default);
var loanTwoInput=publication with{Code="LOAN_STAGE_TWO",RequestType="loan",Stage=2,ApproverPermission=stage2Permission};
await RegressionFailure("access_denied","Publisher needs separate stage-two grant",()=>service.PublishAsync(loanTwoInput,"test-loan-stage2-001",requestContext,default));
var stage2Grant=new PermissionGrant(stage2Permission,true,Guid.NewGuid(),AccessScopeKind.All,null);
grants.Add(stage2Grant);
await RegressionSuccess("Stage-two authority can be explicitly delegated",async()=>{
 var second=await service.GrantAsync(delegationInput with{RequestType="loan",Permission=stage2Permission,StartsAt=clock.GetUtcNow().AddHours(1),EndsAt=clock.GetUtcNow().AddHours(4)},"test-stage2-delegate-001",requestContext,default);
 Check(second.Permission==stage2Permission,"Delegated stage-two permission retained");
});
await RegressionFailure("invalid_approval_delegation","Generic review permission cannot be delegated",()=>service.GrantAsync(delegationInput with{Permission="approvals.requests.review"},"test-generic-delegate-001",requestContext,default));
await RegressionFailure("invalid_approval_delegation","Unsupported stage cannot be delegated",()=>service.GrantAsync(delegationInput with{Permission="approvals.stages.21.review"},"test-stage21-delegate-001",requestContext,default));
ApprovalRouteDto? loanTwo=null;
await RegressionSuccess("Stage-two rule publication supported",async()=>loanTwo=await service.PublishAsync(loanTwoInput,"test-loan-stage2-001",requestContext,default));
if(loanTwo is null)repo.Add(new ApprovalRouteRule("LOAN_STAGE_TWO",1,"loan",stage2Permission,2,date,null,"synthetic"));
var loanContext=new ApprovalRoutingContext("loan",date);
var loanRoute=new ApprovalRouteResolver().Resolve(loanContext,repo.Rules);
var loanStageTwo=loanRoute.Stages.Single(x=>x.Stage==2).Rule;
grants.Remove(stage2Grant);
await RegressionFailure("access_denied","Stage-one grant does not authorize stage two",()=>authority.RequireAsync(loanContext,loanRoute.Fingerprint,loanStageTwo.Id,2,Guid.NewGuid().ToString(),beneficiary,new(EmployeeId:beneficiary),default));
grants.Add(stage2Grant);
await RegressionSuccess("Separate stage-two grant authorizes stage two",async()=>{
 var second=await authority.RequireAsync(loanContext,loanRoute.Fingerprint,loanStageTwo.Id,2,Guid.NewGuid().ToString(),beneficiary,new(EmployeeId:beneficiary),default);
 Check(second.RoleAssignmentId==stage2Grant.RoleAssignmentId && second.RoleAssignmentId!=reviewGrant.RoleAssignmentId,"Distinct stage grant identity retained");
});
var sourceActor=actor;
actor=new(target.Id,target.EmployeeId,Guid.NewGuid(),1,SessionClient.Web,true);
var wrongStageDelegation=new ApprovalDelegation(sourceActor.AccountId.ToString(),actor.AccountId.ToString(),"loan",clock.GetUtcNow().AddMinutes(-1),clock.GetUtcNow().AddHours(1),sourceActor.AccountId.ToString(),"Synthetic stage-one delegation",ApprovalPermissions.Review,AccessScopeKind.All,null);
repo.Add(wrongStageDelegation);
await RegressionFailure("access_denied","Stage-one delegation does not authorize stage two",()=>authority.RequireAsync(loanContext,loanRoute.Fingerprint,loanStageTwo.Id,2,Guid.NewGuid().ToString(),beneficiary,new(EmployeeId:beneficiary),default));
var rightStageDelegation=new ApprovalDelegation(sourceActor.AccountId.ToString(),actor.AccountId.ToString(),"loan",clock.GetUtcNow().AddMinutes(-1),clock.GetUtcNow().AddHours(1),sourceActor.AccountId.ToString(),"Synthetic stage-two delegation",stage2Permission,AccessScopeKind.All,null);
repo.Add(rightStageDelegation);
await RegressionSuccess("Stage-two delegation authorizes only selected stage",async()=>{
 var second=await authority.RequireAsync(loanContext,loanRoute.Fingerprint,loanStageTwo.Id,2,Guid.NewGuid().ToString(),beneficiary,new(EmployeeId:beneficiary),default);
 Check(second.DelegationId==rightStageDelegation.Id && second.RoleAssignmentId==stage2Grant.RoleAssignmentId,"Stage-two delegation and source grant retained");
});
actor=sourceActor;
foreach(var failure in regressionFailures)Console.WriteLine("FAIL "+failure);
if(regressionFailures.Count>0)throw new Exception($"{regressionFailures.Count} approval regression checks failed");
Console.WriteLine($"PASS {checks} actual routing service/authority checks (synthetic repositories/private store; no SQL/provider acceptance)");

static void Set(object instance,string property,object value)=>instance.GetType().GetProperty(property)!.SetValue(instance,value);
static UserAccount Account(Guid id,Guid? employeeId=null){
 var e=new Employee(DateTimeOffset.UtcNow,"synthetic"); if(employeeId.HasValue)Set(e,"Id",employeeId.Value);
 Set(e,"Status",EmployeeStatus.Active);Set(e,"OnboardingStatus",OnboardingStatus.Approved);Set(e,"JoinedOn",new DateOnly(2026,1,1));
 var d=(EmployeeOnboardingDecision)Activator.CreateInstance(typeof(EmployeeOnboardingDecision),true)!;
 Set(d,"Id",Guid.NewGuid());Set(d,"EmployeeId",e.Id);Set(d,"Decision",OnboardingDecision.Approve);Set(e,"ApprovedOnboardingDecisionId",d.Id);
 var a=(UserAccount)Activator.CreateInstance(typeof(UserAccount),true)!;
 Set(a,"Id",id);Set(a,"EmployeeId",e.Id);Set(a,"Employee",e);Set(a,"ApprovalDecision",d);Set(a,"ApprovalDecisionId",d.Id);Set(a,"Status",AccountStatus.Active);Set(a,"WebAccessEnabled",true);return a;
}
public sealed class TestClock:TimeProvider{
 DateTimeOffset now=new(2026,10,13,0,0,0,TimeSpan.Zero);
 public override DateTimeOffset GetUtcNow()=>now;
 public void Advance(TimeSpan amount)=>now+=amount;
}
public class Proxy:DispatchProxy{
 public Func<MethodInfo,object?[]?,object?> Handler=null!;
 protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!,args);
 public static T Create<T>(Func<MethodInfo,object?[]?,object?> handler)where T:class{var value=DispatchProxy.Create<T,Proxy>();((Proxy)(object)value).Handler=handler;return value;}
}
public sealed class MemoryFiles:IPrivateFileStore{
 readonly Dictionary<string,(string Purpose,byte[] Bytes)> content=[];
 public int Count=>content.Count;
 public Task<PrivateFileReference> WriteAsync(byte[] bytes,string purpose,CancellationToken ct){var key=Guid.NewGuid().ToString("N");content[key]=(purpose,bytes.ToArray());return Task.FromResult(new PrivateFileReference(key,"1",Convert.ToHexString(SHA256.HashData(bytes)),"synthetic-key",bytes.Length));}
 public Task<byte[]> ReadAsync(PrivateFileReference reference,string purpose,CancellationToken ct){var value=content[reference.StorageKey];if(value.Purpose!=purpose)throw new Exception("Wrong purpose");return Task.FromResult(value.Bytes.ToArray());}
}
public sealed class MemoryRepository:IApprovalRoutingRepository{
 public List<ApprovalRouteRule> Rules=[];public List<ApprovalDelegation> Delegations=[];public List<IdempotencyReceipt> Receipts=[];public List<AuditEvent> Audits=[];
 public Task<IReadOnlyList<ApprovalRouteRule>> RulesAsync(string type,CancellationToken ct)=>Task.FromResult<IReadOnlyList<ApprovalRouteRule>>(Rules.Where(x=>x.RequestType==type).ToArray());
 public Task<IReadOnlyList<ApprovalRouteRule>> VersionsAsync(string code,CancellationToken ct)=>Task.FromResult<IReadOnlyList<ApprovalRouteRule>>(Rules.Where(x=>x.Code==code).OrderBy(x=>x.Version).ToArray());
 public Task ValidateTargetsAsync(Guid? a,Guid? b,Guid? c,CancellationToken ct)=>Task.CompletedTask;
 public Task<IReadOnlyList<ApprovalDelegation>> DelegationsAsync(Guid id,string type,DateTimeOffset now,CancellationToken ct)=>Task.FromResult<IReadOnlyList<ApprovalDelegation>>(Delegations.Where(x=>x.DelegateUserId==id.ToString() && x.RequestType==type && x.StartsAt<=now && now<x.EndsAt && x.RevokedAt==null).ToArray());
 public Task<ApprovalDelegation?> DelegationAsync(Guid id,CancellationToken ct)=>Task.FromResult(Delegations.SingleOrDefault(x=>x.Id==id));
 public Task<IReadOnlyList<ApprovalDelegation>> OwnDelegationsAsync(Guid id,CancellationToken ct)=>Task.FromResult<IReadOnlyList<ApprovalDelegation>>(Delegations.Where(x=>x.DelegatorUserId==id.ToString()).ToArray());
 public Task<IdempotencyReceipt?> ReceiptAsync(string actor,string operation,string key,CancellationToken ct)=>Task.FromResult(Receipts.SingleOrDefault(x=>x.ActorUserId==actor && x.Operation==operation && x.Key==key));
 public void Add(ApprovalRouteRule rule)=>Rules.Add(rule);
 public void Add(ApprovalDelegation d){typeof(ApprovalDelegation).GetProperty("RowVersion")!.SetValue(d,new byte[8]);Delegations.Add(d);}
 public void Add(IdempotencyReceipt r)=>Receipts.Add(r);public void Add(AuditEvent a)=>Audits.Add(a);
 public void ExpectVersion(ApprovalDelegation d,byte[] version){}
 public Task SaveAsync(CancellationToken ct)=>Task.CompletedTask;
}
