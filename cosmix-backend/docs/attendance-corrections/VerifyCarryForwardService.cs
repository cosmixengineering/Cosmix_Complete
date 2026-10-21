#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyCarryForwardService.packages.lock.json
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.CarryForward;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Attendance.Calculation;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.Policies;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.Attendance.Calculation;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Attendance.CarryForward;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Idempotency;

var checks = 0;
void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
async Task Fails(Func<Task> action, string code)
{ checks++; try { await action(); } catch(AuthenticationFailure ex) when(ex.Code == code) { return; } throw new Exception("Expected " + code); }
var f = new Fixture(); var source = await f.Preview();
Check(source.SourceSummaryId == f.Frozen.Summary.Id && source.SourceLockEventId == f.Frozen.Lock.Event!.Id, "preview binds actual frozen lock membership");
Check(source.Sessions.Single().SourceWorkSessionId == f.Original.Id && source.CurrentWorkedMinutes == 480 && source.TargetMonthStart == new DateOnly(2026,10,1), "pinned session and first next unlocked calendar month");
var proposal = await f.Proposal(); var pending = await f.Submit(proposal);
Check(pending.State == "Pending" && pending.RoutingStatus == "one_hr_reviewer" && f.Adjustments.Count == 0, "submission creates real pending immutable request only");
Check(await f.Submit(proposal) == pending && f.Records.Count == 1, "retry replays receipt without duplicate proposal");
var detail = await f.Service.DetailAsync(null,pending.Id,true,false,f.Context,default);
Check(detail.Source.SourceFingerprint == source.SourceFingerprint && detail.WrittenEvidence == proposal.WrittenEvidence, "exact protected source and written evidence retained");
await Fails(()=>f.Submit(proposal with { RequestedTimeUtc = proposal.RequestedTimeUtc.AddMinutes(1) }), "idempotency_key_reused");
await Fails(()=>f.Submit(proposal,"different-request-key"),"equivalent_carry_forward_pending");
var originalSummary = f.Frozen.Summary.Id; var sourceLock = f.Frozen.Lock.Event!.Id;
f.Reviewer(); var approved = await f.Decide(pending);
Check(approved.AppliedAdjustmentId.HasValue && approved.DeltaWorkedTicks == TimeSpan.FromHours(1).Ticks && approved.DeltaWorkedMinutes == 60, "approval appends factual one-hour delta");
Check(f.Decisions.Count == 1 && f.Adjustments.Count == 1 && f.Notifications.Count == 1 && f.Original.EndsAtUtc == f.OriginalEnd, "single atomic decision/ledger/outbox and no raw/session rewrite");
Check(f.Frozen.Summary.Id == originalSummary && f.Frozen.Lock.Event!.Id == sourceLock && f.Frozen.Lock.Period.IsLocked, "source lock and source summary untouched");
Check(await f.Decide(pending) == approved && f.Adjustments.Count == 1, "decision replay ignores stale input version without extra ledger");
var monthly = await f.Service.MonthAsync(f.Employee,new(new(2026,10,1)),false,f.Context,default);
Check(monthly.TotalDeltaWorkedTicks == TimeSpan.FromHours(1).Ticks && monthly.TotalDeltaWorkedMinutes == 60 && monthly.FinancialState == "pending_policy", "target month includes factual posting without financial amounts");
var retained = await f.Evidence.AdjustmentAsync(f.Adjustments[0],default);
Check(retained.FrozenSessionIds.Single() == f.Original.Id && retained.Sessions[0].EndsAtUtc == f.OriginalEnd.AddHours(1), "ledger evidence validates original exact membership and virtual outcome");
var lockRows = await new CarryForwardLockReader(f.Evidence).ReadForLockAsync(new(2026,10,1),new(2026,11,1),default);
Check(lockRows.Count == 1 && lockRows[0].AdjustmentId == f.Adjustments[0].Id, "month lock freezes posted adjustment membership");
var secondProposal = await f.Proposal(end:f.OriginalEnd.AddHours(2)); var second = await f.Submit(secondProposal,"second-proposal-key");
f.Reviewer(); var secondApproval = await f.Decide(second,"second-decision-key");
Check(secondApproval.DeltaWorkedMinutes == 60 && f.Adjustments[1].BeforeWorkedTicks == TimeSpan.FromHours(9).Ticks && f.Adjustments[1].Revision == 2, "successive adjustment delta uses last approved virtual baseline");
monthly = await f.Service.MonthAsync(f.Employee,new(new(2026,10,1),1,1),false,f.Context,default);
Check(monthly.TotalCount == 2 && monthly.Entries.Count == 1 && monthly.TotalDeltaWorkedMinutes == 120, "monthly sum includes all posted revisions before pagination");
Check((await new CarryForwardLockReader(f.Evidence).ReadForLockAsync(new(2026,10,1),new(2026,11,1),default)).Count == 2, "lock reader never takes latest only");
var thirdProposal = await f.Proposal(end:f.OriginalEnd.AddHours(1)); var third = await f.Submit(thirdProposal,"third-proposal-key"); f.Reviewer();
var thirdApproval = await f.Decide(third,"third-decision-key");
Check(thirdApproval.DeltaWorkedMinutes == -60 && thirdApproval.DeltaWorkedTicks == -TimeSpan.FromHours(1).Ticks, "negative correction is signed factual delta");

