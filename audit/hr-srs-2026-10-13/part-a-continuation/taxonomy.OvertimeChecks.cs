using System.Reflection;
using System.Text.Json;
using Cosmix.Application.HR.StaffCategories;
using System.Security.Cryptography;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Application.HR.Compensation;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Compensation;

// Real OT service/source/authorization/receipt logic with synthetic repositories and policy proof.
// No SQL, real private storage, organizational grant or financial acceptance is exercised.
var checks = 0;
try {
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
async Task Fails(Func<Task> action, string code)
{ checks++; try { await action(); } catch (AuthenticationFailure e) when (e.Code == code) { return; } throw new Exception("Expected " + code); }
var f = new OtFixture(); var proposal = await f.Proposal();
var submitted = await f.Submit(proposal);
Check(submitted.State == "Pending" && submitted.ApprovedMinutes is null && submitted.PayableAmount is null, "Submission never awards minutes or money");
Check(f.Rows.Count == 1 && f.Receipts.Count == 1 && f.Notifications.Count == 0, "Submission records real request/receipt without invented reviewer notification");
Check((await f.Submit(proposal)).Id == submitted.Id && f.Rows.Count == 1, "Exact replay returns same immutable request");
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
// Taxonomy continuation: real source, eligibility, operation, request, decision and protected evidence paths.
async Task<OtFixture> Outside(bool allowGlobal = true)
{
    var f = new OtFixture(true, true) { StaffCategory = "OUTSIDE-SYNTHETIC" };
    if (allowGlobal)
    {
        var preview = await f.EligibilityService.PreviewAsync(null, f.Date, f.Context, default);
        await f.EligibilityService.SetAsync(null, new(f.Date, true, 0, preview.TimelineFingerprint, "Explicit synthetic global allowance"),
            "outside-global-001", f.Context, default);
    }
    return f;
}
var broader = await Outside();
var broaderSource = await broader.Sources.LoadAsync(broader.Employee, broader.Date, default);
Check(broaderSource.Source.StaffCategory == "OUTSIDE-SYNTHETIC" && broaderSource.Employment.StaffCategory == "OUTSIDE-SYNTHETIC", "Broader source preserves real category instead of rewriting to SITE");
Check(broaderSource.Source.Roster.Taxonomy?.PolicyVersionId == broader.TaxonomyVersion && broaderSource.Source.Roster.Taxonomy.Fingerprint == broader.TaxonomyHash, "Protected source retains exact published taxonomy");
var broaderProposal = broader.MakeProposal(broaderSource.Source.SourceFingerprint);
var broaderRequest = await broader.Submit(broaderProposal);
Check(broaderRequest.State == "Pending" && broaderRequest.ApprovedMinutes is null, "Approved Outside inheritance permits pending intake without award");
broader.Reviewer(); var broaderApproval = await broader.Decide(broaderRequest, ApprovalState.Approved);
Check(broaderApproval.ApprovedTicks == TimeSpan.FromHours(2).Ticks, "Approved Outside source still requires real eligibility and nine-hour duty rule");
Check((await broader.Service.FinancialAsync(broader.Employee, broaderRequest.Id, false, broader.Context, default)).Amount == 717, "Broader mapping does not replace confirmed salary/formula provenance");
Check((await broader.Decide(broaderRequest, ApprovalState.Approved)).DecisionId == broaderApproval.DecisionId, "Unchanged broader decision replay is single immutable award");
broader.TaxonomyHash = new('B',64);
await Fails(() => broader.Decide(broaderRequest, ApprovalState.Approved), "overtime_source_changed");
Check((await broader.Service.DetailAsync(broader.Employee, broaderRequest.Id, false, false, broader.Context, default)).Request.ApprovedTicks == TimeSpan.FromHours(2).Ticks, "Historical detail verifies frozen taxonomy rather than rewriting award after publication");
var noGlobal = await Outside(false);
var noGlobalRequest = await noGlobal.Submit(await noGlobal.Proposal()); noGlobal.Reviewer();
await Fails(() => noGlobal.Decide(noGlobalRequest, ApprovalState.Approved), "overtime_eligibility_not_configured");
Check(noGlobal.Rows.Single().Approval.State == ApprovalState.Pending, "Outside mapping alone never supplies eligibility or money");
var restricted = await Outside(); restricted.OutsideInheritance = false;
Check((await restricted.EligibilityService.PreviewAsync(restricted.Employee, restricted.Date, restricted.Context, default)).Eligibility?.Allowed == false, "Explicit Outside inheritance false denies existing common ON");
await Fails(async () => await restricted.Submit(await restricted.Proposal()), "overtime_category_unavailable");
var broaderOffice = await Outside(); broaderOffice.OfficeMode = true; // Even a broader Office flag cannot enable OT.
Check((await broaderOffice.EligibilityService.PreviewAsync(broaderOffice.Employee, broaderOffice.Date, broaderOffice.Context, default)).Eligibility?.Status == "office_ineligible", "Office-basis mapping always rejects common ON and inheritance flag");
await Fails(async () => await broaderOffice.Submit(await broaderOffice.Proposal()), "overtime_office_ineligible");
var unavailable = await Outside(); unavailable.MappingAvailable = false;
await Fails(() => unavailable.Proposal(), "overtime_category_unavailable");
var staleTaxonomy = await Outside(); var staleTaxonomyProposal = await staleTaxonomy.Proposal(); staleTaxonomy.TaxonomyVersion = Guid.NewGuid();
await Fails(() => staleTaxonomy.Submit(staleTaxonomyProposal), "overtime_source_changed");
Check(staleTaxonomy.Rows.Count == 0, "Taxonomy version change blocks fresh request before staging");
var replayTaxonomy = await Outside(); var replayProposal = await replayTaxonomy.Proposal(); await replayTaxonomy.Submit(replayProposal);
replayTaxonomy.TaxonomyHash = new('C',64);
await Fails(() => replayTaxonomy.Submit(replayProposal), "overtime_source_changed");
Check(replayTaxonomy.Rows.Count == 1 && replayTaxonomy.Receipts.Count == 2, "Changed taxonomy blocks old receipt without duplicating request");
var approvalTaxonomy = await Outside(); var approvalTaxonomyRequest = await approvalTaxonomy.Submit(await approvalTaxonomy.Proposal()); approvalTaxonomy.Reviewer();
approvalTaxonomy.TaxonomyHash = new('D',64);
await Fails(() => approvalTaxonomy.Decide(approvalTaxonomyRequest, ApprovalState.Approved), "overtime_source_changed");
Check(approvalTaxonomy.Rows.Single().Approval.State == ApprovalState.Pending && approvalTaxonomy.Notifications.Count == 0, "Changed taxonomy denies approval with no award or notification");
var globalOff = await Outside(); var globalOffPreview = await globalOff.EligibilityService.PreviewAsync(null, globalOff.Date, globalOff.Context, default);
await globalOff.EligibilityService.SetAsync(null, new(globalOff.Date, false, globalOffPreview.LatestRevision, globalOffPreview.TimelineFingerprint, "Global OFF"), "outside-global-off", globalOff.Context, default);
await Fails(async () => await globalOff.Submit(await globalOff.Proposal()), "overtime_not_eligible");
var personalOffFixture = await Outside(); var personalOffPreview = await personalOffFixture.EligibilityService.PreviewAsync(personalOffFixture.Employee, personalOffFixture.Date, personalOffFixture.Context, default);
await personalOffFixture.EligibilityService.SetAsync(personalOffFixture.Employee, new(personalOffFixture.Date, false, 0, personalOffPreview.TimelineFingerprint, "Personal OFF"), "outside-personal-off", personalOffFixture.Context, default);
await Fails(async () => await personalOffFixture.Submit(await personalOffFixture.Proposal()), "overtime_not_eligible");
var wrongTaxonomyCode = await Outside(); wrongTaxonomyCode.RosterWrongCode = true;
await Fails(() => wrongTaxonomyCode.Proposal(), "overtime_taxonomy_source_conflict");
var wrongMembership = await Outside(); wrongMembership.WrongMembership = true;
await Fails(() => wrongMembership.Proposal(), "overtime_taxonomy_source_conflict");
var crossDate = await Outside(); crossDate.RangeUnavailable = true; crossDate.Sessions[0] = crossDate.Sessions[0] with { EndsAtUtc = crossDate.End.AddDays(1) };
await Fails(() => crossDate.Proposal(), "overtime_taxonomy_source_conflict");
var requestRace = await Outside(); var requestRaceProposal = await requestRace.Proposal();
requestRace.OnPrivateWrite = purpose => { if (purpose.EndsWith(":proposal")) requestRace.TaxonomyHash = new('E',64); };
await Fails(() => requestRace.Submit(requestRaceProposal), "overtime_source_changed");
Check(requestRace.Rows.Count == 0 && requestRace.Receipts.Count == 1, "Post-private-IO taxonomy race stages no request or receipt");
var decisionRace = await Outside(); var decisionRaceRequest = await decisionRace.Submit(await decisionRace.Proposal()); decisionRace.Reviewer();
decisionRace.OnPrivateWrite = purpose => { if (purpose.EndsWith(":decision")) decisionRace.TaxonomyHash = new('F',64); };
await Fails(() => decisionRace.Decide(decisionRaceRequest, ApprovalState.Approved), "overtime_source_changed");
Check(decisionRace.Rows.Single().Approval.State == ApprovalState.Pending && decisionRace.Notifications.Count == 0, "Post-private-IO taxonomy race mutates no decision or notification");
var settingRace = await Outside(); var settingRacePreview = await settingRace.EligibilityService.PreviewAsync(settingRace.Employee, settingRace.Date, settingRace.Context, default);
settingRace.OnPrivateWrite = purpose => { if (purpose.Contains(":eligibility:")) settingRace.TaxonomyHash = new('B',64); };
await Fails(() => settingRace.EligibilityService.SetAsync(settingRace.Employee, new(settingRace.Date, true, 0, settingRacePreview.TimelineFingerprint, "Restore inheritance"), "outside-setting-race", settingRace.Context, default), "overtime_source_changed");
Check(settingRace.EligibilityRows.Count == 1, "Employee eligibility publication checks fresh taxonomy after private evidence IO");
var broaderBulk = await Outside(); var broaderBulkProposal = await broaderBulk.Proposal();
var broaderBatch = new SubmitOvertimeBulkRequest([new(broaderBulk.Employee, broaderBulkProposal)], "Synthetic named batch");
var broaderBulkRequest = await broaderBulk.Service.SubmitBulkAsync(broaderBatch, "outside-bulk-submit", broaderBulk.Context, default);
Check(broaderBulkRequest.Employees.Single().State == "Pending", "Named bulk preserves approved broader category source");
broaderBulk.TaxonomyHash = new('C',64);
await Fails(() => broaderBulk.Service.SubmitBulkAsync(broaderBatch, "outside-bulk-submit", broaderBulk.Context, default), "overtime_source_changed");
var broaderBulkDecide = await Outside(); var broaderBulkDecideProposal = await broaderBulkDecide.Proposal();
var broaderBulkDecideRequest = await broaderBulkDecide.Submit(broaderBulkDecideProposal); broaderBulkDecide.Reviewer();
var decisionBatch = new DecideOvertimeBulkRequest([new(broaderBulkDecide.Employee, broaderBulkDecideRequest.Id, new(broaderBulkDecideRequest.Version, ApprovalState.Approved, "Synthetic review"))], "Synthetic named decision");
var decidedBatch = await broaderBulkDecide.Service.DecideBulkAsync(decisionBatch, "outside-bulk-decide", broaderBulkDecide.Context, default);
Check(decidedBatch.Employees.Single().ApprovedTicks == TimeSpan.FromHours(2).Ticks, "Named bulk decision uses real inherited OT duty source");
broaderBulkDecide.TaxonomyVersion = Guid.NewGuid();
await Fails(() => broaderBulkDecide.Service.DecideBulkAsync(decisionBatch, "outside-bulk-decide", broaderBulkDecide.Context, default), "overtime_source_changed");
var publishedSite = new OtFixture(false,true); var publishedSiteProposal = await publishedSite.Proposal(); await publishedSite.Submit(publishedSiteProposal);
publishedSite.ReturnBaseline = true;
await Fails(() => publishedSite.Submit(publishedSiteProposal), "overtime_source_changed");
Check(publishedSite.Rows.Count == 1, "Published SITE cannot replay through a current null baseline proof");
var baselineFixture = new OtFixture(); var baselineSource = await baselineFixture.Sources.LoadAsync(baselineFixture.Employee, baselineFixture.Date, default);
Check(baselineSource.Source.Roster.Taxonomy is null && !JsonSerializer.Serialize(baselineSource).Contains("\"Taxonomy\""), "Confirmed baseline source omits new proof property exactly");
Console.WriteLine($"PASS overtime service checks: {checks}; synthetic IO/policy only, no SQL/payment acceptance.");

}
catch(Exception e) { Console.Error.WriteLine($"FAIL OT continuation: {checks-1} checks passed before {e.Message}"); Environment.ExitCode=1; }

