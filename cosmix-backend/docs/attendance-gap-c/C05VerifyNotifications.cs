#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Application.Attendance.Waiting;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Common.Approvals;

var checks=0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
var requester=Guid.NewGuid(); var reviewer=Guid.NewGuid(); var now=DateTimeOffset.UtcNow;
var context=new AuthRequestContext("", "c05-synthetic-outbox-check"); var outbox=new SyntheticOutbox();
foreach (var kind in new[] { "pair", "request", "effect" }) foreach (var state in new[] { ApprovalState.Approved, ApprovalState.Returned, ApprovalState.Rejected })
{
    var approval=new ApprovalRequest(WaitingWorkflowService.RequestType(kind),Guid.NewGuid(),requester.ToString(),"synthetic-protected-reference",1,now);
    var decision=approval.Decide(state,reviewer.ToString(),"synthetic-audit-basis","Standalone decision check",now.AddSeconds(1),"synthetic-decision-proof");
    Check(await WaitingDecisionNotifications.StageAsync(outbox,approval,decision,context,default),kind+" first intent staged");
    var intent=outbox.Intents.Last();
    Check(intent.EventId==decision.Id && intent.RecipientAccountId==requester && intent.ResourceId==approval.SubjectId && intent.TemplateCode==NotificationTemplates.Decision,
        kind+" notification derives exact event/requester/case and existing generic template");
    Check(!await WaitingDecisionNotifications.StageAsync(outbox,approval,decision,context,default),kind+" duplicate intent deduplicates");
    try { await outbox.StageAsync(intent with {ResourceId=Guid.NewGuid()},context,default); throw new Exception("Changed duplicate resource accepted"); }
    catch(AuthenticationFailure e) when(e.Code=="notification_event_identity_conflict") { checks++; }
}
var caseRequest=new ApprovalRequest(WaitingWorkflowService.RequestType("request"),Guid.NewGuid(),requester.ToString(),"synthetic",1,now);
var caseDecision=caseRequest.Decide(ApprovalState.Approved,reviewer.ToString(),"synthetic-audit-basis",null,now.AddSeconds(1),"synthetic-proof");
outbox.FailNext=true;
try { await WaitingDecisionNotifications.StageAsync(outbox,caseRequest,caseDecision,context,default); throw new Exception("Outbox failure suppressed"); }
catch(AuthenticationFailure e) when(e.Code=="private_storage_unavailable") { checks++; }
Check(outbox.Intents.All(x=>x.EventId!=caseDecision.Id),"Failed stage cannot report a staged intent");
var other=new ApprovalRequest(WaitingWorkflowService.RequestType("effect"),Guid.NewGuid(),requester.ToString(),"synthetic",1,now);
try { await WaitingDecisionNotifications.StageAsync(outbox,other,caseDecision,context,default); throw new Exception("Foreign decision accepted"); }
catch(AuthenticationFailure e) when(e.Code=="waiting_notification_binding_conflict") { checks++; }
Console.WriteLine($"C05 decision/outbox contract checks passed: {checks}. Synthetic adapter only; no SQL, host, send or transaction acceptance.");
sealed class SyntheticOutbox : INotificationOutbox
{
    private readonly Dictionary<string,NotificationIntent> saved=[];
    public IReadOnlyList<NotificationIntent> Intents=>saved.Values.ToArray();
    public bool FailNext;
    public Task<bool> StageAsync(NotificationIntent intent,AuthRequestContext context,CancellationToken ct)
    {
        NotificationOutbox.Validate(intent);
        if(FailNext) { FailNext=false; throw new AuthenticationFailure("private_storage_unavailable",503); }
        var key=NotificationOutbox.Key(intent);
        if(saved.TryGetValue(key,out var previous)) {
            if(previous!=intent) throw new AuthenticationFailure("notification_event_identity_conflict",409);
            return Task.FromResult(false);
        }
        saved.Add(key,intent); return Task.FromResult(true);
    }
}
