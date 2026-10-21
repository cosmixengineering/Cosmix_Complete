#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyOvertimeService.packages.lock.json
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Application.HR.Compensation;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Compensation;

// Real OT service/source/authorization/receipt logic with synthetic repositories and policy proof.
// No SQL, real private storage, organizational grant or financial acceptance is exercised.
var checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
async Task Fails(Func<Task> action, string code)
{ checks++; try { await action(); } catch (AuthenticationFailure e) when (e.Code == code) { return; } throw new Exception("Expected " + code); }
var f = new OtFixture(); var proposal = await f.Proposal();
var submitted = await f.Submit(proposal);
Check(submitted.State == "Pending" && submitted.ApprovedMinutes is null && submitted.PayableAmount is null, "Submission never awards minutes or money");
Check(f.Rows.Count == 1 && f.Receipts.Count == 1 && f.Notifications.Count == 0, "Submission records real request/receipt without invented reviewer notification");
Check((await f.Submit(proposal)).Id == submitted.Id && f.Rows.Count == 1, "Exact replay returns same immutable request");
var pendingFinancial = await f.Service.FinancialAsync(f.Employee, submitted.Id, false, f.Context, default);
Check(pendingFinancial.State == "not_awarded" && pendingFinancial.Amount is null
    && !JsonSerializer.SerializeToElement(pendingFinancial).TryGetProperty("Award", out _),
    "Pending requests have no Finance award or fabricated zero amount");
await Fails(() => f.Submit(proposal with { Reason = "Changed claim" }), "idempotency_key_reused");
var detail = await f.Service.DetailAsync(f.Employee, submitted.Id, false, true, f.Context, default);
Check(detail.WrittenEvidence == proposal.WrittenEvidence && detail.Source.Sessions.Count == 1, "Requester sees protected evidence and retained full source");
await Fails(() => f.Decide(submitted, ApprovalState.Approved), "overtime_self_decision_forbidden");
f.Actor = f.Actor with { AccountId = Guid.NewGuid(), EmployeeId = f.Employee };
await Fails(() => f.Decide(submitted, ApprovalState.Approved), "overtime_self_decision_forbidden");
f.Reviewer(); var approved = await f.Decide(submitted, ApprovalState.Approved);
Check(approved.State == "Approved" && approved.ApprovedMinutes == 120 && approved.PayableAmount is null, "Synthetic configured duration approves work minutes without guessing rates");
Check(approved.DecisionId.HasValue && f.Notifications.Single().EventId == approved.DecisionId, "Notification uses actual immutable decision ID");
Check((await f.Decide(submitted, ApprovalState.Approved)).DecisionId == approved.DecisionId && f.Notifications.Count == 1, "Decision replay does not duplicate award or notification");
Check((await f.Service.DetailAsync(f.Employee, submitted.Id, false, false, f.Context, default)).Request.ApprovedMinutes == 120, "Approved history verifies protected award provenance");
var financial = await f.Service.FinancialAsync(f.Employee, submitted.Id, false, f.Context, default);
Check(financial.Amount == 717 && financial.Currency == "PKR", "Separate authorized financial read uses retained base, not gross, and actual month days");
var lockDependencies = f.LockDependencies();
Task<IReadOnlyList<MonthLockOvertimeEntry>> ReadLockOutsideTransaction() => ((IMonthLockOvertimeReader)f.Service).ReadAsync(
    new(2026,10,1),new(2026,11,1),lockDependencies,new(f.Actor,"Configured-HR",f.Clock.GetUtcNow(),null),default);
async Task<IReadOnlyList<MonthLockOvertimeEntry>> ReadLock()
{
    // MonthLockService normally supplies the surrounding AttendanceOperation transaction.
    var prior = f.InTransaction; f.InTransaction = true;
    try { return await ReadLockOutsideTransaction(); }
    finally { f.InTransaction = prior; }
}
try
{
    await ReadLockOutsideTransaction();
    throw new Exception("Month-lock OT must not bypass recovery allocation validation outside a transaction");
}
catch (Exception error) when (error.Message == "Recovery allocations require the authorized transaction")
{
    Check(!f.InTransaction, "direct month-lock OT read rejects recovery allocation access outside a transaction");
}
var lockedAwards = await ReadLock();
Check(lockedAwards.Count==1 && lockedAwards[0].Award.AwardFingerprint==financial.Award!.AwardFingerprint,
    "month reader reuses actual protected award validation and retains the exact approved award");
MonthLockEvidence.RequireOvertimeDependencies(lockedAwards,lockDependencies);
var lockSummaries=new[] {new MonthLockSummaryEntry(f.Employee,f.Date,Guid.NewGuid(),new('A',64),f.Policy,"synthetic-summary")};
MonthLockEvidence.ValidateOvertimeMembership(new(2026,10,1),new(2026,11,1),lockSummaries,lockedAwards);
await Fails(()=>Task.Run(()=>MonthLockEvidence.ValidateOvertimeMembership(new(2026,9,1),new(2026,10,1),
    lockSummaries,lockedAwards)),"month_lock_overtime_contract_invalid");
await Fails(()=>Task.Run(()=>MonthLockEvidence.ValidateOvertimeMembership(new(2026,10,1),new(2026,11,1),
    lockSummaries,[lockedAwards[0],lockedAwards[0]])),"month_lock_overtime_contract_invalid");
await Fails(()=>Task.Run(()=>MonthLockEvidence.RequireOvertimeDependencies([],lockDependencies)),"month_lock_overtime_membership_conflict");
await Fails(()=>Task.Run(()=>MonthLockEvidence.RequireOvertimeDependencies(lockedAwards,[])),"month_lock_overtime_membership_conflict");
var changedLockedAward=lockedAwards[0] with {Award=lockedAwards[0].Award with {
    FinancialEvidence=lockedAwards[0].Award.FinancialEvidence with {Amount=718}}};