var targetRace = new Fixture(); var staleTarget = await targetRace.Submit(await targetRace.Proposal()); targetRace.Ranges.Add(new(Guid.NewGuid(),new(2026,10,15),new(2026,10,16))); targetRace.Reviewer();
await Fails(()=>targetRace.Decide(staleTarget),"carry_forward_target_month_changed");
Check(targetRace.Records[0].Approval.State == ApprovalState.Pending && targetRace.Adjustments.Count == 0, "target changed never silently reroutes or approves");
var stale = new Fixture(); var staleProposal = await stale.Proposal(); var old = await stale.Submit(staleProposal); stale.Reviewer(); await stale.Decide(old);
await Fails(()=>stale.Submit(staleProposal,"stale-prior-key"),"carry_forward_source_version_conflict");
var same = new Fixture(); var samePending = await same.Submit(await same.Proposal()); same.Reviewer(sameAccount:true);
await Fails(()=>same.Decide(samePending),"correction_requester_cannot_decide");
var version = new Fixture(); var versionPending = await version.Submit(await version.Proposal()); version.Reviewer();
await Fails(()=>version.Service.DecideAsync(version.Employee,versionPending.Id,new(Convert.ToBase64String(BitConverter.GetBytes(900L)),ApprovalState.Approved,"checked"),"bad-version-key",version.Context,default),"carry_forward_version_conflict");
var rejected = new Fixture(); var rejectPending = await rejected.Submit(await rejected.Proposal()); rejected.Reviewer();
var rejectResult = await rejected.Service.DecideAsync(rejected.Employee,rejectPending.Id,new(rejectPending.Version,ApprovalState.Rejected,"Written evidence insufficient"),"reject-key-001",rejected.Context,default);
Check(rejectResult.Request.State == "Rejected" && rejected.Adjustments.Count == 0 && rejected.Decisions.Count == 1, "rejection records decision without factual posting");
var cancelled = new Fixture(); var cancelPending = await cancelled.Submit(await cancelled.Proposal());
var cancel = await cancelled.Service.CancelAsync(null,cancelPending.Id,new(cancelPending.Version,"Withdraw incorrect proposal"),"cancel-key-001",true,cancelled.Context,default);
Check(cancel.State == "Cancelled" && cancelled.Adjustments.Count == 0, "requester cancels own pending proposal");
var auth = new Fixture(); var authPending = await auth.Submit(await auth.Proposal()); auth.Current = false;
await Fails(()=>auth.Service.DetailAsync(null,authPending.Id,true,false,auth.Context,default),"invalid_authentication");
var noGrant = new Fixture(); noGrant.Grants.Clear(); await Fails(()=>noGrant.Preview(),"access_denied");
var rollback = new Fixture(); var rollPending = await rollback.Submit(await rollback.Proposal()); rollback.Reviewer(); rollback.FailNotification = true;
await Fails(()=>rollback.Decide(rollPending),"synthetic_notification_failure");
Check(rollback.Records[0].Approval.State == ApprovalState.Pending && rollback.Decisions.Count == 0 && rollback.Adjustments.Count == 0 && rollback.Notifications.Count == 0,
    "simulated outer transaction rolls back approval and ledger on outbox failure");
var transfer = new Fixture { AssignmentValid = false }; await Fails(async()=>await transfer.Submit(await transfer.Proposal()),"correction_crosses_site_transfer");
var employment = new Fixture { Eligible = false }; await Fails(async()=>await employment.Submit(await employment.Proposal()),"correction_employee_historically_ineligible");
var ambiguous = new Fixture(); ambiguous.OtherSessions.Add(new(Guid.NewGuid(),1,ambiguous.Employee,new(2026,9,9),ambiguous.OriginalEnd,ambiguous.OriginalEnd.AddHours(2),ambiguous.Roster.ShiftVersionId!.Value,null,
    ambiguous.Clock.Now,null,correctionId:Guid.NewGuid(),siteId:ambiguous.Roster.SiteId));
await Fails(async()=>await ambiguous.Submit(await ambiguous.Proposal()),"carry_forward_session_overlap");
var manual = new Fixture(); var manualSource = await manual.Preview();
var manualProposal = (await manual.Proposal()) with { Kind=AttendanceCorrectionKind.ManualSession,TargetSessionKey=null,
    RequestedTimeUtc=manual.OriginalEnd.AddMinutes(10),RequestedEndUtc=manual.OriginalEnd.AddMinutes(40) };
var manualPending = await manual.Submit(manualProposal); manual.Reviewer(); await manual.Decide(manualPending);
var manualAfter = (await manual.Evidence.AdjustmentAsync(manual.Adjustments[0],default)).Sessions.Single(x=>x.IsManual && x.SourceWorkSessionId is null);
Check(manualAfter.CheckInPunchId is null && manualAfter.CheckOutPunchId is null && manualAfter.OriginCorrectionId == manualPending.Id, "manual virtual duty has explicit HR origin and no invented punch or face");
Check(RosterSchedule.Today(manualProposal.RequestedTimeUtc) != manualSource.SourceBusinessDate, "after midnight manual start retains original shift-start business date");
var corrupt = new Fixture(); var corruptSummaryId = corrupt.Frozen.Summary.Id;
Fixture.Set(corrupt.Frozen.Links[0],"WorkSessionId",Guid.NewGuid()); await Fails(()=>corrupt.Preview(),"carry_forward_evidence_unavailable");
var subminute = new Fixture(); var subPending = await subminute.Submit(await subminute.Proposal(end:subminute.OriginalEnd.AddTicks(-1))); subminute.Reviewer(); var subResult=await subminute.Decide(subPending);
Check(subResult.DeltaWorkedTicks == -1 && subResult.DeltaWorkedMinutes == -1, "signed subminute delta uses difference of floored totals");
var bounds = new Fixture(); await Fails(()=>bounds.Service.MonthAsync(null,new(new(2026,10,2)),true,bounds.Context,default),"invalid_carry_forward_month");
var noChange = new Fixture(); await Fails(async()=>await noChange.Submit(await noChange.Proposal(end:noChange.OriginalEnd)),"carry_forward_no_interpretation_change");
var badLink = new Fixture(); Fixture.Set(badLink.Frozen.Links[0],"EmployeeId",Guid.NewGuid());
await Fails(()=>badLink.Preview(),"carry_forward_evidence_unavailable");
var badSummaryLink = new Fixture(); Fixture.Set(badSummaryLink.Frozen.Links[0],"DailyAttendanceSummaryId",Guid.NewGuid());
await Fails(()=>badSummaryLink.Preview(),"carry_forward_evidence_unavailable");
var rawScope = new Fixture(); rawScope.Reviewer();
var rawForeignSite = new RawPunchEvent(rawScope.Employee,PunchAction.CheckIn,Guid.NewGuid().ToString(),"failed-evidence-key",new string('A',64),
    rawScope.Original.StartsAtUtc,false,businessDate:rawScope.Date,siteId:Guid.NewGuid());
rawScope.RawEvidence = new(rawForeignSite,new(rawForeignSite,1,PunchAssessmentOutcome.Rejected,"FACE_REQUIRED",null,"synthetic",rawForeignSite.ReceivedAtUtc));
rawScope.Grants.Clear();rawScope.Grants.Add(new(CorrectionPermissions.Submit,true,Guid.NewGuid(),AccessScopeKind.Site,rawScope.Roster.SiteId));
await Fails(()=>rawScope.Service.SourceAsync(rawScope.Employee,rawScope.Date,rawForeignSite.Id,false,rawScope.Context,default),"access_denied");
var aggregateScope = new Fixture();var aggregatePending=await aggregateScope.Submit(await aggregateScope.Proposal());aggregateScope.Reviewer();await aggregateScope.Decide(aggregatePending);
var currentSite=Guid.NewGuid();aggregateScope.Scopes=[new([new(aggregateScope.Employee,SiteId:currentSite)])];aggregateScope.Grants.Clear();
aggregateScope.Grants.Add(new(CorrectionPermissions.Review,true,Guid.NewGuid(),AccessScopeKind.Site,currentSite));
await Fails(()=>aggregateScope.Service.MonthAsync(aggregateScope.Employee,new(new(2026,10,1)),false,aggregateScope.Context,default),"access_denied");
var moved = new Fixture(); var movedProposal=(await moved.Proposal()) with {Kind=AttendanceCorrectionKind.CheckInTime,RequestedTimeUtc=moved.Original.StartsAtUtc.AddHours(2)};
var movedPending=await moved.Submit(movedProposal);moved.Reviewer();await moved.Decide(movedPending);
Check(!await moved.Overlap.HasOverlapAsync(moved.Employee,[],moved.Original.StartsAtUtc,moved.Original.StartsAtUtc.AddHours(1),default),
    "virtual moved-away interval replaces original raw window even outside virtual bounding query");
