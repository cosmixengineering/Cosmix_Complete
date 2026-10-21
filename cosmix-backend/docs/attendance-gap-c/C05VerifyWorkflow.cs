#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.Waiting;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.Policies;
using Cosmix.Application.HR.Policies.Advanced;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.Common.Jobs;
using Cosmix.Domain.HR.PolicyEffects;
using Cosmix.Domain.HR.Policies;

// Real services and actual AttendanceOperation/NotificationOutbox. Synthetic boundary IO only.
// No SQL, host, provider, real identity, filesystem private store or persisted employee fixture.
var checks=0;
void Check(bool ok,string name) { if(!ok) throw new Exception(name); checks++; }
async Task Failure(Func<Task> action,string code) {
    try { await action(); } catch(AuthenticationFailure e) when(e.Code==code) { checks++; return; }
    throw new Exception("Expected "+code);
}
foreach(var definition in WaitingPermissions.Definitions)
    Check(AccessPermissionCatalog.Find(definition.Code) is not null,"Parent bound exact permission "+definition.Code);
var denied=new BoundaryHarness(); denied.NoGrants=true;
await Failure(()=>denied.Service.PreviewPairAsync(denied.PairInput(denied.A),denied.Context,default),"access_denied");
Check(denied.Rows.Count==0 && denied.Files.Count==0,"Denied authorization precedes all protected/business writes");
denied.NoGrants=false; denied.SessionCurrent=false;
await Failure(()=>denied.Service.PreviewPairAsync(denied.PairInput(denied.A),denied.Context,default),"invalid_authentication");
var f=new BoundaryHarness(); var pairInput=f.PairInput(f.A);
var pairPreview=await f.Service.PreviewPairAsync(pairInput,f.Context,default);
var pair=await f.Service.CreatePairAsync(new(pairInput,pairPreview.ProofFingerprint),"c05-pair-0001",f.Context,default);
Check(pair.State=="Pending" && f.Verified.Count==0 && f.Decisions.Count==0,"Pair proposal cannot fabricate verified actor/evidence");
Check(await f.Service.CreatePairAsync(new(pairInput,pairPreview.ProofFingerprint),"c05-pair-0001",f.Context,default)==pair && f.Rows.Count==1,"Pair exact receipt replay");
var pairReview=await f.Service.PairReviewPreviewAsync(pair.Id,f.Context,default);
var pairDecision=new WaitingReviewRequest(pair.Version,ApprovalState.Approved,pairReview.ProofFingerprint,"Synthetic independent pair review");
await Failure(()=>f.Service.ReviewPairAsync(pair.Id,pairDecision,"c05-pair-self",f.Context,default),"access_denied");
f.Actor=f.Reviewer with {EmployeeId=f.A};
await Failure(()=>f.Service.ReviewPairAsync(pair.Id,pairDecision,"c05-pair-beneficiary",f.Context,default),"access_denied");
f.Actor=f.Reviewer with {EmployeeId=f.B};
await Failure(()=>f.Service.ReviewPairAsync(pair.Id,pairDecision,"c05-pair-causer",f.Context,default),"access_denied");
f.Actor=f.Reviewer;
pair=await f.Service.ReviewPairAsync(pair.Id,pairDecision,"c05-pair-approved",f.Context,default);
Check(pair.State=="Approved" && f.Decisions.Count==1 && f.Jobs.Count==1,"Independent pair review and actual outbox stage");
var pairIntent=await NotificationOutbox.ReadAsync(f.Jobs.Single(),f,default);
Check(pairIntent.RecipientAccountId==f.Submitter.AccountId && pairIntent.EventId==pair.DecisionId && pairIntent.ResourceId==pair.Id,"Actual outbox uses real approval decision/requester/case IDs");
f.Actor=f.Submitter;
var request=await f.Input(pair.Id); var pending=await f.Service.SubmitAsync(request,"c05-intake-0001",f.Context,default);
Check(pending.State=="Pending" && pending.VerifiedMinutes is null && f.Verified.Count==0,"Intake is pending and never constructs WaitingTimeEvidence");
var originalReference=f.Rows.Single(x=>x.SubjectId==pending.Id).SnapshotPrivateReference;
Check(await f.Service.SubmitAsync(request,"c05-intake-0001",f.Context,default)==pending && f.RequestRows==1,"Intake receipt replay produces no duplicate");
await Failure(()=>f.Service.SubmitAsync(request with {WrittenEvidence="Altered evidence"},"c05-intake-0001",f.Context,default),"idempotency_key_reused");
await Failure(()=>f.Service.SubmitAsync(request,"c05-intake-duplicate",f.Context,default),"waiting_duplicate_intake");
f.OnlyEmployees=[f.A];
await Failure(()=>f.Service.DetailAsync(pending.Id,f.Context,default),"access_denied");
f.OnlyEmployees=null;
f.Actor=f.Reviewer;
var preview=await f.Service.ReviewPreviewAsync(pending.Id,f.Context,default);
var decision=new WaitingReviewRequest(pending.Version,ApprovalState.Approved,preview.ProofFingerprint,"Synthetic factual verification");
f.CauserArrival=f.At(10,46);
await Failure(()=>f.Service.ReviewAsync(pending.Id,decision,"c05-stale-source",f.Context,default),"waiting_source_changed");
Check(f.Verified.Count==0 && f.Rows.Single(x=>x.SubjectId==pending.Id).State==ApprovalState.Pending,"Stale preview leaves pending source unchanged");
f.CauserArrival=f.At(10,45); f.Actor=f.Submitter;
await Failure(()=>f.Service.ReviewAsync(pending.Id,decision,"c05-self-review",f.Context,default),"access_denied");
f.Actor=f.Reviewer with {EmployeeId=f.A};
await Failure(()=>f.Service.ReviewAsync(pending.Id,decision,"c05-beneficiary-review",f.Context,default),"access_denied");
f.Actor=f.Reviewer with {EmployeeId=f.B};
await Failure(()=>f.Service.ReviewAsync(pending.Id,decision,"c05-causer-review",f.Context,default),"access_denied");
f.Actor=f.Reviewer with {MfaSatisfied=false};
await Failure(()=>f.Service.ReviewAsync(pending.Id,decision,"c05-no-mfa",f.Context,default),"access_denied");
f.Actor=f.Reviewer; f.HistoricalEligible=false;
await Failure(()=>f.Service.ReviewAsync(pending.Id,decision,"c05-history-unavailable",f.Context,default),"waiting_historical_assignment_changed");
f.HistoricalEligible=true;
var approved=await f.Service.ReviewAsync(pending.Id,decision,"c05-approve-0001",f.Context,default);
Check(approved.State=="Approved" && approved.VerifiedMinutes==20 && f.Verified.Count==1,"Actual review intersects delay/duty and subtracts productive10:20-10:45");
var proof=f.Proof(approved.Id);
Check(proof.Attribution.ProvenTicks==20*TimeSpan.TicksPerMinute && proof.GroupSequence==1,"Private exact ticks and causal sequence survive actual service");
Check(f.Verified.Single().VerifiedByUserId==f.Reviewer.AccountId.ToString() && f.Verified.Single().ApprovalRequestId==approved.ApprovalRequestId,"Verified model links actual independent actor and approval");
Check(await f.Service.ReviewAsync(pending.Id,decision,"c05-approve-0001",f.Context,default)==approved && f.Verified.Count==1 && f.Jobs.Count==2,"Approved replay retains exactly one evidence and notification");
f.NoGrants=true;
await Failure(()=>f.Service.ReviewAsync(pending.Id,decision,"c05-approve-0001",f.Context,default),"access_denied");
f.NoGrants=false;f.OnlyEmployees=[f.A];
await Failure(()=>f.Service.ReviewAsync(pending.Id,decision,"c05-approve-0001",f.Context,default),"access_denied");f.OnlyEmployees=null;
await Failure(()=>f.Service.ReviewAsync(pending.Id,decision,"c05-approve-stale",f.Context,default),"waiting_stale_version");
f.Actor=f.Submitter;
var effect=await f.Effect(approved);
Check(effect.State=="Pending" && f.Assessments.Count==0,"Factual approval alone is no performance assessment");
Check((await f.Read()).State=="unavailable","Pending separate interpretation blocks C04 source");
f.Actor=f.Reviewer; effect=await f.ApproveEffect(effect);
var consumed=await f.Read();
Check(consumed.State=="verified" && consumed.Points.Single().ExactPoints==1m && consumed.Points.Single().AssessmentId==f.Assessments.Single().Id,"Actual reader consumes separately approved exact group points");
Check(consumed.RetainedScopes.SelectMany(x=>x.Alternatives).Select(x=>x.EmployeeId).Distinct().Count()==2,"Both workers' retained/current scopes travel to C04");
Check(f.Assessments.Single().ProposedMoneyImpact is null && f.Assessments.Single().ProposedKpiPercent is null,"No salary charge or fabricated percentage from point evidence");
f.CauserArrival=f.At(10,46);
Check((await f.Read()).State=="unavailable","Changed accepted source blocks live performance");
Check((await f.Service.DetailAsync(approved.Id,f.Context,default)).Record.State=="Approved","Source correction does not erase authorized immutable history");
f.CauserArrival=f.At(10,45); f.Actor=f.Submitter;
var dispute=await f.Service.DisputeAsync(approved.Id,new(approved.Version,"Review causation again","Synthetic dispute"),"c05-dispute-0001",f.Context,default);
Check((await f.Read()).Reasons.Contains("waiting_disputed"),"Actual dispute prevents source consumption");
Check((await f.Service.DetailAsync(approved.Id,f.Context,default)).Disputes.Single().Id==dispute.Id,"Dispute is read from purpose-bound persisted audit record");
var amendment=await f.Input(pair.Id);
amendment=amendment with {PreviousRequestId=approved.Id,ExpectedPreviousVersion=approved.Version,ClaimedInterval=new(f.At(10),f.At(10,15)),ProductiveAlternateWork=[],WrittenEvidence="Retained amendment after dispute"};
var revised=await f.Service.SubmitAsync(amendment,"c05-revision-0001",f.Context,default);
await Failure(()=>f.Service.SubmitAsync(amendment,"c05-revision-branch",f.Context,default),"waiting_revision_conflict");
f.Actor=f.Reviewer; revised=await f.Approve(revised);
Check(revised.RootId==approved.RootId && revised.Revision==2 && f.Proof(revised.Id).Attribution.DeltaTicks==-5*TimeSpan.TicksPerMinute,"Dispute resolution is a retained signed revision");
Check(f.Rows.Single(x=>x.SubjectId==approved.Id).SnapshotPrivateReference==originalReference && f.Verified.Count==2,"Original source/evidence remains immutable");
Check((await f.Read()).Reasons.Contains("waiting_effect_unverified"),"Amendment needs its own separately approved effect checkpoint");
f.Actor=f.Submitter; var revisedEffect=await f.Effect(revised); f.Actor=f.Reviewer; await f.ApproveEffect(revisedEffect);
Check((await f.Read()).Points.Single().ExactPoints==.75m && f.Assessments.Count==2,"C04 reads current corrected checkpoint without adding old assessment again");
f.Actor=f.Submitter; var pairC=await f.CreatePair(f.C); f.Actor=f.Reviewer; pairC=await f.ApprovePair(pairC); f.Actor=f.Submitter;
var claimC=await f.Input(pairC.Id); claimC=claimC with {ClaimedInterval=new(f.At(10,10),f.At(10,30)),ProductiveAlternateWork=[],SupportingEvidence=[new(AdvancedEvidenceKind.AcceptedDuty,f.Sessions[f.C].Id)]};
var c=await f.Service.SubmitAsync(claimC,"c05-worker-c",f.Context,default); f.Actor=f.Reviewer; c=await f.Approve(c);
Check(f.Proof(c.Id).Attribution.CurrentLossTicks==30*TimeSpan.TicksPerMinute && f.Proof(c.Id).Attribution.DeltaTicks==15*TimeSpan.TicksPerMinute,"Actual A/C workflow unions one causal loss");
f.Actor=f.Submitter; var cEffect=await f.Effect(c); f.Actor=f.Reviewer; await f.ApproveEffect(cEffect);
var union=await f.Read();
Check(union.Points.Count==1 && union.Points[0].ExactPoints==1.5m,"Reader returns one current union checkpoint, never A plus C charges");
Check(f.Decisions.Select(x=>x.DecidedAt).Distinct().Count()==1 && f.Proof(c.Id).GroupSequence==3,"Same timestamp decisions retain causal ordering through explicit sequence");
Check(union.RetainedScopes.SelectMany(x=>x.Alternatives).Select(x=>x.EmployeeId).Distinct().Count()==3,"Other worker retained/current scopes are mandatory");
f.OnlyEmployees=[f.A,f.B];
await Failure(()=>f.Service.DetailAsync(approved.Id,f.Context,default),"access_denied"); f.OnlyEmployees=null;

