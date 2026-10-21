#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=B03VerifyAlternativePolicies.packages.lock.json
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.EmployeeDocuments;

var checks = 0;
void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
async Task Fails(Func<Task> action, string code)
{ checks++; try { await action(); } catch(AuthenticationFailure e) when(e.Code==code) { return; } throw new Exception("Expected " + code); }
var f = new PolicyFixture(); var date = new DateOnly(2026,10,8);
await Fails(()=>f.Service.CaptureAsync(f.Employee,date,CorrectionAlternativeKind.Medical,Guid.NewGuid(),null,f.Clock.Now,default),"alternative_evidence_policy_required");
var request = new ConfigureCorrectionAlternativePolicy(CorrectionAlternativeKind.Medical,new(2026,10,13),0,false,[],"SYNTHETIC WRITTEN-EVIDENCE APPROVAL","Configure test rules explicitly");
var first = await f.Service.ConfigureAsync(request,"policy-configure-001",f.Context,default);
Check(first.Version==1 && !first.RequiresSupportingDocuments && f.Audits.Count==1 && f.Receipts.Count==1,"actual publication persists protected immutable audit and receipt, not invented defaults");
Check(JsonSerializer.Serialize(await f.Service.ConfigureAsync(request,"policy-configure-001",f.Context,default))==JsonSerializer.Serialize(first) && f.Audits.Count==2,"same-key retry adds replay audit only, no policy version");
Check((await f.Service.HistoryAsync(f.Context,default)).Count==1,"history excludes replay audit and validates authoritative policy evidence");
await Fails(()=>f.Service.ConfigureAsync(request with {RequiresSupportingDocuments=true,AllowedDocumentTypes=[EmployeeDocumentType.Other]},"policy-configure-001",f.Context,default),"idempotency_key_reused");
await Fails(()=>f.Service.ConfigureAsync(request,"policy-configure-002",f.Context,default),"alternative_policy_version_conflict");
var captured = await f.Service.CaptureAsync(f.Employee,date,CorrectionAlternativeKind.Medical,first.Id,null,f.Clock.Now,default);
Check(captured.Documents.Count==0 && captured.Policy.Id==first.Id,"explicit approved written-only rules permit no supporting documents without claiming a medical certificate");
await f.Service.RevalidateAsync(f.Employee,date,captured,f.Clock.Now,default);
checks++;
f.IsFixed=false; await Fails(()=>f.Service.ConfigureAsync(request,"policy-configure-001",f.Context,default),"access_denied"); f.IsFixed=true;
f.Role="CUSTOM_HR"; await Fails(()=>f.Service.ConfigureAsync(request,"policy-configure-001",f.Context,default),"access_denied"); f.Role="ADMIN";
f.Actor=f.Actor with {MfaSatisfied=false}; await Fails(()=>f.Service.HistoryAsync(f.Context,default),"access_denied"); f.Actor=f.Actor with {MfaSatisfied=true};
await Fails(()=>f.Service.ConfigureAsync(request with {ExpectedLatestVersion=1,EffectiveFrom=new(2026,10,12)},"policy-backdate-001",f.Context,default),"alternative_policy_must_be_prospective");
var docRequest=request with {ExpectedLatestVersion=1,EffectiveFrom=new(2026,10,14),RequiresSupportingDocuments=true,AllowedDocumentTypes=[EmployeeDocumentType.Other]};
var second=await f.Service.ConfigureAsync(docRequest,"policy-docs-002",f.Context,default); f.Clock.Now=f.Clock.Now.AddDays(1);
await Fails(()=>f.Service.RevalidateAsync(f.Employee,date,captured,f.Clock.Now,default),"alternative_policy_version_conflict");
await Fails(()=>f.Service.CaptureAsync(f.Employee,date,CorrectionAlternativeKind.Medical,second.Id,null,f.Clock.Now,default),"alternative_supporting_documents_required");
var document = new EmployeeDocument(f.Employee,EmployeeDocumentType.Other,"synthetic-private-key","synthetic.pdf","application/pdf",100,new string('A',64),f.Actor.AccountId.ToString(),
    new(2026,10,1,0,0,0,TimeSpan.Zero),issuedOn:new(2026,10,1),privacy:EmployeeDocumentPrivacy.Medical);
