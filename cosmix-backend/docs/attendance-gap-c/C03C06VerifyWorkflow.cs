#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Api.Attendance;
using Cosmix.Api.Controllers.Attendance;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Application.HR.Policies.Advanced;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.Common.Jobs;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.PolicyEffects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Cosmix.Infrastructure.Repositories.Attendance;

var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
async Task Deny(Func<Task> action, string code) {
    try { await action(); } catch (AuthenticationFailure e) when (e.Code == code) { checks++; return; }
    throw new Exception("Expected " + code);
}
var tokenClock = new TestClock();
var tokenProvider = new EphemeralDataProtectionProvider();
var tokenProtector = new WorkTreatmentTicketProtector(tokenProvider, tokenClock);
var ticket = new WorkTreatmentTicket(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),WorkTreatmentKind.EarlyInstruction,
    new(2026,10,13),new(2026,10,13),new string('A',64),tokenClock.Now.AddMinutes(15));
var protectedTicket = tokenProtector.Protect(ticket);
Check(tokenProtector.Unprotect(protectedTicket) == ticket, "Actual API protector retains every exact ticket binding");
await Deny(() => { tokenProtector.Unprotect(protectedTicket + "="); return Task.CompletedTask; },"invalid_work_source_ticket");
await Deny(() => { tokenProtector.Unprotect(new string('x',4097)); return Task.CompletedTask; },"invalid_work_source_ticket");
var otherPurpose = tokenProvider.CreateProtector("Cosmix.Attendance.Cursor.v1").Protect(JsonSerializer.Serialize(ticket));
await Deny(() => { tokenProtector.Unprotect(otherPurpose); return Task.CompletedTask; },"invalid_work_source_ticket");
var malformedEnvelope = tokenProvider.CreateProtector("attendance.work-treatment.source.v1").Protect(JsonSerializer.Serialize(
    new { Version = 1, IssuedAtUtc = tokenClock.Now, Ticket = ticket, Unexpected = "reject" }));
await Deny(() => { tokenProtector.Unprotect(malformedEnvelope); return Task.CompletedTask; },"invalid_work_source_ticket");
tokenClock.Now = tokenClock.Now.AddDays(1);
Check(tokenProtector.Unprotect(protectedTicket) == ticket, "Expired ticket can authenticate retained replay; workflow owns live expiry");
Check(typeof(WorkTreatmentsController).GetCustomAttribute<AuthorizeAttribute>() is not null &&
    typeof(WorkTreatmentsController).GetCustomAttribute<ResponseCacheAttribute>()?.NoStore == true, "Compiled HTTP controller requires authorization and no cache");
Check(typeof(WorkTreatmentView).GetProperties().All(x => !x.Name.Contains("PrivateReference")), "Client view has no private-storage reference");
var f = new Fixture();
var registered = WorkTreatmentPermissionDefs.Definitions.All(x => AccessPermissionCatalog.Find(x.Code) == x);
if (!registered) {
    await Deny(() => f.Source(WorkTreatmentKind.EarlyInstruction), "access_denied");
    Check(f.Rows.Count == 0 && f.Receipts.Count == 0, "Missing parent catalog fails closed");
    Console.WriteLine($"PASS: {checks} compiled controller/catalog guard checks.");
    Console.WriteLine("HOLD: actual service success/retry/race tests were NOT executed because parent catalog binding is missing.");
    if (args.Contains("--require-catalog")) throw new Exception("Parent work-treatment catalog binding required; no guard weakening.");
    return;
}
var p = await f.Instruction(WorkTreatmentKind.EarlyInstruction);
var instruction = await f.Service.SubmitAsync(f.Employee, p, "early-instruction-key", f.Context, default);
Check(f.Rows.Count == 1 && f.Receipts.Count == 1 && f.Facts.Count == 1, "Actual submission saves linked approval, controlled fact and scoped receipt");
Check((await f.Service.SubmitAsync(f.Employee, p, "early-instruction-key", f.Context, default)).Id == instruction.Id && f.Rows.Count == 1,
    "Exact retry returns retained result without duplicate approval");
await Deny(() => f.Service.SubmitAsync(f.Employee, p with { Reason = "changed payload" }, "early-instruction-key", f.Context, default), "idempotency_key_reused");
await Deny(() => f.Service.DecideAsync(f.Employee, instruction.Id, p.Kind, new(instruction.Version, ApprovalState.Approved, null, [], "review"),
    "self-review-key", f.Context, default), "work_treatment_self_review_forbidden");
f.Actor = f.Reviewer;
var approved = await f.Service.DecideAsync(f.Employee, instruction.Id, p.Kind, new(instruction.Version, ApprovalState.Approved, null, [], "independent exact review"),
    "instruction-review-key", f.Context, default);
Check(approved.State == ApprovalState.Approved && approved.ReviewedResult!.ExactTicks == 0 && f.Decisions.Count == 1 && f.Notifications.Count == 1,
    "Actual instruction approval records zero credit and stages one decision outbox intent");
