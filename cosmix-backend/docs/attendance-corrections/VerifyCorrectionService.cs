#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyCorrectionService.packages.lock.json
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.CarryForward;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.Common.Audit;
using Cosmix.Application.Attendance.Calculation;

// Actual intake/cancellation services and attendance authority/receipt boundary with in-memory IO.
// No SQL transaction, running HTTP server, employee fixture or live session is created.
var checks = 0;
void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
async Task Fails(Func<Task> action, string code)
{
    checks++;
    try { await action(); } catch (AuthenticationFailure failure) when (failure.Code == code) { return; }
    throw new Exception("Expected " + code);
}
var f = new CorrectionFixture();
// New correction review contract keeps nullable additions absent from legacy request digests.
var missingCheckoutProposal = f.Proposal with { Kind = AttendanceCorrectionKind.CheckOutTime, RawPunchId = null,
    ExpectedAssessmentId = null, TargetWorkSessionId = Guid.NewGuid() };
AttendanceCorrectionService.ValidateProposal(missingCheckoutProposal);
Check(!JsonSerializer.Serialize(f.Proposal).Contains("TargetWorkSessionId") &&
    !JsonSerializer.Serialize(f.Proposal).Contains("ResubmissionOfCorrectionId"), "new nullable review fields preserve legacy request bytes");
await Fails(() => Task.Run(() => AttendanceCorrectionService.ValidateProposal(missingCheckoutProposal with { TargetWorkSessionId = null })), "invalid_correction_typed_values");
var source = await f.Service.SourcesAsync(f.Proposal.BusinessDate, f.Raw.Id, f.Context, default);
Check(source.LatestAssessmentId == f.Assessment.Id && source.LatestSummaryId == f.Summary.Id && !source.PeriodLocked,
    "self source preview exposes immutable source identities");
Check(source.SummaryCalculationState == "factual_only" && source.SummaryStatus is null && source.HistoricalRoster?.ShiftVersionId == f.Roster.ShiftVersionId,
    "factual source never represents policy status and retains immutable roster version");
var response = await f.Submit();
Check(response.State == "Pending" && response.RoutingStatus == "hr_review_required" && !response.ChangesAttendance,
    "intake requires permission-scoped HR review and has no attendance effect");
Check(f.Records.Count == 1 && f.Records[0].Approval.RouteRuleId is null && f.Records[0].Approval.Stage == 1, "no approval role or route invented");
Check(f.Raw.ReceivedAtUtc == response.OriginalTimeUtc && f.Summary.InputFingerprint == new string('B', 64), "raw and calculated source remain untouched");
Check(await f.Submit() == response && f.Records.Count == 1, "same-key replay does not duplicate proposals");
var detail = await f.Service.DetailAsync(response.Id, f.Context, default);
Check(detail.WrittenEvidence == f.Proposal.WrittenEvidence && JsonSerializer.Serialize(detail.CapturedSource) == JsonSerializer.Serialize(source), "private evidence and source snapshot recovered");
var history = await f.Service.ListAsync(f.Range, f.Context, default);
Check(history.TotalCount == 1 && history.Items[0].Id == response.Id, "own proposal history");
await Fails(() => f.Submit(f.Proposal with { RequestedTimeUtc = f.Proposal.RequestedTimeUtc!.Value.AddMinutes(-1) }), "idempotency_key_reused");
await Fails(() => f.Submit(key: "another-key-001"), "equivalent_correction_pending");
var staleAssessment = new CorrectionFixture();
await Fails(() => staleAssessment.Submit(staleAssessment.Proposal with { ExpectedAssessmentId = Guid.NewGuid() }), "correction_source_version_conflict");
Check(staleAssessment.Records.Count == 0 && staleAssessment.Files.Count == 0, "stale assessment denied before persisted proposal or evidence");
var staleSummary = new CorrectionFixture();
await Fails(() => staleSummary.Submit(staleSummary.Proposal with { ExpectedSummaryId = Guid.NewGuid() }), "correction_source_version_conflict");
await Fails(() => staleSummary.Submit(staleSummary.Proposal with { RawPunchId = Guid.NewGuid() }), "attendance_resource_not_found");
await Fails(() => staleSummary.Submit(staleSummary.Proposal with { Kind = AttendanceCorrectionKind.CheckOutTime }), "correction_punch_action_mismatch");
await Fails(() => staleSummary.Submit(staleSummary.Proposal with { BusinessDate = new(2026, 10, 7) }), "correction_punch_date_mismatch");
await Fails(() => staleSummary.Submit(staleSummary.Proposal with { BusinessDate = new(2026, 9, 30) }), "correction_date_before_employment");
await Fails(() => staleSummary.Submit(staleSummary.Proposal with { BusinessDate = new(2026, 10, 10) }), "correction_date_in_future");
await Fails(() => staleSummary.Submit(staleSummary.Proposal with { RequestedTimeUtc = staleSummary.Clock.Now.AddMinutes(1) }), "correction_time_in_future");
await Fails(() => staleSummary.Submit(staleSummary.Proposal with { Kind = (AttendanceCorrectionKind)99 }), "invalid_correction_proposal");
await Fails(() => staleSummary.Submit(staleSummary.Proposal with { WrittenEvidence = new string('a', 4001) }), "invalid_attendance_request");
var locked = new CorrectionFixture { Locked = true };
await Fails(() => locked.Submit(), "correction_locked_period_adjustment_required"); Check(locked.Records.Count == 0, "locked date intake rejected");
await Fails(() => f.Service.CancelAsync(response.Id, new(Convert.ToBase64String(BitConverter.GetBytes(777L)), "Withdraw incorrect request"),
    "cancel-stale-001", f.Context, default), "correction_version_conflict");
Check(f.Records[0].Approval.State == ApprovalState.Pending, "stale cancel preserves state");
f.Locked = true;
var cancelled = await f.Service.CancelAsync(response.Id, new(response.Version, "Withdraw incorrect request"), "cancel-key-001", f.Context, default);
Check(cancelled.State == "Cancelled" && !cancelled.ChangesAttendance, "requester may cancel pending proposal after period locks");
Check(await f.Service.CancelAsync(response.Id, new(response.Version, "Withdraw incorrect request"), "cancel-key-001", f.Context, default) == cancelled,
    "cancellation receipt replays despite old expected version");
await Fails(() => f.Service.CancelAsync(response.Id, new(cancelled.Version, "Try again"), "cancel-key-002", f.Context, default), "correction_not_pending");
var foreign = new CorrectionFixture(); var foreignResponse = await foreign.Submit();
CorrectionFixture.Set(foreign.Records[0].Approval, "RequesterUserId", Guid.NewGuid().ToString());
await Fails(() => foreign.Service.DetailAsync(foreignResponse.Id, foreign.Context, default), "attendance_resource_not_found");
await Fails(() => foreign.Service.CancelAsync(foreignResponse.Id, new(foreignResponse.Version, "Not mine"), "foreign-cancel", foreign.Context, default), "attendance_resource_not_found");
var noAccess = new CorrectionFixture(); noAccess.Grants.Clear(); await Fails(() => noAccess.Submit(), "access_denied");
var revoked = new CorrectionFixture(); await revoked.Submit(); revoked.Current = false;
await Fails(() => revoked.Submit(), "invalid_authentication");
var manual = new CorrectionFixture();
var manualRequest = manual.Proposal with { Kind = AttendanceCorrectionKind.ManualSession, RawPunchId = null, ExpectedAssessmentId = null,
    RequestedTimeUtc = new DateTimeOffset(2026, 10, 8, 20, 0, 0, TimeSpan.Zero), RequestedEndUtc = new DateTimeOffset(2026, 10, 9, 2, 0, 0, TimeSpan.Zero) };
Check((await manual.Submit(manualRequest)).State == "Pending", "overnight manual proposal stored without creating a work session");
Check(RosterSchedule.Today(manualRequest.RequestedTimeUtc!.Value) != manualRequest.BusinessDate,
    "after-midnight late start belongs to previous business-date overnight roster");
await Fails(() => manual.Submit(manualRequest with { RequestedEndUtc = manualRequest.RequestedTimeUtc }, "invalid-window"), "invalid_correction_typed_values");
await Fails(() => manual.Submit(manualRequest with { RequestedTimeUtc = manual.Roster.EndsAt!.Value.AddMinutes(1),
    RequestedEndUtc = manual.Roster.EndsAt!.Value.AddMinutes(2) }, "outside-window"), "correction_session_outside_roster");
var unavailable = new CorrectionFixture(); unavailable.Roster = unavailable.Roster with { Status = "unassigned", StartsAt = null, EndsAt = null };
await Fails(() => unavailable.Submit(), "correction_roster_unavailable");
Check(unavailable.Records.Count == 0 && unavailable.Files.Count == 0, "unresolved roster is explicit before protected write");
var late = new CorrectionFixture();
Check((await late.Submit(late.Proposal with { RequestedTimeUtc = new DateTimeOffset(2026, 10, 8, 21, 0, 0, TimeSpan.Zero) })).State == "Pending",
    "own immutable raw business date retained for overnight proposed time");
var rebound = new CorrectionFixture(); var first = await rebound.Submit();
await rebound.Service.CancelAsync(first.Id, new(first.Version, "Correct my written evidence"), "withdraw-to-resubmit", rebound.Context, default);
var second = await rebound.Submit(rebound.Proposal with { WrittenEvidence = "Different contemporaneous evidence." }, "resubmitted-correction");
var firstReference = System.Text.Json.JsonSerializer.Deserialize<PrivateFileReference>(rebound.Records[0].Correction.PrivateEvidenceReference)!;
var secondReference = System.Text.Json.JsonSerializer.Deserialize<PrivateFileReference>(rebound.Records[1].Correction.PrivateEvidenceReference)!;
rebound.Files[secondReference.StorageKey] = rebound.Files[firstReference.StorageKey];
await Fails(() => rebound.Service.DetailAsync(second.Id, rebound.Context, default), "correction_evidence_unavailable");
Check(rebound.Records[0].Approval.SubjectId != rebound.Records[1].Approval.SubjectId, "every submission has independent evidence binding");
await Fails(() => manual.Service.ListAsync(manual.Range with { Page = 0 }, manual.Context, default), "invalid_correction_range");
await Fails(() => manual.Service.ExceptionsAsync(manual.Range with { PageSize = 101 }, manual.Context, default), "invalid_correction_range");
var notices = await manual.Service.ExceptionsAsync(manual.Range, manual.Context, default);
Check(notices.Items.Single().NoticeCode == "attendance_evidence_requires_review", "confidential review reason/kind not projected to employee");
Check(!System.Text.Json.JsonSerializer.Serialize(notices).Contains("confidential", StringComparison.Ordinal), "exception inbox hides private reason text");

var staleSession = new CorrectionFixture();
var sessionKey = Guid.NewGuid();
staleSession.Sessions.Add(new(sessionKey, 1, staleSession.Actor.EmployeeId, staleSession.Proposal.BusinessDate,
    staleSession.Raw.ReceivedAtUtc, null, staleSession.Roster.ShiftVersionId!.Value, null, staleSession.Clock.Now, staleSession.Raw,
    siteId: staleSession.Roster.SiteId));