var unverified=new BoundaryHarness(); var up=await unverified.ReadyPair(); unverified.NoDuty=true;
var ui=await unverified.Service.SubmitAsync(await unverified.Input(up.Id),"c05-unverified-intake",unverified.Context,default);
unverified.Actor=unverified.Reviewer; var uv=await unverified.Service.ReviewPreviewAsync(ui.Id,unverified.Context,default);
Check(uv.State=="unverified","Actual missing-duty preview does not manufacture verification");
await Failure(()=>unverified.Service.ReviewAsync(ui.Id,new(ui.Version,ApprovalState.Approved,uv.ProofFingerprint,"No proof"),"c05-unverified-approve",unverified.Context,default),"waiting_actual_duty_unverified");
var returned=await unverified.Service.ReviewAsync(ui.Id,new(ui.Version,ApprovalState.Returned,uv.ProofFingerprint,"Supply duty proof"),"c05-return-unverified",unverified.Context,default);
Check(returned.State=="Returned" && unverified.Verified.Count==0,"Return closes unverified intake without verified evidence");
unverified.NoDuty=false; unverified.Actor=unverified.Submitter;
var resubmission=(await unverified.Input(up.Id)) with {PreviousRequestId=returned.Id,ExpectedPreviousVersion=returned.Version};
var resubmitted=await unverified.Service.SubmitAsync(resubmission,"c05-resubmitted",unverified.Context,default);
unverified.Actor=unverified.Reviewer; resubmitted=await unverified.Approve(resubmitted);
unverified.Actor=unverified.Submitter; var ue=await unverified.Effect(resubmitted); unverified.Actor=unverified.Reviewer; await unverified.ApproveEffect(ue);
Check((await unverified.Read()).State=="verified","Resolved old Return does not permanently block real reader");