PolicyFixture.Set(document,"RowVersion",BitConverter.GetBytes(1L)); f.Documents.Add(document);
await Fails(()=>f.Service.CaptureAsync(f.Employee,date,CorrectionAlternativeKind.Medical,second.Id,[document.Id],f.Clock.Now,default),"alternative_supporting_documents_unavailable");
document.RecordScan(DocumentScanStatus.Clean,true,"SYNTHETIC SCAN ONLY",f.Clock.Now.AddMinutes(-2));
document.RecordVerification(DocumentVerificationStatus.Verified,f.Actor.AccountId.ToString(),"Synthetic validation",f.Clock.Now.AddMinutes(-1));
var withDoc=await f.Service.CaptureAsync(f.Employee,date,CorrectionAlternativeKind.Medical,second.Id,[document.Id],f.Clock.Now,default);
Check(withDoc.Documents.Single().Sha256==document.Sha256 && !JsonSerializer.Serialize(withDoc).Contains("synthetic-private-key"),"evidence retains verified digest/version without private file key or medical file content");
PolicyFixture.Set(document,"RowVersion",BitConverter.GetBytes(2L));
await Fails(()=>f.Service.RevalidateAsync(f.Employee,date,withDoc,f.Clock.Now,default),"alternative_evidence_version_conflict");
PolicyFixture.Set(document,"RowVersion",BitConverter.GetBytes(1L));
document.ChangeLifecycle(EmployeeDocumentLifecycleStatus.Revoked,f.Actor.AccountId.ToString(),"Synthetic evidence withdrawn",f.Clock.Now);
await Fails(()=>f.Service.RevalidateAsync(f.Employee,date,withDoc,f.Clock.Now,default),"alternative_supporting_documents_unavailable");
var foreign = Guid.NewGuid();
await Fails(()=>f.Service.CaptureAsync(foreign,date,CorrectionAlternativeKind.Medical,second.Id,[document.Id],f.Clock.Now,default),"alternative_supporting_documents_unavailable");
await Fails(()=>f.Service.ConfigureAsync(docRequest with {Kind=CorrectionAlternativeKind.Accessibility,ExpectedLatestVersion=0,AllowedDocumentTypes=[]},"policy-invalid-doc-001",f.Context,default),"invalid_alternative_evidence_policy");
f.Actor=f.Actor with {EmployeeId=f.Employee,Client=SessionClient.Mobile,MfaSatisfied=false};f.Grants=[new(CorrectionPermissions.Self,false,Guid.NewGuid(),AccessScopeKind.Self,f.Employee)];
var safe = await f.Service.MineAsync(CorrectionAlternativeKind.Medical,f.Context,default);
Check(safe.PolicyId==second.Id && safe.RequiresSupportingDocuments==true && !JsonSerializer.Serialize(safe).Contains("RecordedBy"),"self requirements disclose only configured requirements, no private approval actor or document payload");
var unknownTiming=new PolicyFixture();unknownTiming.Grants=unknownTiming.Grants.Select(x=>x with {AuthorityTimingKnown=false}).ToList();
await Fails(()=>unknownTiming.Service.ConfigureAsync(request,"unknown-timing-001",unknownTiming.Context,default),"access_denied");
Check(unknownTiming.Audits.Count==0 && unknownTiming.Receipts.Count==0,"unknown grant timing cannot publish special-case policy");
var futureTiming=new PolicyFixture();futureTiming.Grants=futureTiming.Grants.Select(x=>x with {ValidFromUtc=futureTiming.Clock.Now.AddMinutes(1)}).ToList();
await Fails(()=>futureTiming.Service.ConfigureAsync(request,"future-timing-001",futureTiming.Context,default),"access_denied");
var crossedTiming=new PolicyFixture();crossedTiming.Grants=crossedTiming.Grants.Select(x=>x with {ValidUntilUtc=crossedTiming.Clock.Now.AddSeconds(30)}).ToList();
crossedTiming.AdvanceDuringGrantRead=true;
await Fails(()=>crossedTiming.Service.ConfigureAsync(request,"crossed-timing-001",crossedTiming.Context,default),"access_denied");
Check(crossedTiming.Audits.Count==0 && crossedTiming.Receipts.Count==0,"grant expiry during query is evaluated using time after query");
Console.WriteLine($"B03 alternative evidence policy checks passed: {checks}. Actual services, synthetic adapters only; no SQL or document scanner acceptance.");