var sessionPreview = await staleSession.PreviewProposal();
var immutableAssessment = staleSession.Assessment.Id; var immutableSummary = staleSession.Summary.Id;
var checkout = new RawPunchEvent(staleSession.Actor.EmployeeId, PunchAction.CheckOut, Guid.NewGuid().ToString(), "later-checkout", new string('C',64),
    staleSession.Raw.ReceivedAtUtc.AddHours(1), false, businessDate: staleSession.Proposal.BusinessDate);
staleSession.Sessions.Add(new(sessionKey, 2, staleSession.Actor.EmployeeId, staleSession.Proposal.BusinessDate,
    staleSession.Raw.ReceivedAtUtc, checkout.ReceivedAtUtc, staleSession.Roster.ShiftVersionId.Value, null, staleSession.Clock.Now,
    staleSession.Raw, checkout, siteId: staleSession.Roster.SiteId));
await Fails(() => staleSession.Service.SubmitAsync(sessionPreview, "stale-session-proof", staleSession.Context, default), "correction_source_version_conflict");
Check(staleSession.Assessment.Id == immutableAssessment && staleSession.Summary.Id == immutableSummary &&
    staleSession.Records.Count == 0 && staleSession.Files.Count == 0, "new checkout conflicts without changed original assessment/summary or protected writes");
var refreshed = await staleSession.PreviewProposal();
Check(refreshed.ExpectedSourceFingerprint != sessionPreview.ExpectedSourceFingerprint, "latest session revision contributes to source fingerprint");
var refreshedSource = await staleSession.Service.SourcesAsync(refreshed.BusinessDate, refreshed.RawPunchId, staleSession.Context, default);
Check(refreshedSource.SessionRevisions?.Single().Revision == 2 && refreshedSource.SessionRevisions.Single().CheckOutPunchId == checkout.Id,
    "preview pins latest immutable session revision and checkout source");
Check((await staleSession.Service.SubmitAsync(refreshed, "refreshed-session-proof", staleSession.Context, default)).State == "Pending", "fresh source proof accepts proposal");
var newSession = new CorrectionFixture(); var emptyPreview = await newSession.PreviewProposal();
newSession.Sessions.Add(new(Guid.NewGuid(), 1, newSession.Actor.EmployeeId, newSession.Proposal.BusinessDate,
    newSession.Raw.ReceivedAtUtc, null, newSession.Roster.ShiftVersionId!.Value, null, newSession.Clock.Now, newSession.Raw,
    siteId: newSession.Roster.SiteId));
await Fails(() => newSession.Service.SubmitAsync(emptyPreview, "new-session-proof", newSession.Context, default), "correction_source_version_conflict");
var scopeChange = new CorrectionFixture(); var scopePreview = await scopeChange.PreviewProposal();
scopeChange.Scopes = [new([new(scopeChange.Actor.EmployeeId, SiteId: Guid.NewGuid())])];
await Fails(() => scopeChange.Service.SubmitAsync(scopePreview, "changed-source-scope", scopeChange.Context, default), "correction_source_version_conflict");
var order = new CorrectionFixture(); var teamA = Guid.NewGuid(); var teamB = Guid.NewGuid();
order.Scopes = [new([new(order.Actor.EmployeeId, TeamId:teamA, SiteId:order.Roster.SiteId),new(order.Actor.EmployeeId, TeamId:teamB, SiteId:order.Roster.SiteId)])];
var ordered = await order.PreviewProposal();
order.Scopes = [new(order.Scopes[0].Alternatives.Reverse().ToArray())];
Check((await order.PreviewProposal()).ExpectedSourceFingerprint == ordered.ExpectedSourceFingerprint, "equivalent scope ordering does not cause false stale conflict");
await Fails(() => order.Service.SubmitAsync(order.Proposal with { ExpectedSourceFingerprint = null }, "missing-source-proof", order.Context, default), "correction_source_fingerprint_required");
await Fails(() => order.Service.SubmitAsync(order.Proposal with { ExpectedSourceFingerprint = "not-a-sha256" }, "invalid-source-proof", order.Context, default), "invalid_correction_source_fingerprint");

var legacy = new CorrectionFixture(); var legacyResponse = await legacy.Submit();
var legacyProposal = legacy.Proposal with { ExpectedSourceFingerprint = null };
// Pin the exact pre-change JSON field order/omission; this is the payload the old operation hashed.
var pinnedLegacyObject = new { legacyProposal.BusinessDate, legacyProposal.Kind, legacyProposal.RawPunchId,
    legacyProposal.ExpectedAssessmentId, legacyProposal.ExpectedSummaryId, legacyProposal.RequestedTimeUtc,
    legacyProposal.RequestedEndUtc, legacyProposal.RequestedStatus, legacyProposal.Reason, legacyProposal.WrittenEvidence };
var pinnedLegacyPayload = JsonSerializer.Serialize(pinnedLegacyObject);
Check(JsonSerializer.Serialize(legacyProposal) == pinnedLegacyPayload, "omitted fingerprint retains exact legacy payload bytes");
// Keep native DateTimeOffset values in the wrapper too. A JsonElement reserialization can escape '+'
// differently and would produce a hash of reconstructed JSON rather than the original DTO bytes.
var pinnedDigest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
    permission=CorrectionPermissions.Self, employeeId=(Guid?)legacy.Actor.EmployeeId, DeviceId=(Guid?)null,
    payload=pinnedLegacyObject, reason=legacyProposal.Reason.Trim() })));
CorrectionFixture.Set(legacy.Receipts.Single(), "RequestSha256", pinnedDigest);
Check(await legacy.Service.SubmitAsync(legacyProposal, "correction-key-001", legacy.Context, default) == legacyResponse && legacy.Records.Count == 1,
    "exact old receipt reconciles without source proof or duplicate write");
legacy.Grants.Clear();
await Fails(() => legacy.Service.SubmitAsync(legacyProposal, "correction-key-001", legacy.Context, default), "access_denied");
legacy.Grants.Add(new(CorrectionPermissions.Self,false,Guid.NewGuid(),AccessScopeKind.Self,legacy.Actor.EmployeeId));
var legacyReference = JsonSerializer.Deserialize<PrivateFileReference>(legacy.Records[0].Correction.PrivateEvidenceReference)!;
var retained = legacy.Files[legacyReference.StorageKey];
var legacySnapshot = JsonNode.Parse(retained.Bytes)!;
legacySnapshot["SchemaVersion"] = 1; legacySnapshot.AsObject().Remove("OriginalResourceScopes");
legacySnapshot["Proposal"]!.AsObject().Remove("ExpectedSourceFingerprint");
legacySnapshot["Source"]!.AsObject().Remove("SourceFingerprint"); legacySnapshot["Source"]!.AsObject().Remove("SessionRevisions");
legacySnapshot["Source"]!.AsObject().Remove("OriginalPunchContext");
legacy.Files[legacyReference.StorageKey] = (retained.Purpose,JsonSerializer.SerializeToUtf8Bytes(legacySnapshot));
Check((await legacy.Service.DetailAsync(legacyResponse.Id,legacy.Context,default)).CapturedSource.SourceFingerprint is null,
    "schema1 protected proposal remains readable without fabricating missing source proof");
var frozenFixture = new CorrectionFixture(); var frozenResponse = await frozenFixture.Submit();
var frozenReference = JsonSerializer.Deserialize<PrivateFileReference>(frozenFixture.Records[0].Correction.PrivateEvidenceReference)!;
var frozenBytes = frozenFixture.Files[frozenReference.StorageKey];
var frozenSnapshot = JsonSerializer.Deserialize<CorrectionEvidenceSnapshot>(frozenBytes.Bytes)!;
Check(frozenSnapshot.SchemaVersion == 2 && frozenSnapshot.OriginalResourceScopes?.Any(x=>x.Alternatives.Any(r=>r.SiteId == frozenFixture.Roster.SiteId)) == true,
    "schema2 snapshot retains original historical scope privately");
Check(frozenSnapshot.OriginalResourceScopes!.Any(x=>x.Alternatives.Count==1&&x.Alternatives[0].EmployeeId == frozenFixture.Actor.EmployeeId&&
    x.Alternatives[0].SiteId is null)&&frozenSnapshot.Source.OriginalPunchContext?.BusinessDate==frozenFixture.Raw.BusinessDate,
    "legacy raw site-less source retains its own original requirement and recorded business date");
Check(!JsonSerializer.Serialize(source).Contains("OriginalResourceScopes",StringComparison.Ordinal), "public preview excludes private authority paths");
frozenFixture.Files[frozenReference.StorageKey] = (frozenBytes.Purpose,JsonSerializer.SerializeToUtf8Bytes(frozenSnapshot with {
    OriginalResourceScopes=[new([new(frozenFixture.Actor.EmployeeId,SiteId:Guid.NewGuid())])] }));
await Fails(() => frozenFixture.Service.DetailAsync(frozenResponse.Id,frozenFixture.Context,default), "correction_evidence_unavailable");
var apply = new CorrectionFixture(); apply.EnableValidDuty();
var submitted = await apply.Submit();
var source3 = await apply.Service.SourcesAsync(apply.Proposal.BusinessDate,null,apply.Context,default);
Check(source3.DutyProofs?.Single().HistoricallyVerified == true && source3.RoutingStatus == "hr_review_required", "associated valid CheckIn proof frozen on missing checkout source");
apply.BecomeReviewer(); var baseline=apply.Transactions;
var applied=await apply.Decide(submitted);
Check(applied.Request.ChangesAttendance && applied.AppliedSessionRevisionId==apply.Sessions.Last().Id && applied.AppliedSummaryRevisionId==apply.Summary.Id, "HR missing checkout applies session and summary provenance");
Check(apply.Sessions.Last().CheckOutPunchId is null && apply.Sessions.Last().CorrectionId==submitted.Id && apply.Sessions[0].EndsAtUtc is null, "missing checkout adds interpretation revision without fake raw checkout");
Check(apply.Summary.PayableMinutes is null && apply.Summary.LateMinutes is null && apply.Summary.OvertimeMinutes is null, "approval does not fabricate financial results");
Check(apply.Transactions==baseline+1 && apply.TransactionDepth==0 && apply.Decisions.Count==1, "approval and internal summary use one caller-owned transaction");
Check(await apply.Decide(submitted)==applied && apply.Sessions.Count==2 && apply.Notifications.Count==1, "decision retry replays original evidence without duplicate revision or notification");
Check(apply.Notifications.Single().EventId==applied.DecisionId && apply.Notifications.Single().ResourceId==submitted.Id && apply.Notifications.Single().TemplateCode==NotificationTemplates.Decision,"decision notification uses immutable event and safe template only");
var appliedDetail=await apply.Review.DetailAsync(apply.EmployeeId,submitted.Id,false,apply.Context,default);
Check(appliedDetail.Request.ChangesAttendance && appliedDetail.Request.Interpretation=="corrected_verified_duty", "detail derives factual effect from session and summary links");
apply.Grants.Clear();await Fails(()=>apply.Decide(submitted),"access_denied");
var overnightReview=new CorrectionFixture();overnightReview.EnableClosedDuty();var overnightRow=await overnightReview.Submit();
Check(RosterSchedule.Today(overnightReview.Proposal.RequestedTimeUtc!.Value)>overnightReview.Proposal.BusinessDate,"fixture start is next calendar day in Pakistan but same overnight duty");
overnightReview.BecomeReviewer();var overnightApplied=await overnightReview.Decide(overnightRow);
Check(overnightApplied.Request.ChangesAttendance && overnightReview.Sessions.Last().AttendanceDate==overnightReview.Proposal.BusinessDate &&
    overnightReview.Sessions.Last().StartsAtUtc==overnightReview.Proposal.RequestedTimeUtc,"valid overnight CheckIn-time correction retains original business date");