var race=new BoundaryHarness(); var rp=await race.ReadyPair(); var ri=await race.Input(rp.Id);
race.AfterNextWrite=()=>race.CauserArrival=race.At(10,46);
await Failure(()=>race.Service.SubmitAsync(ri,"c05-source-io-race",race.Context,default),"waiting_source_changed");
Check(race.RequestRows==0 && race.Verified.Count==0 && race.Receipts.All(x=>x.Operation!="attendance.waiting.intake"),"Source race after private IO has no domain/receipt commit");

var atomic=new BoundaryHarness(); var ap=await atomic.CreatePair(atomic.A); atomic.Actor=atomic.Reviewer;
var av=await atomic.Service.PairReviewPreviewAsync(ap.Id,atomic.Context,default); atomic.RecipientExists=false;
await Failure(()=>atomic.Service.ReviewPairAsync(ap.Id,new(ap.Version,ApprovalState.Approved,av.ProofFingerprint,"Missing recipient"),"c05-outbox-rollback",atomic.Context,default),"notification_recipient_not_found");
Check(atomic.Rows.Single().State==ApprovalState.Pending && atomic.Decisions.Count==0 && atomic.Jobs.Count==0,"Outbox failure rolls back synthetic producer state, decision and receipt");
atomic.RecipientExists=true; ap=await atomic.ApprovePair(ap); atomic.Actor=atomic.Submitter;
var ai=await atomic.Service.SubmitAsync(await atomic.Input(ap.Id),"c05-atomic-intake",atomic.Context,default); atomic.Actor=atomic.Reviewer;
var aiPreview=await atomic.Service.ReviewPreviewAsync(ai.Id,atomic.Context,default); atomic.FailResultWrite=true;
var jobsBefore=atomic.Jobs.Count; var receiptsBefore=atomic.Receipts.Count;
await Failure(()=>atomic.Service.ReviewAsync(ai.Id,new(ai.Version,ApprovalState.Approved,aiPreview.ProofFingerprint,"Receipt storage fault"),"c05-result-rollback",atomic.Context,default),"private_storage_unavailable");
Check(atomic.Rows.Single(x=>x.SubjectId==ai.Id).State==ApprovalState.Pending && atomic.Verified.Count==0 && atomic.Jobs.Count==jobsBefore && atomic.Receipts.Count==receiptsBefore,"Receipt IO failure restores staged business/evidence/decision/outbox boundary");
atomic.FailResultWrite=false;