await Fails(()=>Task.Run(()=>MonthLockEvidence.RequireOvertimeMatch(lockedAwards,[changedLockedAward])),"month_lock_overtime_membership_conflict");
await Fails(()=>Task.Run(()=>MonthLockEvidence.RequireOvertimeMatch(lockedAwards,[])),"month_lock_overtime_membership_conflict");
var financialGrants=f.Grants.Where(x=>x.Permission==OvertimePermissions.FinancialRead).ToArray();
f.Grants.RemoveAll(x=>x.Permission==OvertimePermissions.FinancialRead);
await Fails(()=>ReadLock(),"access_denied");
f.Grants.AddRange(financialGrants);
typeof(WorkSession).GetProperty(nameof(WorkSession.Revision))!.SetValue(lockDependencies[0].Session,2);
await Fails(()=>ReadLock(),"month_lock_overtime_source_changed");
typeof(WorkSession).GetProperty(nameof(WorkSession.Revision))!.SetValue(lockDependencies[0].Session,1);
Check((await ReadLock())[0].Award.FinancialEvidence.Amount==717,"unchanged retained source remains readable after denied changes");
Check(!f.InTransaction, "month-lock reader fixture restores transaction state after successful and denied reads");
var nonNine = new OtFixture(); nonNine.ScheduleEnd = nonNine.Start.AddHours(8);
var nonNineRequest = await nonNine.Submit(await nonNine.Proposal()); nonNine.Reviewer();
await Fails(() => nonNine.Decide(nonNineRequest, ApprovalState.Approved), "overtime_non_nine_hour_schedule_unavailable");
Check(nonNine.Rows.Single().Approval.State == ApprovalState.Pending, "An alternate evaluated helper cannot price an eight-hour schedule with the nine-hour formula");
foreach (var rate in new string?[] { null, "UNAPPROVED-OTHER-RATE" })
{
    var unsupported = new OtFixture { RateRuleCode = rate };
    var unsupportedRequest = await unsupported.Submit(await unsupported.Proposal()); unsupported.Reviewer();
    await Fails(() => unsupported.Decide(unsupportedRequest, ApprovalState.Approved), "overtime_rate_policy_unavailable");
    Check(unsupported.Rows.Single().Approval.State == ApprovalState.Pending, "Missing or foreign rate contract remains pending without a guessed monetary award");
}
foreach (var role in new[] { WorkCreditRole.EarlyCredit, WorkCreditRole.MakeUp, WorkCreditRole.NormalWorkTravel,
    WorkCreditRole.OrdinaryPay, WorkCreditRole.LeaveRecovery })
{
    var allocated = new OtFixture(workCredits: true);
    var allocatedRequest = await allocated.Submit(await allocated.Proposal()); allocated.Reviewer();
    allocated.WorkCredits.Add(allocated.Credit(role, allocated.ScheduleEnd.AddTicks(1), allocated.ScheduleEnd.AddTicks(2)));
    var moneyReads = allocated.CompensationWindowReads;
    await Fails(() => allocated.Decide(allocatedRequest, ApprovalState.Approved), "overtime_work_credit_overlap");
    Check(allocated.Rows.Single().Approval.State == ApprovalState.Pending && allocated.Notifications.Count == 0
        && allocated.CompensationWindowReads == moneyReads + 1,
        "Physical " + role + " allocation on a different session prevents OT approval before amount calculation");
    Check(allocated.WorkReads.All(x => x == (allocated.Employee, allocated.Date, allocated.Date)),
        "OT reads real employee/business-date credit proof inside the existing transaction");
}
var gapClaim = new OtFixture(workCredits: true);
gapClaim.EligibleOverride = [new(gapClaim.ScheduleEnd, gapClaim.ScheduleEnd.AddHours(1)),
    new(gapClaim.ScheduleEnd.AddMinutes(90), gapClaim.End)];
var gapRequest = await gapClaim.Submit(await gapClaim.Proposal()); gapClaim.Reviewer();
gapClaim.WorkCredits.Add(gapClaim.Credit(WorkCreditRole.MakeUp, gapClaim.ScheduleEnd.AddMinutes(70), gapClaim.ScheduleEnd.AddMinutes(80)));
await Fails(() => gapClaim.Decide(gapRequest, ApprovalState.Approved), "overtime_work_credit_overlap");
Check(gapClaim.Rows.Single().Approval.State == ApprovalState.Pending,
    "A reserved gap in the full requested span is rejected even when actual approved monetary intervals exclude it");
var privateRace = new OtFixture(workCredits: true);
var privateRequest = await privateRace.Submit(await privateRace.Proposal()); privateRace.Reviewer();
privateRace.AfterPrivateWrite = purpose => {
    if (purpose.EndsWith(":decision", StringComparison.Ordinal))
        privateRace.WorkCredits.Add(privateRace.Credit(WorkCreditRole.LeaveRecovery, privateRace.ScheduleEnd.AddMinutes(1), privateRace.End));
};
await Fails(() => privateRace.Decide(privateRequest, ApprovalState.Approved), "overtime_work_credit_overlap");
Check(privateRace.Rows.Single().Approval.State == ApprovalState.Pending && privateRace.Notifications.Count == 0
    && privateRace.Receipts.Count == 1, "After-private-IO allocation recheck runs before decision/notification/success receipt staging");
var privatePending = await privateRace.Service.FinancialAsync(privateRace.Employee, privateRequest.Id, false, privateRace.Context, default);
Check(privatePending.Amount is null && privatePending.State == "not_awarded",
    "Unbound private output from a denied approval does not become a financial award");
var previewClaim = new OtFixture(workCredits: true);
var previewProposal = await previewClaim.Proposal();
previewClaim.WorkCredits.Add(previewClaim.Credit(WorkCreditRole.NormalWorkTravel, previewClaim.ScheduleEnd, previewClaim.End));
await Fails(() => previewClaim.Proposal(), "overtime_work_credit_overlap");
await Fails(() => previewClaim.Submit(previewProposal), "overtime_work_credit_overlap");
Check(previewClaim.Rows.Count == 0 && previewClaim.Receipts.Count == 0, "Preview and submission cannot bypass approved physical allocations");
var replayClaim = new OtFixture(workCredits: true);
var replayProposal = await replayClaim.Proposal(); var replayRequest = await replayClaim.Submit(replayProposal);
replayClaim.WorkCredits.Add(replayClaim.Credit(WorkCreditRole.MakeUp, replayClaim.ScheduleEnd, replayClaim.End));
await Fails(() => replayClaim.Submit(replayProposal), "overtime_work_credit_overlap");
Check(replayClaim.Rows.Count == 1 && replayClaim.Receipts.Count == 1, "Submission replay rechecks work allocations without staging another request");
replayClaim.WorkCredits.Clear(); replayClaim.Reviewer(); var replayApproved = await replayClaim.Decide(replayRequest, ApprovalState.Approved);
var historicalFinance = await replayClaim.Service.FinancialAsync(replayClaim.Employee, replayRequest.Id, false, replayClaim.Context, default);
replayClaim.WorkCredits.Add(replayClaim.Credit(WorkCreditRole.EarlyCredit, replayClaim.ScheduleEnd, replayClaim.End));
await Fails(() => replayClaim.Decide(replayRequest, ApprovalState.Approved), "overtime_work_credit_overlap");
Check((await replayClaim.Service.FinancialAsync(replayClaim.Employee, replayRequest.Id, false, replayClaim.Context, default)).Amount
    == historicalFinance.Amount && replayClaim.Notifications.Count == 1,
    "Replay conflict does not reprice or erase the actual immutable historical Finance award");
var legacyMetadata = new OtFixture(); var legacyProposal = await legacyMetadata.Proposal();
var workBatch = new OtFixture(workCredits: true); var workBatchProposal = await workBatch.Proposal();
var workBatchInput = new SubmitOvertimeBulkRequest([new(workBatch.Employee, workBatchProposal)], "Named allocation boundary");
var workBatchRows = await workBatch.Service.SubmitBulkAsync(workBatchInput, "work-bulk-submit-001", workBatch.Context, default);
workBatch.WorkCredits.Add(workBatch.Credit(WorkCreditRole.NormalWorkTravel, workBatch.ScheduleEnd, workBatch.End));
await Fails(() => workBatch.Service.SubmitBulkAsync(workBatchInput, "work-bulk-submit-001", workBatch.Context, default), "overtime_work_credit_overlap");
workBatch.WorkCredits.Clear(); workBatch.Reviewer(); var workBatchRow = workBatchRows.Employees.Single();
var workBatchDecision = new DecideOvertimeBulkRequest([new(workBatch.Employee, workBatchRow.Id,
    new(workBatchRow.Version, ApprovalState.Approved, "Named reviewed work"))], "Named reviewed allocation");
await workBatch.Service.DecideBulkAsync(workBatchDecision, "work-bulk-decide-001", workBatch.Context, default);
workBatch.WorkCredits.Add(workBatch.Credit(WorkCreditRole.MakeUp, workBatch.ScheduleEnd, workBatch.End));
await Fails(() => workBatch.Service.DecideBulkAsync(workBatchDecision, "work-bulk-decide-001", workBatch.Context, default), "overtime_work_credit_overlap");
Check(workBatch.Rows.Count == 1 && workBatch.Notifications.Count == 1,
    "Named bulk replay rechecks actual physical allocation without duplicating an award/notification");