var outsideReview=new CorrectionFixture();outsideReview.EnableClosedDuty();outsideReview.Proposal=outsideReview.Proposal with {RequestedTimeUtc=outsideReview.Roster.StartsAt!.Value.AddDays(-1)};
var outsideRow=await outsideReview.Submit();outsideReview.BecomeReviewer();
await Fails(()=>outsideReview.Decide(outsideRow),"correction_checkin_outside_retained_schedule");
Check(outsideReview.Decisions.Count==0 && outsideReview.Sessions.Count==2,"out-of-window CheckIn correction leaves interpretation untouched");
var selfReview=new CorrectionFixture();selfReview.EnableValidDuty();var selfRow=await selfReview.Submit();selfReview.BecomeReviewer(true);
await Fails(()=>selfReview.Decide(selfRow),"correction_requester_cannot_decide");
var laterAssessment=new CorrectionFixture();laterAssessment.EnableValidDuty();var laterRow=await laterAssessment.Submit();
laterAssessment.Assessment=new(laterAssessment.Raw,2,PunchAssessmentOutcome.Rejected,"REJECTED",null,"synthetic",laterAssessment.Clock.Now);
laterAssessment.BecomeReviewer();await Fails(()=>laterAssessment.Decide(laterRow),"correction_source_version_conflict");
var transfer=new CorrectionFixture();transfer.EnableValidDuty();var transferRow=await transfer.Submit();transfer.AssignmentValid=false;transfer.BecomeReviewer();
await Fails(()=>transfer.Decide(transferRow),"correction_crosses_site_transfer");
var overlap=new CorrectionFixture();overlap.EnableValidDuty();var overlapRow=await overlap.Submit();overlap.HasOverlap=true;overlap.BecomeReviewer();
await Fails(()=>overlap.Decide(overlapRow),"correction_session_overlap");
var virtualOverlap=new CorrectionFixture();virtualOverlap.EnableValidDuty();var virtualRow=await virtualOverlap.Submit();
virtualOverlap.HasVirtualOverlap=true;virtualOverlap.BecomeReviewer();
await Fails(()=>virtualOverlap.Decide(virtualRow),"correction_session_overlap");
Check(virtualOverlap.Decisions.Count==0 && virtualOverlap.Sessions.Count==1,
    "ordinary correction cannot overlap approved carry-forward virtual duty or append a decision");
var laterLock=new CorrectionFixture();laterLock.EnableValidDuty();var lockRow=await laterLock.Submit();laterLock.Locked=true;laterLock.BecomeReviewer();
await Fails(()=>laterLock.Decide(lockRow),"correction_locked_period_adjustment_required");
var failedAppend=new CorrectionFixture();failedAppend.EnableValidDuty();var appendRow=await failedAppend.Submit();failedAppend.BecomeReviewer();failedAppend.FailSummary=true;
await Fails(()=>failedAppend.Decide(appendRow),"synthetic_summary_failure");
Check(failedAppend.Decisions.Count==0 && failedAppend.Sessions.Count==1 && failedAppend.Records.Single().Approval.State==ApprovalState.Pending && failedAppend.Summary.Revision==1,
    "summary failure rolls back staged decision and session in synthetic outer transaction");
var failedNotification=new CorrectionFixture();failedNotification.EnableValidDuty();var notifyRow=await failedNotification.Submit();failedNotification.BecomeReviewer();failedNotification.FailNotification=true;
await Fails(()=>failedNotification.Decide(notifyRow),"synthetic_notification_failure");
Check(failedNotification.Decisions.Count==0 && failedNotification.Sessions.Count==1 && failedNotification.Summary.Revision==1 && failedNotification.Records.Single().Approval.State==ApprovalState.Pending,
    "notification-stage failure rolls back synthetic decision/session/summary transaction");
var returned=new CorrectionFixture();returned.EnableValidDuty();var returnRow=await returned.Submit();var originalActor=returned.Actor;returned.BecomeReviewer();
var returnResult=await returned.Decide(returnRow,ApprovalState.Returned);
Check(returnResult.Request.State=="Returned" && !returnResult.Request.ChangesAttendance && returned.Sessions.Count==1,"return closes request without changing attendance");
returned.Actor=originalActor;returned.Grants=[new(CorrectionPermissions.Self,false,Guid.NewGuid(),AccessScopeKind.Self,originalActor.EmployeeId)];
var resubmission=await returned.Submit(returned.Proposal with {ResubmissionOfCorrectionId=returnRow.Id},"resubmit-key-001");
Check(resubmission.Id!=returnRow.Id && returned.Records.Count==2,"returned request resubmits as distinct immutable linked request");
var v2Review=new CorrectionFixture();var v2Row=await v2Review.Submit();v2Review.BecomeReviewer();
await Fails(()=>v2Review.Decide(v2Row),"correction_legacy_source_refresh_required");
var queue=new CorrectionFixture();queue.EnableValidDuty();var queueRow=await queue.Submit();queue.BecomeReviewer();
var queueResult=await queue.Review.QueueAsync(new(queue.Range.From,queue.Range.To,CorrectionQueueScope.All),queue.Context,default);
Check(queueResult.Items.Count==1 && !JsonSerializer.Serialize(queueResult).Contains("TotalCount"),"queue returns only scoped items and no unauthorized count");
queue.Grants=[new(CorrectionPermissions.Review,true,Guid.NewGuid(),AccessScopeKind.Site,Guid.NewGuid())];
await Fails(()=>queue.Review.QueueAsync(new(queue.Range.From,queue.Range.To,CorrectionQueueScope.All),queue.Context,default),"access_denied");
var supervisor=new CorrectionFixture();supervisor.EnableValidDuty();supervisor.BecomeReviewer();
var supervisorPreview=await supervisor.Review.SourcesAsync(supervisor.EmployeeId,supervisor.Proposal.BusinessDate,null,supervisor.Context,default);
var supervisorRow=await supervisor.Review.SubmitAsync(supervisor.EmployeeId,supervisor.Proposal with {ExpectedSourceFingerprint=supervisorPreview.SourceFingerprint},"supervisor-key-001",supervisor.Context,default);
Check(supervisor.Records.Single().Correction.RequestedBy==supervisor.Actor.AccountId.ToString() && supervisorRow.EmployeeId==supervisor.EmployeeId,"supervisor request keeps separate beneficiary and requester identity");
await Fails(()=>supervisor.Decide(supervisorRow),"correction_requester_cannot_decide");
var manualApply=new CorrectionFixture();manualApply.WithDutyProofs=true;
manualApply.Roster=manualApply.Roster with {Explanation=new("site",Guid.NewGuid(),Guid.NewGuid(),null,null,"site-only")};
manualApply.Proposal=manualApply.Proposal with {Kind=AttendanceCorrectionKind.ManualSession,RawPunchId=manualApply.Raw.Id,ExpectedAssessmentId=manualApply.Assessment.Id,
    RequestedTimeUtc=manualApply.Roster.StartsAt,RequestedEndUtc=manualApply.Roster.EndsAt};
var manualRow=await manualApply.Submit();manualApply.BecomeReviewer();var manualResult=await manualApply.Decide(manualRow);
Check(manualResult.Request.Interpretation=="hr_approved_manual_duty" && manualResult.Request.ChangesAttendance && manualApply.Sessions.Single().CheckInPunchId is null,
    "HR explicitly approves manualApply duty with no fake CheckIn raw");
Check(manualApply.Assessment.Outcome==PunchAssessmentOutcome.Rejected && manualApply.Proof is null && manualApply.Sessions.Single().CorrectionId==manualRow.Id,"failed raw and face outcome remain unchanged after manualApply interpretation");
var ineligible=new CorrectionFixture();ineligible.WithDutyProofs=true;ineligible.Roster=ineligible.Roster with {Explanation=new("site",Guid.NewGuid(),Guid.NewGuid(),null,null,"site-only")};
ineligible.Proposal=ineligible.Proposal with {Kind=AttendanceCorrectionKind.ManualSession,RawPunchId=null,ExpectedAssessmentId=null,RequestedTimeUtc=ineligible.Roster.StartsAt,RequestedEndUtc=ineligible.Roster.EndsAt};
var ineligibleRow=await ineligible.Submit();ineligible.HistoricallyEligible=false;ineligible.BecomeReviewer();
await Fails(()=>ineligible.Decide(ineligibleRow),"correction_employee_historically_ineligible");
var office = new CorrectionFixture(); office.EnableOfficeManual();
var officeSource = await office.Service.SourcesAsync(office.Proposal.BusinessDate, office.Raw.Id, office.Context, default);
Check(JsonSerializer.Serialize(officeSource).Contains("OfficeSource"), "office source preview retains real office provenance instead of a fake site");
var officeRow = await office.Submit(); office.BecomeReviewer(); var officeApplied = await office.Decide(officeRow);
Check(officeApplied.Request.ChangesAttendance && office.Sessions.Single().BranchId == office.Roster.BranchId &&
    office.Sessions.Single().OfficeRosterAssignmentId == office.Roster.AssignmentId && office.Sessions.Single().SiteId is null,
    "manual office approval creates real branch duty without a site");
Check(office.Sessions.Single().CheckInPunchId is null && office.Sessions.Single().OfficeGeofenceVersionId == office.Boundary.Id &&
    office.Sessions.Single().EmployeeAssignmentId == office.Roster.Explanation!.MembershipRecordId &&
    office.Sessions.Single().HolidayCalendarId == office.Roster.Holiday!.CalendarId && office.Assessment.Outcome == PunchAssessmentOutcome.Rejected,
    "manual office duty retains exact source IDs and never fabricates face or raw evidence");