var actualNotification = await NotificationOutbox.ReadAsync(f.Notifications.Single(), f, default);
Check(actualNotification.EventId == f.Decisions.Single().Id && actualNotification.ResourceId == instruction.Id,
    "Actual NotificationOutbox binds its protected payload to the real decision and request");
Check((await f.Service.DecideAsync(f.Employee, instruction.Id, p.Kind, new(instruction.Version, ApprovalState.Approved, null, [], "independent exact review"),
    "instruction-review-key", f.Context, default)).Id == instruction.Id && f.Notifications.Count == 1, "Review receipt retries do not stage another notification");
f.Actor = f.Submitter;
var creditSource = await f.Source(WorkTreatmentKind.EarlyCredit);
var credit = f.Request(creditSource, f.At(8,45).AddTicks(3), f.At(9)) with { InstructionRequestId = instruction.Id };
var creditPending = await f.Service.SubmitAsync(f.Employee, credit, "early-credit-key", f.Context, default);
f.Actor = f.Reviewer;
var creditApproved = await f.Service.DecideAsync(f.Employee, creditPending.Id, credit.Kind,
    new(creditPending.Version, ApprovalState.Approved, null, [], "review exact actual interval"), "credit-review-key", f.Context, default);
Check(creditApproved.ReviewedResult!.ExactTicks == 15 * TimeSpan.TicksPerMinute - 3 && f.Notifications.Count == 2,
    "Actual early-credit workflow retains fractional ticks and distinct notification identity");
var retainedCredits = await f.Transaction(ct => f.Credits.ReadAsync(f.Employee, f.Date, f.Date, ct), default);
Check(retainedCredits.Single().DecisionId == f.Decisions.Last().Id && retainedCredits.Single().Range.Ticks == creditApproved.ReviewedResult.ExactTicks,
    "Credit reader returns real approval/decision IDs and exact retained interval");
Check(retainedCredits.Single().InstructionProof?.RequestId == instruction.Id &&
    retainedCredits.Single().InstructionProof!.DecisionId == f.Decisions.First().Id && retainedCredits.Single().PolicyVersionId == f.PolicyId &&
    retainedCredits.Single().InstructionProof!.WrittenDocumentId == f.Document && retainedCredits.Single().OriginalScopes.Count > 0,
    "Early calculation evidence includes actual named-instruction approval/decision/document/policy and retained scope proof");
await Deny(() => f.Transaction(async ct => { await f.Credits.CheckExclusiveAsync(f.Employee,
    [retainedCredits[0].Range with { SourceId = Guid.NewGuid(), SessionKey = Guid.NewGuid(), Role = WorkCreditRole.LeaveRecovery }], [], ct); return true; }, default),
    "work_interval_already_claimed");
await Deny(() => f.Transaction(async ct => { await f.Credits.CheckExclusiveAsync(f.Employee,
    [retainedCredits[0].Range with { SourceId = Guid.NewGuid(), SessionKey = Guid.NewGuid(), BusinessDate = f.Date.AddDays(1), Role = WorkCreditRole.LeaveRecovery }], [], ct); return true; }, default),
    "work_interval_already_claimed");
f.Actor = f.Submitter;
await Deny(() => f.Service.SubmitAsync(f.Employee, credit, "duplicate-source-key", f.Context, default), "work_interval_already_claimed");
f.CurrentSession = false;
await Deny(() => f.Service.SubmitAsync(f.Employee, credit, "early-credit-key", f.Context, default), "invalid_authentication");
f.CurrentSession = true; f.Actor = f.Actor with { MfaSatisfied = false };
await Deny(() => f.Source(WorkTreatmentKind.CompanyTrip), "access_denied");
f.Actor = f.Submitter; f.Scope = AccessScopeKind.Site; f.TargetSite = f.Site;
var visible = await f.Service.DetailAsync(f.Employee, creditPending.Id, false, f.Context, default);
Check(visible.Request.Id == creditPending.Id, "Exact original/current site grant reads retained evidence");
f.CurrentSite = Guid.NewGuid();
await Deny(() => f.Service.DetailAsync(f.Employee, creditPending.Id, false, f.Context, default), "access_denied");
f.CurrentSite = f.Site; f.Scope = AccessScopeKind.All;
var study = await f.Instruction(WorkTreatmentKind.StudyInstruction);
var studyPending = await f.Service.SubmitAsync(f.Employee, study, "study-instruction-key", f.Context, default);
f.Actor = f.Reviewer;
var dispute = await f.Service.DisputeAsync(f.Employee, studyPending.Id, new(studyPending.Version, "Disputed obligation evidence", []), "study-dispute-key", false, f.Context, default);
await Deny(() => f.Service.DecideAsync(f.Employee, studyPending.Id, study.Kind, new(studyPending.Version, ApprovalState.Approved, null, [], "review"),
    "unaddressed-dispute-key", f.Context, default), "work_treatment_disputes_require_explicit_review");
var studyApproved = await f.Service.DecideAsync(f.Employee, studyPending.Id, study.Kind,
    new(studyPending.Version, ApprovalState.Approved, null, dispute.DisputeIds, "reviewed exact disputed obligation"), "study-review-key", f.Context, default);