public sealed class PolicyClock : TimeProvider
{ public DateTimeOffset Now=new(2026,10,13,6,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Now; }
public class PolicyProxy : DispatchProxy
{
    public Func<MethodInfo,object?[],object?> Handler=null!;
    protected override object? Invoke(MethodInfo? m,object?[]? a)=>Handler(m!,a??[]);
    public static T Make<T>(Func<MethodInfo,object?[],object?> handler) where T:class
    {var value=Create<T,PolicyProxy>();((PolicyProxy)(object)value).Handler=handler;return value;}
}
public sealed class PolicyFixture
{
    public static void Set(object value,string name,object? content)=>value.GetType().GetProperty(name)!.SetValue(value,content);
    public PolicyClock Clock=new(); public Guid Employee=Guid.NewGuid();
    public AuthenticatedAccount Actor=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Web,true);
    public bool IsFixed=true;public string Role="ADMIN";public List<PermissionGrant> Grants=[];
    public bool AdvanceDuringGrantRead;
    public List<AuditEvent> Audits=[];public List<IdempotencyReceipt> Receipts=[];public List<EmployeeDocument> Documents=[];
    public Dictionary<string,(string Purpose,byte[] Bytes)> Files=[];
    public CorrectionAlternativePolicyService Service;
    public AuthRequestContext Context=new("127.0.0.1","synthetic-alternative-policy");
    public PolicyFixture()
    {
        Grants=[new(CorrectionPermissions.AlternativesConfigure,true,Guid.NewGuid(),AccessScopeKind.All,null) { AuthorityTimingKnown=true, ValidFromUtc=Clock.Now.AddDays(-1) }];
        var current=PolicyProxy.Make<ICurrentAuthentication>((m,a)=>Actor);
        var access=PolicyProxy.Make<IAccountAccessReader>((m,a)=>{
            var result=Grants.ToArray();if(AdvanceDuringGrantRead){Clock.Now=Clock.Now.AddMinutes(1);AdvanceDuringGrantRead=false;}
            return Task.FromResult<IReadOnlyList<PermissionGrant>>(result);
        });
        var auth=PolicyProxy.Make<IAccessManagementRepository>((m,a)=>m.Name switch {
            "InTransactionAsync"=>GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this,a),
            "IsCurrentSystemRoleAssignmentAsync"=>Task.FromResult(IsFixed),"GetAssignmentRoleCodeAsync"=>Task.FromResult<string?>(Role),_=>throw new Exception(m.Name)});
        var journal=PolicyProxy.Make<ISchedulingJournalRepository>((m,a)=>m.Name switch {
            "IsCurrentSessionAsync"=>Task.FromResult(true),"FindReceiptAsync"=>Task.FromResult(Receipts.SingleOrDefault(x=>x.ActorUserId==(string)a[0]!&&x.Operation==(string)a[1]!&&x.Key==(string)a[2]!)),
            "AddReceipt"=>AddReceipt((IdempotencyReceipt)a[0]!),"AddAudit"=>AddAudit((AuditEvent)a[0]!),"SaveChangesAsync"=>Task.CompletedTask,_=>throw new Exception(m.Name)});
        var files=PolicyProxy.Make<IPrivateFileStore>((m,a)=>m.Name=="WriteAsync"?Task.FromResult(Write((byte[])a[0]!, (string)a[1]!)):Task.FromResult(Read((PrivateFileReference)a[0]!, (string)a[1]!)));
        var authentication=PolicyProxy.Make<IAuthenticationRepository>((m,a)=>throw new Exception("No face/device access"));
        var operation=new AttendanceOperation(current,access,auth,journal,authentication,Clock,files);
        var repository=PolicyProxy.Make<ICorrectionAlternativePolicyRepository>((m,a)=>m.Name=="HistoryAsync"
            ?Task.FromResult<IReadOnlyList<AuditEvent>>(Audits.Where(x=>x.EventType==CorrectionAlternativePolicyService.ConfigureOperation).ToArray())
            :Task.FromResult<IReadOnlyList<EmployeeDocument>>(Documents.Where(x=>x.EmployeeId==(Guid)a[0]!&&((IReadOnlyList<Guid>)a[1]!).Contains(x.Id)).ToArray()));
        Service=new(operation,repository,access,auth,Clock);
    }
    private object? AddReceipt(IdempotencyReceipt x){Receipts.Add(x);return null;}
    private object? AddAudit(AuditEvent x){Audits.Add(x);return null;}
    private PrivateFileReference Write(byte[] bytes,string purpose)
    {var key=Guid.NewGuid().ToString("N");Files[key]=(purpose,bytes);return new(key,"1",Convert.ToHexString(SHA256.HashData(bytes)),"synthetic",bytes.Length);}
    private byte[] Read(PrivateFileReference reference,string purpose)
    {var value=Files[reference.StorageKey];if(value.Purpose!=purpose)throw new Exception("Purpose mismatch");return value.Bytes;}
    public async Task<T> Transaction<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct)
    {var audits=Audits.Count;var receipts=Receipts.Count;try{return await action(ct);}catch{Audits.RemoveRange(audits,Audits.Count-audits);Receipts.RemoveRange(receipts,Receipts.Count-receipts);throw;}}
}