Check(await office.Decide(officeRow) == officeApplied && office.Sessions.Count == 1, "office same-key approval replays without duplicate duty");
var officeStale = new CorrectionFixture(); officeStale.EnableOfficeManual(); var officeStaleRow = await officeStale.Submit();
officeStale.Boundary = officeStale.Boundary with { RadiusMeters = 120m }; officeStale.BecomeReviewer();
// Geometry is immutable: replacing a version requires a NEW ID. The test intentionally changes the authoritative version ID.
officeStale.Boundary = officeStale.Boundary with { Id = Guid.NewGuid() };
await Fails(() => officeStale.Decide(officeStaleRow), "correction_office_source_unavailable");
Check(officeStale.Decisions.Count == 0 && officeStale.Sessions.Count == 0, "changed office boundary never applies stale approval");
var officeMissing = new CorrectionFixture(); officeMissing.EnableOfficeManual(); officeMissing.OfficeConfigured = false;
await Fails(() => officeMissing.Submit(), "correction_office_source_unavailable");
Check(officeMissing.Files.Count == 0 && officeMissing.Records.Count == 0, "missing office boundary cannot be treated as manual exception authority");
var officeSelf = new CorrectionFixture(); officeSelf.EnableOfficeManual(); var officeSelfRow = await officeSelf.Submit(); officeSelf.BecomeReviewer(true);
await Fails(() => officeSelf.Decide(officeSelfRow), "correction_requester_cannot_decide");
var officeTransfer = new CorrectionFixture(); officeTransfer.EnableOfficeManual(); var officeTransferRow = await officeTransfer.Submit();
officeTransfer.AssignmentValid = false; officeTransfer.BecomeReviewer();
await Fails(() => officeTransfer.Decide(officeTransferRow), "correction_crosses_site_transfer");
var officeLocked = new CorrectionFixture(); officeLocked.EnableOfficeManual(); var officeLockedRow = await officeLocked.Submit();
officeLocked.Locked = true; officeLocked.BecomeReviewer(); await Fails(() => officeLocked.Decide(officeLockedRow), "correction_locked_period_adjustment_required");
var officeDuty = new CorrectionFixture(); officeDuty.EnableOfficeDuty(); var officeDutyRow = await officeDuty.Submit();
var pinnedOffice = officeDuty.Sessions.Single(); officeDuty.BecomeReviewer(); var officeDutyApproved = await officeDuty.Decide(officeDutyRow);
Check(officeDutyApproved.Request.ChangesAttendance && officeDuty.Sessions.Last().BranchId == pinnedOffice.BranchId &&
    officeDuty.Sessions.Last().OfficeGeofenceVersionId == pinnedOffice.OfficeGeofenceVersionId && officeDuty.Sessions.Last().CheckOutPunchId is null,
    "office missing checkout retains pinned opening office IDs and adds no raw checkout");
Check(officeDuty.Sessions[0].EndsAtUtc is null && officeDuty.Sessions.Last().AttendanceDate == officeDuty.Proposal.BusinessDate &&
    officeDuty.Sessions.Last().EmployeeAssignmentId == pinnedOffice.EmployeeAssignmentId, "overnight office correction retains original business date and membership");
var officeReturn = new CorrectionFixture(); officeReturn.EnableOfficeManual(); var officeReturnRow = await officeReturn.Submit(); officeReturn.BecomeReviewer();
Check((await officeReturn.Decide(officeReturnRow, ApprovalState.Returned)).Request.State == "Returned" && officeReturn.Sessions.Count == 0,
    "office return does not mutate factual duty");
var officeReject = new CorrectionFixture(); officeReject.EnableOfficeManual(); var officeRejectRow = await officeReject.Submit(); officeReject.BecomeReviewer();
Check((await officeReject.Decide(officeRejectRow, ApprovalState.Rejected)).Request.State == "Rejected" && officeReject.Sessions.Count == 0,
    "office rejection does not mutate factual duty");
var officeNoMfa = new CorrectionFixture(); officeNoMfa.EnableOfficeManual(); var officeNoMfaRow = await officeNoMfa.Submit(); officeNoMfa.BecomeReviewer();
officeNoMfa.Actor = officeNoMfa.Actor with { MfaSatisfied = false };
await Fails(() => officeNoMfa.Decide(officeNoMfaRow), "access_denied");
var alternativeHr = new CorrectionFixture(); alternativeHr.EnableOfficeManual();
alternativeHr.Proposal = alternativeHr.Proposal with { AlternativeKind = CorrectionAlternativeKind.Accessibility, ExpectedAlternativePolicyId = Guid.NewGuid() };
var alternativeHrRow = await alternativeHr.Submit(); alternativeHr.BecomeReviewer();
await Fails(() => alternativeHr.Review.DecideAsync(alternativeHr.EmployeeId, alternativeHrRow.Id,
    new(alternativeHrRow.Version, ApprovalState.Approved, "Synthetic review", CorrectionAlternativeKind.Accessibility), "alternative-hr-decision", alternativeHr.Context, default), "access_denied");
foreach (var kind in Enum.GetValues<CorrectionAlternativeKind>())
{
    var alt = new CorrectionFixture(); alt.EnableOfficeManual();
    alt.Proposal = alt.Proposal with { AlternativeKind = kind, ExpectedAlternativePolicyId = alt.AlternativePolicyId };
    var altRow = await alt.Submit(); alt.BecomeAlternativeAdmin();
    var decision = new DecideCorrectionRequest(altRow.Version, ApprovalState.Approved, "Synthetic Admin checked exact documented case", kind);
    var accepted = await alt.Review.DecideAsync(alt.EmployeeId, altRow.Id, decision, "alternative-admin-key", alt.Context, default);
    Check(accepted.Request.ChangesAttendance && alt.Sessions.Single().CheckInPunchId is null && alt.Assessment.Outcome == PunchAssessmentOutcome.Rejected,
        "Admin alternative " + kind + " records corrected duty, not successful raw face");
    Check(await alt.Review.DecideAsync(alt.EmployeeId, altRow.Id, decision, "alternative-admin-key", alt.Context, default) == accepted && alt.Decisions.Count == 1,
        "Admin alternative receipt replay is duplicate safe for " + kind);
    var retainedProof = await alt.AlternativeApprovals.ReadAsync(alt.EmployeeId, altRow.Id, accepted.DecisionId, default);
    Check(retainedProof is { IsAuthorizedStagedApplication: false, Audit: not null } && retainedProof.AuthorityAtUtc == alt.Decisions.Single().DecidedAt,
        "real authority writer/reader binds the actual lowercase outer audit and decision-time authority for " + kind);
    Check(alt.StagedProofs == 1 && alt.AuthoritySnapshot().Permissions.Single().AuthorityTimingKnown &&
        alt.AuthoritySnapshot().Permissions.Single().ValidFromUtc.HasValue && alt.Sessions.Single().CalculatedAtUtc == alt.Decisions.Single().DecidedAt,
        "trusted staged proof and explicit persisted permission intervals for " + kind);
    alt.IsFixedAdmin = false;
    await Fails(() => alt.Review.DecideAsync(alt.EmployeeId, altRow.Id, decision, "alternative-admin-key", alt.Context, default), "access_denied");
    alt.Current = false; alt.Actor = alt.Actor with { MfaSatisfied = false }; alt.Grants.Clear(); alt.AlternativePolicyCurrent = false;
    alt.Clock.Now = alt.Clock.Now.AddMonths(1);
    Check(await alt.AlternativeApprovals.ReadAsync(alt.EmployeeId, altRow.Id, accepted.DecisionId, default) is { IsAuthorizedStagedApplication: false },
        "revoked current role/session/policy cannot rewrite retained approved history for " + kind);
    alt.Audits.RemoveAll(x => x.EventType == "attendance.correction.decide");
    Check(await alt.AlternativeApprovals.ReadAsync(alt.EmployeeId, altRow.Id, accepted.DecisionId, default) is null,
        "missing outer audit cannot recreate disposed trusted staged scope for " + kind);
}
foreach (var kind in Enum.GetValues<CorrectionAlternativeKind>())
{
    var site = new CorrectionFixture(); site.EnableSiteManual();
    site.Proposal = site.Proposal with { AlternativeKind = kind, ExpectedAlternativePolicyId = site.AlternativePolicyId };
    var row = await site.Submit(); site.BecomeAlternativeAdmin();
    var decision = await site.Review.DecideAsync(site.EmployeeId, row.Id,
        new(row.Version, ApprovalState.Approved, "Synthetic Site authority", kind), "site-authority-writer", site.Context, default);
    Check(await site.AlternativeApprovals.ReadAsync(site.EmployeeId, row.Id, decision.DecisionId, default) is { IsAuthorizedStagedApplication: false } &&
        site.Sessions.Single().SiteId == site.Roster.SiteId && site.Sessions.Single().BranchId is null && site.StagedProofs == 1,
        "actual Site schema5 authority writer/reader preserves Site identity for " + kind);
}
var wrongCase = new CorrectionFixture(); wrongCase.EnableOfficeManual();
wrongCase.Proposal = wrongCase.Proposal with { AlternativeKind = CorrectionAlternativeKind.Medical, ExpectedAlternativePolicyId = wrongCase.AlternativePolicyId };
var wrongCaseRow = await wrongCase.Submit(); wrongCase.BecomeAlternativeAdmin();
await Fails(() => wrongCase.Review.DecideAsync(wrongCase.EmployeeId, wrongCaseRow.Id,
    new(wrongCaseRow.Version, ApprovalState.Approved, "Wrong case", CorrectionAlternativeKind.Accessibility), "wrong-alternative-case", wrongCase.Context, default), "alternative_decision_kind_required");
var changedAltPolicy = new CorrectionFixture(); changedAltPolicy.EnableOfficeManual();
changedAltPolicy.Proposal = changedAltPolicy.Proposal with { AlternativeKind = CorrectionAlternativeKind.Medical, ExpectedAlternativePolicyId = changedAltPolicy.AlternativePolicyId };
var changedAltRow = await changedAltPolicy.Submit(); changedAltPolicy.AlternativePolicyCurrent = false; changedAltPolicy.BecomeAlternativeAdmin();
await Fails(() => changedAltPolicy.Review.DecideAsync(changedAltPolicy.EmployeeId, changedAltRow.Id,
    new(changedAltRow.Version, ApprovalState.Approved, "Synthetic review", CorrectionAlternativeKind.Medical), "changed-alternative-policy", changedAltPolicy.Context, default), "alternative_policy_version_conflict");
Check(changedAltPolicy.Sessions.Count == 0 && changedAltPolicy.Decisions.Count == 0, "changed configured alternative policy blocks approval atomically");
var outsideHr = new CorrectionFixture(); outsideHr.EnableOfficeManual();
outsideHr.Assessment = new(outsideHr.Raw, 2, PunchAssessmentOutcome.QueuedForReview, "OUTSIDE_GEOFENCE", null, "synthetic", outsideHr.Clock.Now, insideGeofence: false);
outsideHr.Proposal = outsideHr.Proposal with { ExpectedAssessmentId = outsideHr.Assessment.Id };
var outsideHrRow = await outsideHr.Submit(); outsideHr.BecomeReviewer();
await Fails(() => outsideHr.Decide(outsideHrRow), "out_of_boundary_admin_alternative_required");
var altQueue = new CorrectionFixture(); altQueue.EnableOfficeManual();
altQueue.Proposal = altQueue.Proposal with { AlternativeKind = CorrectionAlternativeKind.Medical, ExpectedAlternativePolicyId = altQueue.AlternativePolicyId };
var altQueueRow = await altQueue.Submit(); altQueue.BecomeReviewer();
Check((await altQueue.Review.QueueAsync(new(altQueue.Range.From,altQueue.Range.To,CorrectionQueueScope.All),altQueue.Context,default)).Items.Count==0,
    "ordinary HR queue does not return Admin alternatives");