var studyEvidence = await f.Transaction(ct => f.Credits.StudyAsync(f.Employee, f.Date, f.Date, ct), default);
Check(studyEvidence.Single().Rules.KpiTreatment == StudyKpiTreatment.ExcludeApprovedExcusedInterval && studyEvidence.Single().ExcusedEndUtc == f.At(11),
    "Actual study reader exposes exact approved KPI rule and excused interval");
Check(studyEvidence.Single().InstructionProof?.RequestId == studyPending.Id &&
    studyEvidence.Single().InstructionProof!.NamedWrittenInstruction == study.Instruction!.NamedInstruction,
    "Study calculation evidence retains the named written instruction and actual proof identities");
f.Actor = f.Submitter;
var makeUpSource = await f.Source(WorkTreatmentKind.MakeUp);
var makeUp = f.Request(makeUpSource, f.At(18), f.At(19)) with { InstructionRequestId = studyPending.Id };
var makeUpPending = await f.Service.SubmitAsync(f.Employee, makeUp, "makeup-request-key", f.Context, default);
f.Actor = f.Reviewer;
await f.Service.DecideAsync(f.Employee, makeUpPending.Id, makeUp.Kind, new(makeUpPending.Version, ApprovalState.Approved, null, [], "actual fulfillment"),
    "makeup-review-key", f.Context, default);
f.Actor = f.Submitter;
var excess = f.Request(makeUpSource, f.At(19), f.At(20)) with { InstructionRequestId = studyPending.Id };
var excessPending = await f.Service.SubmitAsync(f.Employee, excess, "excess-makeup-key", f.Context, default);
f.Actor = f.Reviewer;
await Deny(() => f.Service.DecideAsync(f.Employee, excessPending.Id, excess.Kind, new(excessPending.Version, ApprovalState.Approved, null, [], "review"),
    "excess-makeup-review-key", f.Context, default), "make_up_obligation_overallocated");
var staleVersion = Convert.ToBase64String(new byte[8]);
await Deny(() => f.Service.DecideAsync(f.Employee, excessPending.Id, excess.Kind, new(staleVersion, ApprovalState.Returned, null, [], "return"),
    "stale-review-key", f.Context, default), "work_treatment_version_conflict");
await f.Service.DecideAsync(f.Employee, excessPending.Id, excess.Kind, new(excessPending.Version, ApprovalState.Returned, null, [], "return for correction"),
    "returned-review-key", f.Context, default);
f.Actor = f.Submitter;
var resubmission = await f.Service.SubmitAsync(f.Employee, excess with { ResubmissionOf = excessPending.Id }, "returned-resubmit-key", f.Context, default);
Check(resubmission.Id != excessPending.Id, "Returned proposal resubmission keeps original request immutable");
var tripSource = await f.Source(WorkTreatmentKind.CompanyTrip);
var trip = f.Request(tripSource, f.At(12), f.At(13)) with { DocumentIds = [f.Document] };
f.Ot = [new(f.Employee, Guid.NewGuid(), WorkCreditRole.Overtime, Guid.NewGuid(), Guid.NewGuid(), f.Date, f.At(12), f.At(13))];
await Deny(() => f.Service.SubmitAsync(f.Employee, trip, "ot-conflict-key", f.Context, default), "work_interval_already_claimed");
f.Ot = [];
var race = new Fixture();
var raceInstruction = await race.Instruction(WorkTreatmentKind.EarlyInstruction);
race.AfterPrivateWrite = () => race.SourceRevision++;
await Deny(() => race.Service.SubmitAsync(race.Employee, raceInstruction, "source-race-key", race.Context, default), "work_treatment_source_changed");
Check(race.Rows.Count == 0 && race.Facts.Count == 0 && race.Receipts.Count == 0 && race.Notifications.Count == 0,
    "Private-I/O source race rolls back approval/audit/receipt/outbox");
var sessionRace = new Fixture();
var sessionRaceInstruction = await sessionRace.Instruction(WorkTreatmentKind.EarlyInstruction);
sessionRace.AfterPrivateWrite = () => sessionRace.CurrentSession = false;
await Deny(() => sessionRace.Service.SubmitAsync(sessionRace.Employee, sessionRaceInstruction, "session-race-key", sessionRace.Context, default), "invalid_authentication");
Check(sessionRace.Rows.Count == 0 && sessionRace.Receipts.Count == 0, "Exact session revocation during private I/O fails closed");
var outboxFailure = new Fixture();
var outboxProposal = await outboxFailure.Instruction(WorkTreatmentKind.EarlyInstruction);
var outboxPending = await outboxFailure.Service.SubmitAsync(outboxFailure.Employee,outboxProposal,"outbox-submit-key",outboxFailure.Context,default);
outboxFailure.Actor = outboxFailure.Reviewer; outboxFailure.RecipientExists = false;
var receiptsBefore = outboxFailure.Receipts.Count;
await Deny(() => outboxFailure.Service.DecideAsync(outboxFailure.Employee,outboxPending.Id,outboxProposal.Kind,
    new(outboxPending.Version,ApprovalState.Approved,null,[],"review"),"outbox-failure-review",outboxFailure.Context,default),"notification_recipient_not_found");