var concurrent=new BoundaryHarness(); var cp=await concurrent.ReadyPair(); var ci=await concurrent.Input(cp.Id);
async Task<string> SubmitRace(string key) { try { return (await concurrent.Service.SubmitAsync(ci,key,concurrent.Context,default)).Id.ToString(); } catch(AuthenticationFailure e) { return e.Code; } }
var duplicateRace=await Task.WhenAll(SubmitRace("c05-concurrent-a"),SubmitRace("c05-concurrent-b"));
Check(concurrent.RequestRows==1 && duplicateRace.Count(x=>x=="waiting_duplicate_intake")==1,"Different-key competing intakes serialize to one pending claim");
var same=new BoundaryHarness(); var sp=await same.ReadyPair(); var si=await same.Input(sp.Id);
var replayRace=await Task.WhenAll(same.Service.SubmitAsync(si,"c05-concurrent-same",same.Context,default),same.Service.SubmitAsync(si,"c05-concurrent-same",same.Context,default));
Check(replayRace[0]==replayRace[1] && same.RequestRows==1,"Concurrent same-key intakes share one durable receipt identity");
same.Actor=same.Reviewer; var sv=await same.Service.ReviewPreviewAsync(replayRace[0].Id,same.Context,default);
var sr=new WaitingReviewRequest(replayRace[0].Version,ApprovalState.Approved,sv.ProofFingerprint,"Concurrent exact review");
async Task<string> DecideRace(string key) { try { return (await same.Service.ReviewAsync(replayRace[0].Id,sr,key,same.Context,default)).State; } catch(AuthenticationFailure e) { return e.Code; } }
var decisionRace=await Task.WhenAll(DecideRace("c05-concurrent-review-a"),DecideRace("c05-concurrent-review-b"));
Check(decisionRace.Count(x=>x=="Approved")==1 && decisionRace.Count(x=>x=="waiting_stale_version")==1 && same.Verified.Count==1,"Competing reviews cannot double-materialize evidence");
var two=new BoundaryHarness();var tp=await two.ReadyPair();two.Actor=two.Submitter;var tpc=await two.CreatePair(two.C);two.Actor=two.Reviewer;tpc=await two.ApprovePair(tpc);two.Actor=two.Submitter;
var ta=await two.Service.SubmitAsync(await two.Input(tp.Id),"c05-two-worker-a",two.Context,default);
var tcInput=(await two.Input(tpc.Id)) with {ClaimedInterval=new(two.At(10,10),two.At(10,30)),ProductiveAlternateWork=[]};
var tc=await two.Service.SubmitAsync(tcInput,"c05-two-worker-c",two.Context,default);two.Actor=two.Reviewer;
var tav=await two.Service.ReviewPreviewAsync(ta.Id,two.Context,default);var tcv=await two.Service.ReviewPreviewAsync(tc.Id,two.Context,default);
async Task<string> CausalReview(WaitingView item,WaitingPreview view,string key) {
    try {return (await two.Service.ReviewAsync(item.Id,new(item.Version,ApprovalState.Approved,view.ProofFingerprint,"Competing causal review"),key,two.Context,default)).State;}
    catch(AuthenticationFailure e) {return e.Code;}
}
var causalRace=await Task.WhenAll(CausalReview(ta,tav,"c05-two-review-a"),CausalReview(tc,tcv,"c05-two-review-c"));
Check(causalRace.Count(x=>x=="Approved")==1 && causalRace.Count(x=>x=="waiting_source_changed")==1,"Competing A/C reviews invalidate the old group preview instead of multiplying loss");
var retry=two.Rows.Single(x=>x.RequestType==WaitingWorkflowService.RequestType("request") && x.State==ApprovalState.Pending);
var retryView=retry.SubjectId==ta.Id?ta:tc;await two.Approve(retryView);
Check(two.Verified.Count==2 && two.Proof(retryView.Id).Attribution.CurrentLossTicks==30*TimeSpan.TicksPerMinute,"Fresh competing review retains one union after retry");
var factual=new BoundaryHarness(WaitingEffect.FactualOnly);
Check((await factual.Read()).State=="verified" && (await factual.Read()).Points.Count==0,"Explicit factual-only empty query is verified empty");
factual.MissingPolicy=true;
Check((await factual.Read()).State=="unavailable","Actual missing family is not empty-as-zero");
var financial=new BoundaryHarness(WaitingEffect.FinancialProposal);var fp=await financial.ReadyPair();
var fi=await financial.Service.SubmitAsync(await financial.Input(fp.Id),"c05-financial-intake",financial.Context,default);
financial.Actor=financial.Reviewer;fi=await financial.Approve(fi);financial.Actor=financial.Submitter;
var fe=await financial.Effect(fi);financial.Actor=financial.Reviewer;fe=await financial.ApproveEffect(fe);
Check(fe.MoneyState=="amount_unavailable" && fe.Money is null && fe.Currency is null && !fe.SettlementReady,"Actual configured financial proposal retains unavailable money, never zero/settlement");
Check((await financial.Read()).State=="verified" && (await financial.Read()).Points.Count==0,"Financial interpretation cannot become performance points");
Check(f.MaxDepth==1 && same.MaxDepth==1,"Reader/workflow never nests AttendanceOperation transaction");
Console.WriteLine($"C05 actual workflow checks passed: {checks}. Synthetic authorization/source/persistence/private/outbox boundaries; no SQL, host, provider or persisted employee fixtures. Database isolation/restart/encrypted storage acceptance is separate.");