await Fails(()=>altQueue.Review.DetailAsync(altQueue.EmployeeId,altQueueRow.Id,false,altQueue.Context,default),"access_denied");
altQueue.BecomeAlternativeAdmin();
Check((await altQueue.Review.AlternativeQueueAsync(new(altQueue.Range.From,altQueue.Range.To,CorrectionQueueScope.All),altQueue.Context,default)).Items.Single().AlternativeKind==CorrectionAlternativeKind.Medical,
    "dedicated fixed Admin queue returns exact alternative case");
Check((await altQueue.Review.AlternativeDetailAsync(altQueue.EmployeeId,altQueueRow.Id,altQueue.Context,default)).AlternativeEvidence?.Policy.Id==altQueue.AlternativePolicyId,
    "dedicated Admin detail retains protected configured requirements");
var altFailure = new CorrectionFixture(); altFailure.EnableOfficeManual();
altFailure.Proposal = altFailure.Proposal with { AlternativeKind = CorrectionAlternativeKind.Accessibility, ExpectedAlternativePolicyId = altFailure.AlternativePolicyId };
var altFailureRow = await altFailure.Submit(); altFailure.BecomeAlternativeAdmin(); altFailure.FailNotification=true;
await Fails(()=>altFailure.Review.DecideAsync(altFailure.EmployeeId,altFailureRow.Id,
    new(altFailureRow.Version,ApprovalState.Approved,"Synthetic Admin review",CorrectionAlternativeKind.Accessibility),"alternative-failed-outbox",altFailure.Context,default),"synthetic_notification_failure");
Check(altFailure.Decisions.Count==0 && altFailure.Sessions.Count==0 && altFailure.Records.Single().Approval.State==ApprovalState.Pending,
    "alternative decision/session/summary failure rolls back synthetic transaction");
Check(altFailure.Audits.All(x => x.EventType != AlternativeCorrectionApprovalJournal.AuthorityOperation),
    "authority audit rolls back with failed business operation");
altFailure.FailNotification = false;
Check((await altFailure.Review.DecideAsync(altFailure.EmployeeId,altFailureRow.Id,
    new(altFailureRow.Version,ApprovalState.Approved,"Synthetic Admin review",CorrectionAlternativeKind.Accessibility),"alternative-failed-outbox",altFailure.Context,default)).Request.ChangesAttendance &&
    altFailure.Audits.Count(x => x.EventType == AlternativeCorrectionApprovalJournal.AuthorityOperation) == 1,
    "rollback clears the stage lease and same-key retry writes exactly one authority audit");
var altScoped = new CorrectionFixture(); altScoped.EnableOfficeManual();
altScoped.Proposal = altScoped.Proposal with { AlternativeKind = CorrectionAlternativeKind.Medical, ExpectedAlternativePolicyId = altScoped.AlternativePolicyId };
var altScopedRow=await altScoped.Submit();altScoped.BecomeAlternativeAdmin();
altScoped.Grants=[new(CorrectionPermissions.AlternativesDecide,true,Guid.NewGuid(),AccessScopeKind.Site,Guid.NewGuid())];
await Fails(()=>altScoped.Review.DecideAsync(altScoped.EmployeeId,altScopedRow.Id,new(altScopedRow.Version,ApprovalState.Approved,"Wrong scope",CorrectionAlternativeKind.Medical),
    "alternative-scoped-denial",altScoped.Context,default),"access_denied");
var freshTime = new CorrectionFixture(); freshTime.EnableOfficeManual();
var lastSessionExpiry = new CorrectionFixture(); lastSessionExpiry.EnableOfficeManual();
lastSessionExpiry.Proposal = lastSessionExpiry.Proposal with { AlternativeKind = CorrectionAlternativeKind.Medical, ExpectedAlternativePolicyId = lastSessionExpiry.AlternativePolicyId };
var lastSessionRow = await lastSessionExpiry.Submit(); lastSessionExpiry.BecomeAlternativeAdmin();
var expiresAt = lastSessionExpiry.Clock.Now.AddHours(1);
lastSessionExpiry.Grants = [lastSessionExpiry.Grants.Single(x => x.Permission == CorrectionPermissions.AlternativesDecide) with { ValidUntilUtc = expiresAt }];
lastSessionExpiry.BeforeStagedRead = () => { lastSessionExpiry.Clock.Now = expiresAt.AddTicks(-1); lastSessionExpiry.SessionCalls = 0; lastSessionExpiry.AdvanceOnFinalSession = expiresAt; };
await Fails(() => lastSessionExpiry.Review.DecideAsync(lastSessionExpiry.EmployeeId, lastSessionRow.Id,
    new(lastSessionRow.Version, ApprovalState.Approved, "Expiry during final session read", CorrectionAlternativeKind.Medical), "final-session-expiry", lastSessionExpiry.Context, default),
    "attendance_alternative_approval_unavailable");
freshTime.Proposal = freshTime.Proposal with { AlternativeKind = CorrectionAlternativeKind.Medical, ExpectedAlternativePolicyId = freshTime.AlternativePolicyId };
var freshRow = await freshTime.Submit(); freshTime.BecomeAlternativeAdmin();
freshTime.Clock.Now = freshTime.Clock.Now.AddMinutes(1);
freshTime.Grants = [freshTime.Grants.Single(x => x.Permission == CorrectionPermissions.AlternativesDecide) with { ValidFromUtc = freshTime.Clock.Now.AddSeconds(1) }];
freshTime.AccessReadAdvance = TimeSpan.FromSeconds(2);
var freshResult = await freshTime.Review.DecideAsync(freshTime.EmployeeId, freshRow.Id,
    new(freshRow.Version, ApprovalState.Approved, "New permission must precede decision", CorrectionAlternativeKind.Medical), "fresh-decision-time", freshTime.Context, default);
Check(freshTime.Decisions.Single().DecidedAt >= freshTime.Grants.Single().ValidFromUtc &&
    freshTime.Sessions.Single().CalculatedAtUtc == freshTime.Decisions.Single().DecidedAt &&
    await freshTime.AlternativeApprovals.ReadAsync(freshTime.EmployeeId, freshRow.Id, freshResult.DecisionId, default) is not null,
    "special flow refreshes decision and session time consistently after authorization awaits, never before a new grant");
async Task<(CorrectionFixture Fixture, CorrectionResponse Row, CorrectionDecisionResponse Decision)> CommittedAlternative()
{
    var value = new CorrectionFixture(); value.EnableOfficeManual();
    value.Proposal = value.Proposal with { AlternativeKind = CorrectionAlternativeKind.Accessibility, ExpectedAlternativePolicyId = value.AlternativePolicyId };
    var row = await value.Submit(); value.BecomeAlternativeAdmin();
    var decision = await value.Review.DecideAsync(value.EmployeeId, row.Id,
        new(row.Version, ApprovalState.Approved, "Synthetic authority snapshot", CorrectionAlternativeKind.Accessibility), "authority-snapshot-check", value.Context, default);
    return (value, row, decision);
}
foreach (var mutate in new Func<AlternativeCorrectionAuthoritySnapshot, AlternativeCorrectionAuthoritySnapshot>[] {
    s => s with { SchemaVersion = 0 }, s => s with { EmployeeId = Guid.NewGuid() }, s => s with { AppliedSessionId = Guid.NewGuid() },
    s => s with { SourceEvidenceReference = "unrelated" }, s => s with { SourceEvidenceDigest = new('F',64) },
    s => s with { DecidedAtUtc = s.DecidedAtUtc.ToOffset(TimeSpan.FromHours(1)) },
    s => s with { VerifiedAtUtc = s.VerifiedAtUtc.ToOffset(TimeSpan.FromHours(1)) },
    s => s with { Actor = s.Actor with { AccountId = Guid.NewGuid() } }, s => s with { Actor = s.Actor with { MfaSatisfied = false } },
    s => s with { Actor = s.Actor with { EmployeeId = s.EmployeeId } }, s => s with { SystemRole = SystemRoleKind.SuperAdmin },
    s => s with { RoleAssignmentId = Guid.NewGuid() }, s => s with { AuthorizationScopes = [] },
    s => s with { Permissions = [s.Permissions[0] with { AuthorityTimingKnown = false }] },
    s => s with { Permissions = [s.Permissions[0] with { ValidFromUtc = null }] },
    s => s with { Permissions = [s.Permissions[0] with { ValidUntilUtc = s.DecidedAtUtc }] },
    s => s with { Permissions = [s.Permissions[0] with { ValidFromUtc = s.DecidedAtUtc.AddTicks(1) }] },
    s => s with { Permissions = [s.Permissions[0] with { ValidFromUtc = s.Permissions[0].ValidFromUtc!.Value.ToOffset(TimeSpan.FromHours(1)) }] },
    s => s with { AlternativePolicyId = Guid.NewGuid() }, s => s with { DocumentIds = [Guid.NewGuid()] }
}) {
    var (value, row, decision) = await CommittedAlternative(); value.ReplaceAuthority(mutate);
    Check(await value.AlternativeApprovals.ReadAsync(value.EmployeeId, row.Id, decision.DecisionId, default) is null,
        "malformed or tampered retained authority cannot reconstruct approval");
}
var (duplicateAuthority, duplicateRow, duplicateDecision) = await CommittedAlternative();
duplicateAuthority.Audits.Add(duplicateAuthority.Audits.Single(x => x.EventType == AlternativeCorrectionApprovalJournal.AuthorityOperation));
Check(await duplicateAuthority.AlternativeApprovals.ReadAsync(duplicateAuthority.EmployeeId, duplicateRow.Id, duplicateDecision.DecisionId, default) is null,
    "duplicate durable authority is unavailable, never first-row success");