Check(outboxFailure.Rows.Single().State == ApprovalState.Pending && outboxFailure.Decisions.Count == 0 && outboxFailure.Notifications.Count == 0 && outboxFailure.Receipts.Count == receiptsBefore,
    "Real outbox unavailable-recipient guard rolls back review and receipt without inventing a recipient");
var competing = new Fixture();
var competingProposal = await competing.Instruction(WorkTreatmentKind.EarlyInstruction);
var competingPending = await competing.Service.SubmitAsync(competing.Employee,competingProposal,"competing-submit-key",competing.Context,default);
competing.Actor = competing.Reviewer;
async Task<string> Compete(string key) {
    try { await competing.Service.DecideAsync(competing.Employee,competingPending.Id,competingProposal.Kind,
        new(competingPending.Version,ApprovalState.Approved,null,[],"independent review"),key,competing.Context,default); return "approved"; }
    catch (AuthenticationFailure e) { return e.Code; }
}
var races = await Task.WhenAll(Compete("competing-review-one"),Compete("competing-review-two"));
Check(races.Count(x => x == "approved") == 1 && races.Count(x => x == "work_treatment_version_conflict") == 1 && competing.Decisions.Count == 1 && competing.Notifications.Count == 1,
    "Competing actual service reviews under a serialized transaction commit one decision and one real outbox record");
var noSource = new Fixture { NoSessions = true };
var emptySource = await noSource.Source(WorkTreatmentKind.EarlyInstruction);
Check(emptySource.Source.State == "no_actual_sessions" && emptySource.Source.Explanation.Contains("No real work-session"),
    "No-source state reports factual absence without claiming a permanent feature block");
var emptyInstruction = await noSource.Instruction(WorkTreatmentKind.EarlyInstruction);
Check((await noSource.Service.SubmitAsync(noSource.Employee, emptyInstruction, "future-instruction-key", noSource.Context, default)).State == ApprovalState.Pending,
    "Dated written instruction can enter review without fabricated actual sessions");
var dateScope = new Fixture { NoSessions = true, Scope = AccessScopeKind.Site };
dateScope.TargetSite = dateScope.Site; dateScope.PreviousSite = Guid.NewGuid(); dateScope.CustomRosterStart = dateScope.At(0,30);
await Deny(() => dateScope.Source(WorkTreatmentKind.EarlyInstruction),"access_denied");
dateScope.Scope = AccessScopeKind.All;
var crossDateInstruction = await dateScope.Source(WorkTreatmentKind.EarlyInstruction);
Check(crossDateInstruction.Source.Scopes.SelectMany(x => x.Alternatives).Any(x => x.SiteId == dateScope.PreviousSite),
    "Pre-start instruction retains previous physical business-date scope as well as the duty source date");
var cases = new Fixture();
var continuous = new Fixture();
var continuousSource = await continuous.Source(WorkTreatmentKind.ContinuousDuty);
var continuousProposal = continuous.Request(continuousSource,continuous.At(8),continuous.At(20)) with {
    DocumentIds = [continuous.Document], CycleEvents = [new(WorkCycleEventKind.Break,continuous.At(12),continuous.At(12,30),continuous.Document)]
};
var continuousPending = await continuous.Service.SubmitAsync(continuous.Employee,continuousProposal,"continuous-submit-key",continuous.Context,default);
continuous.Actor = continuous.Reviewer;
await continuous.Service.DecideAsync(continuous.Employee,continuousPending.Id,continuousProposal.Kind,
    new(continuousPending.Version,ApprovalState.Approved,null,[],"Review exact continuous evidence"),"continuous-review-key",continuous.Context,default);
var continuousEvidence = await continuous.Transaction(ct => continuous.Credits.ContinuousAsync(continuous.Employee,continuous.Date,continuous.Date,ct),default);
Check(continuousEvidence.Single().DecisionId == continuous.Decisions.Single().Id && continuousEvidence.Single().Sessions.Single().BusinessDate == continuous.Date &&
    continuousEvidence.Single().CycleEvents.Count == 1 && continuousEvidence.Single().Cycles.Sum(x => x.ActualTicks) == TimeSpan.TicksPerHour * 11.5m &&
    continuousEvidence.Single().Rules.ApprovedExampleReference == "EXPLICIT-TEST-EXAMPLE",
    "Continuous reader returns whole exact actual sessions/events/cycles and configured example with real decision proof, not a payable source");
var targetEvidence = await continuous.Transaction(ct => continuous.Credits.ContinuousAsync(continuous.Employee,continuous.Date.AddDays(1),continuous.Date.AddDays(1),ct),default);
Check(targetEvidence.Single().TargetDays.All(x => x.SourceBusinessDate == continuous.Date && x.TargetBusinessDate == continuous.Date.AddDays(1) &&
    x.NextDayRequiredTicks == 4.5m * TimeSpan.TicksPerHour) && targetEvidence.Single().Cycles.Count == continuousEvidence.Single().Cycles.Count,
    "Target-day lookup returns whole reviewed cycles based on source business dates without clipping or inventing a reset");