legacyMetadata.Service = legacyMetadata.CreateService(legacyMetadata.CreditReader);
var withEmptyReader = await legacyMetadata.Proposal();
Check(withEmptyReader.ExpectedSourceFingerprint == legacyProposal.ExpectedSourceFingerprint,
    "Optional empty work reader does not alter legacy source fingerprint/schema metadata");
legacyMetadata.WorkCredits.Add(legacyMetadata.Credit(WorkCreditRole.MakeUp, legacyMetadata.ScheduleEnd.AddMinutes(-1), legacyMetadata.ScheduleEnd));
var boundaryRequest = await legacyMetadata.Submit(withEmptyReader); legacyMetadata.Reviewer();
Check((await legacyMetadata.Decide(boundaryRequest, ApprovalState.Approved)).ApprovedTicks == TimeSpan.FromHours(2).Ticks,
    "Exact adjacent work-credit boundary does not overlap or change legacy approved ticks");
var readerFailure = new OtFixture(workCredits: true); var readerFailureRequest = await readerFailure.Submit(await readerFailure.Proposal());
readerFailure.Reviewer(); readerFailure.FailWorkReader = true;
await Fails(() => readerFailure.Decide(readerFailureRequest, ApprovalState.Approved), "work_treatment_credit_source_changed");
Check(readerFailure.Rows.Single().Approval.State == ApprovalState.Pending, "Unavailable actual work proof is never silently treated as no allocation");
JsonElement Award(OvertimeFinancialResponse response)
{
    var json = JsonSerializer.SerializeToElement(response);
    Check(json.TryGetProperty("Award", out var award) && award.ValueKind == JsonValueKind.Object,
        "Authorized Finance read returns an immutable source-bound OT award");
    return award;
}
var retainedAward = Award(financial);
var retainedInputs = retainedAward.GetProperty("Inputs");
var retainedSource = retainedInputs.GetProperty("Source");
Check(retainedAward.GetProperty("RequestId").GetGuid() == submitted.Id
    && retainedAward.GetProperty("DecisionId").GetGuid() == approved.DecisionId
    && retainedAward.GetProperty("ApprovalRequestId").GetGuid() == f.Rows.Single().Approval.Id,
    "Finance award retains the actual request, approval and decision identities");
Check(retainedSource.GetProperty("SourceFingerprint").GetString() == detail.Source.SourceFingerprint
    && retainedInputs.GetProperty("CompensationRecordId").GetGuid() == f.Salary.Id
    && retainedSource.GetProperty("DurationPolicy").GetProperty("PolicyVersionId").GetGuid() == f.Policy
    && retainedSource.GetProperty("Eligibility").GetProperty("VersionId").GetGuid() == f.EligibilityId,
    "Finance award retains actual source, compensation, duration policy and eligibility IDs");
Check(retainedAward.GetProperty("SiteId").GetGuid() == f.Site
    && retainedAward.GetProperty("WorkSessionId").GetGuid() == proposal.WorkSessionId
    && retainedAward.GetProperty("JobReference").GetString() == "job-001",
    "Finance award retains the original job/site and selected session");
Check(retainedAward.GetProperty("Authorization").GetProperty("ReviewerAccountId").GetGuid() == f.Actor.AccountId
    && retainedAward.GetProperty("Authorization").GetProperty("ReviewerRoleCode").GetString() == "Configured-HR"
    && retainedAward.GetProperty("Authorization").GetProperty("ApprovedAtUtc").GetDateTimeOffset() == f.Clock.Now,
    "Finance award carries actual independent decision-time reviewer evidence");
var retainedFingerprint = retainedAward.GetProperty("AwardFingerprint").GetString();
Check(retainedFingerprint is { Length: 64 } && retainedFingerprint.All(Uri.IsHexDigit)
    && Award(await f.Service.FinancialAsync(f.Employee, submitted.Id, false, f.Context, default))
        .GetProperty("AwardFingerprint").GetString() == retainedFingerprint,
    "Repeated reads derive the same fingerprint from immutable approved evidence");
var awardType = typeof(OvertimeService).Assembly.GetType(typeof(OvertimeService).Namespace + ".OvertimeFinancialAward");
var awardRulesType = typeof(OvertimeService).Assembly.GetType(typeof(OvertimeService).Namespace + ".OvertimeFinancialAwardRules");
Check(awardType is not null && awardRulesType is not null, "Finance award fingerprint is produced by the actual OT helper");
var fingerprintMethod = awardRulesType!.GetMethod("Fingerprint", BindingFlags.Public | BindingFlags.Static)!;
string Fingerprint(JsonNode award) => (string)fingerprintMethod.Invoke(null,
    [JsonSerializer.Deserialize(award.ToJsonString(), awardType!)!])!;
Check(Fingerprint(JsonNode.Parse(retainedAward.GetRawText())!) == retainedFingerprint,
    "Actual helper reproduces the issued immutable award identity, excluding the digest field itself");
foreach (var path in new string[][] {
    ["RequestId"], ["DecisionId"], ["SiteId"], ["WorkSessionId"],
    ["Inputs", "CompensationRecordId"], ["Inputs", "Employment", "AssignmentId"],
    ["Inputs", "Source", "DurationPolicy", "PolicyVersionId"],
    ["Inputs", "Source", "Eligibility", "VersionId"], ["Authorization", "ReviewerAccountId"] })
{
    var changedAward = JsonNode.Parse(retainedAward.GetRawText())!;
    var target = changedAward;
    foreach (var segment in path[..^1]) target = target[segment]!;
    target[path[^1]] = JsonValue.Create(Guid.NewGuid());
    Check(Fingerprint(changedAward) != retainedFingerprint,
        "Changing retained " + string.Join(".", path) + " changes the actual award identity");
}
var changedSource = JsonNode.Parse(retainedAward.GetRawText())!;
changedSource["Inputs"]!["Source"]!["SourceFingerprint"] = new string('A', 64);
Check(Fingerprint(changedSource) != retainedFingerprint, "Award digest binds the actual source fingerprint");
var changedAmount = JsonNode.Parse(retainedAward.GetRawText())!;
changedAmount["FinancialEvidence"]!["Amount"] = 718m;
Check(Fingerprint(changedAmount) != retainedFingerprint, "Award digest binds the retained financial amount");
var changedReviewTime = JsonNode.Parse(retainedAward.GetRawText())!;
changedReviewTime["Authorization"]!["ApprovedAtUtc"] = JsonValue.Create(f.Clock.Now.AddTicks(1));
Check(Fingerprint(changedReviewTime) != retainedFingerprint, "Award digest binds actual decision-time authority");
Check(!JsonSerializer.Serialize(approved).Contains("Award") && !JsonSerializer.Serialize(detail).Contains("MonthlyBaseSalary"),
    "Ordinary approval/receipt/detail projections do not leak the protected financial award");