Check(await moved.Overlap.HasOverlapAsync(moved.Employee,[],moved.Original.StartsAtUtc.AddHours(3),moved.Original.StartsAtUtc.AddHours(4),default),
    "virtual retained duty blocks adjacent-day interpretation overlap");
Check(!await moved.Overlap.HasOverlapAsync(moved.Employee,[moved.Original.SessionKey],moved.Original.StartsAtUtc.AddHours(3),moved.Original.StartsAtUtc.AddHours(4),default),
    "changing own virtual session excludes exact stable session key");
var unchangedNeighbor=new WorkSession(Guid.NewGuid(),1,moved.Employee,moved.Date.AddDays(-1),moved.Original.StartsAtUtc,moved.Original.StartsAtUtc.AddHours(1),
    moved.Roster.ShiftVersionId!.Value,null,moved.Clock.Now,null,correctionId:Guid.NewGuid(),siteId:moved.Roster.SiteId);
moved.OtherSessions.Add(unchangedNeighbor);
Check(await moved.Overlap.HasOverlapAsync(moved.Employee,[],unchangedNeighbor.StartsAtUtc,unchangedNeighbor.EndsAtUtc!.Value,default),
    "unrelated raw neighbor still counted after virtual source replacement");
var nextMonth=new Fixture();var firstPosting=await nextMonth.Submit(await nextMonth.Proposal());nextMonth.Reviewer();await nextMonth.Decide(firstPosting);
nextMonth.Ranges.Add(new(Guid.NewGuid(),new(2026,10,1),new(2026,11,1)));
var laterProposal=await nextMonth.Proposal(end:nextMonth.OriginalEnd.AddMinutes(30));
Check(laterProposal.ExpectedTargetMonthStart==new DateOnly(2026,11,1),"next approval after target lock previews first later available month");
var laterRequest=await nextMonth.Submit(laterProposal,"later-month-proposal");nextMonth.Reviewer();await nextMonth.Decide(laterRequest,"later-month-decision");
var oldMonth=await nextMonth.Service.MonthAsync(nextMonth.Employee,new(new(2026,10,1)),false,nextMonth.Context,default);
var futureMonth=await nextMonth.Service.MonthAsync(nextMonth.Employee,new(new(2026,11,1)),false,nextMonth.Context,default);
Check(oldMonth.TotalDeltaWorkedMinutes==60&&futureMonth.TotalDeltaWorkedMinutes==-30,"old locked target posting remains plus60 while new month receives minus30");
var zeroMinutes=new Fixture();var zeroProposal=await zeroMinutes.Submit(await zeroMinutes.Proposal(end:zeroMinutes.OriginalEnd.AddSeconds(30)));zeroMinutes.Reviewer();var zeroDecision=await zeroMinutes.Decide(zeroProposal);
Check(zeroDecision.DeltaWorkedMinutes==0&&zeroDecision.DeltaWorkedTicks==TimeSpan.FromSeconds(30).Ticks,"actual changed timestamps retained even when whole-minute delta is zero");
var rebound=new Fixture();var reboundPending=await rebound.Submit(await rebound.Proposal());rebound.Reviewer();await rebound.Decide(reboundPending);
var ledgerReference=JsonSerializer.Deserialize<PrivateFileReference>(rebound.Adjustments[0].ProtectedEvidenceReference)!;
var originalEvidence=JsonSerializer.Deserialize<CarryForwardInterpretationSnapshot>(rebound.Files[ledgerReference.StorageKey].Bytes)!;
rebound.Files[ledgerReference.StorageKey]=(rebound.Files[ledgerReference.StorageKey].Purpose,JsonSerializer.SerializeToUtf8Bytes(originalEvidence with{SourceSummaryId=Guid.NewGuid()}));
await Fails(()=>rebound.Evidence.AdjustmentAsync(rebound.Adjustments[0],default),"carry_forward_evidence_unavailable");
var membership=new Fixture();Fixture.Set(membership.Frozen.Membership,"AttendancePeriodLockEventId",Guid.NewGuid());
await Fails(()=>membership.Preview(),"carry_forward_evidence_unavailable");
var policyBinding=new Fixture();Fixture.Set(policyBinding.Frozen.Summary,"PayableMinutes",1);
await Fails(()=>policyBinding.Preview(),"carry_forward_evidence_unavailable");
var futureCalculation=new Fixture();Fixture.Set(futureCalculation.Frozen.Summary,"CalculatedAtUtc",futureCalculation.Frozen.Lock.Event!.AtUtc.AddTicks(1));
await Fails(()=>futureCalculation.Preview(),"carry_forward_evidence_unavailable");
var graphType=typeof(CarryForwardEvidence).GetNestedType("ValidationGraph",BindingFlags.NonPublic)!;
var graph=Activator.CreateInstance(graphType,true)!;
var monthMethod=typeof(CarryForwardEvidence).GetMethod("Month",BindingFlags.Instance|BindingFlags.NonPublic)!;
Task<IReadOnlyList<MonthLockCarryForwardEntry>> ReadSameGraph()=>
    (Task<IReadOnlyList<MonthLockCarryForwardEntry>>)monthMethod.Invoke(f.Evidence,[new DateOnly(2026,10,1),new DateOnly(2026,11,1),graph,CancellationToken.None])!;
var cachedVector=await ReadSameGraph();var firstReads=f.MonthReads;var sameVector=await ReadSameGraph();
Check(f.MonthReads==firstReads && cachedVector.SequenceEqual(sameVector) && cachedVector.Count==3,"same validation graph memoizes only fully validated immutable monthly vector");
await f.Evidence.MonthAsync(new(2026,10,1),new(2026,11,1),default);
Check(f.MonthReads>firstReads,"fresh public operation reloads ledger data; no cross-request evidence or authority cache");
var office = new Fixture(true); var officePreview = await office.Preview();
Check((object?)officePreview.Sessions.Single().SiteId is null && JsonSerializer.Serialize(officePreview).Contains("OfficeSource"),
    "locked office preview retains real office evidence without a fake site");
var officeProposal = await office.Proposal(); var officePending = await office.Submit(officeProposal); office.Reviewer();
var officeApproved = await office.Decide(officePending);
Check(officeApproved.DeltaWorkedMinutes == 60 && office.Frozen.Lock.Period.IsLocked && office.Original.EndsAtUtc == office.OriginalEnd,
    "locked office correction appends factual delta without reopening or changing original duty");
