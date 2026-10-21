using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Organization;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Organization;
using Cosmix.Domain.HR.Policies;

static class ServiceChecks
{
    public static async Task<int> Run(StaffTaxonomyRules rules)
    {
        var count=0;void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);count++;}
        Check(typeof(StaffTaxonomyService).GetMethod("CancelAsync")!=null,"stale pending proposal can be cancelled before a fresh request");
        async Task Reject(Func<Task> action,string code,string name){try{await action();throw new Exception("FAIL: "+name);}catch(AuthenticationFailure e){Check(e.Code==code,name+": "+e.Code);}}
        var context=new AuthRequestContext("127.0.0.1","synthetic-taxonomy-service");
        var input=new RequestStaffTaxonomy(null,0,new(2026,10,15),new(2026,10,18),rules,"synthetic request reason","synthetic management evidence");
        var expiredDuringIo=new Fixture();expiredDuringIo.Grant(StaffTaxonomyPermissions.Request);
        expiredDuringIo.SessionExpiresAt=expiredDuringIo.Clock.Now.AddSeconds(1);
        expiredDuringIo.Store.OnWrite=purpose=>{if(purpose.EndsWith("/audit",StringComparison.Ordinal))expiredDuringIo.Clock.Now=expiredDuringIo.Clock.Now.AddSeconds(2);};
        await Reject(()=>expiredDuringIo.Service.RequestAsync(input,"taxonomy-expiry-during-io",context,default),"access_denied","session expires during private write before commit");
        var grantChanged=new Fixture();grantChanged.Grant(StaffTaxonomyPermissions.Request);
        grantChanged.Store.OnWrite=purpose=>{if(purpose.EndsWith("/audit",StringComparison.Ordinal)){grantChanged.Grants.Clear();grantChanged.Grant(StaffTaxonomyPermissions.Request);}};
        await Reject(()=>grantChanged.Service.RequestAsync(input,"taxonomy-assignment-change",context,default),"access_denied","replacement valid grant cannot substitute original assignment during private IO");
        var actorChanged=new Fixture();actorChanged.Grant(StaffTaxonomyPermissions.Request);
        actorChanged.Store.OnWrite=purpose=>{if(purpose.EndsWith("/audit",StringComparison.Ordinal))actorChanged.Current.Actor=actorChanged.Current.Actor with{SessionId=Guid.NewGuid()};};
        await Reject(()=>actorChanged.Service.RequestAsync(input,"taxonomy-session-change",context,default),"access_denied","replacement live session cannot substitute original identity");
        var crossedDate=new Fixture();crossedDate.Grant(StaffTaxonomyPermissions.Request);
        crossedDate.Store.OnWrite=purpose=>{if(purpose.EndsWith("/audit",StringComparison.Ordinal))crossedDate.Clock.Now=new(2026,10,15,7,0,0,TimeSpan.Zero);};
        await Reject(()=>crossedDate.Service.RequestAsync(input,"taxonomy-midnight-date",context,default),"staff_taxonomy_future_date_required","future effective date rechecked after private IO");
        var authorityExpired=new Fixture(new(){Mode=StaffTaxonomyAuthorityMode.IndependentFixedAdministrator,ApprovedVersion="synthetic-expiring-authority",EffectiveFrom=new(2026,1,1),EffectiveTo=new(2026,10,16)});authorityExpired.Grant(StaffTaxonomyPermissions.Request);
        authorityExpired.Store.OnWrite=purpose=>{if(purpose.EndsWith("/audit",StringComparison.Ordinal))authorityExpired.Clock.Now=new(2026,10,16,7,0,0,TimeSpan.Zero);};
        await Reject(()=>authorityExpired.Service.RequestAsync(input,"taxonomy-authority-expiry",context,default),"staff_taxonomy_authority_approval_required","authority window rechecked after private IO");
        var candidateChanged=new Fixture();candidateChanged.Grant(StaffTaxonomyPermissions.Request);
        candidateChanged.Store.OnWrite=purpose=>{if(purpose.EndsWith("/audit",StringComparison.Ordinal))candidateChanged.Repo.Policies.Single().AdvanceRevision();};
        await Reject(()=>candidateChanged.Service.RequestAsync(input,"taxonomy-candidate-change",context,default),"staff_taxonomy_candidate_changed","written candidate revision rechecked after private IO");
        var becameLocked=new Fixture();becameLocked.Grant(StaffTaxonomyPermissions.Request);
        becameLocked.Store.OnWrite=purpose=>{if(purpose.EndsWith("/audit",StringComparison.Ordinal))becameLocked.Repo.Locked=true;};
        await Reject(()=>becameLocked.Service.RequestAsync(input,"taxonomy-period-lock",context,default),"staff_taxonomy_period_locked","period lock rechecked after private IO");
        var expiredReview=new Fixture();expiredReview.Grant(StaffTaxonomyPermissions.Request);
        var reviewRequest=await expiredReview.Service.RequestAsync(input,"taxonomy-expiry-proposal",context,default);
        expiredReview.Current.Actor=expiredReview.Current.Actor with{AccountId=Guid.NewGuid(),EmployeeId=Guid.NewGuid(),SessionId=Guid.NewGuid()};expiredReview.Grants.Clear();expiredReview.Grant(StaffTaxonomyPermissions.Review);
        expiredReview.FixedExpiresAt=expiredReview.Clock.Now.AddSeconds(1);
        expiredReview.Store.OnWrite=purpose=>{if(purpose.EndsWith("/audit",StringComparison.Ordinal))expiredReview.Clock.Now=expiredReview.Clock.Now.AddSeconds(2);};
        await Reject(()=>expiredReview.Service.DecideAsync(reviewRequest.Request.Id,new(reviewRequest.Policy.Version,reviewRequest.Request.Version,ApprovalState.Approved,"synthetic review","synthetic proof"),"taxonomy-expiry-review",context,default),"access_denied","fixed reviewer assignment expires during private IO");
        var disabled=new Fixture(new());disabled.Grant(StaffTaxonomyPermissions.Request);
        await Reject(()=>disabled.Service.RequestAsync(input,"taxonomy-disabled",context,default),"staff_taxonomy_authority_approval_required","unconfigured authority before mutation");
        Check(disabled.Repo.Policies.Count==0&&disabled.Repo.Requests.Count==0,"unconfigured gate no policy/request");
        var badAudit=new Fixture();badAudit.Grant(StaffTaxonomyPermissions.Request);badAudit.Store.BadAuditMetadata=true;
        // This synthetic collaborator has no SQL rollback. The assertion verifies rejection, not transaction persistence.
        await Reject(()=>badAudit.Service.RequestAsync(input,"taxonomy-bad-audit",context,default),"staff_taxonomy_private_write_invalid","incorrect private audit metadata rejected");
        var fixture=new Fixture();fixture.Grant(StaffTaxonomyPermissions.Request);
        var requested=await fixture.Service.RequestAsync(input,"taxonomy-request-001",context,default);
        Check(fixture.Repo.Requests.Count==1&&fixture.Repo.Versions.Count==0,"request does not publish");
        Check((await fixture.Resolver.ResolveAsync("OFFICE",new(2026,10,16),default)).Baseline,"pending proposal preserves confirmed baseline");
        var retry=await fixture.Service.RequestAsync(input,"taxonomy-request-001",context,default);
        Check(retry.Request.Id==requested.Request.Id&&fixture.Repo.Requests.Count==1,"identical request replay exact ID");
        await Reject(()=>fixture.Service.RequestAsync(input with{Reason="different"},"taxonomy-request-001",context,default),"idempotency_key_reused","different payload same receipt key");
        await Reject(()=>fixture.Service.RequestAsync(input with{ExpectedPolicyVersion=requested.Policy.Version,ExpectedRevision=1},"taxonomy-pending-002",context,default),"staff_taxonomy_request_pending","pending request prevents competing proposal");
        fixture.Grant(StaffTaxonomyPermissions.Review);
        var decision=new DecideStaffTaxonomy(requested.Policy.Version,requested.Request.Version,ApprovalState.Approved,"synthetic decision reason","synthetic independent authority evidence");
        await Reject(()=>fixture.Service.DecideAsync(requested.Request.Id,decision,"taxonomy-self-review",context,default),"access_denied","requester cannot independently review");
        fixture.Current.Actor=fixture.Current.Actor with{AccountId=Guid.NewGuid(),EmployeeId=Guid.NewGuid(),SessionId=Guid.NewGuid()};fixture.Grants.Clear();fixture.Grant(StaffTaxonomyPermissions.Review);
        fixture.Fixed=false;
        await Reject(()=>fixture.Service.DecideAsync(requested.Request.Id,decision,"taxonomy-custom-review",context,default),"access_denied","review requires same fixed assignment");fixture.Fixed=true;
        await Reject(()=>fixture.Service.DecideAsync(requested.Request.Id,decision with{ExpectedRequestVersion=Convert.ToBase64String(new byte[8])},"taxonomy-stale-review",context,default),"staff_taxonomy_request_version_conflict","stale approval rowversion");
        var published=await fixture.Service.DecideAsync(requested.Request.Id,decision,"taxonomy-review-001",context,default);
        Check(published.PublishedVersionId.HasValue&&fixture.Repo.Versions.Count==1&&fixture.Repo.Decisions.Count==1,"independent review publishes one immutable version");
        var timeline=await fixture.Resolver.VerifiedTimelineAsync(default);Check(timeline.Single().EvidenceValid,"exact approved request/decision/protected proof provenance");
        Check((await fixture.Resolver.ResolveAsync("DRIVER",new(2026,10,16),default)).Available,"approved broader category async resolver");
        Check(!(await fixture.Resolver.ResolveForRangeAsync("DRIVER",new(2026,10,16),new(2026,10,19),default)).Available,"range expiry failclosed");
        Check((await fixture.Resolver.ResolveForRangeAsync("DRIVER",new(2026,10,16),new(2026,10,18),default)).Available,"end-exclusive expiry boundary");
        var decisionRetry=await fixture.Service.DecideAsync(requested.Request.Id,decision,"taxonomy-review-001",context,default);
        Check(decisionRetry.PublishedVersionId==published.PublishedVersionId&&fixture.Repo.Versions.Count==1,"decision retry exact published ID");
        fixture.Grants.Clear();await Reject(()=>fixture.Service.DecideAsync(requested.Request.Id,decision,"taxonomy-review-001",context,default),"access_denied","revoked permissions deny receipt replay");
        fixture.Grant(StaffTaxonomyPermissions.Review);fixture.Live=false;
        await Reject(()=>fixture.Service.DecideAsync(requested.Request.Id,decision,"taxonomy-review-001",context,default),"access_denied","revoked live session denies replay");fixture.Live=true;
        await Reject(()=>fixture.Service.DecideAsync(requested.Request.Id,decision with{ExpectedPolicyVersion=published.Policy.Version,ExpectedRequestVersion=published.Request.Version},"taxonomy-new-decision",context,default),"staff_taxonomy_request_closed","closed request never decides twice");
        fixture.Store.Tamper=true;Check((await fixture.Resolver.ResolveAsync("DRIVER",new(2026,10,16),default)).State=="invalid_taxonomy_evidence","tampered private content never eligibility");fixture.Store.Tamper=false;
        fixture.Current.Actor=fixture.Current.Actor with{Client=SessionClient.Mobile};
        await Reject(()=>fixture.Service.HistoryAsync(1,25,context,default),"access_denied","mobile cannot use taxonomy administration");
        Check(fixture.Audits.Any(a=>a.EventType.EndsWith(".replay",StringComparison.Ordinal))&&fixture.Audits.Any(a=>a.ProtectedChangesReference!=null),"audited replay and protected write evidence");
        var cancelledFixture=new Fixture();cancelledFixture.Grant(StaffTaxonomyPermissions.Request);
        var cancellationRequest=await cancelledFixture.Service.RequestAsync(input,"taxonomy-cancel-request",context,default);
        var requester=cancelledFixture.Current.Actor;cancelledFixture.SetAuthority(new());
        var cancel=new CancelStaffTaxonomy(cancellationRequest.Policy.Version,cancellationRequest.Request.Version,"synthetic stale-authority cancellation","synthetic cancellation evidence");
        cancelledFixture.Current.Actor=requester with{AccountId=Guid.NewGuid(),EmployeeId=Guid.NewGuid()};
        await Reject(()=>cancelledFixture.Service.CancelAsync(cancellationRequest.Request.Id,cancel,"taxonomy-cancel-other",context,default),"access_denied","only original requester cancels");
        cancelledFixture.Current.Actor=requester;
        await Reject(()=>cancelledFixture.Service.CancelAsync(cancellationRequest.Request.Id,cancel with{ExpectedRequestVersion=Convert.ToBase64String(new byte[8])},"taxonomy-cancel-stale",context,default),"staff_taxonomy_request_version_conflict","cancel uses current request rowversion");
        var cancelled=await cancelledFixture.Service.CancelAsync(cancellationRequest.Request.Id,cancel,"taxonomy-cancel-001",context,default);
        Check(cancelled.Request.State=="Cancelled"&&cancelledFixture.Repo.Versions.Count==0&&cancelledFixture.Repo.Requests.Count==1,"cancel after authority removal preserves request and publishes nothing");
        var cancelRetry=await cancelledFixture.Service.CancelAsync(cancellationRequest.Request.Id,cancel,"taxonomy-cancel-001",context,default);
        Check(cancelRetry.Request.Id==cancelled.Request.Id&&cancelRetry.Request.Version==cancelled.Request.Version,"cancel replay exact result");
        cancelledFixture.Grants.Clear();await Reject(()=>cancelledFixture.Service.CancelAsync(cancellationRequest.Request.Id,cancel,"taxonomy-cancel-001",context,default),"access_denied","revoked Request rights deny cancel replay");cancelledFixture.Grant(StaffTaxonomyPermissions.Request);
        var audit=cancelledFixture.Audits.Single(a=>a.EventType=="hr.staff_taxonomy.cancelled");
        var auditBytes=await cancelledFixture.Store.ReadAsync(StaffTaxonomyResolver.ParseReference(audit.ProtectedChangesReference),$"hr/staff-taxonomy/{cancelled.Policy.Id:N}/audit",default);
        Check(System.Text.Encoding.UTF8.GetString(auditBytes).Contains(cancel.Reason,StringComparison.Ordinal),"cancel reason retained privately");
        cancelledFixture.SetAuthority(new(){Mode=StaffTaxonomyAuthorityMode.IndependentFixedAdministrator,ApprovedVersion="synthetic-authority-v2",EffectiveFrom=new(2026,1,1)});
        var fresh=await cancelledFixture.Service.RequestAsync(input with{ExpectedPolicyVersion=cancelled.Policy.Version,ExpectedRevision=cancelled.Policy.Revision},"taxonomy-fresh-after-cancel",context,default);
        Check(fresh.Request.Id!=cancelled.Request.Id&&cancelledFixture.Repo.Requests.Count==2,"fresh proposal possible after stale cancellation");
        fixture.Current.Actor=fixture.Current.Actor with{Client=SessionClient.Web};fixture.Grant(StaffTaxonomyPermissions.Request);
        var second=await fixture.Service.RequestAsync(input with{ExpectedPolicyVersion=published.Policy.Version,ExpectedRevision=published.Policy.Revision,EffectiveFrom=new(2026,10,19),EffectiveTo=new(2026,10,25)},"taxonomy-second-publication",context,default);
        fixture.Current.Actor=fixture.Current.Actor with{AccountId=Guid.NewGuid(),EmployeeId=Guid.NewGuid()};
        await fixture.Service.DecideAsync(second.Request.Id,new(second.Policy.Version,second.Request.Version,ApprovalState.Approved,"synthetic second approval","synthetic second proof"),"taxonomy-second-decision",context,default);
        Check((await fixture.Resolver.ResolveAsync("DRIVER",new(2026,10,20),default)).Available,"later complete publication available");
        fixture.Repo.Versions.RemoveAt(0);
        Check(!(await fixture.Resolver.ResolveAsync("DRIVER",new(2026,10,20),default)).Available,"missing predecessor cannot establish later publication history");
        Console.WriteLine($"PASS: {count} synthetic real taxonomy service/operation/evidence checks.");return count;
    }
}