foreach (var state in new[] { ApprovalState.Approved, ApprovalState.Returned, ApprovalState.Rejected })
{
    var beneficiarySelf = new CorrectionFixture(); beneficiarySelf.EnableOfficeManual();
    beneficiarySelf.Proposal = beneficiarySelf.Proposal with { AlternativeKind = CorrectionAlternativeKind.Medical, ExpectedAlternativePolicyId = beneficiarySelf.AlternativePolicyId };
    var beneficiaryAccount = beneficiarySelf.Actor.AccountId;
    var beneficiaryRow = await beneficiarySelf.SubmitBySupervisor(); beneficiarySelf.BecomeAlternativeAdmin();
    beneficiarySelf.Actor = beneficiarySelf.Actor with { AccountId = beneficiaryAccount, EmployeeId = beneficiarySelf.EmployeeId };
    await Fails(() => beneficiarySelf.Review.DecideAsync(beneficiarySelf.EmployeeId, beneficiaryRow.Id,
        new(beneficiaryRow.Version, state, "Cannot decide own beneficiary duty", CorrectionAlternativeKind.Medical), "beneficiary-self-denial", beneficiarySelf.Context, default),
        "correction_beneficiary_cannot_decide");
    Check(beneficiarySelf.Decisions.Count == 0 && beneficiarySelf.Sessions.Count == 0 && beneficiarySelf.Notifications.Count == 0 &&
        beneficiarySelf.Receipts.Count == 1 && beneficiarySelf.Records.Single().Approval.State == ApprovalState.Pending,
        "special beneficiary " + state + " is denied before mutation, even with a different requester account");
    var replay = new CorrectionFixture(); replay.EnableOfficeManual();
    replay.Proposal = replay.Proposal with { AlternativeKind = CorrectionAlternativeKind.Medical, ExpectedAlternativePolicyId = replay.AlternativePolicyId };
    var row = await replay.SubmitBySupervisor(); replay.BecomeAlternativeAdmin();
    var request = new DecideCorrectionRequest(row.Version, state, "Synthetic prior decision", CorrectionAlternativeKind.Medical);
    await replay.Review.DecideAsync(replay.EmployeeId, row.Id, request, "beneficiary-prior-receipt", replay.Context, default);
    var decisionCount = replay.Decisions.Count; var sessionCount = replay.Sessions.Count; var noticeCount = replay.Notifications.Count;
    var auditCount = replay.Audits.Count; var receiptCount = replay.Receipts.Count;
    replay.Actor = replay.Actor with { EmployeeId = replay.EmployeeId };
    await Fails(() => replay.Review.DecideAsync(replay.EmployeeId, row.Id, request, "beneficiary-prior-receipt", replay.Context, default),
        "correction_beneficiary_cannot_decide");
    Check(replay.Decisions.Count == decisionCount && replay.Sessions.Count == sessionCount && replay.Notifications.Count == noticeCount &&
        replay.Audits.Count == auditCount && replay.Receipts.Count == receiptCount,
        "prior " + state + " receipt cannot bypass beneficiary denial or append audit/outbox/duty");
}
var transactionDrift = new CorrectionFixture(); transactionDrift.EnableOfficeManual();
transactionDrift.Proposal = transactionDrift.Proposal with { AlternativeKind = CorrectionAlternativeKind.Medical, ExpectedAlternativePolicyId = transactionDrift.AlternativePolicyId };
var driftRow = await transactionDrift.Submit(); transactionDrift.BecomeAlternativeAdmin(); transactionDrift.BeforeStagedRead = transactionDrift.ChangeTransaction;
await Fails(() => transactionDrift.Review.DecideAsync(transactionDrift.EmployeeId, driftRow.Id,
    new(driftRow.Version, ApprovalState.Approved, "Different transaction cannot reuse stage", CorrectionAlternativeKind.Medical), "transaction-drift-denial", transactionDrift.Context, default),
    "attendance_alternative_approval_unavailable");
Check(transactionDrift.Decisions.Count == 0 && transactionDrift.Audits.All(x => x.EventType != AlternativeCorrectionApprovalJournal.AuthorityOperation),
    "different transaction is denied and authority audit rolls back");
foreach (var fileFailure in new[] { true, false })
{
    var value = new CorrectionFixture(); value.EnableOfficeManual();
    value.Proposal = value.Proposal with { AlternativeKind = CorrectionAlternativeKind.Medical, ExpectedAlternativePolicyId = value.AlternativePolicyId };
    var row = await value.Submit(); value.BecomeAlternativeAdmin(); value.FailAuthorityFileWrite = fileFailure; value.FailAuthorityFlush = !fileFailure;
    await Fails(() => value.Review.DecideAsync(value.EmployeeId, row.Id,
        new(row.Version, ApprovalState.Approved, "Synthetic authority failure", CorrectionAlternativeKind.Medical), "authority-write-retry", value.Context, default),
        fileFailure ? "synthetic_authority_file_failure" : "synthetic_authority_flush_failure");
    Check(value.Decisions.Count == 0 && value.Sessions.Count == 0 && value.Audits.All(x => x.EventType != AlternativeCorrectionApprovalJournal.AuthorityOperation),
        "authority file/flush failure cannot commit duty or leak a trusted stage");
    value.FailAuthorityFileWrite = false; value.FailAuthorityFlush = false;
    Check((await value.Review.DecideAsync(value.EmployeeId, row.Id,
        new(row.Version, ApprovalState.Approved, "Synthetic authority failure", CorrectionAlternativeKind.Medical), "authority-write-retry", value.Context, default)).Request.ChangesAttendance,
        "authority file/flush failure is recoverable by the same business key");
}
Console.WriteLine($"Correction intake/review service checks passed: {checks}. Synthetic IO only; no real SQL approval acceptance claimed.");