public class BoundaryProxy:DispatchProxy
{
    public Func<MethodInfo,object?[],object?> Handler=null!;
    protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!,args??[]);
    public static T Make<T>(Func<MethodInfo,object?[],object?> handler) where T:class
    { var result=Create<T,BoundaryProxy>(); ((BoundaryProxy)(object)result).Handler=handler; return result; }
}
public sealed class BoundaryClock:TimeProvider
{ public override DateTimeOffset GetUtcNow()=>new(2026,10,13,23,0,0,TimeSpan.Zero); }
public sealed class BoundaryHarness:IWaitingWorkflowRepository,IPrivateFileStore
{
    public readonly Guid A=Guid.NewGuid(),B=Guid.NewGuid(),C=Guid.NewGuid(),Site=Guid.NewGuid(),Department=Guid.NewGuid(),Category=Guid.NewGuid(),Shift=Guid.NewGuid();
    public readonly DateOnly Date=new(2026,10,13);
    public readonly BoundaryClock Clock=new();
    public readonly AuthenticatedAccount Submitter=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Web,true);
    public readonly AuthenticatedAccount Reviewer=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Web,true);
    public AuthenticatedAccount Actor;
    public AuthRequestContext Context=new("127.0.0.1","synthetic-c05-workflow-boundary");
    public bool NoGrants,SessionCurrent=true,HistoricalEligible=true,NoDuty,RecipientExists=true,FailResultWrite,MissingPolicy;
    public Guid[]? OnlyEmployees;
    public DateTimeOffset CauserArrival;
    public Action? AfterNextWrite;
    public List<ApprovalRequest> Rows=[];
    public List<ApprovalDecision> Decisions=[];
    public List<AuditEvent> Facts=[],Audits=[];
    public List<IdempotencyReceipt> Receipts=[];
    public List<WaitingTimeEvidence> Verified=[];
    public List<PolicyImpactAssessment> Assessments=[];
    public List<OutboxMessage> Jobs=[];
    public Dictionary<Guid,OvertimeSessionSource> Sessions=[];
    public Dictionary<string,(byte[] Data,string Purpose)> Files=[];
    public WaitingWorkflowService Service;
    public IWaitingPerformanceEffectReader Reader;
    public int MaxDepth;
    private readonly AsyncLocal<int> flowDepth=new();
    private long versionCounter;
    private readonly SemaphoreSlim gate=new(1,1);
    private Dictionary<Guid,(ApprovalState State,DateTimeOffset? Closed,byte[] Version)> saved=[];
    private readonly Dictionary<Guid,Guid> assignments=[],policyAssignments=[];
    private readonly Guid roleAssignment=Guid.NewGuid();
    public int RequestRows=>Rows.Count(x=>x.RequestType==WaitingWorkflowService.RequestType("request"));
    public DateTimeOffset At(int hour,int minute=0)=>new(2026,10,13,hour,minute,0,TimeSpan.Zero);
    public BoundaryHarness(WaitingEffect effect=WaitingEffect.PerformanceProposal)
    {
        Actor=Submitter; CauserArrival=At(10,45);
        foreach(var employee in new[]{A,B,C}) {
            assignments[employee]=Guid.NewGuid(); policyAssignments[employee]=Guid.NewGuid();
            Sessions[employee]=new(Guid.NewGuid(),Guid.NewGuid(),1,employee==B?CauserArrival:At(10),At(18),Site,Shift,null,Guid.NewGuid(),Guid.NewGuid(),null,true);
        }
        var policy=new Policy(AdvancedAttendanceRulesCodec.Code(AdvancedAttendanceFamily.Waiting),"Synthetic explicit waiting policy",PolicyCategory.EmployeeException);
        var rules=new AdvancedAttendanceRules(1,AdvancedAttendanceFamily.Waiting,["SITE"],
            new("SYNTHETIC-ONLY-NOT-MANAGEMENT-APPROVAL",WaitingPermissions.Decide,true,[AdvancedEvidenceKind.AcceptedDuty]),
            Waiting:new(3600m,true,true,effect,effect==WaitingEffect.PerformanceProposal?3m:null,effect==WaitingEffect.FinancialProposal?"SYNTHETIC-F-CONTRACT":null));
        var version=new PolicyVersion(policy.Id,1,Date.AddDays(-1),null,AdvancedAttendanceRulesCodec.Schema,
            AdvancedAttendanceRulesCodec.Validate(rules).CanonicalJson!,null,null,"synthetic-author",Clock.GetUtcNow().AddDays(-2),PolicyAuthority.DirectHr);
        var policyRepo=BoundaryProxy.Make<IAdvancedPolicyPublicationRepository>((m,a)=> {
            if(m.Name!="DatedSourceAsync") throw new Exception("Unexpected policy IO "+m.Name);
            var employee=(Guid)a[0]!;
            var membership=new AttendancePolicyMembership(assignments[employee],employee,Department,Category,Site,Date.AddDays(-20),null);
            var assignment=new PolicyAssignment(version.Id,null,employee,null,null,null,Date.AddDays(-1),null,"Synthetic only",null,null,"synthetic-author",Clock.GetUtcNow().AddDays(-2),PolicyAuthority.DirectHr);
            typeof(PolicyAssignment).GetProperty(nameof(PolicyAssignment.Id))!.SetValue(assignment,policyAssignments[employee]);
            return Task.FromResult(new AdvancedPolicyDatedSource(policy,new([membership],MissingPolicy?[]:[new(assignment,version,null,true)]),[new(membership.Id,Category,"SITE")]));
        });
        var policies=new AdvancedPolicyReader(policyRepo);
        var current=BoundaryProxy.Make<ICurrentAuthentication>((_,_)=>Actor);
        var access=BoundaryProxy.Make<IAccountAccessReader>((_,_)=>Task.FromResult<IReadOnlyList<PermissionGrant>>(NoGrants?[]:
            WaitingPermissions.Definitions.SelectMany(x=>OnlyEmployees is null
                ? new[]{new PermissionGrant(x.Code,x.Sensitive,roleAssignment,AccessScopeKind.All,null)}
                : OnlyEmployees.Select(id=>new PermissionGrant(x.Code,x.Sensitive,roleAssignment,AccessScopeKind.Employee,id))).ToArray()));
        var authorization=BoundaryProxy.Make<IAccessManagementRepository>((m,a)=>m.Name switch {
            "InTransactionAsync"=>GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this,a),
            "GetAssignmentRoleCodeAsync"=>Task.FromResult<string?>("SYNTHETIC-AUDIT-BASIS"),
            _=>throw new Exception("Unexpected authorization IO "+m.Name)
        });
        var journal=BoundaryProxy.Make<ISchedulingJournalRepository>((m,a)=>m.Name switch {
            "IsCurrentSessionAsync"=>Task.FromResult(SessionCurrent && (AuthenticatedAccount)a[0]! == Actor),
            "FindReceiptAsync"=>Task.FromResult(Receipts.SingleOrDefault(x=>x.ActorUserId==(string)a[0]! && x.Operation==(string)a[1]! && x.Key==(string)a[2]!)),
            "AddReceipt"=>Append(Receipts,(IdempotencyReceipt)a[0]!), "AddAudit"=>Append(Audits,(AuditEvent)a[0]!),
            "SaveChangesAsync"=>SaveChangesAsync((CancellationToken)a[0]!), _=>throw new Exception("Unexpected journal IO "+m.Name)
        });
        var operation=new AttendanceOperation(current,access,authorization,journal,
            BoundaryProxy.Make<IAuthenticationRepository>((m,_)=>throw new Exception("Forbidden mobile/auth provider IO "+m.Name)),Clock,this);
        var duty=BoundaryProxy.Make<IOvertimeRepository>((m,a)=>m.Name switch {
            "EmploymentAsync"=>Task.FromResult<OvertimeEmploymentSource?>(new(assignments[(Guid)a[0]!],Category,"SITE",Site,Date.AddDays(-20),null)),
            "SessionSourcesAsync"=>Task.FromResult<IReadOnlyList<OvertimeSessionSource>>([Sessions[(Guid)a[0]!] with {StartsAtUtc=(Guid)a[0]! == B?CauserArrival:At(10),ValidDutyEvidence=!NoDuty}]),
            _=>throw new Exception("Waiting must not call OT valuation/workflow "+m.Name)
        });
        var roster=BoundaryProxy.Make<IIndividualRosterResolver>((_,a)=>Task.FromResult(new EffectiveRosterResponse((Guid)a[0]!, (DateOnly)a[1]!,"resolved","UTC",
            SiteId:Site,AssignmentId:assignments[(Guid)a[0]!],ShiftVersionId:Shift,StartsAt:At(10),EndsAt:At(18))));
        var summaries=BoundaryProxy.Make<IAttendanceSummaryRepository>((m,a)=>m.Name=="ResourceScopesAsync"?
            Task.FromResult<IReadOnlyList<AttendanceResourceScope>>([new([new(EmployeeId:(Guid)a[0]!,SiteId:Site)])]):throw new Exception("Unexpected summary IO "+m.Name));
        var leave=BoundaryProxy.Make<ILeaveAttendanceContextReader>((_,_)=>Task.FromResult(new LeaveAttendanceContext(LeaveContextStatus.Complete,[],"synthetic-no-leave",[])));
        var corrections=BoundaryProxy.Make<ICorrectionReviewRepository>((m,_)=>m.Name is "HistoricallyEligibleAsync" or "PreservesAssignmentAsync"?
            Task.FromResult(HistoricalEligible):throw new Exception("Unexpected correction IO "+m.Name));
        var taxonomy=BoundaryProxy.Make<IStaffTaxonomyResolver>((m,a)=>Task.FromResult(StaffTaxonomyResolver.Resolve((string)a[0]!, (DateOnly)a[1]!,[])));
        var sources=new WaitingSources(duty,roster,summaries,leave,policies,corrections,taxonomy,operation);
        var jobs=BoundaryProxy.Make<IDurableJobRepository>((m,a)=>m.Name switch {
            "RequireProducerTransaction"=>RequireProducer(),
            "FindByIdentityAsync"=>Task.FromResult(Jobs.SingleOrDefault(x=>x.MessageType==(string)a[0]! && x.DeduplicationKey==(string)a[1]!)),
            "RecipientExistsAsync"=>Task.FromResult(RecipientExists && ((Guid)a[0]! == Submitter.AccountId || (Guid)a[0]! == Reviewer.AccountId)),
            "Stage"=>Append(Jobs,(OutboxMessage)a[0]!), _=>throw new Exception("Unexpected outbox IO "+m.Name)
        });
        Service=new(operation,this,sources,this,access,new NotificationOutbox(jobs,this,Clock));
        Reader=new WaitingPerformanceEffectReader(this,Service,policies);
    }
    public WaitingPairInput PairInput(Guid worker)=>new(worker,B,Site,Date,Date.AddDays(1),"same-causal-task","Synthetic dated instruction","Synthetic pair reason");
    public async Task<WaitingView> CreatePair(Guid worker) {
        var input=PairInput(worker); var preview=await Service.PreviewPairAsync(input,Context,default);
        return await Service.CreatePairAsync(new(input,preview.ProofFingerprint),"c05-new-pair-"+worker.ToString("N"),Context,default);
    }
    public async Task<WaitingView> ApprovePair(WaitingView pair) {
        var preview=await Service.PairReviewPreviewAsync(pair.Id,Context,default);
        return await Service.ReviewPairAsync(pair.Id,new(pair.Version,ApprovalState.Approved,preview.ProofFingerprint,"Synthetic independent pair review"),"c05-approve-pair-"+pair.Id.ToString("N"),Context,default);
    }
    public async Task<WaitingView> ReadyPair() { Actor=Submitter;var p=await CreatePair(A);Actor=Reviewer;p=await ApprovePair(p);Actor=Submitter;return p; }
    public async Task<SubmitWaitingRequest> Input(Guid pair) {
        var source=await Service.IntakeSourcesAsync(pair,Date,Context,default);
        var primary=Rows.Single(x=>x.SubjectId==pair);var stored=Payload<WaitingPairSnapshot>(primary.SnapshotPrivateReference);
        return new(pair,Date,new(At(10),At(10,45)),[new(At(10,20),At(10,45))],[new(AdvancedEvidenceKind.AcceptedDuty,Sessions[stored.Input.WaitingEmployeeId].Id)],
            "Synthetic original waiting evidence","Synthetic waiting reason",source.ProofFingerprint);
    }
    public async Task<WaitingView> Approve(WaitingView item) {
        var preview=await Service.ReviewPreviewAsync(item.Id,Context,default);
        return await Service.ReviewAsync(item.Id,new(item.Version,ApprovalState.Approved,preview.ProofFingerprint,"Synthetic actual factual review"),"c05-review-"+item.Id.ToString("N"),Context,default);
    }
    public async Task<WaitingEffectView> Effect(WaitingView item) {
        var preview=await Service.EffectPreviewAsync(item.Id,Context,default);
        return await Service.ProposeEffectAsync(item.Id,new(item.Version,preview.ProofFingerprint,"Synthetic separate effect proposal"),"c05-effect-"+item.Id.ToString("N"),Context,default);
    }
    public async Task<WaitingEffectView> ApproveEffect(WaitingEffectView effect) {
        var preview=await Service.EffectReviewPreviewAsync(effect.Id,Context,default);
        return await Service.ReviewEffectAsync(effect.Id,new(effect.Version,ApprovalState.Approved,preview.ProofFingerprint,"Synthetic exact interpretation review"),"c05-effect-review-"+effect.Id.ToString("N"),Context,default);
    }
    public Task<WaitingPerformanceEffects> Read()=>Transaction(ct=>Reader.ReadAsync(B,Date,Date.AddDays(1),ct),default);
    public WaitingDecisionProof Proof(Guid request)=>Payload<WaitingDecisionProof>(Decisions.Single(x=>x.ApprovalRequestId==Rows.Single(r=>r.SubjectId==request).Id).ProtectedChangeReference!);
    private T Payload<T>(string reference) { var file=JsonSerializer.Deserialize<PrivateFileReference>(reference)!;
        using var document=JsonDocument.Parse(Files[file.StorageKey].Data);return document.RootElement.GetProperty("Value").Deserialize<T>()!; }
    public async Task<T> Transaction<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct) {
        if(flowDepth.Value!=0) throw new Exception("Nested authorization transaction forbidden");
        await gate.WaitAsync(ct); flowDepth.Value++;MaxDepth=Math.Max(MaxDepth,flowDepth.Value);
        var counts=new[]{Rows.Count,Decisions.Count,Facts.Count,Audits.Count,Receipts.Count,Verified.Count,Assessments.Count,Jobs.Count};
        var originals=Rows.Select(x=>(Row:x,State:x.State,Closed:x.ClosedAt,Version:x.RowVersion.ToArray())).ToArray();
        var oldSaved=saved.ToDictionary(x=>x.Key,x=>x.Value);
        try { await Task.Yield();return await action(ct); }
        catch {
            Truncate(Rows,counts[0]);Truncate(Decisions,counts[1]);Truncate(Facts,counts[2]);Truncate(Audits,counts[3]);Truncate(Receipts,counts[4]);Truncate(Verified,counts[5]);Truncate(Assessments,counts[6]);Truncate(Jobs,counts[7]);
            foreach(var s in originals) {Set(s.Row,"State",s.State);Set(s.Row,"ClosedAt",s.Closed);Set(s.Row,"RowVersion",s.Version);}saved=oldSaved;throw;
        } finally {flowDepth.Value--;gate.Release();}
    }
    private static void Truncate<T>(List<T> list,int count)=>list.RemoveRange(count,list.Count-count);
    private static object? Append<T>(List<T> list,T value) {list.Add(value);return null;}
    private static void Set(object row,string property,object? value)=>row.GetType().GetProperty(property)!.SetValue(row,value);
    private object? RequireProducer() {RequireTransaction();return null;}
    public void RequireTransaction() {if(flowDepth.Value!=1) throw new InvalidOperationException("Existing synthetic transaction required");}
    public Task<PrivateFileReference> WriteAsync(byte[] content,string purpose,CancellationToken ct) {
        if(FailResultWrite && purpose.EndsWith(":result",StringComparison.Ordinal)) throw new AuthenticationFailure("private_storage_unavailable",503);
        var id=Guid.NewGuid().ToString("N");Files[id]=(content.ToArray(),purpose);
        var after=AfterNextWrite;AfterNextWrite=null;after?.Invoke();
        return Task.FromResult(new PrivateFileReference(id,"1",Convert.ToHexString(SHA256.HashData(content)),"synthetic-memory-key",content.Length));
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference,string purpose,CancellationToken ct) {
        var f=Files[reference.StorageKey];if(f.Purpose!=purpose || Convert.ToHexString(SHA256.HashData(f.Data))!=reference.Sha256) throw new AuthenticationFailure("private_evidence_unavailable",409);
        return Task.FromResult(f.Data.ToArray());
    }
    private ApprovalRequest Copy(ApprovalRequest approval) {
        var copy=(ApprovalRequest)typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(approval,[])!;
        if(saved.TryGetValue(approval.Id,out var state)) {Set(copy,"State",state.State);Set(copy,"ClosedAt",state.Closed);Set(copy,"RowVersion",state.Version.ToArray());}return copy;
    }
    private IReadOnlyList<WaitingStoredRecord> Records(string kind,bool tracked=false)=>Facts.Where(a=>a.EventType==WaitingWorkflowService.Event(kind)).Select(a=> {
        var approval=Rows.Single(x=>x.RequestType==WaitingWorkflowService.RequestType(kind) && x.SnapshotPrivateReference==a.ProtectedChangesReference);
        var ds=Decisions.Where(x=>x.ApprovalRequestId==approval.Id).ToArray();return new WaitingStoredRecord(a,tracked?approval:Copy(approval),ds.SingleOrDefault(),ds.Length);
    }).ToArray();
    public Task<WaitingStoredRecord?> FindAsync(string kind,Guid id,bool tracked,CancellationToken ct) {RequireTransaction();return Task.FromResult(Records(kind,tracked).SingleOrDefault(x=>x.Approval.SubjectId==id));}
    public Task<IReadOnlyList<WaitingStoredRecord>> PairsAsync(Guid worker,CancellationToken ct) {RequireTransaction();return Task.FromResult<IReadOnlyList<WaitingStoredRecord>>(Records("pair").Where(x=>x.Audit.EmployeeScopeId==worker).ToArray());}
    public Task<IReadOnlyList<WaitingStoredRecord>> LossAsync(string key,CancellationToken ct) {RequireTransaction();return Task.FromResult<IReadOnlyList<WaitingStoredRecord>>(Records("request").Where(x=>x.Audit.TargetId==key).ToArray());}
    public Task<IReadOnlyList<WaitingStoredRecord>> CausedAsync(Guid causer,DateOnly from,DateOnly to,CancellationToken ct) {
        RequireTransaction();var ids=Facts.Where(x=>x.EventType=="attendance.waiting.request.cause" && x.EmployeeScopeId==causer &&
            string.CompareOrdinal(x.TargetId,WaitingWorkflowService.CauseKey(causer,from))>=0 && string.CompareOrdinal(x.TargetId,WaitingWorkflowService.CauseKey(causer,to))<0).Select(x=>x.ProtectedChangesReference).ToArray();
        return Task.FromResult<IReadOnlyList<WaitingStoredRecord>>(Records("request").Where(x=>ids.Contains(x.Approval.SnapshotPrivateReference)).ToArray());
    }
    public Task<IReadOnlyList<AuditEvent>> DisputesAsync(Guid root,CancellationToken ct) {RequireTransaction();return Task.FromResult<IReadOnlyList<AuditEvent>>(Facts.Where(x=>x.EventType=="attendance.waiting.dispute.record" && x.TargetId==root.ToString("N")).ToArray());}
    public Task<IReadOnlyList<WaitingStoredRecord>> EffectsAsync(Guid request,CancellationToken ct) {RequireTransaction();return Task.FromResult<IReadOnlyList<WaitingStoredRecord>>(Records("effect").Where(x=>x.Audit.TargetId==request.ToString("N")).ToArray());}
    public Task<WaitingDocumentProof?> DocumentAsync(Guid id,Guid employee,DateOnly date,CancellationToken ct)=>throw new Exception("No fake verified documents in this workflow check");
    public Task<WaitingTimeEvidence?> VerifiedAsync(Guid approval,CancellationToken ct)=>Task.FromResult(Verified.SingleOrDefault(x=>x.ApprovalRequestId==approval));
    public Task<PolicyImpactAssessment?> AssessmentAsync(Guid id,CancellationToken ct)=>Task.FromResult(Assessments.SingleOrDefault(x=>x.Id==id));
    public void Add(ApprovalRequest approval,AuditEvent audit) {RequireTransaction();Rows.Add(approval);Facts.Add(audit);}
    public void Add(AuditEvent audit) {RequireTransaction();Facts.Add(audit);}
    public void Add(ApprovalDecision decision) {RequireTransaction();Decisions.Add(decision);}
    public void Add(WaitingTimeEvidence evidence) {RequireTransaction();Verified.Add(evidence);}
    public void Add(PolicyImpactAssessment assessment) {RequireTransaction();Assessments.Add(assessment);}
    public void ExpectVersion(ApprovalRequest approval,byte[] expected) {if(!approval.RowVersion.SequenceEqual(expected)) throw new AuthenticationFailure("waiting_stale_version",409);}
    public Task SaveChangesAsync(CancellationToken ct) {
        RequireTransaction();foreach(var row in Rows) {if(!saved.TryGetValue(row.Id,out var previous) || previous.State!=row.State || previous.Closed!=row.ClosedAt)
            Set(row,"RowVersion",BitConverter.GetBytes(++versionCounter));saved[row.Id]=(row.State,row.ClosedAt,row.RowVersion.ToArray());}return Task.CompletedTask;
    }
}