f.Grants.RemoveAll(x => x.Permission == OvertimePermissions.FinancialRead);
await Fails(() => f.Service.FinancialAsync(f.Employee, submitted.Id, false, f.Context, default), "access_denied");
Check((await f.Decide(submitted, ApprovalState.Approved)).PayableAmount is null, "Privileged financial read never changes ordinary receipt payload after grant revocation");
var altered = new OtFixture(); var alteredRequest = await altered.Submit(await altered.Proposal()); altered.Reviewer();
altered.Sessions[0] = altered.Sessions[0] with { Id = Guid.NewGuid(), Revision = 2, EndsAtUtc = altered.End.AddMinutes(1) };
await Fails(() => altered.Decide(alteredRequest, ApprovalState.Approved), "overtime_source_changed");
Check(altered.Rows.Single().Approval.State == ApprovalState.Pending, "Stale source leaves request pending before mutation");
var extra = new OtFixture(); var extraRequest = await extra.Submit(await extra.Proposal()); extra.Reviewer();
extra.Sessions.Add(extra.Sessions[0] with { Id = Guid.NewGuid(), SessionKey = Guid.NewGuid(), StartsAtUtc = extra.Start.AddDays(1), EndsAtUtc = null });
await Fails(() => extra.Decide(extraRequest, ApprovalState.Approved), "overtime_source_changed");
var unknown = new OtFixture { DurationReady = false }; var unknownRequest = await unknown.Submit(await unknown.Proposal()); unknown.Reviewer();
await Fails(() => unknown.Decide(unknownRequest, ApprovalState.Approved), "overtime_scheduled_duty_not_confirmed");
var returned = await unknown.Decide(unknownRequest, ApprovalState.Returned);
Check(returned.State == "Returned" && returned.ApprovedMinutes is null, "HR can return a real pending-policy request");
unknown.Actor = unknown.Requester; var resubmission = await unknown.Proposal();
var resubmitted = await unknown.Service.SubmitAsync(unknown.Employee, resubmission with { ResubmissionOfRequestId = returned.Id }, "overtime-resubmit-001", false, unknown.Context, default);
Check(resubmitted.Id != returned.Id && unknown.Rows.Count == 2, "Returned resubmission is a new immutable linked request");
var eligibility = new OtFixture { EligibilityReady = false }; var eligibilityRequest = await eligibility.Submit(await eligibility.Proposal()); eligibility.Reviewer();
await Fails(() => eligibility.Decide(eligibilityRequest, ApprovalState.Approved), "overtime_eligibility_not_configured");
var disabled = new OtFixture { EligibilityAllowed = false };
await Fails(async () => await disabled.Submit(await disabled.Proposal()), "overtime_not_eligible");
var office = new OtFixture { StaffCategory = "OFFICE" };
await Fails(async () => await office.Submit(await office.Proposal()), "overtime_office_ineligible");
var travel = new OtFixture(); var travelProposal = (await travel.Proposal()) with { Activity = OvertimeActivity.CompanyTravel };
var travelRequest = await travel.Submit(travelProposal); travel.Reviewer();
await Fails(() => travel.Decide(travelRequest, ApprovalState.Approved), "overtime_travel_requires_separate_policy");
var before = new OtFixture(); var beforeProposal = await before.Proposal();
await Fails(() => before.Submit(beforeProposal with { StartsAtUtc = before.ScheduleEnd.AddMinutes(-1) }), "overtime_before_scheduled_end");
var locked = new OtFixture { Locked = true };
await Fails(async () => await locked.Submit(await locked.Proposal()), "overtime_locked_period_adjustment_required");
var invalidDuty = new OtFixture(); invalidDuty.Sessions[0] = invalidDuty.Sessions[0] with { ValidDutyEvidence = false };
await Fails(async () => await invalidDuty.Submit(await invalidDuty.Proposal()), "overtime_closed_session_required");
var transfer = new OtFixture { AssignmentValid = false };
await Fails(async () => await transfer.Submit(await transfer.Proposal()), "overtime_site_transfer_conflict");
var mfa = new OtFixture(); mfa.Actor = mfa.Actor with { MfaSatisfied = false };
await Fails(() => mfa.Proposal(), "access_denied");
var mobile = new OtFixture(); mobile.Actor = mobile.Actor with { Client = SessionClient.Mobile };
await Fails(() => mobile.Proposal(), "access_denied");
var revoked = new OtFixture { SessionCurrent = false };
await Fails(() => revoked.Proposal(), "invalid_authentication");
var self = new OtFixture(); self.Actor = self.Actor with { EmployeeId = self.Employee, Client = SessionClient.Mobile, MfaSatisfied = false };
var ownSource = await self.Service.SourcesAsync(null, self.Date, true, self.Context, default);
Check(ownSource.EmployeeId == self.Actor.EmployeeId, "Mobile source identity is server-derived");
await Fails(() => self.Service.SourcesAsync(Guid.NewGuid(), self.Date, true, self.Context, default), "invalid_attendance_request");
var ownProposal = self.MakeProposal(ownSource.SourceFingerprint); var own = await self.Service.SubmitAsync(null, ownProposal, "self-request-001", true, self.Context, default);
Check((await self.Service.HistoryAsync(null, new(self.Date, self.Date.AddDays(1)), true, self.Context, default)).Items.Single().Id == own.Id, "Self history uses employee identity");
var supervisor = new OtFixture(); var supervisorRequest = await supervisor.Submit(await supervisor.Proposal());
supervisor.Actor = supervisor.Actor with { AccountId = Guid.NewGuid(), EmployeeId = supervisor.Employee, Client = SessionClient.Mobile, MfaSatisfied = false };
var employeeDetail = await supervisor.Service.DetailAsync(null, supervisorRequest.Id, true, true, supervisor.Context, default);
Check(employeeDetail.WrittenEvidence is null && employeeDetail.EvidenceVisibility == "requester_only", "Beneficiary record does not leak supervisor written evidence");
await Fails(() => supervisor.Service.CancelAsync(null, supervisorRequest.Id, new(supervisorRequest.Version, "Cancel claim"), "self-cancel-001", true, supervisor.Context, default), "attendance_resource_not_found");
var stale = new OtFixture(); var staleRequest = await stale.Submit(await stale.Proposal()); stale.Reviewer();
typeof(ApprovalRequest).GetProperty("RowVersion")!.SetValue(stale.Rows[0].Approval, BitConverter.GetBytes(55L));
await Fails(() => stale.Decide(staleRequest, ApprovalState.Rejected), "overtime_version_conflict");
var conflicts = new OtFixture(); var conflictProposal = await conflicts.Proposal(); await conflicts.Submit(conflictProposal);
await Fails(() => conflicts.Service.SubmitAsync(conflicts.Employee, conflictProposal, "other-request-001", false, conflicts.Context, default), "overtime_interval_already_requested");
var storage = new OtFixture(); var storageProposal = await storage.Proposal(); storage.FailStorage = true;
await Fails(() => storage.Submit(storageProposal), "synthetic_storage_unavailable");
Check(storage.Rows.Count == 0 && storage.Receipts.Count == 0, "Protected proposal failure occurs before request staging");
var notices = new OtFixture(); var noticeRequest = await notices.Submit(await notices.Proposal()); notices.Reviewer(); notices.FailNotification = true;
await Fails(() => notices.Decide(noticeRequest, ApprovalState.Rejected), "synthetic_outbox_unavailable");
Check(notices.Receipts.Count == 1, "Outbox failure creates no success receipt; SQL rollback remains separate acceptance");
var denied = new OtFixture(); var deniedProposal = await denied.Proposal(); var deniedRequest = await denied.Submit(deniedProposal);
denied.Grants.Clear(); await Fails(() => denied.Submit(deniedProposal), "access_denied");
var moved = new OtFixture(); moved.RestrictToTeam(moved.Team); var movedProposal = await moved.Proposal(); await moved.Submit(movedProposal);
var oldTeam = moved.Team; moved.Team = Guid.NewGuid(); moved.RestrictToTeam(moved.Team);
await Fails(() => moved.Submit(movedProposal), "access_denied");
moved.Grants.Add(new(OvertimePermissions.Submit, false, Guid.NewGuid(), AccessScopeKind.Team, oldTeam));
Check((await moved.Submit(movedProposal)).Id == moved.Rows[0].Request.Id, "Replay requires both original and current grouped authority");
var bulk = new OtFixture(); var bulkProposal = await bulk.Proposal();
await Fails(() => bulk.Service.SubmitBulkAsync(new([new(bulk.Employee, bulkProposal), new(bulk.Employee, bulkProposal)], "Named batch"), "bulk-request-001", bulk.Context, default), "invalid_overtime_named_batch");
Check(bulk.Rows.Count == 0, "Duplicate bulk beneficiary stages no rows");
var named = new OtFixture(); var namedProposal = await named.Proposal();
var namedSubmission = new SubmitOvertimeBulkRequest([new(named.Employee, namedProposal)], "Explicit one-employee named batch");
var namedBatch = await named.Service.SubmitBulkAsync(namedSubmission, "named-submit-001", named.Context, default);
Check(namedBatch.Employees.Single().EmployeeId == named.Employee && named.Rows.Count == 1,
    "Named bulk submission affects exactly its explicit beneficiary");