Check(await office.Decide(officePending) == officeApproved && office.Adjustments.Count == 1, "office carry-forward decision retry creates no duplicate ledger");
var officeRetained = await office.Evidence.AdjustmentAsync(office.Adjustments.Single(), default);
Check(JsonSerializer.Serialize(officeRetained.Sessions).Contains("OfficeSource") && officeRetained.SchemaVersion == 2 &&
    officeRetained.Sessions.Single().OfficeSource?.EmployeeAssignmentId == office.Original.EmployeeAssignmentId,
    "office carry-forward schema2 retains exact opening office membership and immutable interpretation");
var officeSecondProposal = await office.Proposal(end: office.OriginalEnd.AddHours(2));
var officeSecondPending = await office.Submit(officeSecondProposal, "office-second-source");
office.Reviewer();
var officeSecond = await office.Decide(officeSecondPending, "office-second-decision");
Check(officeSecond.DeltaWorkedMinutes == 60 && office.Adjustments.Last().BeforeWorkedTicks == TimeSpan.FromHours(9).Ticks,
    "subsequent office revision uses approved virtual baseline without double counting");
var officeBoundary = new Fixture(true); var officeBoundaryPending = await officeBoundary.Submit(await officeBoundary.Proposal());
officeBoundary.Boundary = officeBoundary.Boundary with { Id = Guid.NewGuid() }; officeBoundary.Reviewer();
await Fails(() => officeBoundary.Decide(officeBoundaryPending), "correction_office_source_unavailable");
Check(officeBoundary.Adjustments.Count == 0 && officeBoundary.Decisions.Count == 0, "changed pinned office geometry fails before ledger mutation");
var officeCalendar = new Fixture(true); var officeCalendarPending = await officeCalendar.Submit(await officeCalendar.Proposal());
officeCalendar.Roster = officeCalendar.Roster with { Holiday = officeCalendar.Roster.Holiday! with { CalendarId = Guid.NewGuid() } }; officeCalendar.Reviewer();
await Fails(() => officeCalendar.Decide(officeCalendarPending), "carry_forward_office_source_changed");
var officeTransfer = new Fixture(true); var officeTransferPending = await officeTransfer.Submit(await officeTransfer.Proposal());
officeTransfer.AssignmentValid = false; officeTransfer.Reviewer();
await Fails(() => officeTransfer.Decide(officeTransferPending), "correction_crosses_site_transfer");
var officeTarget = new Fixture(true); var officeTargetPending = await officeTarget.Submit(await officeTarget.Proposal());
officeTarget.Ranges.Add(new(Guid.NewGuid(),new(2026,10,1),new(2026,11,1))); officeTarget.Reviewer();
await Fails(() => officeTarget.Decide(officeTargetPending), "carry_forward_target_month_changed");
Check(officeTarget.Adjustments.Count == 0 && (await officeTarget.Preview()).TargetMonthStart == new DateOnly(2026,11,1),
    "new first available month is visible before office resubmission and never silently rerouted");
var officeSelf = new Fixture(true); var officeSelfPending = await officeSelf.Submit(await officeSelf.Proposal()); officeSelf.Reviewer(true);
await Fails(() => officeSelf.Decide(officeSelfPending), "correction_requester_cannot_decide");
var officeRevoked = new Fixture(true); var officeRevokedProposal = await officeRevoked.Proposal(); await officeRevoked.Submit(officeRevokedProposal);
officeRevoked.Current = false; await Fails(() => officeRevoked.Submit(officeRevokedProposal), "invalid_authentication");
var officeManual = new Fixture(true); var officeManualSource = await officeManual.Preview();
var officeManualProposal = new SubmitCarryForwardRequest(officeManual.Date, AttendanceCorrectionKind.ManualSession, null, null,
    officeManual.OriginalEnd, officeManual.OriginalEnd.AddHours(1), officeManualSource.SourceLockEventId, officeManualSource.SourceSummaryId,
    null, officeManualSource.TargetMonthStart, officeManualSource.SourceFingerprint, "Synthetic documented missing duty", "SYNTHETIC TEST ONLY");
var officeManualPending = await officeManual.Submit(officeManualProposal); officeManual.Reviewer(); var officeManualApproved = await officeManual.Decide(officeManualPending);
var officeManualEvidence = await officeManual.Evidence.AdjustmentAsync(officeManual.Adjustments.Single(), default);
Check(officeManualApproved.DeltaWorkedMinutes == 60 && officeManualEvidence.Sessions.Last().IsManual &&
    officeManualEvidence.Sessions.Last().CheckInPunchId is null && officeManualEvidence.Sessions.Last().SiteId is null &&
    officeManualEvidence.Sessions.Last().OfficeSource?.OfficeGeofenceVersionId == officeManual.Boundary.Id,
    "locked office manual additional duty keeps exact retained office sources without fake raw or face");
var officeAbsent = new Fixture(true, true); var officeAbsentSource = await officeAbsent.Preview();
Check(officeAbsentSource.Sessions.Count == 0 && officeAbsentSource.OfficeSource is not null && officeAbsentSource.CurrentWorkedMinutes == 0,
    "authoritatively calculated empty office day retains exact geometry through frozen calculation provenance");
var officeAbsentProposal = new SubmitCarryForwardRequest(officeAbsent.Date, AttendanceCorrectionKind.ManualSession, null, null,
    officeAbsent.Roster.StartsAt!.Value, officeAbsent.Roster.EndsAt, officeAbsentSource.SourceLockEventId, officeAbsentSource.SourceSummaryId,
    null, officeAbsentSource.TargetMonthStart, officeAbsentSource.SourceFingerprint, "Synthetic missing CheckIn duty", "SYNTHETIC TEST ONLY");
var officeAbsentPending = await officeAbsent.Submit(officeAbsentProposal); officeAbsent.Reviewer();
Check((await officeAbsent.Decide(officeAbsentPending)).DeltaWorkedMinutes == 600 && officeAbsent.Frozen.Sessions.Count == 0 && officeAbsent.Frozen.Lock.Period.IsLocked,
    "manual missing CheckIn on frozen office absence appends factual delta only");
var officeAbsentLegacy = new Fixture(true, true, false);
await Fails(() => officeAbsentLegacy.Preview(), "carry_forward_office_calculation_provenance_required");
Check(officeAbsentLegacy.Records.Count == 0 && officeAbsentLegacy.Adjustments.Count == 0,
    "legacy empty office source without retained calculation proof is never silently treated as zero");