public sealed class OtClock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 10, 16, 0, 0, TimeSpan.Zero); }
public class OtProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, OtProxy>(); ((OtProxy)(object)proxy).Handler = handler; return proxy; }
}
public sealed class OtFixture
{
    public Guid Employee = Guid.NewGuid(), Site = Guid.NewGuid(), Team = Guid.NewGuid(), Policy = Guid.NewGuid(), EligibilityId = Guid.NewGuid();
    public DateOnly Date = new(2026, 10, 9);
    public DateTimeOffset Start = new(2026, 10, 9, 4, 0, 0, TimeSpan.Zero), ScheduleEnd = new(2026, 10, 9, 13, 0, 0, TimeSpan.Zero), End = new(2026, 10, 9, 15, 0, 0, TimeSpan.Zero);
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true), Requester;
    public bool DurationReady = true, EligibilityReady = true, EligibilityAllowed = true, Locked, AssignmentValid = true, SessionCurrent = true, FailStorage, FailNotification, FixedSettingsAuthority = true;
    public string StaffCategory = "SITE";
    public bool ReturnBaseline, TaxonomyMode, MappingAvailable = true, OutsideInheritance = true, OfficeMode, RangeUnavailable, RosterWrongCode, WrongMembership;
    public Guid TaxonomyVersion = Guid.NewGuid(), OfficeBranch = Guid.NewGuid();
    public string TaxonomyHash = new('A', 64);
    public Action<string>? OnPrivateWrite;
    public OvertimeSources Sources;
    public StaffTaxonomyResolution Mapping() => !TaxonomyMode || ReturnBaseline ? StaffTaxonomyResolver.Resolve(StaffCategory, Date, []) :
        !MappingAvailable ? new("taxonomy_not_effective", null, null, new('F', 64)) :
        new("effective", new(StaffCategory, "Synthetic outside category", OfficeMode ? StaffWorkLocation.Office : StaffWorkLocation.Site,
            OfficeMode ? StaffScheduleBasis.Office : StaffScheduleBasis.Site, false, OutsideInheritance, []), TaxonomyVersion, TaxonomyHash,
            false, Date.AddDays(-30), null);
    public RosterStaffTaxonomySource? Pin() => !TaxonomyMode || ReturnBaseline || !MappingAvailable ? null : new(RosterWrongCode ? "OTHER" : StaffCategory,
        TaxonomyVersion, TaxonomyHash, OfficeMode ? StaffWorkLocation.Office : StaffWorkLocation.Site,
        OfficeMode ? StaffScheduleBasis.Office : StaffScheduleBasis.Site, OutsideInheritance);
    public List<OvertimeSessionSource> Sessions = []; public List<OvertimeRecord> Rows = []; public List<PermissionGrant> Grants = [];
    public List<IdempotencyReceipt> Receipts = []; public List<NotificationIntent> Notifications = [];
    public Dictionary<string, (string Purpose, byte[] Data)> Files = []; private readonly Dictionary<Guid, ApprovalState> saved = [];
    public OvertimeService Service; public AuthRequestContext Context = new("127.0.0.1", "synthetic-overtime");
    public OvertimeEligibilityService EligibilityService;
    public List<OvertimeEligibilityVersion> EligibilityRows = [];
    public OtFixture(bool realRules = false, bool taxonomyMode = false)
    {
        if (realRules) { Date = Date.AddDays(1); Start = Start.AddDays(1); ScheduleEnd = ScheduleEnd.AddDays(1); End = End.AddDays(1); }
        TaxonomyMode = taxonomyMode; Requester = Actor; var shift = Guid.NewGuid();
        Sessions.Add(new(Guid.NewGuid(), Guid.NewGuid(), 1, Start, End, Site, shift, null, Guid.NewGuid(), Guid.NewGuid(), null, true));
        foreach (var permission in OvertimePermissions.Definitions) Grants.Add(new(permission.Code, permission.Sensitive, Guid.NewGuid(), AccessScopeKind.All, null));
        var current = OtProxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = OtProxy.Make<IAccountAccessReader>((m, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var authz = OtProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(a[1]),
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
            if (m.Name == "WriteAsync") { if (FailStorage) throw new AuthenticationFailure("synthetic_storage_unavailable", 503); var bytes = (byte[])a[0]!; var id = Guid.NewGuid().ToString("N"); Files[id] = ((string)a[1]!, bytes); OnPrivateWrite?.Invoke((string)a[1]!); return Task.FromResult(new PrivateFileReference(id, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length)); }
            var value = Files[((PrivateFileReference)a[0]!).StorageKey]; if (value.Purpose != (string)a[1]!) throw new Exception("Wrong evidence purpose"); return Task.FromResult(value.Data); });
        var operation = new AttendanceOperation(current, access, authz, journal, auth, new OtClock(), files);
        var assignment = Guid.NewGuid(); var category = Guid.NewGuid();
        var repo = OtProxy.Make<IOvertimeRepository>((m, a) => m.Name switch {
            "EmploymentAsync" => Task.FromResult<OvertimeEmploymentSource?>(new(assignment, category, StaffCategory, OfficeMode ? null : Site, Date.AddDays(-30), null, OfficeMode ? OfficeBranch : null)),
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
            OfficeMode ? null : Site, assignment, Guid.Empty, shift, 1, Start, ScheduleEnd, 540, 0, 0) { Taxonomy = Pin(), BranchId = OfficeMode ? OfficeBranch : null, Explanation = taxonomyMode ? new("synthetic-taxonomy", null, WrongMembership ? Guid.NewGuid() : assignment, null, null, "synthetic") : null }));
        var eligibilityRepository = OtProxy.Make<IOvertimeEligibilityRepository>((m, a) => m.Name switch {
            "TimelineAsync" => Task.FromResult<IReadOnlyList<OvertimeEligibilityVersion>>(EligibilityRows.Where(x => x.Target == (OvertimeEligibilityTarget)a[0]! && x.EmployeeId == (Guid?)a[1]).OrderBy(x => x.Revision).ToArray()),
            "Add" => AddEligibility((OvertimeEligibilityVersion)a[0]!), "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception(m.Name) });
        var taxonomy = OtProxy.Make<IStaffTaxonomyResolver>((m, a) => Task.FromResult(m.Name == "ResolveForRangeAsync" && RangeUnavailable
            ? new StaffTaxonomyResolution("mapping_changes_during_interval", null, null, new('F', 64)) : Mapping()));
        EligibilityService = new(operation, eligibilityRepository, repo, summary, files, taxonomyMode ? taxonomy : null);
        var eligibility = OtProxy.Make<IOvertimeEligibilityReader>((m, a) => Task.FromResult(new OvertimeEligibility(EligibilityReady ? "configured" : "eligibility_not_configured", EligibilityReady ? EligibilityId : null, EligibilityReady ? EligibilityAllowed : null)));
        var policy = OtProxy.Make<IOvertimeDurationPolicy>((m, a) => Task.FromResult(DurationReady ? new OvertimeDurationResult("evaluated", Policy, "synthetic-confirmed-duration-v1", true, [new(ScheduleEnd, End)]) : new OvertimeDurationResult("duration_policy_unavailable", null, null, null, [])));
        var salary = new CompensationRecord(Employee, 1, null, 100000, 120000, "PKR", WageBasis.Monthly, PaymentMethod.Bank, Date.AddDays(-30), null, null, "synthetic-approved-hiring", Start.AddDays(-30));
        var compensation = OtProxy.Make<IEmployeeCompensationRepository>((m, a) => m.Name switch { "HasConflictingHistoryAsync" => Task.FromResult(false), "WindowAsync" => Task.FromResult(new CompensationWindow(salary, null, salary)), _ => throw new Exception(m.Name) });
        var employment = OtProxy.Make<ICorrectionReviewRepository>((m, a) => m.Name switch { "HistoricallyEligibleAsync" => Task.FromResult(true), "PreservesAssignmentAsync" => Task.FromResult(AssignmentValid), "ScopeExistsAsync" => Task.FromResult(true), _ => throw new Exception(m.Name) });
        var source = new OvertimeSources(repo, summary, rosters, realRules ? EligibilityService : eligibility,
            realRules ? new ConfirmedOvertimeDurationPolicy() : policy, compensation, employment, operation, taxonomyMode ? taxonomy : null); Sources = source;
        var tokens = new Dictionary<string, (Guid Account, string Purpose, string Filter, AttendanceCursorPosition Position)>();
        var cursors = OtProxy.Make<IAttendanceCursorProtector>((m, a) => { if (m.Name == "Protect") { var id = Guid.NewGuid().ToString(); tokens[id] = ((Guid)a[0]!, (string)a[1]!, (string)a[2]!, (AttendanceCursorPosition)a[3]!); return id; } var row = tokens[(string)a[0]!]; if (row.Account != (Guid)a[1]! || row.Purpose != (string)a[2]! || row.Filter != (string)a[3]!) throw new AuthenticationFailure("invalid_attendance_cursor", 400); return row.Position; });
        var notifications = OtProxy.Make<INotificationOutbox>((m, a) => { if (FailNotification) throw new AuthenticationFailure("synthetic_outbox_unavailable", 503); Notifications.Add((NotificationIntent)a[0]!); return Task.FromResult(true); });
        Service = new(operation, repo, source, summary, employment, files, access, cursors, notifications);
    }
    public async Task<SubmitOvertimeRequest> Proposal() => MakeProposal((await Service.SourcesAsync(Employee, Date, false, Context, default)).SourceFingerprint);
    public SubmitOvertimeRequest MakeProposal(string fingerprint) => new(Date, Sessions[0].Id, ScheduleEnd, End, OvertimeActivity.Work, fingerprint, "Documented assigned work", "Synthetic written work evidence", "job-001");
    public Task<OvertimeResponse> Submit(SubmitOvertimeRequest request) => Service.SubmitAsync(Employee, request, "overtime-submit-001", false, Context, default);
    public Task<OvertimeResponse> Decide(OvertimeResponse row, ApprovalState decision) => Service.DecideAsync(Employee, row.Id, new(row.Version, decision, "Reviewed synthetic evidence"), "overtime-decision-" + decision, Context, default);
    public void Reviewer() => Actor = Actor with { AccountId = Guid.NewGuid(), EmployeeId = Guid.NewGuid(), Client = SessionClient.Web, MfaSatisfied = true };
    public void RestrictToTeam(Guid team) { Grants.Clear(); foreach (var p in OvertimePermissions.Definitions.Where(x => !x.SelfService)) Grants.Add(new(p.Code, p.Sensitive, Guid.NewGuid(), AccessScopeKind.Team, team)); }
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