Check((await named.Service.SubmitBulkAsync(namedSubmission, "named-submit-001", named.Context, default)).BulkId == namedBatch.BulkId
    && named.Rows.Count == 1, "Named bulk retry retains its immutable batch and stages no additional employee");
named.Reviewer(); var namedRequest = namedBatch.Employees.Single();
var namedDecision = new DecideOvertimeBulkRequest([new(named.Employee, namedRequest.Id,
    new(namedRequest.Version, ApprovalState.Approved, "Reviewed named work"))], "Explicit named approval");
var namedApproved = await named.Service.DecideBulkAsync(namedDecision, "named-decide-001", named.Context, default);
Check(namedApproved.Employees.Single().ApprovedTicks == TimeSpan.FromHours(2).Ticks && named.Rows.Count == 1
    && named.Notifications.Count == 1, "Named bulk approval awards only the explicit employee once");
Check((await named.Service.DecideBulkAsync(namedDecision, "named-decide-001", named.Context, default)).Employees.Single().DecisionId
    == namedApproved.Employees.Single().DecisionId && named.Notifications.Count == 1,
    "Named bulk decision replay preserves the original decision and notification");
named.Grants.RemoveAll(x => x.Permission == OvertimePermissions.Decide);
await Fails(() => named.Service.DecideBulkAsync(namedDecision, "named-decide-001", named.Context, default), "access_denied");
Check(named.Rows.Count == 1 && named.Notifications.Count == 1, "Revoked batch review authority cannot mutate the retained award");
var frozen = new OtFixture(); var frozenRequest = await frozen.Submit(await frozen.Proposal()); frozen.Reviewer();
await frozen.Decide(frozenRequest, ApprovalState.Approved);
var beforeChange = Award(await frozen.Service.FinancialAsync(frozen.Employee, frozenRequest.Id, false, frozen.Context, default));
frozen.Salary = new CompensationRecord(frozen.Employee, 2, frozen.Salary.Id, 200000, 250000, "PKR", WageBasis.Monthly,
    PaymentMethod.Bank, frozen.Date.AddDays(1), null, null, "synthetic-later-salary", frozen.End.AddDays(1));
frozen.Sessions[0] = frozen.Sessions[0] with { Id = Guid.NewGuid(), Revision = 2, EndsAtUtc = frozen.End.AddHours(1) };
var afterChange = Award(await frozen.Service.FinancialAsync(frozen.Employee, frozenRequest.Id, false, frozen.Context, default));
Check(afterChange.GetProperty("AwardFingerprint").GetString() == beforeChange.GetProperty("AwardFingerprint").GetString()
    && afterChange.GetProperty("FinancialEvidence").GetProperty("Amount").GetDecimal() == 717m,
    "Historical award retains its original source and compensation without repricing later revisions");
frozen.Actor = frozen.Actor with { EmployeeId = frozen.Employee, Client = SessionClient.Mobile, MfaSatisfied = false };
var selfFinancial = await frozen.Service.FinancialAsync(null, frozenRequest.Id, true, frozen.Context, default);
Check(selfFinancial.Amount == 717m && !JsonSerializer.SerializeToElement(selfFinancial).TryGetProperty("Award", out _),
    "Employee financial self read keeps its existing amount without exposing private reviewer/source evidence");
var nextMonth = new OtFixture(); var nextMonthRequest = await nextMonth.Submit(await nextMonth.Proposal());
nextMonth.Reviewer(); nextMonth.Clock.Now = new(2026, 11, 1, 16, 0, 0, TimeSpan.Zero);
await nextMonth.Decide(nextMonthRequest, ApprovalState.Approved);
Check((await nextMonth.Service.FinancialAsync(nextMonth.Employee, nextMonthRequest.Id, false, nextMonth.Context, default)).Amount == 717m,
    "November approval retains the October source-month 31-day divisor");
var settings = new OtFixture(true); var settingDate = settings.Date;
var emptySetting = await settings.EligibilityService.PreviewAsync(null, settingDate, settings.Context, default);
Check(emptySetting.State == "not_configured" && emptySetting.LatestRevision == 0, "No automatic common OT grant");
var commonOn = new SetOvertimeEligibilityRequest(settingDate, true, emptySetting.LatestRevision, emptySetting.TimelineFingerprint, "Allow outside staff OT");
settings.FixedSettingsAuthority = false;
await Fails(() => settings.EligibilityService.SetAsync(null, commonOn, "denied-global-001", settings.Context, default), "access_denied");
Check(settings.EligibilityRows.Count == 0, "Delegated permission without a current fixed authority stages no eligibility setting");
settings.FixedSettingsAuthority = true;
var enabled = await settings.EligibilityService.SetAsync(null, commonOn, "global-enable-001", settings.Context, default);
Check(enabled.Effect == "global_allow", "Explicit common switch permits outside staff");
Check((await settings.EligibilityService.SetAsync(null, commonOn, "global-enable-001", settings.Context, default)).Id == enabled.Id && settings.EligibilityRows.Count == 1, "Eligibility exact replay is single append");
settings.FixedSettingsAuthority = false;
await Fails(() => settings.EligibilityService.SetAsync(null, commonOn, "global-enable-001", settings.Context, default), "access_denied");
settings.FixedSettingsAuthority = true;
await Fails(() => settings.EligibilityService.SetAsync(null, commonOn with { IsEnabled = false }, "global-enable-001", settings.Context, default), "idempotency_key_reused");
var employeePreview = await settings.EligibilityService.PreviewAsync(settings.Employee, settingDate, settings.Context, default);
Check(employeePreview.Eligibility?.Allowed == true && employeePreview.Eligibility.GlobalVersionId == enabled.Id, "Unrestricted outside employee inherits dated global ON");
var personalOff = new SetOvertimeEligibilityRequest(settingDate, false, employeePreview.LatestRevision, employeePreview.TimelineFingerprint, "HR disables this employee OT");
await settings.EligibilityService.SetAsync(settings.Employee, personalOff, "employee-off-001", settings.Context, default);
employeePreview = await settings.EligibilityService.PreviewAsync(settings.Employee, settingDate, settings.Context, default);
Check(employeePreview.Eligibility?.Allowed == false && employeePreview.Effective?.Effect == "employee_deny", "Profile OFF overrides common ON");
await settings.EligibilityService.SetAsync(settings.Employee, new(settingDate, true, employeePreview.LatestRevision, employeePreview.TimelineFingerprint, "Restore inheritance"), "employee-inherit-001", settings.Context, default);
Check((await settings.EligibilityService.PreviewAsync(settings.Employee, settingDate, settings.Context, default)).Eligibility?.Allowed == true, "Profile ON restores inheritance");
var commonPreview = await settings.EligibilityService.PreviewAsync(null, settingDate, settings.Context, default);
await settings.EligibilityService.SetAsync(null, new(settingDate, false, commonPreview.LatestRevision, commonPreview.TimelineFingerprint, "Suspend common OT"), "global-off-001", settings.Context, default);
Check((await settings.EligibilityService.PreviewAsync(settings.Employee, settingDate, settings.Context, default)).Eligibility?.Allowed == false, "Employee ON cannot bypass global OFF");
await Fails(() => settings.EligibilityService.SetAsync(null, commonOn, "global-stale-001", settings.Context, default), "overtime_eligibility_version_conflict");
await Fails(() => settings.EligibilityService.SetAsync(null, commonOn with { EffectiveFrom = settingDate.AddDays(-1) }, "global-past-001", settings.Context, default), "overtime_eligibility_past_date");
commonPreview = await settings.EligibilityService.PreviewAsync(null, settingDate, settings.Context, default);
await settings.EligibilityService.SetAsync(null, new(settingDate.AddDays(1), true, commonPreview.LatestRevision, commonPreview.TimelineFingerprint, "Future common OT"), "global-future-001", settings.Context, default);
Check((await settings.EligibilityService.PreviewAsync(settings.Employee, settingDate, settings.Context, default)).Eligibility?.Allowed == false &&
    (await settings.EligibilityService.PreviewAsync(settings.Employee, settingDate.AddDays(1), settings.Context, default)).Eligibility?.Allowed == true, "Future changes preserve earlier day resolution");