var officeAbsentPendingSource = new Fixture(true, true);
officeAbsentPendingSource.SyntheticSource = officeAbsentPendingSource.SyntheticSource! with { State = "policy_configuration_required" };
await Fails(() => officeAbsentPendingSource.Preview(), "carry_forward_office_calculation_source_changed");
var officeAbsentChanged = new Fixture(true, true);
var alteredSnapshot = officeAbsentChanged.SyntheticSource!.Snapshot! with {
    OfficeSource = officeAbsentChanged.SyntheticSource.Snapshot!.OfficeSource! with { HolidayCalendarId = Guid.NewGuid() } };
officeAbsentChanged.SyntheticSource = new("complete", alteredSnapshot, CarryForwardEvidence.Fingerprint(alteredSnapshot));
await Fails(() => officeAbsentChanged.Preview(), "carry_forward_office_calculation_source_changed");
var lockedAlternative = new Fixture(true, true);
var lockedAlternativeSource = await lockedAlternative.Preview();
var lockedAlternativeProposal = new SubmitCarryForwardRequest(lockedAlternative.Date, AttendanceCorrectionKind.ManualSession, null, null,
    lockedAlternative.Roster.StartsAt!.Value, lockedAlternative.Roster.EndsAt, lockedAlternativeSource.SourceLockEventId, lockedAlternativeSource.SourceSummaryId,
    null, lockedAlternativeSource.TargetMonthStart, lockedAlternativeSource.SourceFingerprint, "Synthetic accessibility duty", "SYNTHETIC TEST ONLY",
    AlternativeKind: CorrectionAlternativeKind.Accessibility, ExpectedAlternativePolicyId: Guid.NewGuid());
var lockedAlternativePending = await lockedAlternative.Submit(lockedAlternativeProposal); lockedAlternative.Reviewer();
var lockedAlternativeDecision = new DecideCorrectionRequest(lockedAlternativePending.Version, ApprovalState.Approved, "Synthetic exact case approval", CorrectionAlternativeKind.Accessibility);
await Fails(() => lockedAlternative.Service.DecideAsync(lockedAlternative.Employee, lockedAlternativePending.Id, lockedAlternativeDecision,
    "locked-alternative-hr", lockedAlternative.Context, default), "access_denied");
lockedAlternative.RoleCode = "ADMIN"; lockedAlternative.IsFixedAdmin = true;
lockedAlternative.Grants.Add(new(CorrectionPermissions.AlternativesDecide,true,Guid.NewGuid(),AccessScopeKind.All,null) { AuthorityTimingKnown=true, ValidFromUtc=lockedAlternative.Clock.Now.AddDays(-1) });
var lockedAlternativeApplied = await lockedAlternative.Service.DecideAsync(lockedAlternative.Employee, lockedAlternativePending.Id, lockedAlternativeDecision,
    "locked-alternative-admin", lockedAlternative.Context, default);
var lockedAlternativeSaved = await lockedAlternative.Evidence.AdjustmentAsync(lockedAlternative.Adjustments.Single(),default);
Check(lockedAlternativeApplied.DeltaWorkedMinutes == 600 && lockedAlternativeSaved.SchemaVersion == 3 &&
    lockedAlternativeSaved.AlternativeEvidence?.Policy.Kind == CorrectionAlternativeKind.Accessibility && lockedAlternative.Frozen.Lock.Period.IsLocked,
    "Admin alternative on locked office duty retains policy evidence and exact source without reopening");
Check(await lockedAlternative.Service.DecideAsync(lockedAlternative.Employee, lockedAlternativePending.Id, lockedAlternativeDecision,
    "locked-alternative-admin", lockedAlternative.Context, default) == lockedAlternativeApplied && lockedAlternative.Adjustments.Count == 1,
    "locked Admin alternative retry prevents duplicated factual delta");
var reviewerIdentity = lockedAlternative.Actor;
lockedAlternative.Actor = reviewerIdentity with { EmployeeId = lockedAlternative.Employee };
await Fails(() => lockedAlternative.Service.DecideAsync(lockedAlternative.Employee, lockedAlternativePending.Id, lockedAlternativeDecision,
    "locked-alternative-admin", lockedAlternative.Context, default), "correction_beneficiary_cannot_decide");
Check(lockedAlternative.Adjustments.Count == 1 && lockedAlternative.Decisions.Count == 1,
    "beneficiary receipt replay cannot repeat or authorize the retained factual adjustment");
lockedAlternative.Actor = reviewerIdentity;
foreach (var state in new[] { ApprovalState.Approved, ApprovalState.Returned, ApprovalState.Rejected })
{
    var beneficiary = new Fixture(true, true);
    beneficiary.Reviewer();
    var beneficiarySource = await beneficiary.Preview();
    var beneficiaryProposal = new SubmitCarryForwardRequest(beneficiary.Date, AttendanceCorrectionKind.ManualSession, null, null,
        beneficiary.Roster.StartsAt!.Value, beneficiary.Roster.EndsAt, beneficiarySource.SourceLockEventId, beneficiarySource.SourceSummaryId,
        null, beneficiarySource.TargetMonthStart, beneficiarySource.SourceFingerprint, "SYNTHETIC beneficiary case", "SYNTHETIC TEST ONLY",
        AlternativeKind: CorrectionAlternativeKind.Accessibility, ExpectedAlternativePolicyId: Guid.NewGuid());
    var beneficiaryPending = await beneficiary.Submit(beneficiaryProposal);
    beneficiary.Reviewer(); beneficiary.RoleCode = "ADMIN"; beneficiary.IsFixedAdmin = true;
    beneficiary.Actor = beneficiary.Actor with { EmployeeId = beneficiary.Employee };
    beneficiary.Grants.Add(new(CorrectionPermissions.AlternativesDecide, true, Guid.NewGuid(), AccessScopeKind.All, null)
        { AuthorityTimingKnown = true, ValidFromUtc = beneficiary.Clock.Now.AddDays(-1) });
    await Fails(() => beneficiary.Service.DecideAsync(beneficiary.Employee, beneficiaryPending.Id,
        new(beneficiaryPending.Version, state, "Self-beneficiary must not decide", CorrectionAlternativeKind.Accessibility),
        "beneficiary-denial-" + state, beneficiary.Context, default), "correction_beneficiary_cannot_decide");
    Check(beneficiary.Records.Single().Approval.State == ApprovalState.Pending && beneficiary.Decisions.Count == 0 &&
        beneficiary.Adjustments.Count == 0 && beneficiary.Receipts.Count == 1 && beneficiary.Notifications.Count == 0,
        "denied special beneficiary decision leaves request/lock/ledger/receipt/outbox unchanged: " + state);
}
Fixture.Set(lockedAlternative.Decisions.Single(), "ActorRoleCode", "CUSTOM_HR");
await Fails(() => lockedAlternative.Evidence.AdjustmentAsync(lockedAlternative.Adjustments.Single(), default), "carry_forward_evidence_unavailable");
Console.WriteLine($"Carry-forward service checks passed: {checks}. Real services, synthetic IO only; no SQL, provider or real account writes.");