var allegedSource = await cases.Source(WorkTreatmentKind.AllegedFakePunch);
var alleged = cases.Request(allegedSource,cases.At(9),cases.At(10)) with { Intervals = [],DocumentIds = [cases.Document] };
var allegedPending = await cases.Service.SubmitAsync(cases.Employee,alleged,"alleged-fake-key",cases.Context,default);
Check(cases.Cases.Count == 0, "Allegation intake cannot create a disciplinary entity or punishment");
cases.Actor = cases.Reviewer;
await cases.Service.DecideAsync(cases.Employee,allegedPending.Id,alleged.Kind,
    new(allegedPending.Version,ApprovalState.Rejected,null,[],"Evidence rejected"),"alleged-reject-key",cases.Context,default);
Check(cases.Cases.Count == 0, "Rejected alleged fake punch does not create a disciplinary closure");
cases.Actor = cases.Submitter;
var freshAllegedSource = await cases.Source(WorkTreatmentKind.AllegedFakePunch);
var reviewedAlleged = cases.Request(freshAllegedSource,cases.At(9),cases.At(10)) with { Intervals = [],DocumentIds = [cases.Document] };
var reviewedAllegedPending = await cases.Service.SubmitAsync(cases.Employee,reviewedAlleged,"reviewed-alleged-key",cases.Context,default);
cases.Actor = cases.Reviewer;
var closedAlleged = await cases.Service.DecideAsync(cases.Employee,reviewedAllegedPending.Id,reviewedAlleged.Kind,
    new(reviewedAllegedPending.Version,ApprovalState.Approved,WorkCaseOutcome.Unsubstantiated,[],"Independent factual closure"),"alleged-close-key",cases.Context,default);
Check(cases.Cases.Single().ClosedAt.HasValue && closedAlleged.ClosedDisciplinaryCaseId == cases.Cases.Single().Id && closedAlleged.ReviewedResult!.ExactTicks == 0,
    "Actual independent approved closure creates only the linked closed case with zero monetary/time award");
if (AccessPermissionCatalog.Find("attendance.kpi.decide") is not null) {
    var annual = new Fixture();
    var annualSource = await annual.Service.SourcesAsync(annual.Employee,WorkTreatmentKind.AnnualLate,annual.Date,annual.Date,annual.Context,default,annual.AnnualSourceId);
    var annualProposal = annual.Request(annualSource,annual.At(9),annual.At(10)) with { Intervals = [],AnnualLateSourceId = annual.AnnualSourceId };
    var annualPending = await annual.Service.SubmitAsync(annual.Employee,annualProposal,"annual-case-key",annual.Context,default);
    annual.Actor = annual.Reviewer;
    var annualClosed = await annual.Service.DecideAsync(annual.Employee,annualPending.Id,annualProposal.Kind,
        new(annualPending.Version,ApprovalState.Approved,WorkCaseOutcome.FactualFinding,[],"Independent annual count review"),"annual-case-review-key",annual.Context,default);
    var outcomeReader = new WorkTreatmentDisciplinaryOutcomeReader(annual,annual.Evidence);
    var annualCalls = annual.AnnualCalls;
    annual.AnnualPending = false;
    var outcome = await annual.Transaction(ct => outcomeReader.ReadAsync(annual.Employee,annual.AnnualSourceId,new string('B',64),ct),default);
    Check(outcome?.AnnualLateSourceId == annual.AnnualSourceId && outcome.DecisionId == annual.Decisions.Single().Id && annual.AnnualCalls == annualCalls,
        "Retained actual annual closure validates the pending request identity without rereading a now-approved KPI source");
    var history = await annual.Service.DetailAsync(annual.Employee,annualPending.Id,false,annual.Context,default);
    Check(history.Request.Id == annualClosed.Id, "Annual history survives KPI approval without a circular source dependency");
    var retry = await annual.Service.DecideAsync(annual.Employee,annualPending.Id,annualProposal.Kind,
        new(annualPending.Version,ApprovalState.Approved,WorkCaseOutcome.FactualFinding,[],"Independent annual count review"),"annual-case-review-key",annual.Context,default);
    Check(retry.Id == annualClosed.Id && annual.Notifications.Count == 1, "Annual decision receipt remains valid after KPI approval");
    annual.Actor = annual.Submitter;
    Check((await annual.Service.SubmitAsync(annual.Employee,annualProposal,"annual-case-key",annual.Context,default)).Id == annualPending.Id,
        "Annual submission receipt does not rerun the pending-only annual source reader");
}
f.Actor = f.Submitter;
f.Clock.Now = f.Clock.Now.AddDays(2);
var recovered = await f.Service.ReconcileAsync(f.Employee, "early-instruction-key", f.Context, default);
Check(recovered.Request.Id == instruction.Id && recovered.Request.State == ApprovalState.Approved,
    "Permanent submission-key reconciliation works after receipt expiry");
Console.WriteLine($"PASS: {checks} C03/C06 actual service/reader/retry/scope/dispute/race/outbox-intent checks; in-memory IO, no SQL/host/provider.");