settings.StaffCategory = "OFFICE";
Check((await settings.EligibilityService.PreviewAsync(settings.Employee, settingDate.AddDays(1), settings.Context, default)).Eligibility?.Allowed == false, "Office remains ineligible despite common and employee ON");
await Fails(() => settings.EligibilityService.SetAsync(settings.Employee, personalOff, "office-restrict-001", settings.Context, default), "overtime_site_staff_required");
settings.Locked = true;
await Fails(() => settings.EligibilityService.SetAsync(null, commonOn, "locked-setting-001", settings.Context, default), "attendance_period_locked");
settings.Locked = false; settings.RestrictToTeam(settings.Team);
await Fails(() => settings.EligibilityService.PreviewAsync(null, settingDate, settings.Context, default), "access_denied");
await Fails(() => settings.EligibilityService.SetAsync(null, commonOn, "global-enable-001", settings.Context, default), "access_denied");
var actual = new OtFixture(true);
var actualPreview = await actual.EligibilityService.PreviewAsync(null, actual.Date, actual.Context, default);
await actual.EligibilityService.SetAsync(null, new(actual.Date, true, 0, actualPreview.TimelineFingerprint, "Real rules fixture"), "actual-global-001", actual.Context, default);
var actualRequest = await actual.Submit(await actual.Proposal()); actual.Reviewer();
var actualApproved = await actual.Decide(actualRequest, ApprovalState.Approved);
Check(actualApproved.ApprovedTicks == TimeSpan.FromHours(2).Ticks && actualApproved.PayableAmount is null, "Real eligibility and confirmed nine-hour evaluator approve two elapsed extra hours");
Check((await actual.Service.FinancialAsync(actual.Employee, actualRequest.Id, false, actual.Context, default)).Amount == 717, "Real confirmed formula returns restricted base-salary amount");
var policyFlip = new OtFixture(true); var flipPreview = await policyFlip.EligibilityService.PreviewAsync(null, policyFlip.Date, policyFlip.Context, default);
await policyFlip.EligibilityService.SetAsync(null, new(policyFlip.Date, true, 0, flipPreview.TimelineFingerprint, "Enable"), "flip-global-001", policyFlip.Context, default);
var flipRequest = await policyFlip.Submit(await policyFlip.Proposal());
var flipEmployee = await policyFlip.EligibilityService.PreviewAsync(policyFlip.Employee, policyFlip.Date, policyFlip.Context, default);
await policyFlip.EligibilityService.SetAsync(policyFlip.Employee, new(policyFlip.Date, false, 0, flipEmployee.TimelineFingerprint, "Disable before approval"), "flip-employee-001", policyFlip.Context, default);
policyFlip.Reviewer();
await Fails(() => policyFlip.Decide(flipRequest, ApprovalState.Approved), "overtime_source_changed");
Check(policyFlip.Rows.Single().Approval.State == ApprovalState.Pending, "Changed eligibility cannot award stale OT");
Console.WriteLine($"PASS overtime service checks: {checks}; synthetic IO/policy only, no SQL/payment acceptance.");