sealed class Store : IPrivateFileStore
{
    public bool Tamper;public bool BadAuditMetadata;public Action<string>? OnWrite;readonly Dictionary<string,(string Purpose,byte[] Bytes)> objects=[];
    public Task<PrivateFileReference> WriteAsync(byte[] bytes,string purpose,CancellationToken ct){var key=Guid.NewGuid().ToString("N");objects[key]=(purpose,bytes.ToArray());OnWrite?.Invoke(purpose);return Task.FromResult(new PrivateFileReference(key,"1",Convert.ToHexString(SHA256.HashData(bytes)),"synthetic-key",bytes.Length+(BadAuditMetadata&&purpose.EndsWith("/audit",StringComparison.Ordinal)?1:0)));}
    public Task<byte[]> ReadAsync(PrivateFileReference reference,string purpose,CancellationToken ct){var obj=objects[reference.StorageKey];if(obj.Purpose!=purpose)throw new Exception("wrong private purpose");var bytes=obj.Bytes.ToArray();if(Tamper)bytes[0]^=1;return Task.FromResult(bytes);}
}
sealed class Repo : IStaffTaxonomyRepository
{
    public bool Locked;
    public List<Policy> Policies=[];public List<PolicyVersion> Versions=[];public List<ApprovalRequest> Requests=[];public List<ApprovalDecision> Decisions=[];
    readonly Dictionary<Guid,string> saved=[];long sequence;
    public Task<bool> NamespaceExistsAsync(CancellationToken ct)=>Task.FromResult(Versions.Count!=0);
    public Task<Policy?> FindPolicyAsync(CancellationToken ct)=>Task.FromResult(Policies.SingleOrDefault());
    public Task<IReadOnlyList<StaffTaxonomyVersionSource>> TimelineAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<StaffTaxonomyVersionSource>>(Versions.Select(v=>new StaffTaxonomyVersionSource(Policies.Single(p=>p.Id==v.PolicyId),v,Requests.SingleOrDefault(r=>r.Id==v.ApprovalRequestId),Decisions.Where(d=>d.ApprovalRequestId==v.ApprovalRequestId).ToArray())).ToArray());
    public Task<ApprovalRequest?> FindRequestAsync(Guid id,CancellationToken ct)=>Task.FromResult(Requests.SingleOrDefault(r=>r.Id==id));
    public Task<PagedResult<StaffTaxonomyRequestView>> RequestsAsync(int page,int pageSize,CancellationToken ct)=>Task.FromResult(new PagedResult<StaffTaxonomyRequestView>([],0,page,pageSize));
    public Task<bool> HasPendingRequestAsync(Guid id,CancellationToken ct)=>Task.FromResult(Requests.Any(r=>r.SubjectId==id&&r.State==ApprovalState.Pending));
    public Task<bool> HasLockedPeriodAsync(DateOnly from,DateOnly? to,CancellationToken ct)=>Task.FromResult(Locked);
    public Task ValidatePolicyReferencesAsync(StaffTaxonomyRules rules,DateOnly from,DateOnly? to,CancellationToken ct)=>Task.CompletedTask;
    public void Add(Policy value)=>Policies.Add(value);public void Add(ApprovalRequest value)=>Requests.Add(value);public void Add(ApprovalDecision value)=>Decisions.Add(value);public void Add(PolicyVersion value)=>Versions.Add(value);
    public void ExpectVersion(Policy value,byte[] expected){if(!value.RowVersion.SequenceEqual(expected))throw new AuthenticationFailure("staff_taxonomy_policy_version_conflict",409);}
    public void ExpectVersion(ApprovalRequest value,byte[] expected){if(!value.RowVersion.SequenceEqual(expected))throw new AuthenticationFailure("staff_taxonomy_request_version_conflict",409);}
    public Task SaveChangesAsync(CancellationToken ct){foreach(var p in Policies)Save(p,p.Id,p.Revision.ToString());foreach(var r in Requests)Save(r,r.Id,r.State.ToString());return Task.CompletedTask;}
    void Save(object row,Guid id,string value){if(!saved.TryGetValue(id,out var prior)||prior!=value){saved[id]=value;row.GetType().GetProperty("RowVersion")!.SetValue(row,BitConverter.GetBytes(++sequence));}}
}
public class FakeProxy : DispatchProxy
{
    public Func<MethodInfo,object?[]?,object?> Handler=null!;
    protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!,args);
    public static T Make<T>(Func<MethodInfo,object?[]?,object?> handler)where T:class{var value=DispatchProxy.Create<T,FakeProxy>();((FakeProxy)(object)value).Handler=handler;return value;}
}
sealed class Clock : TimeProvider{public DateTimeOffset Now=new(2026,10,13,7,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Now;}
sealed class Current : ICurrentAuthentication{public AuthenticatedAccount Actor=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Web,true);public AuthenticatedAccount Require()=>Actor;}
sealed class Fixture
{
    public Repo Repo=new();public Store Store=new();public Current Current=new();public List<PermissionGrant> Grants=[];public bool Live=true;public bool Fixed=true;public List<AuditEvent> Audits=[];
    public Clock Clock=new();public DateTimeOffset SessionExpiresAt=DateTimeOffset.MaxValue;public DateTimeOffset FixedExpiresAt=DateTimeOffset.MaxValue;
    public StaffTaxonomyResolver Resolver;public StaffTaxonomyService Service;public StaffTaxonomyOperation Operation;readonly List<IdempotencyReceipt> receipts=[];readonly List<OrganizationChange> changes=[];
    public Fixture(StaffTaxonomyAuthorityOptions? options=null)
    {
        var actualRoleCode=Role.CreateSystem(SystemRoleKind.Admin,Guid.NewGuid(),new Clock().GetUtcNow()).Code;
        var auth=FakeProxy.Make<IAccessManagementRepository>((m,a)=>m.Name switch{"InTransactionAsync"=>((Delegate)a![0]!).DynamicInvoke(a[1]),"IsCurrentAdministratorAsync"=>Task.FromResult(Live&&(DateTimeOffset)a![1]!<SessionExpiresAt),"IsCurrentSystemRoleAssignmentAsync"=>Task.FromResult(Fixed&&(DateTimeOffset)a![2]!<FixedExpiresAt),"GetAssignmentRoleCodeAsync"=>Task.FromResult<string?>(actualRoleCode),"AddAudit"=>Audit((AuditEvent)a![0]!),"SaveChangesAsync"=>Repo.SaveChangesAsync(default),_=>throw new Exception("unexpected auth "+m.Name)});
        var access=FakeProxy.Make<IAccountAccessReader>((m,a)=>Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var org=FakeProxy.Make<IOrganizationRepository>((m,a)=>m.Name switch{"FindReceiptAsync"=>Task.FromResult(receipts.SingleOrDefault(r=>r.ActorUserId==(string)a![0]!&&r.Operation==(string)a[1]!&&r.Key==(string)a[2]!)),"FindChangeAsync"=>Task.FromResult(changes.SingleOrDefault(c=>c.Id==(Guid)a![0]!)),"AddChange"=>Change((OrganizationChange)a![0]!, (AuditEvent)a[1]!, (IdempotencyReceipt)a[2]!),"SaveChangesAsync"=>Repo.SaveChangesAsync(default),_=>throw new Exception("unexpected organization "+m.Name)});
        Resolver=new(Repo,Store);Operation=new(Current,access,auth,org,Store,Clock);Service=new(Operation,Repo,Resolver,new(options??new(){Mode=StaffTaxonomyAuthorityMode.IndependentFixedAdministrator,ApprovedVersion="synthetic-approved-authority",EffectiveFrom=new(2026,1,1)}),Store);
    }
    public void SetAuthority(StaffTaxonomyAuthorityOptions options)=>Service=new(Operation,Repo,Resolver,new(options),Store);
    public void Grant(string permission)=>Grants.Add(new(permission,true,Guid.NewGuid(),AccessScopeKind.All,null));
    object? Audit(AuditEvent audit){Audits.Add(audit);return null;}
    object? Change(OrganizationChange change,AuditEvent audit,IdempotencyReceipt receipt){typeof(OrganizationChange).GetProperty("AuditEvent")!.SetValue(change,audit);changes.Add(change);Audits.Add(audit);receipts.Add(receipt);return null;}
}