public sealed class Clock : TimeProvider { public DateTimeOffset Now = new(2026,10,9,6,0,0,TimeSpan.Zero); public override DateTimeOffset GetUtcNow()=>Now; }
public class Proxy : DispatchProxy
{
    public Func<MethodInfo,object?[],object?> Handler=null!;
    protected override object? Invoke(MethodInfo? m,object?[]? a)=>Handler(m!,a??[]);
    public static T Make<T>(Func<MethodInfo,object?[],object?> handler) where T:class {var x=Create<T,Proxy>();((Proxy)(object)x).Handler=handler;return x;}
}
public sealed class Fixture
{
    public static void Set(object row,string name,object? value)=>row.GetType().GetProperty(name)!.SetValue(row,value);
    public Clock Clock=new(); public AuthenticatedAccount Actor=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Mobile,false);
    public Guid Employee; public bool Current=true,AssignmentValid=true,Eligible=true,FailNotification;
    public DateOnly Date=new(2026,9,8); public DateTimeOffset OriginalEnd=new(2026,9,9,0,0,0,TimeSpan.Zero);
    public EffectiveRosterResponse Roster; public WorkSession Original; public CarryForwardFrozenRows Frozen;
    public List<LockedPeriodRange> Ranges=[]; public List<CarryForwardRequestRow> Records=[]; public List<AttendanceCarryForwardAdjustment> Adjustments=[];
    public List<ApprovalDecision> Decisions=[]; public List<WorkSession> OtherSessions=[]; public List<NotificationIntent> Notifications=[];
    public CarryForwardRawEvidence? RawEvidence;
    public List<PermissionGrant> Grants=[]; public List<IdempotencyReceipt> Receipts=[]; public IReadOnlyList<AttendanceResourceScope> Scopes;
    public Dictionary<string,(string Purpose,byte[] Bytes)> Files=[]; public CarryForwardService Service; public CarryForwardEvidence Evidence; public CarryForwardDutyOverlap Overlap;
    public AuthRequestContext Context=new("127.0.0.1","synthetic-carry-forward"); public int MonthReads; private long version; private int depth;
    public OfficeBoundaryVersionResponse Boundary = new(Guid.NewGuid(), Guid.NewGuid(), 1, new(2026, 9, 1),
        24m, 67m, 100m, 20m, "PK-SD", "SYNTHETIC TEST ONLY", "synthetic", new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
    public AttendanceCalculationSnapshotResult? SyntheticSource;
    public bool IsFixedAdmin; public string RoleCode = "SyntheticRole";
    public Fixture(bool office = false, bool absent = false, bool calculationProvenance = true)
    {
        Employee=Actor.EmployeeId;
        Roster=new(Employee,Date,"scheduled",RosterSchedule.TimeZoneId,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,
            OriginalEnd.AddHours(-8),OriginalEnd.AddHours(2),600,0,0,new("site",Guid.NewGuid(),Guid.NewGuid(),null,null,"site inheritance"));
        if (office) Roster = Roster with { SiteId = null, BranchId = Boundary.BranchId,
            Explanation = new("office", null, Roster.Explanation!.MembershipRecordId, null, null, "office history") { OfficeRosterAssignmentId = Roster.AssignmentId },
            Holiday = new("configured", Date, Guid.NewGuid(), "SYNTHETIC", 1, []) };
        Original=new(Guid.NewGuid(),1,Employee,Date,OriginalEnd.AddHours(-8),OriginalEnd,Roster.ShiftVersionId!.Value,null,OriginalEnd.AddMinutes(1),null,correctionId:Guid.NewGuid(),siteId:Roster.SiteId,
            branchId: Roster.BranchId, officeRosterAssignmentId: office ? Roster.AssignmentId : null,
            employeeAssignmentId: office ? Roster.Explanation!.MembershipRecordId : null, officeGeofenceVersionId: office ? Boundary.Id : null,
            holidayCalendarId: office ? Roster.Holiday!.CalendarId : null);
        Scopes=[new([new(Employee,SiteId:Roster.SiteId)])];
        var facts=AttendanceFacts.Calculate(Employee,Date,absent ? [] : [Original]); var policy=new AttendancePolicyResult("calculated",Guid.NewGuid(),"synthetic-policy",absent ? 0 : 480,0,0,absent ? AttendanceDayStatus.Absent : AttendanceDayStatus.Present,[]);
        if (absent)
        {
            var rules = new AttendancePolicyRules(1, new(300, 1800, 3600),
                new(AttendancePaidTimeBasis.ActualElapsedDuty, AttendanceBreakTreatment.CountAllBreaks, AttendanceEarlyExitTreatment.ActualWorkedTime,
                    14400, AttendanceNonScheduledDayTreatment.NoCredit, AttendanceNonScheduledDayTreatment.NoCredit),
                new(3, AttendanceLateResetPeriod.CalendarMonth, true, true, AttendanceDeductionSalaryBasis.BaseSalary,
                    AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1m, AttendanceDeductionRounding.WholeCurrencyUnitHalfUp));
            var validated = AttendancePolicyRulesCodec.Validate(rules);
            if (!validated.IsValid) throw new Exception("Synthetic absent policy is invalid");
            var snapshot = new AttendanceCalculationSnapshot(2, Employee, Date, "synthetic-policy", facts.InputFingerprint, Roster,
                new(Roster.Explanation!.MembershipRecordId!.Value, Guid.NewGuid(), "OFFICE", null, new(2026,9,1), null, Roster.BranchId),
                new("effective", policy.PolicyVersionId, [Guid.NewGuid()], rules, validated.CanonicalJson, new string('B',64)),
                new(LeaveContextStatus.Complete, [], new string('C',64), []), [], [], [], [],
                new(rules, Roster.StartsAt, Roster.EndsAt, 600, 0, false, false, [], []), 0,
                OfficeAttendanceSource.Capture(Employee, Date, Roster, Boundary));
            SyntheticSource = new("complete", snapshot, CarryForwardEvidence.Fingerprint(snapshot));
            policy = policy with { InputProvenance = calculationProvenance ? SyntheticSource.Fingerprint : null };
        }
        var day=new AttendanceDayResponse(Employee,Date,RosterSchedule.TimeZoneId,"closed",facts,Roster,policy,0,false,null);
        var inputHash=CarryForwardEvidence.Fingerprint(new { day.Facts,day.Roster,day.Policy,day.ExceptionCount });
        var summary=new DailyAttendanceSummary(Employee,Date,1,policy.DayStatus!.Value,policy.PolicyVersionId,Roster.ShiftVersionId,"synthetic-policy",inputHash,600,absent ? 0 : 480,policy.PayableMinutes,0,0,0,
            OriginalEnd.AddMinutes(2),inputEvidenceReference:Store(day,$"attendance:summary:{Employee:N}:{Date:yyyy-MM-dd}"));
        var period=new AttendancePeriodLock(new(2026,9,1),new(2026,10,1));var eventId=Guid.NewGuid();var lockedAt=new DateTimeOffset(2026,10,1,0,0,0,TimeSpan.Zero);
        var manifest=new MonthLockSnapshot(1,eventId,period.Id,1,period.PeriodStart,period.PeriodEndExclusive,"synthetic-locker","Finalized synthetic source month",lockedAt,new string('A',64),
            [new(Employee,Date,summary.Id,inputHash,policy.PolicyVersionId!.Value,"synthetic-policy")],[]);
        var item=period.RecordDirectLock(eventId,manifest.Actor,manifest.Reason,lockedAt,Store(manifest,MonthLockEvidence.Purpose(period.Id,eventId)));
        Frozen=new(new(period,item,[summary],false),new(period,item,summary),summary,absent ? [] : [new(summary,Original,480)],absent ? [] : [Original]);
        Ranges.Add(new(period.Id,period.PeriodStart,period.PeriodEndExclusive));
        Grants.Add(new(CorrectionPermissions.Self,false,Guid.NewGuid(),AccessScopeKind.Self,Employee));
        var current=Proxy.Make<ICurrentAuthentication>((m,a)=>Actor);
        var access=Proxy.Make<IAccountAccessReader>((m,a)=>Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var authorization=Proxy.Make<IAccessManagementRepository>((m,a)=>m.Name switch {
            "InTransactionAsync"=>GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this,a),
            "GetAssignmentRoleCodeAsync"=>Task.FromResult<string?>(RoleCode),"IsCurrentSystemRoleAssignmentAsync"=>Task.FromResult(IsFixedAdmin),_=>throw new Exception(m.Name)});
        var authentication=Proxy.Make<IAuthenticationRepository>((m,a)=>throw new Exception("No face/device required for correction"));
        var journal=Proxy.Make<ISchedulingJournalRepository>((m,a)=>m.Name switch {
            "IsCurrentSessionAsync"=>Task.FromResult(Current),"FindReceiptAsync"=>Task.FromResult(Receipts.SingleOrDefault(x=>x.ActorUserId==(string)a[0]!&&x.Operation==(string)a[1]!&&x.Key==(string)a[2]!)),
            "AddReceipt"=>AppendReceipt((IdempotencyReceipt)a[0]!),"AddAudit"=>null,"SaveChangesAsync"=>Task.CompletedTask,_=>throw new Exception(m.Name)});
        var files=Proxy.Make<IPrivateFileStore>((m,a)=>m.Name=="WriteAsync"?Task.FromResult(Write((byte[])a[0]!, (string)a[1]!)):Task.FromResult(Read((PrivateFileReference)a[0]!, (string)a[1]!)));
        var operation=new AttendanceOperation(current,access,authorization,journal,authentication,Clock,files);
        var officeReader = Proxy.Make<IOfficeAttendanceSourceReader>((m,a) => m.Name == "ReadAsync"
            ? Task.FromResult(Roster.BranchId.HasValue ? new OfficeAttendanceContext("configured", OfficeAttendanceSource.Capture(Employee, Date,
                (EffectiveRosterResponse)a[2]!, Boundary), Boundary) : new OfficeAttendanceContext("not_office", null, null))
            : Task.FromResult(AssignmentValid));
        var rosterReader = Proxy.Make<IIndividualRosterResolver>((m,a) => Task.FromResult(Roster));
        var calculationReader = Proxy.Make<IAttendanceCalculationSnapshotProducer>((m,a) =>
            Task.FromResult(SyntheticSource ?? throw new Exception("No synthetic absence calculation was configured")));
        var repository=Proxy.Make<ICarryForwardRepository>(Repo); Evidence=new(operation,repository,officeReader,rosterReader,calculationReader);Overlap=new(repository,Evidence);
        var summaries=Proxy.Make<IAttendanceSummaryRepository>((m,a)=>m.Name=="ResourceScopesAsync"?Task.FromResult(Scopes):throw new Exception(m.Name));
        var review=Proxy.Make<ICorrectionReviewRepository>((m,a)=>m.Name switch {
            "HistoricallyEligibleAsync"=>Task.FromResult(Eligible),"PreservesAssignmentAsync"=>Task.FromResult(AssignmentValid),"ScopeExistsAsync"=>Task.FromResult(true),_=>throw new Exception(m.Name)});
        var cursor=Proxy.Make<IAttendanceCursorProtector>((m,a)=>m.Name=="Protect"?Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes((AttendanceCursorPosition)a[3]!)):
            JsonSerializer.Deserialize<AttendanceCursorPosition>(Convert.FromBase64String((string)a[0]!))!);
        var notify=Proxy.Make<INotificationOutbox>((m,a)=>{if(FailNotification)throw new AuthenticationFailure("synthetic_notification_failure",503);Notifications.Add((NotificationIntent)a[0]!);return Task.FromResult(true);});
        var alternatives = Proxy.Make<ICorrectionAlternativeEvidenceReader>((m,a) => {
            if (m.Name == "RevalidateAsync") return Task.CompletedTask;
            var value = new CorrectionAlternativePolicy((Guid)a[3]!, (CorrectionAlternativeKind)a[2]!, 1, new(2026,9,1), false, [], "SYNTHETIC TEST APPROVAL", Actor.AccountId.ToString(), Clock.Now, "");
            return Task.FromResult(new CorrectionAlternativeEvidence(value with { Fingerprint = CorrectionAlternativePolicyService.PolicyFingerprint(value) }, []));
        });
        Service=new(operation,repository,Evidence,summaries,review,Overlap,access,cursor,files,notify,alternatives,authorization,Clock);
    }
    public string Store<T>(T value,string purpose)=>AttendanceOperation.Reference(Write(JsonSerializer.SerializeToUtf8Bytes(value),purpose));
    private PrivateFileReference Write(byte[] bytes,string purpose)
    {var key=Guid.NewGuid().ToString("N");Files[key]=(purpose,bytes);return new(key,"1",Convert.ToHexString(SHA256.HashData(bytes)),"synthetic",bytes.Length);}
    private byte[] Read(PrivateFileReference reference,string purpose)
    {var saved=Files[reference.StorageKey];if(saved.Purpose!=purpose)throw new Exception("Private purpose mismatch");return saved.Bytes;}
    private object? AppendReceipt(IdempotencyReceipt receipt){Receipts.Add(receipt);return null;}
    private object? Repo(MethodInfo method,object?[] a)
    {
        switch(method.Name)
        {
            case "FrozenAsync": return Task.FromResult(Frozen);
            case "LockedRangesAsync":return Task.FromResult<IReadOnlyList<LockedPeriodRange>>(Ranges.ToArray());
            case "LatestAsync":return Task.FromResult(Adjustments.Where(x=>x.EmployeeId==(Guid)a[0]!&&x.SourceBusinessDate==(DateOnly)a[1]!).MaxBy(x=>x.Revision));
            case "AdjustmentAsync":return Task.FromResult(Adjustments.SingleOrDefault(x=>x.Id==(Guid)a[0]!));
            case "DecisionAsync":return Task.FromResult(Decisions.SingleOrDefault(x=>x.Id==(Guid)a[0]!));
            case "RawAsync":return Task.FromResult(RawEvidence?.Punch.Id==(Guid)a[1]! ? RawEvidence:null);
            case "RequestAsync":return Task.FromResult(Records.SingleOrDefault(x=>x.Correction.EmployeeId==(Guid)a[0]!&&x.Correction.Id==(Guid)a[1]!) is {} r ? r with {Adjustment=Adjustments.SingleOrDefault(x=>x.CorrectionId==r.Correction.Id)}:null);
            case "EquivalentPendingAsync":var p=(SubmitCarryForwardRequest)a[1]!;return Task.FromResult(Records.Any(x=>x.Approval.State==ApprovalState.Pending&&x.Correction.Kind==p.Kind&&x.Correction.RequestedTimeUtc==p.RequestedTimeUtc));
            case "MonthAsync":MonthReads++;return Task.FromResult<IReadOnlyList<AttendanceCarryForwardAdjustment>>(Adjustments.Where(x=>(a[0] is null || x.EmployeeId==(Guid)a[0]!)&&x.TargetMonthStart>=(DateOnly)a[1]!&&x.TargetMonthStart<(DateOnly)a[2]!).OrderBy(x=>x.Id).ToArray());
            case "OverlappingSessionsAsync":var excluded=(IReadOnlyCollection<Guid>)a[1]!;var start=(DateTimeOffset)a[2]!;var end=(DateTimeOffset)a[3]!;
                return Task.FromResult<IReadOnlyList<WorkSession>>(Frozen.Sessions.Concat(OtherSessions).Where(x=>!excluded.Contains(x.SessionKey)&&x.StartsAtUtc<end&&(x.EndsAtUtc is null||x.EndsAtUtc>start)).ToArray());
            case "OverlapAdjustmentsAsync":var dates=(IReadOnlyCollection<DateOnly>)a[1]!;var from=(DateTimeOffset)a[2]!;var to=(DateTimeOffset)a[3]!;
                return Task.FromResult<IReadOnlyList<AttendanceCarryForwardAdjustment>>(Adjustments.GroupBy(x=>x.SourceBusinessDate).Select(x=>x.MaxBy(y=>y.Revision)!).Where(x=>dates.Contains(x.SourceBusinessDate)||x.FirstStartsAtUtc<to&&x.LastEndsAtUtc>from).ToArray());
            case "CandidatesAsync":var q=(CorrectionQueueRequest)a[0]!;var pos=(AttendanceCursorPosition?)a[1];var owner=(Guid?)a[3];var all=(bool)a[5]!;
                return Task.FromResult<IReadOnlyList<CarryForwardRequestRow>>(Records.Where(x=>(all||x.Approval.State==q.State)&&(!owner.HasValue||x.Approval.RequesterUserId==owner.ToString())&&
                    (pos is null||x.Correction.RequestedAtUtc>pos.CreatedAtUtc||x.Correction.RequestedAtUtc==pos.CreatedAtUtc&&x.Correction.Id.CompareTo(pos.Id)>0)).OrderBy(x=>x.Correction.RequestedAtUtc).ThenBy(x=>x.Correction.Id).Take((int)a[2]!).ToArray());
            case "Add":if(a[0] is AttendanceCorrection c)Records.Add(new(){Correction=c,Approval=(ApprovalRequest)a[1]!});else{Decisions.Add((ApprovalDecision)a[0]!);if(a[1] is AttendanceCarryForwardAdjustment item)Adjustments.Add(item);}return null;
            case "ExpectVersion":return null;
            case "SaveChangesAsync":foreach(var record in Records)Set(record.Approval,"RowVersion",BitConverter.GetBytes(++version));return Task.CompletedTask;
            default:throw new Exception(method.Name);
        }
    }
    public async Task<T> Transaction<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct)
    {
        if(depth++!=0)throw new Exception("Nested transaction");
        var count=Records.Count;var states=Records.Select(x=>(x.Approval,x.Approval.State,x.Approval.ClosedAt,x.Approval.RowVersion)).ToArray();
        var decisions=Decisions.Count;var ledger=Adjustments.Count;var receipts=Receipts.Count;var notices=Notifications.Count;
        try{return await action(ct);}catch{
            foreach(var s in states){Set(s.Approval,"State",s.State);Set(s.Approval,"ClosedAt",s.ClosedAt);Set(s.Approval,"RowVersion",s.RowVersion);}
            Records.RemoveRange(count,Records.Count-count);Decisions.RemoveRange(decisions,Decisions.Count-decisions);Adjustments.RemoveRange(ledger,Adjustments.Count-ledger);
            Receipts.RemoveRange(receipts,Receipts.Count-receipts);Notifications.RemoveRange(notices,Notifications.Count-notices);throw;
        }finally{depth--;}
    }
    public void Reviewer(bool sameAccount=false)
    {Actor=new(sameAccount?Actor.AccountId:Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Web,true);Grants.Clear();foreach(var p in new[]{CorrectionPermissions.Submit,CorrectionPermissions.Review,CorrectionPermissions.Decide})Grants.Add(new(p,true,Guid.NewGuid(),AccessScopeKind.All,null));}
    public Task<CarryForwardSourceResponse> Preview()=>Service.SourceAsync(Actor.Client==SessionClient.Mobile?null:Employee,Date,null,Actor.Client==SessionClient.Mobile,Context,default);
    public async Task<SubmitCarryForwardRequest> Proposal(DateTimeOffset? end=null)
    {var source=await Preview();return new(Date,AttendanceCorrectionKind.CheckOutTime,Original.SessionKey,null,end??OriginalEnd.AddHours(1),null,source.SourceLockEventId,source.SourceSummaryId,source.PriorAdjustmentId,source.TargetMonthStart,source.SourceFingerprint,"Correct documented duty","Contemporaneous synthetic evidence");}
    public Task<CarryForwardRequestResponse> Submit(SubmitCarryForwardRequest proposal,string key="carry-proposal-key")=>Service.SubmitAsync(Actor.Client==SessionClient.Mobile?null:Employee,proposal,key,Actor.Client==SessionClient.Mobile,Context,default);
    public Task<CarryForwardDecisionResponse> Decide(CarryForwardRequestResponse response,string key="carry-decision-key")=>Service.DecideAsync(Employee,response.Id,new(response.Version,ApprovalState.Approved,"HR reviewed retained evidence"),key,Context,default);
}