public sealed class OtClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 10, 16, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public class OtProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, OtProxy>(); ((OtProxy)(object)proxy).Handler = handler; return proxy; }
}
public sealed class OtFixture
{
    public IReadOnlyList<MonthLockOvertimeDependency> LockDependencies() => Rows.Select(row => {
        var source=Sessions.Single(x=>x.Id==row.Request.WorkSessionId);
        // A persisted header projection for the synthetic source, not a punch/correction write.
        var session=(WorkSession)Activator.CreateInstance(typeof(WorkSession),nonPublic:true)!;
        foreach(var property in typeof(OvertimeSessionSource).GetProperties())
            typeof(WorkSession).GetProperty(property.Name)?.SetValue(session,property.GetValue(source));
        typeof(WorkSession).GetProperty(nameof(WorkSession.EmployeeId))!.SetValue(session,Employee);
        typeof(WorkSession).GetProperty(nameof(WorkSession.AttendanceDate))!.SetValue(session,Date);
        return new MonthLockOvertimeDependency(row.Request,session,row.Approval,row.Decision);
    }).ToArray();
    public Guid Employee = Guid.NewGuid(), Site = Guid.NewGuid(), Team = Guid.NewGuid(), Policy = Guid.NewGuid(), EligibilityId = Guid.NewGuid();
    public DateOnly Date = new(2026, 10, 9);
    public DateTimeOffset Start = new(2026, 10, 9, 4, 0, 0, TimeSpan.Zero), ScheduleEnd = new(2026, 10, 9, 13, 0, 0, TimeSpan.Zero), End = new(2026, 10, 9, 15, 0, 0, TimeSpan.Zero);
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true), Requester;
    public bool DurationReady = true, EligibilityReady = true, EligibilityAllowed = true, Locked, AssignmentValid = true, SessionCurrent = true, FailStorage, FailNotification, FixedSettingsAuthority = true;
    public string StaffCategory = "SITE";
    public List<OvertimeSessionSource> Sessions = []; public List<OvertimeRecord> Rows = []; public List<PermissionGrant> Grants = [];
    public List<IdempotencyReceipt> Receipts = []; public List<NotificationIntent> Notifications = [];
    public Dictionary<string, (string Purpose, byte[] Data)> Files = []; private readonly Dictionary<Guid, ApprovalState> saved = [];
    public OvertimeService Service; public AuthRequestContext Context = new("127.0.0.1", "synthetic-overtime");
    public OvertimeEligibilityService EligibilityService;
    public List<OvertimeEligibilityVersion> EligibilityRows = [];
    public OtClock Clock = new();
    public CompensationRecord Salary = null!;
    public string? RateRuleCode = OvertimeFormula.Version;
    public List<WorkTreatmentCreditEvidence> WorkCredits = [];
    public List<(Guid Employee, DateOnly From, DateOnly To)> WorkReads = [];
    public IWorkTreatmentCreditReader CreditReader = null!;
    public Func<IWorkTreatmentCreditReader?, OvertimeService> CreateService = null!;
    public IReadOnlyList<OvertimeEligibleInterval>? EligibleOverride;
    public Action<string>? AfterPrivateWrite;
    public bool InTransaction, FailWorkReader;
    public int CompensationWindowReads;
    public OtFixture(bool realRules = false, bool workCredits = false)
    {
        if (realRules) { Date = Date.AddDays(1); Start = Start.AddDays(1); ScheduleEnd = ScheduleEnd.AddDays(1); End = End.AddDays(1); }
        Requester = Actor; var shift = Guid.NewGuid();
        Sessions.Add(new(Guid.NewGuid(), Guid.NewGuid(), 1, Start, End, Site, shift, null, Guid.NewGuid(), Guid.NewGuid(), null, true));
        foreach (var permission in OvertimePermissions.Definitions) Grants.Add(new(permission.Code, permission.Sensitive, Guid.NewGuid(), AccessScopeKind.All, null));
        var current = OtProxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = OtProxy.Make<IAccountAccessReader>((m, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var authz = OtProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => Transaction((Delegate)a[0]!, a[1]),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>(FixedSettingsAuthority && Grants.Any(x =>
                x.RoleAssignmentId == (Guid)a[0]! && x.Permission == OvertimePermissions.EligibilityManage) ? "ADMIN" : "Configured-HR"),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(FixedSettingsAuthority && (Guid)a[0]! == Actor.AccountId &&
                Grants.Any(x => x.RoleAssignmentId == (Guid)a[1]! && x.Permission == OvertimePermissions.EligibilityManage)),
            _ => throw new Exception(m.Name) });
        var auth = OtProxy.Make<IAuthenticationRepository>((m, a) => throw new Exception(m.Name));
        var journal = OtProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(SessionCurrent), "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => AddReceipt((IdempotencyReceipt)a[0]!), "AddAudit" => null, "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception(m.Name) });
        var files = OtProxy.Make<IPrivateFileStore>((m, a) => {
            if (m.Name == "WriteAsync") { if (FailStorage) throw new AuthenticationFailure("synthetic_storage_unavailable", 503); var bytes = (byte[])a[0]!; var id = Guid.NewGuid().ToString("N"); Files[id] = ((string)a[1]!, bytes); AfterPrivateWrite?.Invoke((string)a[1]!); return Task.FromResult(new PrivateFileReference(id, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length)); }
            var value = Files[((PrivateFileReference)a[0]!).StorageKey]; if (value.Purpose != (string)a[1]!) throw new Exception("Wrong evidence purpose"); return Task.FromResult(value.Data); });
        var operation = new AttendanceOperation(current, access, authz, journal, auth, Clock, files);
        var assignment = Guid.NewGuid(); var category = Guid.NewGuid();
        var repo = OtProxy.Make<IOvertimeRepository>((m, a) => m.Name switch {
            "EmploymentAsync" => Task.FromResult<OvertimeEmploymentSource?>(new(assignment, category, StaffCategory, Site, Date.AddDays(-30), null)),
            "SessionSourcesAsync" => Task.FromResult<IReadOnlyList<OvertimeSessionSource>>(Sessions.ToArray()), "ApprovedPolicyAsync" => Task.FromResult(true),
            "FindAsync" => Task.FromResult(Rows.SingleOrDefault(x => x.Request.EmployeeId == (Guid)a[0]! && x.Request.Id == (Guid)a[1]!)),
            "CandidatesAsync" => Candidates((OvertimeQueueRequest)a[0]!, (AttendanceCursorPosition?)a[1], (int)a[2]!, (Guid?)a[3]),
            "HasConflictingRequestAsync" => Task.FromResult(Rows.Any(x => x.Request.EmployeeId == (Guid)a[0]! && x.Request.Id != (Guid?)a[3] && x.Request.StartsAtUtc < (DateTimeOffset)a[2]! && x.Request.EndsAtUtc > (DateTimeOffset)a[1]! && x.Approval.State is ApprovalState.Pending or ApprovalState.Approved)),
            "Add" => a.Length == 2 ? Add((OvertimeRequest)a[0]!, (ApprovalRequest)a[1]!) : AddDecision((ApprovalDecision)a[0]!),
            "ExpectVersion" => null, "SaveChangesAsync" => Save(), _ => throw new Exception(m.Name) });
        var summary = OtProxy.Make<IAttendanceSummaryRepository>((m, a) => m.Name switch {
            "ResourceScopesAsync" => Task.FromResult<IReadOnlyList<AttendanceResourceScope>>([new([new(EmployeeId: (Guid)a[0]!, SiteId: Site, TeamId: Team)])]),
            "LatestAsync" => Task.FromResult<Cosmix.Domain.Attendance.DailySummaries.DailyAttendanceSummary?>(null), "IsLockedAsync" => Task.FromResult(Locked), _ => throw new Exception(m.Name) });
        var rosters = OtProxy.Make<IIndividualRosterResolver>((m, a) => Task.FromResult(new EffectiveRosterResponse((Guid)a[0]!, (DateOnly)a[1]!, "scheduled", RosterSchedule.TimeZoneId,
            Site, assignment, Guid.Empty, shift, 1, Start, ScheduleEnd, 540, 0, 0)));
        var eligibilityRepository = OtProxy.Make<IOvertimeEligibilityRepository>((m, a) => m.Name switch {
            "TimelineAsync" => Task.FromResult<IReadOnlyList<OvertimeEligibilityVersion>>(EligibilityRows.Where(x => x.Target == (OvertimeEligibilityTarget)a[0]! && x.EmployeeId == (Guid?)a[1]).OrderBy(x => x.Revision).ToArray()),
            "Add" => AddEligibility((OvertimeEligibilityVersion)a[0]!), "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception(m.Name) });
        EligibilityService = new(operation, eligibilityRepository, repo, summary, files);
        var eligibility = OtProxy.Make<IOvertimeEligibilityReader>((m, a) => Task.FromResult(new OvertimeEligibility(EligibilityReady ? "configured" : "eligibility_not_configured", EligibilityReady ? EligibilityId : null, EligibilityReady ? EligibilityAllowed : null)));
        var policy = OtProxy.Make<IOvertimeDurationPolicy>((m, a) => Task.FromResult(DurationReady ? new OvertimeDurationResult("evaluated", Policy, "synthetic-confirmed-duration-v1", true, EligibleOverride ?? [new(ScheduleEnd, End)], RateRuleCode) : new OvertimeDurationResult("duration_policy_unavailable", null, null, null, [])));
        Salary = new CompensationRecord(Employee, 1, null, 100000, 120000, "PKR", WageBasis.Monthly, PaymentMethod.Bank, Date.AddDays(-30), null, null, "synthetic-approved-hiring", Start.AddDays(-30));
        var compensation = OtProxy.Make<IEmployeeCompensationRepository>((m, a) => m.Name switch { "HasConflictingHistoryAsync" => Task.FromResult(false), "WindowAsync" => ReadSalary(), _ => throw new Exception(m.Name) });
        var employment = OtProxy.Make<ICorrectionReviewRepository>((m, a) => m.Name switch { "HistoricallyEligibleAsync" => Task.FromResult(true), "PreservesAssignmentAsync" => Task.FromResult(AssignmentValid), "ScopeExistsAsync" => Task.FromResult(true), _ => throw new Exception(m.Name) });
        var source = new OvertimeSources(repo, summary, rosters, realRules ? EligibilityService : eligibility,
            realRules ? new ConfirmedOvertimeDurationPolicy() : policy, compensation, employment, operation);
        var tokens = new Dictionary<string, (Guid Account, string Purpose, string Filter, AttendanceCursorPosition Position)>();
        var cursors = OtProxy.Make<IAttendanceCursorProtector>((m, a) => { if (m.Name == "Protect") { var id = Guid.NewGuid().ToString(); tokens[id] = ((Guid)a[0]!, (string)a[1]!, (string)a[2]!, (AttendanceCursorPosition)a[3]!); return id; } var row = tokens[(string)a[0]!]; if (row.Account != (Guid)a[1]! || row.Purpose != (string)a[2]! || row.Filter != (string)a[3]!) throw new AuthenticationFailure("invalid_attendance_cursor", 400); return row.Position; });
        var notifications = OtProxy.Make<INotificationOutbox>((m, a) => { if (FailNotification) throw new AuthenticationFailure("synthetic_outbox_unavailable", 503); Notifications.Add((NotificationIntent)a[0]!); return Task.FromResult(true); });
        CreditReader = OtProxy.Make<IWorkTreatmentCreditReader>((m, a) => {
            if (m.Name != "ReadAsync") throw new Exception("OT must read actual proof, not invent work claims: " + m.Name);
            if (!InTransaction) throw new Exception("Work-credit proof escaped the authorized transaction");
            WorkReads.Add(((Guid)a[0]!, (DateOnly)a[1]!, (DateOnly)a[2]!));
            if (FailWorkReader) throw new AuthenticationFailure("work_treatment_credit_source_changed", 409);
            return Task.FromResult<IReadOnlyList<WorkTreatmentCreditEvidence>>(WorkCredits.ToArray());
        });
        var constructor = typeof(OvertimeService).GetConstructors().Single();
        object?[] arguments = [operation, repo, source, summary, employment, files, access, cursors, notifications];
        var recoveryReader = OtProxy.Make<ILeaveRecoveryCreditReader>((m, a) => {
            if (m.Name != "ReadReservedAsync" || !InTransaction) throw new Exception("Recovery allocations require the authorized transaction");
            return Task.FromResult<IReadOnlyList<WorkTreatmentCreditEvidence>>([]);
        });
        CreateService = reader => (OvertimeService)constructor.Invoke(constructor.GetParameters()
            .Any(x => x.ParameterType == typeof(ILeaveRecoveryCreditReader)) ? [.. arguments, reader, recoveryReader] :
            constructor.GetParameters().Any(x => x.ParameterType == typeof(IWorkTreatmentCreditReader)) ? [.. arguments, reader] : arguments);
        Service = CreateService(workCredits ? CreditReader : null);
    }
    public async Task<SubmitOvertimeRequest> Proposal() => MakeProposal((await Service.SourcesAsync(Employee, Date, false, Context, default)).SourceFingerprint);
    public SubmitOvertimeRequest MakeProposal(string fingerprint) => new(Date, Sessions[0].Id, ScheduleEnd, End, OvertimeActivity.Work, fingerprint, "Documented assigned work", "Synthetic written work evidence", "job-001");
    public Task<OvertimeResponse> Submit(SubmitOvertimeRequest request) => Service.SubmitAsync(Employee, request, "overtime-submit-001", false, Context, default);
    public Task<OvertimeResponse> Decide(OvertimeResponse row, ApprovalState decision) => Service.DecideAsync(Employee, row.Id, new(row.Version, decision, "Reviewed synthetic evidence"), "overtime-decision-" + decision, Context, default);
    public void Reviewer() => Actor = Actor with { AccountId = Guid.NewGuid(), EmployeeId = Guid.NewGuid(), Client = SessionClient.Web, MfaSatisfied = true };
    public void RestrictToTeam(Guid team) { Grants.Clear(); foreach (var p in OvertimePermissions.Definitions.Where(x => !x.SelfService)) Grants.Add(new(p.Code, p.Sensitive, Guid.NewGuid(), AccessScopeKind.Team, team)); }
    public WorkTreatmentCreditEvidence Credit(WorkCreditRole role, DateTimeOffset from, DateTimeOffset to) {
        var id = Guid.NewGuid();
        return new(id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('C', 64), new string('D', 64),
            new(Employee, id, role, Guid.NewGuid(), Guid.NewGuid(), Date, from, to), null) {
            PolicyVersionId = Policy, OriginalScopes = [new([new(EmployeeId: Employee, SiteId: Site, TeamId: Team)])]
        };
    }
    private Task<CompensationWindow> ReadSalary() { CompensationWindowReads++; return Task.FromResult(new CompensationWindow(Salary, null, Salary)); }
    private object? Transaction(Delegate action, object? token) {
        var prior = InTransaction; InTransaction = true;
        try {
            var result = action.DynamicInvoke(token);
            if (result is Task task) _ = task.ContinueWith(_ => InTransaction = prior, TaskContinuationOptions.ExecuteSynchronously);
            else InTransaction = prior;
            return result;
        } catch { InTransaction = prior; throw; }
    }
    private object? AddReceipt(IdempotencyReceipt item) { Receipts.Add(item); return null; }
    private object? AddEligibility(OvertimeEligibilityVersion item) { EligibilityRows.Add(item); return null; }
    private object? Add(OvertimeRequest row, ApprovalRequest approval) { Rows.Add(new() { Request = row, Approval = approval, BusinessDate = Date }); return null; }
    private object? AddDecision(ApprovalDecision decision) { var i = Rows.FindIndex(x => x.Approval.Id == decision.ApprovalRequestId); Rows[i] = Rows[i] with { Decision = decision }; return null; }
    private Task Save() { foreach (var row in Rows) { if (!saved.TryGetValue(row.Approval.Id, out var state) || state != row.Approval.State) { var v = row.Approval.RowVersion.Length == 8 ? BitConverter.ToInt64(row.Approval.RowVersion) : 0; typeof(ApprovalRequest).GetProperty("RowVersion")!.SetValue(row.Approval, BitConverter.GetBytes(v + 1)); saved[row.Approval.Id] = row.Approval.State; } } return Task.CompletedTask; }
    private Task<IReadOnlyList<OvertimeRecord>> Candidates(OvertimeQueueRequest request, AttendanceCursorPosition? after, int limit, Guid? requester)
    {
        var query = Rows.Where(x => x.BusinessDate >= request.From && x.BusinessDate < request.To && (!request.State.HasValue || request.State == x.Approval.State) &&
            (!requester.HasValue || x.Request.RequestedBy == requester.ToString()) && (request.Scope != CorrectionQueueScope.Employee || request.TargetId == x.Request.EmployeeId));
        if (after is not null) query = query.Where(x => x.Request.RequestedAtUtc > after.CreatedAtUtc || x.Request.RequestedAtUtc == after.CreatedAtUtc && x.Request.Id.CompareTo(after.Id) > 0);
        return Task.FromResult<IReadOnlyList<OvertimeRecord>>(query.OrderBy(x => x.Request.RequestedAtUtc).ThenBy(x => x.Request.Id).Take(limit).ToArray());
    }
}