public sealed class CorrectionClock : TimeProvider
{ public DateTimeOffset Now = new(2026, 10, 9, 4, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
public class CorrectionProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, CorrectionProxy>(); ((CorrectionProxy)(object)value).Handler = handler; return value; }
}
public sealed class CorrectionFixture
{
    public static void Set(object entity, string name, object? value) => entity.GetType().GetProperty(name)!.SetValue(entity, value);
    public CorrectionClock Clock = new();
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Mobile, false);
    public bool Current = true, Locked, WithDutyProofs, AssignmentValid = true, HasOverlap, HasVirtualOverlap, FailSummary, FailNotification, HistoricallyEligible = true;
    public List<NotificationIntent> Notifications = [];
    public Guid EmployeeId; public FaceVerificationAttempt? Proof; public List<ApprovalDecision> Decisions = [];
    public CorrectionReviewService Review; public int TransactionDepth, Transactions;
    public List<PermissionGrant> Grants = [];
    public List<CorrectionRecord> Records = [];
    public List<IdempotencyReceipt> Receipts = [];
    public List<AuditEvent> Audits = [];
    public AlternativeCorrectionApprovalJournal AlternativeApprovals;
    public int StagedProofs;
    public TimeSpan SessionReadAdvance;
    public TimeSpan AccessReadAdvance;
    public Action? BeforeStagedRead;
    public DateTimeOffset? AdvanceOnFinalSession;
    public int SessionCalls;
    public bool FailAuthorityFileWrite, FailAuthorityFlush;
    private Guid? transactionId;
    public List<WorkSession> Sessions = [];
    public IReadOnlyList<AttendanceResourceScope> Scopes;
    public Dictionary<string, (string Purpose, byte[] Bytes)> Files = [];
    public RawPunchEvent Raw; public RawPunchEvent? ReviewCheckout;
    public PunchAssessment Assessment;
    public DailyAttendanceSummary Summary;
    public EffectiveRosterResponse Roster;
    public CreateCorrectionRequest Proposal;
    public CorrectionListRequest Range = new(new(2026, 10, 1), new(2026, 11, 1));
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-correction-intake");
    public AttendanceCorrectionService Service;
    public OfficeBoundaryVersionResponse Boundary = new(Guid.NewGuid(), Guid.NewGuid(), 1, new(2026, 10, 1),
        24m, 67m, 100m, 20m, "PK-SD", "SYNTHETIC TEST ONLY", "synthetic", new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
    public bool OfficeConfigured = true;
    public string RoleCode = "SyntheticSelfRole"; public bool IsFixedAdmin;
    public Guid AlternativePolicyId = Guid.NewGuid(); public bool AlternativePolicyCurrent = true;
    private long version;
    public CorrectionFixture()
    {
        EmployeeId = Actor.EmployeeId;
        Raw = new(Actor.EmployeeId, PunchAction.CheckIn, Guid.NewGuid().ToString(), "original-raw-key", new string('A', 64),
            Clock.Now.AddDays(-1), false, businessDate: new(2026, 10, 8));
        Assessment = new(Raw, 1, PunchAssessmentOutcome.Rejected, "FACE_VERIFICATION_REQUIRED", null, "synthetic-factual", Raw.ReceivedAtUtc);
        Summary = new(Actor.EmployeeId, new(2026, 10, 8), 1, AttendanceDayStatus.Unverified, null, null, "synthetic-factual",
            new string('B', 64), 480, 0, null, null, null, 1, Raw.ReceivedAtUtc.AddHours(9));
        Roster = new(Actor.EmployeeId, new(2026, 10, 8), "scheduled", RosterSchedule.TimeZoneId,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
            new DateTimeOffset(2026, 10, 8, 16, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 9, 2, 0, 0, TimeSpan.Zero));
        Scopes = [new([new(Actor.EmployeeId,SiteId:Roster.SiteId)])];
        Proposal = new(new(2026, 10, 8), AttendanceCorrectionKind.CheckInTime, Raw.Id, Assessment.Id, Summary.Id,
            Raw.ReceivedAtUtc.AddMinutes(-5), null, null, "Correct my submitted time", "My contemporaneous written explanation.");
        foreach (var code in new[] { CorrectionPermissions.Self, CorrectionPermissions.ExceptionsSelf })
            Grants.Add(new(code, false, Guid.NewGuid(), AccessScopeKind.Self, Actor.EmployeeId));
        var current = CorrectionProxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = CorrectionProxy.Make<IAccountAccessReader>((m, a) => { Clock.Now += AccessReadAdvance;
            return Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()); });
        var authorization = CorrectionProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch
        {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this,a),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>(RoleCode),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(IsFixedAdmin),
            _ => throw new Exception("Unexpected auth " + m.Name)
        });
        var authentication = CorrectionProxy.Make<IAuthenticationRepository>((m, a) => throw new Exception("Self correction must not require a registered device or face"));
        var journal = CorrectionProxy.Make<ISchedulingJournalRepository>((m, a) =>
        {
            switch (m.Name)
            {
                case "IsCurrentSessionAsync": Clock.Now += SessionReadAdvance; SessionCalls++;
                    if (SessionCalls == 2 && AdvanceOnFinalSession is DateTimeOffset expiry) Clock.Now = expiry;
                    return Task.FromResult(Current);
                case "FindReceiptAsync": return Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!));
                case "AddReceipt": Receipts.Add((IdempotencyReceipt)a[0]!); return null;
                case "AddAudit": Audits.Add((AuditEvent)a[0]!); return null;
                case "SaveChangesAsync": return FailAuthorityFlush && Audits.Any(x => x.EventType == AlternativeCorrectionApprovalJournal.AuthorityOperation)
                    ? Task.FromException(new AuthenticationFailure("synthetic_authority_flush_failure",503)) : Task.CompletedTask;
                default: throw new Exception(m.Name);
            }
        });
        var files = CorrectionProxy.Make<IPrivateFileStore>((m, a) =>
        {
            if (m.Name == "WriteAsync")
            {
                if (FailAuthorityFileWrite && ((string)a[1]!).StartsWith("attendance:alternative-authority:", StringComparison.Ordinal))
                    return Task.FromException<PrivateFileReference>(new AuthenticationFailure("synthetic_authority_file_failure",503));
                var bytes = ((byte[])a[0]!).ToArray(); var storage = Guid.NewGuid().ToString("N"); Files.Add(storage, ((string)a[1]!, bytes));
                return Task.FromResult(new PrivateFileReference(storage, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length));
            }
            var reference = (PrivateFileReference)a[0]!; var value = Files[reference.StorageKey];
            if (value.Purpose != (string)a[1]!) throw new Exception("Wrong purpose"); return Task.FromResult(value.Bytes.ToArray());
        });
        var operation = new AttendanceOperation(current, access, authorization, journal, authentication, Clock, files);
        var repository = CorrectionProxy.Make<IAttendanceCorrectionRepository>((m, a) =>
        {
            switch (m.Name)
            {
                case "JoinedOnAsync": return Task.FromResult<DateOnly?>(new(2026, 10, 1));
                case "SourceAsync":
                    var own = (Guid)a[0]! == EmployeeId && (Guid?)a[2] == Raw.Id;
                    return Task.FromResult(new CorrectionSourceState(own ? Raw : null, own ? Assessment : null, Summary, Locked,
                        Sessions.GroupBy(x=>x.SessionKey).Select(x=>x.MaxBy(y=>y.Revision)!).ToArray(),
                        WithDutyProofs ? Sessions.GroupBy(x=>x.SessionKey).Select(x=>x.MaxBy(y=>y.Revision)!).Select(x=>new CorrectionCheckInState(x.Id,Raw,Assessment,Proof)).ToArray() : null));
                case "HasPendingEquivalentAsync":
                    var proposal = (CreateCorrectionRequest)a[1]!;
                    return Task.FromResult(Records.Any(x => x.Approval.State == ApprovalState.Pending && x.Correction.Kind == proposal.Kind &&
                        x.Correction.RawPunchEventId == proposal.RawPunchId && x.Correction.RequestedTimeUtc == proposal.RequestedTimeUtc &&
                        x.Correction.RequestedEndUtc == proposal.RequestedEndUtc && x.Correction.RequestedStatus == proposal.RequestedStatus));
                case "FindOwnAsync": return Task.FromResult(Records.SingleOrDefault(x => x.Correction.EmployeeId == (Guid)a[0]! &&
                    x.Approval.RequesterUserId == ((Guid)a[1]!).ToString() && x.Correction.Id == (Guid)a[2]!));
                case "ListOwnAsync": return Task.FromResult(new PagedResult<CorrectionRecord>(Records.ToArray(), Records.Count, 1, 25));
                case "ExceptionsAsync": return Task.FromResult(new PagedResult<AttendanceException>([new(Actor.EmployeeId, new(2026, 10, 8),
                    AttendanceExceptionKind.SuspectedFalseEvidence, "confidential reviewer-only reason", Clock.Now)], 1, 1, 25));
                case "Add": Records.Add(new((AttendanceCorrection)a[1]!, (ApprovalRequest)a[0]!)); return null;
                case "ExpectVersion": return null;
                case "SaveChangesAsync": foreach (var record in Records) Set(record.Approval, "RowVersion", BitConverter.GetBytes(++version)); return Task.CompletedTask;
                default: throw new Exception("Unexpected correction repository " + m.Name);
            }
        });
        var rosters = CorrectionProxy.Make<IIndividualRosterResolver>((m, a) => Task.FromResult(Roster));
        var summaries = CorrectionProxy.Make<IAttendanceSummaryRepository>((m,a)=>m.Name switch
        {
            "ResourceScopesAsync"=>Task.FromResult(Scopes),
            "IsLockedAsync"=>Task.FromResult(Locked),
            "LatestAsync"=>Task.FromResult<DailyAttendanceSummary?>(Summary),
            "SessionsAsync"=>Task.FromResult<IReadOnlyList<WorkSession>>(Sessions.GroupBy(x=>x.SessionKey).Select(x=>x.MaxBy(y=>y.Revision)!).ToArray()),
            "ExceptionCountAsync"=>Task.FromResult(0),
            "Add"=>AppendSummary((DailyAttendanceSummary)a[0]!),
            _=>throw new Exception("Unexpected summary scope call "+m.Name)
        });
        var offices = CorrectionProxy.Make<IOfficeAttendanceSourceReader>((m,a) => m.Name == "ReadAsync"
            ? Task.FromResult(Roster.BranchId.HasValue ? OfficeConfigured ? new OfficeAttendanceContext("configured", OfficeAttendanceSource.Capture(EmployeeId,
                Proposal.BusinessDate, Roster, Boundary), Boundary) : new OfficeAttendanceContext("office_boundary_not_configured", null, null)
                : new OfficeAttendanceContext("not_office", null, null))
            : Task.FromResult(AssignmentValid));
        var alternatives = CorrectionProxy.Make<ICorrectionAlternativeEvidenceReader>((m,a) =>
        {
            if (m.Name == "RevalidateAsync") return AlternativePolicyCurrent ? Task.CompletedTask : Task.FromException(new AuthenticationFailure("alternative_policy_version_conflict",409));
            var policy = new CorrectionAlternativePolicy((Guid)a[3]!, (CorrectionAlternativeKind)a[2]!, 1, new(2026,10,1), false, [],
                "SYNTHETIC TEST APPROVAL", Actor.AccountId.ToString(), Clock.Now, "");
            policy = policy with { Fingerprint = CorrectionAlternativePolicyService.PolicyFingerprint(policy) };
            return Task.FromResult(new CorrectionAlternativeEvidence(policy, []));
        });
        Service = new(operation, repository, files, rosters, summaries, offices, alternatives);
        var review=CorrectionProxy.Make<ICorrectionReviewRepository>((m,a)=>m.Name switch
        {
            "FindAsync"=>Task.FromResult(Records.SingleOrDefault(x=>x.Correction.EmployeeId==(Guid)a[0]! && x.Correction.Id==(Guid)a[1]!) is {} row ?
                row with {AppliedSessionRevisionId=Sessions.SingleOrDefault(x=>x.CorrectionId==row.Correction.Id)?.Id,
                    AppliedSummaryRevisionId=Sessions.Any(x=>x.CorrectionId==row.Correction.Id)&&Summary.Revision>1 ? Summary.Id:null}:null),
            "PunchAsync"=>Task.FromResult<RawPunchEvent?>((Guid)a[1]! == Raw.Id?Raw:ReviewCheckout?.Id==(Guid)a[1]!?ReviewCheckout:null),
            "PreservesAssignmentAsync"=>Task.FromResult(AssignmentValid),
            "HistoricallyEligibleAsync"=>Task.FromResult(HistoricallyEligible),
            "OverlapsAsync"=>Task.FromResult(HasOverlap),
            "ScopeExistsAsync"=>Task.FromResult(true),
            "CandidatesAsync"=>Task.FromResult<IReadOnlyList<CorrectionRecord>>(Records.Where(x=>x.Approval.State==((CorrectionQueueRequest)a[0]!).State &&
                (a[4] is null || x.Correction.RequestedBy==((Guid)a[4]!).ToString())).ToArray()),
            "Add"=>AppendDecision((ApprovalDecision)a[0]!, (WorkSession?)a[1]),
            _=>throw new Exception("Unexpected review repository "+m.Name)
        });
        var cursor=CorrectionProxy.Make<IAttendanceCursorProtector>((m,a)=>m.Name=="Protect"?"synthetic_cursor":throw new Exception("Unexpected cursor"));
        var notifications=CorrectionProxy.Make<INotificationOutbox>((m,a)=> {
            if(FailNotification)throw new AuthenticationFailure("synthetic_notification_failure",503);
            Notifications.Add((NotificationIntent)a[0]!);return Task.FromResult(true);
        });
        var dutyOverlap=CorrectionProxy.Make<ICarryForwardDutyOverlap>((m,a)=>m.Name=="HasOverlapAsync"
            ? Task.FromResult(HasOverlap || HasVirtualOverlap) : throw new Exception("Unexpected duty overlap call"));
        var authorityRepository = CorrectionProxy.Make<IAlternativeCorrectionApprovalRepository>((m,a) => m.Name switch {
            "get_TransactionId" => transactionId,
            "SourceAsync" => Task.FromResult(AuthoritySource((Guid)a[0]!, (Guid)a[1]!, (Guid)a[2]!)),
            "AuthorityAuditsAsync" => Task.FromResult<IReadOnlyList<AuditEvent>>(Audits.Where(x => x.EventType == AlternativeCorrectionApprovalJournal.AuthorityOperation &&
                x.EmployeeScopeId == (Guid)a[0]! && x.TargetId == ((Guid)a[1]!).ToString()).Take(2).ToArray()),
            "DecisionAuditsAsync" => Task.FromResult<IReadOnlyList<AuditEvent>>(Audits.Where(x => x.EventType == "attendance.correction.decide" &&
                x.EmployeeScopeId == (Guid)a[0]! && x.TargetId == ((Guid)a[1]!).ToString()).Take(2).ToArray()),
            _ => throw new Exception("Unexpected authority repository " + m.Name)
        });
        AlternativeApprovals = new(authorityRepository, current, access, authorization, journal, summaries, operation, files, Clock);
        var calculation = CorrectionProxy.Make<IAttendancePolicyCalculator>((m,a) => Calculate((Guid)a[0]!, (DateOnly)a[1]!, (FactualAttendance)a[2]!, (EffectiveRosterResponse)a[3]!, (CancellationToken)a[4]!));
        Review=new(operation,repository,review,Service,summaries,new AttendanceSummaryService(operation,summaries,rosters,calculation,files),rosters,access,cursor,notifications,dutyOverlap,offices,alternatives,authorization,Clock,AlternativeApprovals);
    }
    private async Task<AttendancePolicyResult> Calculate(Guid employee, DateOnly date, FactualAttendance facts, EffectiveRosterResponse roster, CancellationToken ct)
    {
        if (Proposal.AlternativeKind.HasValue) {
            BeforeStagedRead?.Invoke();
            var decision = Decisions.Single(); var correction = Records.Single().Correction;
            if (await AlternativeApprovals.ReadAsync(employee, correction.Id, decision.Id, ct) is not { IsAuthorizedStagedApplication: true, Audit: null })
                throw AttendanceOperation.Conflict("attendance_alternative_approval_unavailable");
            StagedProofs++;
        }
        return await new UnavailableAttendancePolicyCalculator().CalculateAsync(employee, date, facts, roster, ct);
    }
    private AlternativeCorrectionApprovalSource? AuthoritySource(Guid employee, Guid correction, Guid decision)
    {
        var row = Records.SingleOrDefault(x => x.Correction.EmployeeId == employee && x.Correction.Id == correction);
        var d = Decisions.SingleOrDefault(x => x.Id == decision && x.ApprovalRequestId == row?.Approval.Id);
        var sessions = Sessions.Where(x => x.EmployeeId == employee && x.CorrectionId == correction).ToArray();
        return row is not null && d is not null && sessions.Length == 1 && Decisions.Count(x => x.ApprovalRequestId == row.Approval.Id) == 1
            ? new(row.Correction, row.Approval, d, sessions[0]) : null;
    }
    public AlternativeCorrectionAuthoritySnapshot AuthoritySnapshot()
    {
        var audit = Audits.Single(x => x.EventType == AlternativeCorrectionApprovalJournal.AuthorityOperation);
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(audit.ProtectedChangesReference!)!;
        return JsonSerializer.Deserialize<AlternativeCorrectionAuthoritySnapshot>(Files[reference.StorageKey].Bytes)!;
    }
    public void ChangeTransaction() => transactionId = Guid.NewGuid();
    public void ReplaceAuthority(Func<AlternativeCorrectionAuthoritySnapshot, AlternativeCorrectionAuthoritySnapshot> change)
    {
        var audit = Audits.Single(x => x.EventType == AlternativeCorrectionApprovalJournal.AuthorityOperation);
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(audit.ProtectedChangesReference!)!;
        var value = Files[reference.StorageKey]; Files[reference.StorageKey] = (value.Purpose, JsonSerializer.SerializeToUtf8Bytes(change(AuthoritySnapshot())));
    }
    private object? AppendSummary(DailyAttendanceSummary summary) {if(FailSummary)throw new AuthenticationFailure("synthetic_summary_failure",503); Summary=summary;return null;}
    private object? AppendDecision(ApprovalDecision decision,WorkSession? session) {Decisions.Add(decision);if(session is not null)Sessions.Add(session);return null;}
    public async Task<T> Transaction<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct)
    {
        if(TransactionDepth!=0)throw new Exception("Nested correction transaction");
        Transactions++;TransactionDepth++;transactionId = Guid.NewGuid();
        var states=Records.Select(x=>(x.Approval,x.Approval.State,x.Approval.ClosedAt,x.Approval.RowVersion)).ToArray();
        var sessionCount=Sessions.Count;var decisionCount=Decisions.Count;var receiptCount=Receipts.Count;var notificationCount=Notifications.Count;var auditCount=Audits.Count;var summary=Summary;
        try{return await action(ct);}catch{
            foreach(var state in states){Set(state.Approval,"State",state.State);Set(state.Approval,"ClosedAt",state.ClosedAt);Set(state.Approval,"RowVersion",state.RowVersion);}
            Sessions.RemoveRange(sessionCount,Sessions.Count-sessionCount);Decisions.RemoveRange(decisionCount,Decisions.Count-decisionCount);
            Receipts.RemoveRange(receiptCount,Receipts.Count-receiptCount);Notifications.RemoveRange(notificationCount,Notifications.Count-notificationCount);
            Audits.RemoveRange(auditCount,Audits.Count-auditCount);Summary=summary;throw;
        }finally{TransactionDepth--;transactionId = null;}
    }
    public void EnableValidDuty()
    {
        WithDutyProofs=true;
        Set(Raw,"ReceivedAtUtc",Roster.StartsAt!.Value);
        Set(Raw,"SiteId",Roster.SiteId);Set(Raw,"ShiftVersionId",Roster.ShiftVersionId);Set(Raw,"EmployeeAssignmentId",Guid.NewGuid());
        Proof=(FaceVerificationAttempt)Activator.CreateInstance(typeof(FaceVerificationAttempt),true)!;
        Set(Proof,"Id",Guid.NewGuid());Set(Proof,"EmployeeId",EmployeeId);Set(Proof,"Outcome",FaceVerificationOutcome.Passed);
        Set(Proof,"LivenessPassed",true);Set(Proof,"MatchPassed",true);Set(Proof,"ConsumedByPunchId",Raw.Id);Set(Proof,"ConsumedAtUtc",Raw.ReceivedAtUtc);
        Set(Proof,"CompletedAtUtc",Raw.ReceivedAtUtc);
        Set(Proof,"RegisteredDeviceId",Raw.RegisteredDeviceId);Set(Proof,"PunchIdempotencyKey",Raw.IdempotencyKey);
        Assessment=new(Raw,1,PunchAssessmentOutcome.Accepted,"ACCEPTED",null,"synthetic",Raw.ReceivedAtUtc,Proof);
        Sessions.Add(new(Guid.NewGuid(),1,EmployeeId,Proposal.BusinessDate,Raw.ReceivedAtUtc,null,Roster.ShiftVersionId!.Value,null,Raw.ReceivedAtUtc,Raw,siteId:Roster.SiteId));
        Proposal=Proposal with {Kind=AttendanceCorrectionKind.CheckOutTime,RawPunchId=null,ExpectedAssessmentId=null,TargetWorkSessionId=Sessions[0].Id,RequestedTimeUtc=Raw.ReceivedAtUtc.AddHours(8)};
    }
    public void EnableOfficeManual()
    {
        WithDutyProofs = true;
        Roster = Roster with { SiteId = null, BranchId = Boundary.BranchId,
            Explanation = new("office", null, Guid.NewGuid(), null, null, "office history") { OfficeRosterAssignmentId = Roster.AssignmentId },
            Holiday = new("configured", Proposal.BusinessDate, Guid.NewGuid(), "SYNTHETIC", 1, []) };
        Set(Raw, "BranchId", Roster.BranchId); Set(Raw, "OfficeRosterAssignmentId", Roster.AssignmentId);
        Set(Raw, "SiteId", null);
        Set(Raw, "EmployeeAssignmentId", Roster.Explanation.MembershipRecordId); Set(Raw, "OfficeGeofenceVersionId", Boundary.Id);
        Set(Raw, "HolidayCalendarId", Roster.Holiday.CalendarId); Set(Raw, "ShiftVersionId", Roster.ShiftVersionId);
        Scopes = [new([new(EmployeeId)])];
        Proposal = Proposal with { Kind = AttendanceCorrectionKind.ManualSession, RawPunchId = Raw.Id,
            ExpectedAssessmentId = Assessment.Id, RequestedTimeUtc = Roster.StartsAt, RequestedEndUtc = Roster.EndsAt };
    }
    public void EnableSiteManual()
    {
        WithDutyProofs = true;
        Roster = Roster with { Explanation = new("site", Guid.NewGuid(), Guid.NewGuid(), null, null, "synthetic site history") };
        Proposal = Proposal with { Kind = AttendanceCorrectionKind.ManualSession, RawPunchId = null, ExpectedAssessmentId = null,
            RequestedTimeUtc = Roster.StartsAt, RequestedEndUtc = Roster.EndsAt };
    }
    public void EnableOfficeDuty()
    {
        EnableValidDuty(); EnableOfficeManual(); Sessions.Clear();
        Sessions.Add(new(Guid.NewGuid(), 1, EmployeeId, Proposal.BusinessDate, Raw.ReceivedAtUtc, null,
            Roster.ShiftVersionId!.Value, null, Raw.ReceivedAtUtc, Raw, branchId: Roster.BranchId,
            officeRosterAssignmentId: Roster.AssignmentId, employeeAssignmentId: Roster.Explanation!.MembershipRecordId,
            officeGeofenceVersionId: Boundary.Id, holidayCalendarId: Roster.Holiday!.CalendarId));
        Proposal = Proposal with { Kind = AttendanceCorrectionKind.CheckOutTime, RawPunchId = null, ExpectedAssessmentId = null,
            TargetWorkSessionId = Sessions[0].Id, RequestedTimeUtc = Roster.EndsAt, RequestedEndUtc = null };
    }
    public void EnableClosedDuty()
    {
        EnableValidDuty();
        ReviewCheckout=new(EmployeeId,PunchAction.CheckOut,Raw.RegisteredDeviceId,"review-checkout-key",new string('C',64),Roster.EndsAt!.Value,false,businessDate:Proposal.BusinessDate);
        var closed=new WorkSession(Sessions[0].SessionKey,2,EmployeeId,Proposal.BusinessDate,Raw.ReceivedAtUtc,ReviewCheckout.ReceivedAtUtc,
            Roster.ShiftVersionId!.Value,null,Clock.Now,Raw,ReviewCheckout,siteId:Roster.SiteId);
        Sessions.Add(closed);
        Proposal=Proposal with {Kind=AttendanceCorrectionKind.CheckInTime,RawPunchId=Raw.Id,ExpectedAssessmentId=Assessment.Id,TargetWorkSessionId=closed.Id,
            RequestedTimeUtc=Raw.ReceivedAtUtc.AddHours(4)};
    }
    public void BecomeReviewer(bool sameRequester=false)
    {
        Actor=new(sameRequester?Actor.AccountId:Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Web,true);
        Grants.Clear();foreach(var permission in new[]{CorrectionPermissions.Submit,CorrectionPermissions.Review,CorrectionPermissions.Decide})
            Grants.Add(new(permission,true,Guid.NewGuid(),AccessScopeKind.All,null));
    }
    public void BecomeAlternativeAdmin()
    {
        BecomeReviewer(); RoleCode = "ADMIN"; IsFixedAdmin = true;
        Grants.RemoveAll(x => x.Permission == CorrectionPermissions.Decide);
        Grants.Add(new(CorrectionPermissions.AlternativesDecide, true, Guid.NewGuid(), AccessScopeKind.All, null) {
            AuthorityTimingKnown = true, ValidFromUtc = Clock.Now.AddDays(-1) });
    }
    public Task<CorrectionDecisionResponse> Decide(CorrectionResponse response,ApprovalState state=ApprovalState.Approved,string key="decision-key-001")=>
        Review.DecideAsync(EmployeeId,response.Id,new(response.Version,state,"HR checked written evidence"),key,Context,default);
    public async Task<CreateCorrectionRequest> PreviewProposal(CreateCorrectionRequest? request = null)
    {
        var value=request??Proposal;
        var preview=await Service.SourcesAsync(value.BusinessDate,value.RawPunchId,Context,default);
        return value with {ExpectedSourceFingerprint=preview.SourceFingerprint};
    }
    public async Task<CorrectionResponse> Submit(CreateCorrectionRequest? request = null, string key = "correction-key-001")
    {
        var prepared=request??Proposal;
        if(prepared.ExpectedSourceFingerprint is null) prepared=await PreviewProposal(prepared);
        if(request is null) Proposal=prepared;
        return await Service.SubmitAsync(prepared,key,Context,default);
    }
    public async Task<CorrectionResponse> SubmitBySupervisor()
    {
        Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
        RoleCode = "SyntheticSupervisor";
        Grants = [new(CorrectionPermissions.Submit, true, Guid.NewGuid(), AccessScopeKind.All, null)];
        var source = await Review.SourcesAsync(EmployeeId, Proposal.BusinessDate, Proposal.RawPunchId, Context, default);
        Proposal = Proposal with { ExpectedSourceFingerprint = source.SourceFingerprint };
        return await Review.SubmitAsync(EmployeeId, Proposal, "supervisor-correction-submit", Context, default);
    }
}