sealed class TestClock : TimeProvider {
    public DateTimeOffset Now = new(2026,10,13,23,0,0,TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
class Proxy : DispatchProxy {
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class {
        var value = Create<T, Proxy>(); ((Proxy)(object)value).Handler = handler; return value;
    }
}
sealed class Fixture : IWorkTreatmentRepository, IPrivateFileStore {
    public TestClock Clock = new(); public DateOnly Date = new(2026,10,13);
    public Guid Employee = Guid.NewGuid(), Site = Guid.NewGuid(), CurrentSite, Document = Guid.NewGuid(), Assignment = Guid.NewGuid(), Shift = Guid.NewGuid(), PolicyId = Guid.NewGuid();
    public AuthenticatedAccount Submitter, Reviewer, Actor;
    public AuthRequestContext Context = new("127.0.0.1","synthetic-c03c06");
    public bool CurrentSession = true, InTransaction, NoSessions, AnnualPending = true, RecipientExists = true;
    public Guid AnnualSourceId = Guid.NewGuid(); public int AnnualCalls;
    public AccessScopeKind Scope = AccessScopeKind.All; public Guid? TargetSite;
    public int SourceRevision = 1; public Action? AfterPrivateWrite;
    public Guid? PreviousSite; public DateTimeOffset? CustomRosterStart;
    public List<ApprovalRequest> Rows = []; public List<ApprovalDecision> Decisions = []; public List<AuditEvent> Facts = [], Audits = [];
    public List<IdempotencyReceipt> Receipts = []; public List<OutboxMessage> Notifications = []; public List<DisciplinaryCase> Cases = [];
    public IReadOnlyList<WorkCreditRange> Ot = [];
    public WorkTreatmentService Service; public WorkTreatmentCreditReader Credits; public WorkTreatmentEvidence Evidence;
    private WorkTreatmentSession session;
    private readonly Dictionary<string,(byte[] Data,string Purpose)> files = [];
    private readonly SemaphoreSlim gate = new(1,1);
    public DateTimeOffset At(int hour,int minute=0) => new(2026,10,13,hour,minute,0,TimeSpan.Zero);
    public Fixture() {
        CurrentSite = Site; Submitter = new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Web,true);
        Reviewer = new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Web,true); Actor = Submitter;
        session = new(Guid.NewGuid(),Guid.NewGuid(),1,Date,At(8),At(20),Site,null,Shift,null,Assignment,null,null,null,
            Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),null,null,true,false);
        var current = Proxy.Make<ICurrentAuthentication>((_,_) => Actor);
        var grants = Proxy.Make<IAccountAccessReader>((_,_) => Task.FromResult<IReadOnlyList<PermissionGrant>>(
            WorkTreatmentPermissionDefs.Definitions.Select(x => new PermissionGrant(x.Code,x.Sensitive,Assignment,Scope,Scope == AccessScopeKind.All ? null : TargetSite))
                .Append(new PermissionGrant("attendance.kpi.decide",true,Assignment,Scope,Scope == AccessScopeKind.All ? null : TargetSite)).ToArray()));
        var authority = Proxy.Make<IAccessManagementRepository>((m,a) => m.Name switch {
            "InTransactionAsync" => typeof(Fixture).GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this,[a[0],a[1]]),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("EXPLICIT-TEST-REVIEWER"),
            _ => throw new Exception("Unexpected authority " + m.Name)
        });
        var journal = Proxy.Make<ISchedulingJournalRepository>((m,a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(CurrentSession && (AuthenticatedAccount)a[0]! == Actor),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => Append(Receipts,(IdempotencyReceipt)a[0]!),
            "AddAudit" => Append(Audits,(AuditEvent)a[0]!),
            "SaveChangesAsync" => Task.CompletedTask,
            _ => throw new Exception("Unexpected journal " + m.Name)
        });
        var auth = Proxy.Make<IAuthenticationRepository>((_,_) => throw new Exception("Web workflow cannot infer mobile device"));
        var operation = new AttendanceOperation(current,grants,authority,journal,auth,Clock,this);
        var roster = Proxy.Make<IIndividualRosterResolver>((_,a) => Task.FromResult(new EffectiveRosterResponse((Guid)a[0]!, (DateOnly)a[1]!, "resolved", "UTC",
            SiteId: Site, AssignmentId: Assignment, ShiftVersionId: Shift, StartsAt: CustomRosterStart ?? At(9), EndsAt: At(18))));
        var policies = Proxy.Make<IAdvancedAttendancePolicyReader>((_,a) => {
            var family = (AdvancedAttendanceFamily)a[1]!;
            var rules = Rules(family);
            return Task.FromResult(new AdvancedPolicyResolution("configured", (Guid)a[0]!, family, (DateOnly)a[2]!, PolicyId, [Assignment], rules, new string('A',64)));
        });
        var annualReader = Proxy.Make<IWorkTreatmentAnnualLateReader>((_,_) => {
            AnnualCalls++;
            if (!AnnualPending) throw AttendanceOperation.Conflict("kpi_annual_late_source_not_pending");
            return Task.FromResult<WorkAnnualLateEvidence?>(new(AnnualSourceId,Employee,new(2026,1,1),new(2026,12,31),121,PolicyId,
                new string('A',64),new string('B',64),[new([new(EmployeeId: Employee,SiteId: Site)])]));
        });
        var sources = new WorkTreatmentSources(this,roster,policies,new WorkTreatmentTicketProtector(new EphemeralDataProtectionProvider(),Clock),annualReader);
        var evidence = new WorkTreatmentEvidence(this,operation,this);
        Evidence = evidence;
        var jobs = Proxy.Make<IDurableJobRepository>((m,a) => m.Name switch {
            "RequireProducerTransaction" => RequireProducer(),
            "FindByIdentityAsync" => Task.FromResult(Notifications.SingleOrDefault(x => x.MessageType == (string)a[0]! && x.DeduplicationKey == (string)a[1]!)),
            "RecipientExistsAsync" => Task.FromResult(RecipientExists && ((Guid)a[0]! == Submitter.AccountId || (Guid)a[0]! == Reviewer.AccountId)),
            "Stage" => Append(Notifications,(OutboxMessage)a[0]!),
            _ => throw new Exception("Unexpected outbox call " + m.Name)
        });
        var recoveryCredits = Proxy.Make<ILeaveRecoveryCreditReader>((m,_) => {
            if (m.Name != "ReadReservedAsync") throw new Exception("Unexpected recovery allocation read " + m.Name);
            RequireTransaction();
            return Task.FromResult<IReadOnlyList<WorkTreatmentCreditEvidence>>([]);
        });
        Service = new(operation,this,sources,evidence,journal,new NotificationOutbox(jobs,this,Clock),recoveryCredits);
        Credits = new(this,evidence,sources,recoveryCredits);
    }
    private AdvancedAttendanceRules Rules(AdvancedAttendanceFamily family) => new(1,family,["SITE"],
        new("EXPLICIT-TEST-POLICY",AdvancedAttendanceRulesCodec.ReviewPermission(family),true,
            [AdvancedEvidenceKind.AcceptedDuty,AdvancedEvidenceKind.WrittenInstruction,AdvancedEvidenceKind.TripRecord,AdvancedEvidenceKind.VerifiedDocument]),
        EarlyCredit: family == AdvancedAttendanceFamily.EarlyCredit ? new(3600,true,true) : null,
        StudyMakeUp: family == AdvancedAttendanceFamily.StudyMakeUp ? new(true,true,StudyKpiTreatment.ExcludeApprovedExcusedInterval,7200,7,true) : null,
        CompanyTravel: family == AdvancedAttendanceFamily.CompanyTravel ? new(true,true,false,true) : null,
        ContinuousDuty: family == AdvancedAttendanceFamily.ContinuousDuty ? new(10,4.5m,12,true,true,"EXPLICIT-TEST-EXAMPLE") : null,
        Kpi: family == AdvancedAttendanceFamily.Kpi ? new KpiRules(100,1,2,AdvancedRounding.ToEven,0,100,KpiLateBasis.PercentagePoints,0.8m,0.5m,
            KpiBeyondTierTreatment.ReviewRequired,null,KpiShortageTreatment.None,null,false,null) { Composition = KpiCompositionStrategy.Additive } : null);
    public Task<WorkTreatmentSourceResponse> Source(WorkTreatmentKind kind) => Service.SourcesAsync(Employee,kind,Date,Date,Context,default);
    public WorkTreatmentRequest Request(WorkTreatmentSourceResponse s,DateTimeOffset start,DateTimeOffset end) => new(s.Source.Kind,Date,Date,start,end,
        s.Source.Fingerprint,s.SourceTicket,[new(session.Id,Date,start,end)],[],[],null,null,null,null,"explicit factual proposal");
    public async Task<WorkTreatmentRequest> Instruction(WorkTreatmentKind kind) {
        var s = await Source(kind);
        return Request(s,kind == WorkTreatmentKind.EarlyInstruction ? At(8,30) : At(10),kind == WorkTreatmentKind.EarlyInstruction ? At(9) : At(11)) with {
            Intervals = [], DocumentIds = [Document], Instruction = new(Document,"Named dated written instruction",
                kind == WorkTreatmentKind.StudyInstruction ? TimeSpan.TicksPerHour : null,kind == WorkTreatmentKind.StudyInstruction ? Date.AddDays(7) : null)
        };
    }
    public async Task<T> Transaction<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct) {
        await gate.WaitAsync(ct);
        var counts = new[] { Rows.Count,Decisions.Count,Facts.Count,Audits.Count,Receipts.Count,Notifications.Count,Cases.Count };
        var states = Rows.Select(x => (Row:x,State:x.State,Closed:x.ClosedAt)).ToArray();
        InTransaction = true;
        try { return await action(ct); }
        catch {
            Rows.RemoveRange(counts[0],Rows.Count-counts[0]); Decisions.RemoveRange(counts[1],Decisions.Count-counts[1]); Facts.RemoveRange(counts[2],Facts.Count-counts[2]);
            Audits.RemoveRange(counts[3],Audits.Count-counts[3]); Receipts.RemoveRange(counts[4],Receipts.Count-counts[4]); Notifications.RemoveRange(counts[5],Notifications.Count-counts[5]); Cases.RemoveRange(counts[6],Cases.Count-counts[6]);
            foreach(var s in states) { typeof(ApprovalRequest).GetProperty("State")!.SetValue(s.Row,s.State); typeof(ApprovalRequest).GetProperty("ClosedAt")!.SetValue(s.Row,s.Closed); }
            throw;
        } finally { InTransaction = false; gate.Release(); }
    }
    private object? Append<T>(List<T> list,T value) { RequireTransaction(); list.Add(value); return null; }
    private object? RequireProducer() { RequireTransaction(); return null; }
    public void RequireTransaction() { if (!InTransaction) throw new Exception("Missing authorized transaction"); }
    public Task<IReadOnlyList<WorkTreatmentSession>> SessionsAsync(Guid id,DateOnly from,DateOnly to,CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<WorkTreatmentSession>>(NoSessions ? [] : [session with { Revision = SourceRevision }]);
    public Task<WorkTreatmentEmployment?> EmploymentAsync(Guid id,DateOnly date,CancellationToken ct) =>
        Task.FromResult<WorkTreatmentEmployment?>(new(Assignment,Assignment,"SITE",Site,null,Date,null));
    public Task<IReadOnlyList<AttendanceResourceScope>> ScopesAsync(Guid id,DateOnly from,DateOnly to,CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AttendanceResourceScope>>(from < Date && PreviousSite.HasValue ? [new([new(EmployeeId: id,SiteId: PreviousSite)])] :
            CurrentSite == Site ? [new([new(EmployeeId: id,SiteId: Site)])] :
            [new([new(EmployeeId: id,SiteId: Site)]),new([new(EmployeeId: id,SiteId: CurrentSite)])]);
    public Task<IReadOnlyList<WorkTreatmentDocument>> DocumentsAsync(Guid id,IReadOnlyList<Guid> ids,DateOnly from,DateOnly to,CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<WorkTreatmentDocument>>(ids.Select(x => new WorkTreatmentDocument(x,new string('D',64),1,Convert.ToBase64String(new byte[8]),At(6),"independent-document-verifier")).ToArray());
    public Task<bool> LockedAsync(DateOnly date,CancellationToken ct) => Task.FromResult(false);
    public Task<WorkTreatmentRow?> FindAsync(Guid id,CancellationToken ct) { var a=Rows.SingleOrDefault(x => x.SubjectId == id); return Task.FromResult(a is null ? null : new WorkTreatmentRow(a,Decisions.SingleOrDefault(x => x.ApprovalRequestId == a.Id))); }
    public Task<IReadOnlyList<AuditEvent>> FactsAsync(Guid employee,string operation,CancellationToken ct) => Task.FromResult<IReadOnlyList<AuditEvent>>(Facts.Where(x => x.EmployeeScopeId == employee && x.EventType == operation).ToArray());
    public Task<IReadOnlyList<WorkCreditRange>> ApprovedOvertimeAsync(Guid id,CancellationToken ct) => Task.FromResult(Ot);
    public Task<DisciplinaryCase?> ClosedCaseAsync(Guid employee,Guid approval,Guid id,CancellationToken ct) => Task.FromResult(Cases.SingleOrDefault(x => x.EmployeeId == employee && x.ApprovalRequestId == approval && x.Id == id));
    public void Add(ApprovalRequest a) { RequireTransaction(); typeof(ApprovalRequest).GetProperty("RowVersion")!.SetValue(a,BitConverter.GetBytes(1L)); Rows.Add(a); }
    public void Add(ApprovalDecision d) { RequireTransaction(); Decisions.Add(d); }
    public void Add(AuditEvent a) { RequireTransaction(); Facts.Add(a); }
    public void Add(DisciplinaryCase c) { RequireTransaction(); Cases.Add(c); }
    public void ExpectVersion(ApprovalRequest a,byte[] version) { if (!a.RowVersion.SequenceEqual(version)) throw AttendanceOperation.Conflict("work_treatment_version_conflict"); }
    public Task SaveAsync(CancellationToken ct) { RequireTransaction(); return Task.CompletedTask; }
    public Task<PrivateFileReference> WriteAsync(byte[] bytes,string purpose,CancellationToken ct) {
        var id=Guid.NewGuid().ToString("N"); files[id]=(bytes.ToArray(),purpose); var callback=AfterPrivateWrite; AfterPrivateWrite=null; callback?.Invoke();
        return Task.FromResult(new PrivateFileReference(id,"1",Convert.ToHexString(SHA256.HashData(bytes)),"in-memory-key",bytes.Length));
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference,string purpose,CancellationToken ct) {
        var data=files[reference.StorageKey]; if (data.Purpose != purpose || reference.Sha256 != Convert.ToHexString(SHA256.HashData(data.Data))) throw new Exception("Private binding mismatch");
        return Task.FromResult(data.Data.ToArray());
    }
}
