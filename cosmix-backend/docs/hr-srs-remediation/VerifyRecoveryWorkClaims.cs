#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;



using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Application.Attendance.Overtime;

var checks = 0; var failed = 0;
void Check(bool ok, string name) { checks++; if (!ok) { failed++; Console.WriteLine("FAIL: " + name); } }
async Task Fails(Func<Task> action, string code, int status = 409) {
    try { await action(); Check(false, "Expected " + code); }
    catch (AuthenticationFailure e) { Check(e.Code == code && e.StatusCode == status, "Expected " + code + ", got " + e.Code); }
}
async Task Case(string name, Func<Task> action) {
    try { await action(); } catch (Exception ex) { Check(false, name + ": " + ex.GetBaseException().Message); }
}
await Case("actual partial and final fulfilment", async () => {
    var f = await WorkFixture.Create();
    var first = await f.Submit(0, 30, "work-partial-one");
    Check(first.State == ApprovalState.Pending && first.ApprovedMinutes is null, "Submission is not fulfilment");
    var approved = await f.Approve(first, "work-approve-one");
    Check(approved.ApprovedMinutes == 30, "Independent approval credits exactly thirty actual minutes");
    Check(f.Decisions.Single().ActorRoleCode == Role.CreateSystem(SystemRoleKind.Admin, Guid.NewGuid(), f.Parent.Clock.Now).Code,
        "New recovery work decision uses the canonical system role");
    var progress = await f.Progress();
    Check(progress.FulfilledMinutes == 30 && progress.RemainingMinutes == 30 && progress.Status == "partially_fulfilled", "Partial obligation remains open");
    var second = await f.Submit(30, 60, "work-partial-two");
    await f.Approve(second, "work-approve-two");
    progress = await f.Progress();
    Check(progress.FulfilledMinutes == 60 && progress.RemainingMinutes == 0 && progress.Status == "fulfilled", "Two disjoint approvals fulfil sixty-minute obligation");
    Check(f.Parent.Commitments.Single().CommittedMinutes == 60, "Original promise remains immutable");
});
foreach (var retainedRole in new[] { "ADMIN", "SUPERADMIN", "Admin", "SuperAdmin" })
    await Case("retained recovery reviewer " + retainedRole, async () => {
        var f = await WorkFixture.Create();
        var pending = await f.Submit(0, 30, "work-role-evidence");
        await f.Approve(pending, "work-role-review");
        var decision = f.Decisions.Single();
        var fact = f.Parent.Audits.Single(x => x.EventType == LeaveRecoveryWorkEvidence.Decided &&
            x.TargetType == LeaveRecoveryWorkEvidence.FactTarget && x.TargetId == pending.Id.ToString());
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(decision.ProtectedChangeReference!)!;
        var stored = f.Parent.Files[reference.StorageKey];
        var proof = JsonSerializer.Deserialize<LeaveRecoveryWorkDecision>(stored.Bytes)! with { ReviewerRole = retainedRole };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(proof);
        f.Parent.Files[reference.StorageKey] = (bytes, stored.Purpose);
        var updatedReference = AttendanceOperation.Reference(reference with {
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)), SizeBytes = bytes.Length });
        WorkParentFixture.Set(decision, nameof(ApprovalDecision.ActorRoleCode), retainedRole);
        WorkParentFixture.Set(decision, nameof(ApprovalDecision.ProtectedChangeReference), updatedReference);
        WorkParentFixture.Set(fact, nameof(AuditEvent.ActorRoleCode), retainedRole);
        WorkParentFixture.Set(fact, nameof(AuditEvent.ProtectedChangesReference), updatedReference);
        Check((await f.Reader.ReadReservedAsync(f.Parent.Employee, default)).Count == 1,
            retainedRole + " recovery decision remains readable");
        Check(f.Parent.Files[reference.StorageKey].Bytes.SequenceEqual(bytes),
            retainedRole + " recovery proof bytes preserved");
        WorkParentFixture.Set(fact, nameof(AuditEvent.ActorRoleCode), retainedRole == "Admin" ? "ADMIN" : "Admin");
        await Fails(() => f.Reader.ReadReservedAsync(f.Parent.Employee, default), "leave_recovery_work_decision_unavailable");
        WorkParentFixture.Set(fact, nameof(AuditEvent.ActorRoleCode), retainedRole);
        WorkParentFixture.Set(decision, nameof(ApprovalDecision.ActorRoleCode), retainedRole == "Admin" ? "ADMIN" : "Admin");
        await Fails(() => f.Reader.ReadReservedAsync(f.Parent.Employee, default), "leave_recovery_work_decision_unavailable");
    });
await Case("approval replay and permanent submission key", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-key-one");
    var approved = await f.Approve(p, "work-key-review");
    var replay = await f.Approve(p, "work-key-review");
    Check(replay.Id == approved.Id && f.Decisions.Count == 1, "Review retry has one effect");
    f.Parent.Receipts.Clear();
    var again = await f.Submit(0, 30, "work-key-one");
    Check(again.Id == p.Id && f.Approvals.Count == 1, "Submission reconciliation survives short receipt retention");
    await Fails(() => f.Submit(30, 60, "work-key-one"), "leave_recovery_submission_key_reused");
});
foreach (var role in new[] { WorkCreditRole.Overtime, WorkCreditRole.MakeUp }) {
    await Case("existing competing allocation " + role, async () => {
        var f = await WorkFixture.Create();
        f.OtherClaims.Add(f.Range(role, f.Start, f.Start.AddMinutes(30)));
        await Fails(() => f.Submit(0, 30, "work-other-first"), "work_interval_already_claimed");
        Check(f.Approvals.Count == 0, "Competing allocation prevents submission");
    });
}
await Case("recovery reserved before competing work", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-reserve-one"); await f.Approve(p, "work-reserve-review");
    var reserved = await f.Reader.ReadReservedAsync(f.Parent.Employee, default);
    Check(reserved.Count == 1 && reserved[0].Range.Ticks == 30L * TimeSpan.TicksPerMinute, "Shared reader returns exact approved allocation");
    await Fails(() => f.Credits.CheckExclusiveAsync(f.Parent.Employee,
        [f.Range(WorkCreditRole.MakeUp, f.Start, f.Start.AddMinutes(30))], [], default), "work_interval_already_claimed");
    var laterBusinessDate = f.Range(WorkCreditRole.Overtime, f.Start, f.Start.AddMinutes(30)) with { BusinessDate = new(2026, 10, 17) };
    await Fails(() => f.Credits.CheckExclusiveAsync(f.Parent.Employee, [laterBusinessDate], [], default), "work_interval_already_claimed");
});
await Case("source changed before decision", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-source-one");
    f.Session = f.Session with { Revision = 2, Id = Guid.NewGuid() };
    await Fails(() => f.Approve(p, "work-source-review"), "leave_recovery_work_source_changed");
    Check(f.Decisions.Count == 0, "Changed attendance source creates no fulfilment");
});
await Case("period lock before decision", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-lock-one"); f.Locked = true;
    await Fails(() => f.Approve(p, "work-lock-review"), "leave_recovery_period_locked");
});
await Case("self review and unrelated self reads denied", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-self-one");
    f.Admin(); f.Parent.Actor = f.Parent.Actor with { EmployeeId = f.Parent.Employee };
    await Fails(() => f.Service.DecideAsync(f.Parent.Employee, p.Id, new(p.Version, ApprovalState.Approved, "Synthetic review"),
        "work-self-review", f.Parent.Context, default), "access_denied", 403);
    f.Parent.Actor = f.Parent.Actor with { EmployeeId = Guid.NewGuid() };
    await Fails(() => f.Service.ProgressAsync(f.Parent.Parent.Request.Id, f.Commitment.Id, true, f.Parent.Context, default), "leave_not_found", 404);
});
await Case("cancellation retains allocation and blocks additional claims", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-cancel-one"); await f.Approve(p, "work-cancel-review");
    await f.Parent.PendingCancellation();
    await Fails(() => f.Submit(30, 60, "work-cancel-two"), "leave_recovery_approval_unavailable");
    Check((await f.Reader.ReadReservedAsync(f.Parent.Employee, default)).Count == 1, "Cancellation cannot silently free approved work");
});
await Case("inside scheduled duty is not additional work", async () => {
    var f = await WorkFixture.Create();
    f.Start = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);
    f.Session = f.Session with { StartUtc = f.Start, EndUtc = f.Start.AddHours(2) };
    await Fails(() => f.Submit(0, 30, "work-roster-one"), "leave_recovery_scheduled_work_not_qualifying");
});
await Case("overfulfilment is rejected after partial approval", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 45, "work-cap-one"); await f.Approve(p, "work-cap-review");
    await Fails(() => f.Submit(45, 75, "work-cap-two"), "leave_recovery_obligation_overallocated");
});
await Case("pending authority is explicit failure", async () => {
    var f = await WorkFixture.Create(); f.Configure(x => x with { ReviewAuthority = RecoveryReviewAuthority.Pending });
    await Fails(() => f.Submit(0, 30, "work-pending-policy"), "leave_recovery_fulfilment_policy_pending");
    Check(f.Approvals.Count == 0, "Pending policy creates no approval");
});
await Case("explicit whole obligation policy", async () => {
    var f = await WorkFixture.Create(); f.Configure(x => x with { AllowPartialFulfilment = false });
    await Fails(() => f.Submit(0, 30, "work-no-partial"), "leave_recovery_partial_fulfilment_disabled");
    var full = await f.Submit(0, 60, "work-whole-obligation"); await f.Approve(full, "work-whole-review");
    Check((await f.Progress()).FulfilledMinutes == 60, "Whole-only policy accepts the exact obligation");
});
await Case("configured submission window", async () => {
    var f = await WorkFixture.Create(); f.Configure(x => x with { SubmissionWindowDays = 0 });
    f.Parent.Clock.Now = f.Parent.Clock.Now.AddDays(1);
    await Fails(() => f.Submit(0, 30, "work-window-closed"), "leave_recovery_submission_window_closed");
});
await Case("pending claims cannot bypass approved capacity", async () => {
    var f = await WorkFixture.Create(); var a = await f.Submit(0, 45, "work-pending-cap-a"); var b = await f.Submit(45, 75, "work-pending-cap-b");
    await f.Approve(a, "work-pending-cap-review-a");
    await Fails(() => f.Approve(b, "work-pending-cap-review-b"), "leave_recovery_obligation_overallocated");
    Check(f.Decisions.Count == 1, "Only capacity-backed decision persists");
});
await Case("competing work approved between submission and review", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-race-ot");
    f.OtherClaims.Add(f.Range(WorkCreditRole.Overtime, f.Start, f.Start.AddMinutes(30)));
    await Fails(() => f.Approve(p, "work-race-ot-review"), "work_interval_already_claimed");
    Check(f.Decisions.Count == 0, "Earlier pending recovery cannot displace approved OT");
});
await Case("source correction retains exclusivity without claiming fulfilment", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-correction"); await f.Approve(p, "work-correction-review");
    f.Session = f.Session with { Id = Guid.NewGuid(), Revision = 2 };
    await Fails(() => f.Progress(), "leave_recovery_work_source_changed");
    Check((await f.Reader.ReadReservedAsync(f.Parent.Employee, default)).Count == 1, "Stale approved work remains exclusively reserved");
});
await Case("return is history only and configured claim count remains bounded", async () => {
    var f = await WorkFixture.Create(); f.Configure(x => x with { MaximumClaimsPerCommitment = 1 });
    var p = await f.Submit(0, 30, "work-return"); f.Admin();
    var returned = await f.Service.DecideAsync(f.Parent.Employee, p.Id, new(p.Version, ApprovalState.Returned, "Synthetic clarification"),
        "work-return-review", f.Parent.Context, default);
    Check(returned.State == ApprovalState.Returned && (await f.Progress()).FulfilledMinutes == 0, "Return cannot fulfil a promise");
    Check((await f.Reader.ReadReservedAsync(f.Parent.Employee, default)).Count == 0, "Return creates no work allocation");
    await Fails(() => f.Submit(30, 60, "work-return-second"), "leave_recovery_claim_count_limit");
});
await Case("session verification and closed source required", async () => {
    var f = await WorkFixture.Create(); f.Session = f.Session with { Verified = false };
    await Fails(() => f.Submit(0, 30, "work-unverified"), "leave_recovery_verified_session_required");
    f.Session = f.Session with { Verified = true, EndUtc = null };
    await Fails(() => f.Submit(0, 30, "work-open-session"), "leave_recovery_verified_session_required");
    f.Session = f.Session with { EndUtc = f.Parent.Clock.Now.AddHours(1) };
    await Fails(() => f.Submit(0, 30, "work-future-session"), "leave_recovery_verified_session_required");
});
await Case("lock arriving during protected submission write", async () => {
    var f = await WorkFixture.Create(); f.Parent.OnWrite = purpose => { if (purpose.EndsWith(":submission:v1")) f.Locked = true; };
    await Fails(() => f.Submit(0, 30, "work-write-lock"), "leave_recovery_period_locked");
    Check(f.Approvals.Count == 0, "Lock after proof storage creates no approval");
});
await Case("authority revoked during protected decision write", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-revoked");
    f.Parent.OnWrite = purpose => { if (purpose.EndsWith(":decision:v1")) f.Parent.FixedAdmin = false; };
    await Fails(() => f.Approve(p, "work-revoked-review"), "access_denied", 403);
    Check(f.Decisions.Count == 0 && f.Approvals.Single().State == ApprovalState.Pending, "Revocation creates no decision");
});
await Case("journal failure rolls back business and proof references", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-journal");
    f.Parent.FailJournalSave = true;
    await Fails(() => f.Approve(p, "work-journal-review"), "synthetic_journal_failure", 503);
    Check(f.Decisions.Count == 0 && f.Approvals.Single().State == ApprovalState.Pending, "Failed outer write rolls back approval and immutable decision");
    f.Parent.FailJournalSave = false;
    await f.Approve(p, "work-journal-review");
    Check(f.Decisions.Count == 1, "Retry after rollback has exactly one decision");
});
await Case("missing private submission proof is not an empty allocation", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-private"); await f.Approve(p, "work-private-review");
    f.Parent.Damage(f.Approvals.Single().SnapshotPrivateReference);
    await Fails(() => f.Reader.ReadReservedAsync(f.Parent.Employee, default), "private_evidence_unavailable");
});
await Case("real OT and makeup integration uses all-date retained recovery", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-integration"); await f.Approve(p, "work-integration-review");
    await Fails(() => f.CheckOvertime(f.Start, f.Start.AddMinutes(30), new(2026, 10, 17)), "overtime_work_credit_overlap");
    var claimed = await f.WorkTreatmentClaims();
    Check(claimed.Any(x => x.Role == WorkCreditRole.LeaveRecovery), "WorkTreatmentService consumes recovery allocations before calculation");
    await Fails(() => { WorkTreatmentRules.Exclusive([f.Range(WorkCreditRole.MakeUp, f.Start, f.Start.AddMinutes(30))], claimed); return Task.CompletedTask; }, "work_interval_already_claimed");
    await f.CheckOvertime(f.Start.AddMinutes(30), f.Start.AddMinutes(60), new(2026, 10, 17));
    Check(true, "Adjacent OT remains available");
});
await Case("missing shared allocation reader is fail closed", async () => {
    var f = await WorkFixture.Create();
    await Fails(() => f.CheckOvertime(f.Start, f.Start.AddMinutes(30), new(2026,10,16), omitReader: true), "leave_recovery_allocation_reader_unavailable");
});
await Case("submission and decision notifications are staged once with safe intents", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-notify");
    Check(f.Notifications.Count == 1 && f.Notifications[0].RecipientAccountId == f.Reviewer.AccountId &&
        f.Notifications[0].ResourceId == p.Id && f.Notifications[0].TemplateCode == NotificationTemplates.Decision, "Submission targets current independent review recipient");
    await f.Submit(0, 30, "work-notify");
    Check(f.Notifications.Count == 1, "Submission replay does not restage notification");
    await f.Approve(p, "work-notify-review"); await f.Approve(p, "work-notify-review");
    Check(f.Notifications.Count == 2 && f.Notifications[1].RecipientAccountId == f.Parent.Account &&
        f.Notifications[1].EventId == f.Decisions.Single().Id && f.Notifications[1].ResourceId == p.Id,
        "Decision uses immutable decision ID and original requester without duplicate notification");
});
await Case("submission outbox stage failure rolls back claim", async () => {
    var f = await WorkFixture.Create(); f.FailNotifications = true;
    await Fails(() => f.Submit(0, 30, "work-notify-failure"), "synthetic_notification_failure", 503);
    Check(f.Approvals.Count == 0 && f.Notifications.Count == 0, "No pending claim without its staged notification");
});
await Case("decision outbox stage failure rolls back fulfilment", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-notify-decision-failure"); f.FailNotifications = true;
    await Fails(() => f.Approve(p, "work-notify-decision-failure-review"), "synthetic_notification_failure", 503);
    Check(f.Decisions.Count == 0 && f.Approvals.Single().State == ApprovalState.Pending && f.Notifications.Count == 1,
        "Decision and allocation roll back together with notification failure");
});
await Case("no current independent recipient blocks submission", async () => {
    var f = await WorkFixture.Create(); f.ReviewRecipients.Clear();
    await Fails(() => f.Submit(0, 30, "work-no-recipient"), "leave_recovery_review_recipient_unavailable");
    Check(f.Approvals.Count == 0, "No unroutable silent success");
});
await Case("revoked Admin cannot read review queue or progress", async () => {
    var f = await WorkFixture.Create(); f.Admin(); f.Parent.FixedAdmin = false;
    await Fails(() => f.Service.QueueAsync(f.Parent.Context, default), "access_denied", 403);
    await Fails(() => f.Service.ProgressAsync(f.Parent.Parent.Request.Id, f.Commitment.Id, false, f.Parent.Context, default), "access_denied", 403);
});
await Case("recipient changed during submission outbox staging", async () => {
    var f = await WorkFixture.Create(); f.OnNotification = () => f.ReviewRecipients.Clear();
    await Fails(() => f.Submit(0, 30, "work-notify-recipient-changed"), "leave_recovery_review_recipient_unavailable");
    Check(f.Notifications.Count == 0 && f.Approvals.Count == 0, "Recipient change rolls back staged notification and claim");
});
await Case("review authority rechecked after decision notification staging", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-notify-revocation");
    f.OnNotification = () => f.Parent.FixedAdmin = false;
    await Fails(() => f.Approve(p, "work-notify-revocation-review"), "access_denied", 403);
    Check(f.Decisions.Count == 0 && f.Notifications.Count == 1 && f.Approvals.Single().State == ApprovalState.Pending,
        "Authority loss after staging cannot commit fulfilment");
});
await Case("Admin read authority rechecked after private evidence awaits", async () => {
    var f = await WorkFixture.Create(); await f.Submit(0, 30, "work-read-revoke"); f.Admin();
    f.Parent.OnRead = _ => f.Parent.FixedAdmin = false;
    await Fails(() => f.Service.QueueAsync(f.Parent.Context, default), "access_denied", 403);
    f.Parent.FixedAdmin = true;
    await Fails(() => f.Service.ProgressAsync(f.Parent.Parent.Request.Id, f.Commitment.Id, false, f.Parent.Context, default), "access_denied", 403);
});
await Case("commit fence catches lock after submission receipt IO", async () => {
    var f = await WorkFixture.Create();
    f.Parent.OnWrite = purpose => { if (purpose.EndsWith(LeaveRecoveryWorkEvidence.Submitted + ":audit")) f.Locked = true; };
    await Fails(() => f.Submit(0, 30, "work-final-submit-fence"), "leave_recovery_period_locked");
    Check(f.Approvals.Count == 0 && f.Notifications.Count == 0, "Final lock check rolls back business, outbox and receipt");
});
await Case("commit fence catches reviewer revocation after decision receipt IO", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-final-review-fence");
    f.Parent.OnWrite = purpose => { if (purpose.EndsWith(LeaveRecoveryWorkEvidence.Decided + ":audit")) f.Parent.FixedAdmin = false; };
    await Fails(() => f.Approve(p, "work-final-review-fence-decision"), "access_denied", 403);
    Check(f.Decisions.Count == 0 && f.Approvals.Single().State == ApprovalState.Pending && f.Notifications.Count == 1,
        "Final authority check rolls back decision and its notification");
});
await Case("commit fence catches source revision after decision receipt IO", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-final-source-fence");
    f.Parent.OnWrite = purpose => { if (purpose.EndsWith(LeaveRecoveryWorkEvidence.Decided + ":audit")) f.Session = f.Session with { Revision = 2 }; };
    await Fails(() => f.Approve(p, "work-final-source-fence-decision"), "leave_recovery_work_source_changed");
    Check(f.Decisions.Count == 0 && f.Notifications.Count == 1, "Final source check prevents stale fulfilment");
});
await Case("exact Admin authority expires during callback-owned source read", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-callback-role-expiry");
    var callbackPhase = false; var expiredDuringSourceRead = false;
    f.Parent.OnWrite = purpose => { if (purpose.EndsWith(LeaveRecoveryWorkEvidence.Decided + ":audit")) callbackPhase = true; };
    f.Parent.OnRead = purpose => {
        if (callbackPhase && purpose == LeaveService.DecisionPurpose(f.Parent.Parent.Request.Id)) {
            expiredDuringSourceRead = true; f.Parent.FixedAdmin = false; f.Parent.Clock.Now = f.Parent.Clock.Now.AddSeconds(1);
        }
    };
    await Fails(() => f.Approve(p, "work-callback-role-expiry-review"), "access_denied", 403);
    Check(expiredDuringSourceRead && f.Decisions.Count == 0 && f.Notifications.Count == 1,
        "Role loss during the final source reload rolls back decision and notification");
});
foreach (var approved in new[] { false, true }) {
    await Case("submission receipt rejects competing allocation, approved=" + approved, async () => {
        var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-replay-overlap");
        if (approved) await f.Approve(p, "work-replay-overlap-review");
        f.OtherClaims.Add(f.Range(WorkCreditRole.Overtime, f.Start, f.Start.AddMinutes(30)));
        await Fails(() => f.Submit(0, 30, "work-replay-overlap"), "work_interval_already_claimed");
        Check(f.Approvals.Count == 1 && f.Decisions.Count == (approved ? 1 : 0), "Conflicting replay creates no allocation or decision");
    });
}
await Case("submission receipt rechecks allocation after private receipt read", async () => {
    var f = await WorkFixture.Create(); await f.Submit(0, 30, "work-replay-read-overlap");
    f.Parent.OnRead = purpose => {
        if (purpose.EndsWith(LeaveRecoveryWorkEvidence.Submitted + ":result"))
            f.OtherClaims.Add(f.Range(WorkCreditRole.MakeUp, f.Start, f.Start.AddMinutes(30)));
    };
    await Fails(() => f.Submit(0, 30, "work-replay-read-overlap"), "work_interval_already_claimed");
});
await Case("decision receipt cannot replace missing retained decision", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-lost-decision");
    await f.Approve(p, "work-lost-decision-review");
    f.Decisions.Clear();
    WorkParentFixture.Set(f.Approvals.Single(), "State", ApprovalState.Pending);
    WorkParentFixture.Set(f.Approvals.Single(), "ClosedAt", null);
    f.Parent.Audits.RemoveAll(x => x.EventType == LeaveRecoveryWorkEvidence.Decided && x.TargetType == LeaveRecoveryWorkEvidence.FactTarget);
    await Fails(() => f.Approve(p, "work-lost-decision-review"), "leave_recovery_work_decision_unavailable");
    Check(f.Decisions.Count == 0 && f.Approvals.Single().State == ApprovalState.Pending, "Receipt alone cannot report fulfilled work");
});
await Case("submission receipt rechecks remaining commitment capacity", async () => {
    var f = await WorkFixture.Create(); await f.Submit(0, 30, "work-replay-capacity");
    var other = await f.Submit(30, 90, "work-replay-capacity-other");
    await f.Approve(other, "work-replay-capacity-review");
    await Fails(() => f.Submit(0, 30, "work-replay-capacity"), "leave_recovery_obligation_overallocated");
});
await Case("orphaned decision fact cannot silently release approved work", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-orphan-decision");
    await f.Approve(p, "work-orphan-decision-review");
    f.Decisions.Clear();
    WorkParentFixture.Set(f.Approvals.Single(), "State", ApprovalState.Pending);
    WorkParentFixture.Set(f.Approvals.Single(), "ClosedAt", null);
    await Fails(() => f.Reader.ReadReservedAsync(f.Parent.Employee, default), "leave_recovery_work_decision_unavailable");
});
foreach (var cancelled in new[] { false, true }) {
    foreach (var removeReceipts in new[] { false, true }) {
        await Case($"parent cancellation blocks direct replay, approved={cancelled}, receiptsRemoved={removeReceipts}", async () => {
            var f = await WorkFixture.Create();
            var leaveId = f.Parent.Parent.Request.Id;
            var commitmentId = f.Commitment.Id;
            const string key = "work-cancel-replay";
            f.Self();
            var source = await f.Service.SourceAsync(leaveId, commitmentId, f.Parent.WorkPolicy!.Id, f.Parent.Context, default);
            var request = new SubmitLeaveRecoveryWorkRequest(f.Parent.WorkPolicy.Id, source.Fingerprint,
                [new(f.Session.Id, f.Commitment.CommitmentDate, f.Start, f.Start.AddMinutes(30))], "Synthetic actual work");
            var p = await f.Service.SubmitAsync(leaveId, commitmentId, request, key, f.Parent.Context, default);
            await f.Approve(p, "work-cancel-replay-review");
            await f.Parent.PendingCancellation(cancelled);
            if (removeReceipts) f.Parent.Receipts.RemoveAll(x => x.Operation is LeaveRecoveryWorkEvidence.Submitted or LeaveRecoveryWorkEvidence.Decided);
            var receipts = f.Parent.Receipts.Count;
            var audits = f.Parent.Audits.Count;
            var notifications = f.Notifications.Count;
            f.Self();
            // Retry the original payload directly; fetching a new source would fail before exercising SubmitAsync.
            await Fails(() => f.Service.SubmitAsync(leaveId, commitmentId, request, key, f.Parent.Context, default),
                "leave_recovery_approval_unavailable");
            Check(f.Approvals.Count == 1 && f.Decisions.Count == 1 && f.Parent.Receipts.Count == receipts &&
                f.Parent.Audits.Count == audits && f.Notifications.Count == notifications,
                "Cancelled direct submission replay does not create another claim, decision, receipt, audit or notification");
            await Fails(() => f.Approve(p, "work-cancel-replay-review"), "leave_recovery_approval_unavailable");
            await Fails(() => f.Progress(), "leave_recovery_approval_unavailable");
            Check((await f.Reader.ReadReservedAsync(f.Parent.Employee, default)).Count == 1, "Blocked replay does not free approved actual work");
        });
    }
}
await Case("duplicate pending recovery cannot allocate the same actual minutes twice", async () => {
    var f = await WorkFixture.Create(); var first = await f.Submit(0, 30, "work-duplicate-first");
    var second = await f.Submit(0, 30, "work-duplicate-second");
    await f.Approve(first, "work-duplicate-first-review");
    await Fails(() => f.Approve(second, "work-duplicate-second-review"), "work_interval_already_claimed");
    var progress = await f.Progress();
    Check(progress.FulfilledMinutes == 30 && progress.RemainingMinutes == 30 && f.Decisions.Count == 1,
        "Only the first independent decision credits the overlapping interval");
});
await Case("authorized reviewer can return stale work without approving or freeing time", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-stale-return");
    f.Session = f.Session with { Revision = 2 };
    await Fails(() => f.Approve(p, "work-stale-return-approve"), "leave_recovery_work_source_changed");
    f.Admin();
    var returned = await f.Service.DecideAsync(f.Parent.Employee, p.Id,
        new(p.Version, ApprovalState.Returned, "Synthetic source clarification"), "work-stale-return-review", f.Parent.Context, default);
    Check(returned.State == ApprovalState.Returned && returned.ApprovedMinutes == 0 &&
        (await f.Reader.ReadReservedAsync(f.Parent.Employee, default)).Count == 0, "Authorized stale-work return creates no fulfilment allocation");
});
await Case("changed source blocks approved decision replay after receipt read", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-replay-source");
    await f.Approve(p, "work-replay-source-review");
    f.Parent.OnRead = purpose => {
        if (purpose.EndsWith(LeaveRecoveryWorkEvidence.Decided + ":result")) f.Session = f.Session with { Revision = 2 };
    };
    await Fails(() => f.Approve(p, "work-replay-source-review"), "leave_recovery_work_source_changed");
    Check((await f.Reader.ReadReservedAsync(f.Parent.Employee, default)).Count == 1, "Source change during replay retains original exclusive allocation");
});
await Case("decision replay rechecks exact Admin authority after receipt read", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-replay-authority");
    await f.Approve(p, "work-replay-authority-review");
    f.Parent.OnRead = purpose => {
        if (purpose.EndsWith(LeaveRecoveryWorkEvidence.Decided + ":result")) f.Parent.FixedAdmin = false;
    };
    await Fails(() => f.Approve(p, "work-replay-authority-review"), "access_denied", 403);
});
await Case("missing submission fact cannot hide an approved allocation", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-missing-submission");
    await f.Approve(p, "work-missing-submission-review");
    f.Parent.Audits.RemoveAll(x => x.EventType == LeaveRecoveryWorkEvidence.Submitted &&
        x.TargetType == LeaveRecoveryWorkEvidence.FactTarget && x.TargetId == p.Id.ToString());
    await Fails(() => f.Reader.ReadReservedAsync(f.Parent.Employee, default), "leave_recovery_work_evidence_unavailable");
    await Fails(() => f.Progress(), "leave_recovery_work_evidence_unavailable");
    await Fails(() => f.Submit(0, 30, "work-missing-submission-replacement"), "leave_recovery_work_evidence_unavailable");
    Check(f.Approvals.Count == 1 && f.Decisions.Count == 1, "Missing submission evidence cannot permit replacement allocation");
});
foreach (var state in new[] { ApprovalState.Returned, ApprovalState.Rejected })
    await Case("review lifecycle and durable retry " + state, async () => {
        var f = await WorkFixture.Create();
        Check((await f.Progress()).Status == "commitment_only", "Promise alone has no fulfilled work");
        var p = await f.Submit(0, 30, "work-terminal-claim");
        var progress = await f.Progress();
        Check(progress.FulfilledMinutes == 0 && progress.RemainingMinutes == 60 && progress.Claims.Single().State == ApprovalState.Pending,
            "Pending actual work does not reduce the obligation");
        f.Admin();
        Check((await f.Service.QueueAsync(f.Parent.Context, default)).Single().Id == p.Id, "Pending claim enters reviewer queue");
        var detail = await f.Service.DetailAsync(f.Parent.Employee, p.Id, false, f.Parent.Context, default);
        Check(detail.Request.Intervals.Single().SessionId == f.Session.Id && detail.RetainedSource.Parent.Commitment.Id == f.Commitment.Id,
            "Review detail retains the selected session and commitment");
        var request = new DecideLeaveRecoveryWorkRequest(p.Version, state, "Synthetic non-credit decision");
        var decided = await f.Service.DecideAsync(f.Parent.Employee, p.Id, request, "work-terminal-review", f.Parent.Context, default);
        Check(decided.State == state && decided.ApprovedMinutes == 0 && (await f.Service.QueueAsync(f.Parent.Context, default)).Count == 0,
            "Return or rejection closes the queue without work credit");
        f.Parent.Receipts.Clear();
        f.Session = f.Session with { Revision = 2 };
        var replay = await f.Service.DecideAsync(f.Parent.Employee, p.Id, request, "work-terminal-review", f.Parent.Context, default);
        Check(replay == decided && f.Decisions.Count == 1 && f.Notifications.Count == 2,
            "Non-credit decision reconciles without receipts despite later source change");
        f.Parent.Receipts.Clear();
        await Fails(() => f.Service.DecideAsync(f.Parent.Employee, p.Id, request with { Reason = "Changed reason" },
            "work-terminal-review", f.Parent.Context, default), "leave_recovery_work_already_decided");
        await Fails(() => f.Approve(p, "work-terminal-reopen"), "leave_recovery_work_already_decided");
        progress = await f.Progress();
        Check(progress.FulfilledMinutes == 0 && progress.RemainingMinutes == 60 &&
            (await f.Reader.ReadReservedAsync(f.Parent.Employee, default)).Count == 0, "Closed non-credit claim leaves all recovery minutes outstanding");
        var replacement = await f.Submit(0, 60, "work-terminal-replacement");
        await f.Approve(replacement, "work-terminal-replacement-review");
        progress = await f.Progress();
        Check(progress.Status == "fulfilled" && progress.Claims.Count == 2 && progress.FulfilledMinutes == 60,
            "Fresh current-source claim fulfils work while preserving closed history");
    });
await Case("detail read rechecks reviewer authority after protected evidence IO", async () => {
    var f = await WorkFixture.Create(); var p = await f.Submit(0, 30, "work-detail-authority"); f.Admin();
    f.Parent.OnRead = _ => f.Parent.FixedAdmin = false;
    await Fails(() => f.Service.DetailAsync(f.Parent.Employee, p.Id, false, f.Parent.Context, default), "access_denied", 403);
});
Console.WriteLine($"Recovery work claims: {checks - failed}/{checks} checks passed");
Environment.ExitCode = failed == 0 ? 0 : 1;

public sealed class WorkFixture
{
    public WorkParentFixture Parent = null!;
    public LeaveRecoveryWorkService Service = null!;
    public LeaveRecoveryCreditReader Reader = null!;
    public IWorkTreatmentCreditReader Credits = null!;
    public IWorkTreatmentRepository WorkRepository = null!;
    public WorkTreatmentEvidence WorkEvidence = null!;
    public LeaveRecoveryCommitmentResponse Commitment = null!;
    public AuthenticatedAccount Reviewer = null!;
    public WorkTreatmentSession Session = null!;
    public DateTimeOffset Start = new(2026, 10, 16, 15, 0, 0, TimeSpan.Zero);
    public Guid Site = Guid.NewGuid(), Shift = Guid.NewGuid();
    public bool Locked;
    public bool FailNotifications;
    public Action? OnNotification;
    public List<Guid> ReviewRecipients = [];
    public List<NotificationIntent> Notifications = [];
    public List<ApprovalRequest> Approvals = [];
    public List<ApprovalDecision> Decisions = [];
    public List<WorkCreditRange> OtherClaims = [];
    public static async Task<WorkFixture> Create() {
        var f = new WorkFixture(); await f.Initialize(); return f;
    }
    async Task Initialize() {
        Parent = await WorkParentFixture.Create(); Reviewer = Parent.Actor;
        ReviewRecipients.Add(Reviewer.AccountId);
        Parent.BeginChildTransaction = () => {
            var approvals = Approvals.ToArray(); var decisions = Decisions.ToArray();
            var notifications = Notifications.ToArray();
            var versions = approvals.Select(x => (Row: x, x.State, x.ClosedAt, Version: x.RowVersion.ToArray())).ToArray();
            return () => {
                Approvals = approvals.ToList(); Decisions = decisions.ToList();
                Notifications = notifications.ToList();
                foreach (var value in versions) {
                    WorkParentFixture.Set(value.Row, "State", value.State); WorkParentFixture.Set(value.Row, "ClosedAt", value.ClosedAt);
                    WorkParentFixture.Set(value.Row, "RowVersion", value.Version);
                }
            };
        };
        Commitment = await Parent.Create("work-commitment-one");
        Parent.Clock.Now = new(2026, 10, 16, 18, 0, 0, TimeSpan.Zero);
        var rules = new LeaveRecoveryFulfilmentSettings(RecoveryQualifyingWork.OutsideScheduledDuty, RecoveryCreditBasis.ExactWorkedMinutes,
            RecoveryReviewAuthority.FixedAdmin, true, 15, 120, 8, 30,
            RecoveryParentChangeHandling.BlockClaimsRetainAllocations, RecoverySourceChangeHandling.RejectChangedSourcesRetainAllocations);
        var json = System.Text.Json.Nodes.JsonNode.Parse(Parent.Policy.RulesJson)!.AsObject();
        json["SettingsVersion"] = 4; json["RecoveryFulfilment"] = JsonSerializer.SerializeToNode(rules);
        Parent.WorkPolicy = new(Guid.NewGuid(), 1, new(2026, 4, 1), new(2027, 4, 1), "leave-settings-v4",
            json.ToJsonString(), null, null, Guid.NewGuid().ToString(), Parent.Clock.Now.AddMonths(-6), PolicyAuthority.DirectHr);
        Session = new(Guid.NewGuid(), Guid.NewGuid(), 1, Commitment.CommitmentDate, Start, Start.AddHours(2), Site, null, Shift,
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), null, null, true, false);
        var repo = WorkProxy.Make<ILeaveRecoveryWorkRepository>((m,a) => m.Name switch {
            "RequireTransaction" => null,
            "FindAsync" => Task.FromResult(Find((Guid)a[0]!, (Guid)a[1]!)),
            "FactsAsync" => Task.FromResult<IReadOnlyList<AuditEvent>>(Parent.Audits.Where(x => x.EmployeeScopeId == (Guid)a[0]! &&
                x.TargetType == LeaveRecoveryWorkEvidence.FactTarget && x.EventType == (string)a[1]!).ToArray()),
            "Add" when a[0] is ApprovalRequest row => Add(Approvals, row),
            "Add" when a[0] is ApprovalDecision row => Add(Decisions, row),
            "Add" when a[0] is AuditEvent row => Add(Parent.Audits, row),
            "SaveAsync" => Save(), "ExpectVersion" => null,
            "PendingAsync" => Task.FromResult<IReadOnlyList<(Guid EmployeeId, Guid Id)>>(Approvals.Where(x => x.State == ApprovalState.Pending).Select(x => (Parent.Employee, x.SubjectId)).ToArray()),
            "ReviewRecipientAccountIdsAsync" => Task.FromResult<IReadOnlyList<Guid>>(ReviewRecipients.ToArray()),
            _ => throw new Exception(m.Name)
        });
        var workRepo = WorkProxy.Make<IWorkTreatmentRepository>((m,a) => m.Name switch {
            "RequireTransaction" => null,
            "SessionsAsync" => Task.FromResult<IReadOnlyList<WorkTreatmentSession>>([Session]),
            "EmploymentAsync" => Task.FromResult<WorkTreatmentEmployment?>(new(Session.AssignmentId!.Value, Parent.Category, "SITE", Site, null, new(2020,1,1), null)),
            "LockedAsync" => Task.FromResult(Locked),
            "ApprovedOvertimeAsync" => Task.FromResult<IReadOnlyList<WorkCreditRange>>(OtherClaims.ToArray()),
            "FactsAsync" => Task.FromResult<IReadOnlyList<AuditEvent>>([]),
            _ => throw new Exception(m.Name)
        });
        var rosters = WorkProxy.Make<IIndividualRosterResolver>((m,a) => {
            var date = (DateOnly)a[1]!;
            var start = new DateTimeOffset(date.ToDateTime(new(4,0)), TimeSpan.Zero);
            return Task.FromResult(new EffectiveRosterResponse(Parent.Employee, date, "assigned", "Asia/Karachi", Site,
                Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("20000000-0000-0000-0000-000000000001"), Shift, 1,
                start, start.AddHours(9), 540, 0, 0));
        });
        var evidence = new LeaveRecoveryWorkEvidence(repo, Parent.Operation, Parent.Store);
        Reader = new(evidence);
        WorkRepository = workRepo; WorkEvidence = new(workRepo, Parent.Operation, Parent.Store);
        Credits = new WorkTreatmentCreditReader(workRepo, WorkEvidence, null!, Reader);
        var notifications = WorkProxy.Make<INotificationOutbox>((m,a) => {
            if (m.Name != "StageAsync") throw new Exception(m.Name);
            if (FailNotifications) throw new AuthenticationFailure("synthetic_notification_failure", 503);
            var intent = (NotificationIntent)a[0]!; NotificationOutbox.Validate(intent);
            if (Notifications.Contains(intent)) return Task.FromResult(false);
            OnNotification?.Invoke();
            Notifications.Add(intent); return Task.FromResult(true);
        });
        object?[] dependencies = [Parent.Operation, repo, evidence, new LeaveRecoveryWorkSources(Parent.Service, workRepo, rosters),
            Parent.Service, Parent.Authority, Credits, Reader, Parent.Journal, Parent.Clock];
        var constructor = typeof(LeaveRecoveryWorkService).GetConstructors().Single();
        Service = (LeaveRecoveryWorkService)constructor.Invoke(constructor.GetParameters().Any(x => x.ParameterType == typeof(INotificationOutbox))
            ? [.. dependencies, notifications] : dependencies);
    }
    public void Self() => Parent.Actor = Reviewer with { AccountId = Parent.Account, EmployeeId = Parent.Employee };
    public void Admin() => Parent.Actor = Reviewer;
    public async Task<LeaveRecoveryWorkView> Submit(int from, int to, string key) {
        Self();
        var source = await Service.SourceAsync(Parent.Parent.Request.Id, Commitment.Id, Parent.WorkPolicy!.Id, Parent.Context, default);
        return await Service.SubmitAsync(Parent.Parent.Request.Id, Commitment.Id,
            new(Parent.WorkPolicy.Id, source.Fingerprint, [new(Session.Id, Commitment.CommitmentDate, Start.AddMinutes(from), Start.AddMinutes(to))], "Synthetic actual work"),
            key, Parent.Context, default);
    }
    public Task<LeaveRecoveryWorkView> Approve(LeaveRecoveryWorkView claim, string key) {
        Admin();
        return Service.DecideAsync(Parent.Employee, claim.Id, new(claim.Version, ApprovalState.Approved, "Synthetic independent review"), key, Parent.Context, default);
    }
    public Task<LeaveRecoveryWorkProgress> Progress() {
        Self(); return Service.ProgressAsync(Parent.Parent.Request.Id, Commitment.Id, true, Parent.Context, default);
    }
    public WorkCreditRange Range(WorkCreditRole role, DateTimeOffset start, DateTimeOffset end) =>
        new(Parent.Employee, Guid.NewGuid(), role, Session.Key, Session.Id, Commitment.CommitmentDate, start, end);
    public void Configure(Func<LeaveRecoveryFulfilmentSettings, LeaveRecoveryFulfilmentSettings> change) {
        var policy = Parent.WorkPolicy!;
        var settings = LeavePolicySettings.Parse(policy.RulesJson, policy.RulesSchemaVersion);
        WorkParentFixture.Set(policy, "RulesJson", JsonSerializer.Serialize(settings with { RecoveryFulfilment = change(settings.RecoveryFulfilment!) }));
    }
    public async Task CheckOvertime(DateTimeOffset start, DateTimeOffset end, DateOnly date, bool omitReader = false) {
        var service = new OvertimeService(null!, null!, null!, null!, null!, null!, null!, null!, null!, recoveryCredits: omitReader ? null : Reader);
        var method = typeof(OvertimeService).GetMethod("RequireWorkAvailable", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)method.Invoke(service, [Parent.Employee, date, new OvertimeEligibleInterval[] { new(start, end) }, CancellationToken.None])!;
    }
    public async Task<IReadOnlyList<WorkCreditRange>> WorkTreatmentClaims() {
        var service = new WorkTreatmentService(null!, WorkRepository, null!, WorkEvidence, Parent.Journal, null!, Reader);
        var method = typeof(WorkTreatmentService).GetMethod("Claimed", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return await (Task<IReadOnlyList<WorkCreditRange>>)method.Invoke(service, [Parent.Employee, CancellationToken.None])!;
    }
    LeaveRecoveryWorkRow? Find(Guid employee, Guid id) {
        var a = Approvals.SingleOrDefault(x => x.SubjectId == id);
        return employee == Parent.Employee && a is not null ? new(a, Decisions.SingleOrDefault(x => x.ApprovalRequestId == a.Id)) : null;
    }
    Task Save() {
        foreach (var a in Approvals) WorkParentFixture.Set(a, "RowVersion", BitConverter.GetBytes(1L + Decisions.Count(x => x.ApprovalRequestId == a.Id)));
        return Task.CompletedTask;
    }
    static object? Add<T>(List<T> list, T row) { list.Add(row); return null; }
}
public class WorkProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, WorkProxy>(); ((WorkProxy)(object)proxy).Handler = handler; return proxy; }
}
public sealed class WorkClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public sealed class WorkParentFixture
{
    public readonly Guid Employee = Guid.NewGuid(), Account = Guid.NewGuid(), Category = Guid.NewGuid(), Role = Guid.NewGuid(), CustomRole = Guid.NewGuid();
    public readonly WorkClock Clock = new();
    public readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-g1-recovery");
    public AuthenticatedAccount Actor = null!;
    public bool PolicyAvailable = true, ParentAvailable = true, Current = true, FixedAdmin = true;
    public bool CustomGrantFirst, FailJournalSave;
    public bool AdminGrant = true;
    public int? WritesUntilFailure;
    public Action<string>? OnWrite;
    public Action<string>? OnRead;
    public Func<Action>? BeginChildTransaction;
    private readonly SemaphoreSlim transaction = new(1, 1);
    public LeaveRow Parent = null!;
    public readonly Dictionary<Guid, LeaveRow> OtherParents = [];
    public bool HideDateRows;
    public PolicyVersion Policy = null!;
    public PolicyVersion? WorkPolicy;
    public ISchedulingJournalRepository Journal = null!;
    public LeaveEncashmentService Service = null!;
    public AttendanceOperation Operation = null!;
    public LeaveProvenance Provenance = null!;
    public IPrivateFileStore Store = null!;
    public ILeaveRepository Leaves = null!;
    public ILeaveEncashmentRepository Repository = null!;
    public LeaveReviewAuthority Authority = null!;
    public List<LeaveRecoveryCommitment> Commitments = [];
    public List<IdempotencyReceipt> Receipts = [];
    public List<AuditEvent> Audits = [];
    public Dictionary<string, (byte[] Bytes, string Purpose)> Files = [];
    public CreateLeaveRecoveryCommitmentRequest Request => new(new(2026, 10, 16), 60, false, "Synthetic recovery promise");
    public static async Task<WorkParentFixture> Create()
    {
        var f = new WorkParentFixture(); await f.Initialize(); return f;
    }
    async Task Initialize()
    {
        Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
        var current = WorkProxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = WorkProxy.Make<IAccountAccessReader>((m, a) => Task.FromResult(Grants()));
        var authz = WorkProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, a),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(FixedAdmin && (Guid)a[1]! == Role),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>(FixedAdmin && (Guid)a[0]! == Role ? Cosmix.Domain.Authentication.Role.CreateSystem(SystemRoleKind.Admin, Guid.NewGuid(), Clock.Now).Code : "CustomHR"),
            _ => throw new Exception(m.Name)
        });
        Store = WorkProxy.Make<IPrivateFileStore>((m, a) => m.Name switch {
            "WriteAsync" => Write((byte[])a[0]!, (string)a[1]!),
            "ReadAsync" => Read((PrivateFileReference)a[0]!, (string)a[1]!), _ => throw new Exception(m.Name)
        });
        var journal = WorkProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(Current),
            "SaveChangesAsync" => FailJournalSave ? Task.FromException(new AuthenticationFailure("synthetic_journal_failure", 503)) : Task.CompletedTask,
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)a[0]!), "AddAudit" => Add(Audits, (AuditEvent)a[0]!), _ => throw new Exception(m.Name)
        });
        var auth = WorkProxy.Make<IAuthenticationRepository>((m, a) => throw new Exception("Provider call forbidden"));
        Journal = journal;
        Operation = new(current, access, authz, journal, auth, Clock, Store);
        Provenance = new(Operation, Store); Authority = new(access, authz);
        var settings = new LeavePolicySettings(11, 3, 4, 2, 420, [Category], Guid.NewGuid(), LeaveHolidayTreatment.Exclude, []) {
            SettingsVersion = 3, YearStartMonth = 4, YearStartDay = 1,
            Recovery = new([LeaveKind.Other], [LeavePayTreatment.Unpaid], 420, 120, 30, 60)
        };
        var policy = new Policy("SYNTHETIC-G1", "Synthetic recovery policy", PolicyCategory.Leave);
        Policy = new(policy.Id, 1, new(2026, 4, 1), new(2027, 4, 1), settings.PublishedSchemaVersion,
            JsonSerializer.Serialize(settings), null, null, Guid.NewGuid().ToString(), Clock.Now.AddMonths(-6), PolicyAuthority.DirectHr);
        var type = new LeaveType("SYNTHETIC-G1", "Synthetic recovery leave", false, false, false, LeaveKind.Other);
        var date = new DateOnly(2026, 10, 10); var start = new DateTimeOffset(2026, 10, 10, 4, 0, 0, TimeSpan.Zero);
        var leave = new LeaveRequest(Employee, type.Id, date, date, 420, "Synthetic leave", null, null, null, null,
            LeavePayTreatment.PendingReview, null, Account.ToString(), Clock.Now.AddDays(-10));
        leave.Days.Add(new(leave.Id, date, start, start.AddHours(7), Guid.NewGuid()));
        var approval = new ApprovalRequest(LeaveRules.RequestType, leave.Id, Account.ToString(), await Provenance.FreezeAsync(leave, default), 1, leave.CreatedAt);
        leave.Submit(approval);
        var proof = new LeaveDecisionProof(leave.Id, ApprovalState.Approved, LeavePayTreatment.Unpaid, Policy.Id, LeaveProvenance.SubmissionDigest(leave),
            Policy.RulesJson, Category, new(2020, 1, 1), [], new(0, 0));
        var reference = AttendanceOperation.Reference(await Write(JsonSerializer.SerializeToUtf8Bytes(proof), LeaveService.DecisionPurpose(leave.Id)));
        var decision = approval.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(), "Admin", "Synthetic approval", Clock.Now.AddDays(-9), reference);
        leave.SetApprovedTreatment(LeavePayTreatment.Unpaid, Policy.Id);
        Parent = new(leave, type, approval, [decision], [], null);
        Leaves = WorkProxy.Make<ILeaveRepository>((m, a) => m.Name switch {
            "FindAsync" => Task.FromResult(Find((Guid)a[0]!, (Guid?)a[1])),
            "EmployeeExistsAsync" => Task.FromResult(true), "AnyLockedAsync" => Task.FromResult(false),
            "PolicyAsync" => Task.FromResult<LeavePolicyEvidence?>(PolicyAvailable ? new((Guid)a[2]! == Policy.Id ? Policy : WorkPolicy!, Category, new(2020, 1, 1), true, [], new(0, 0)) : null),
            _ => throw new Exception("Leave " + m.Name)
        });
        Repository = WorkProxy.Make<ILeaveEncashmentRepository>((m, a) => m.Name switch {
            "GetRecoveryCommitmentsAsync" => Task.FromResult<IReadOnlyList<LeaveRecoveryCommitment>>(Commitments.Where(x => x.LeaveRequestId == (Guid)a[0]!).ToArray()),
            "RecoveryOnDateAsync" => Task.FromResult<IReadOnlyList<LeaveRecoveryCommitment>>(Commitments.Where(x => !HideDateRows && x.CommitmentDate == (DateOnly)a[1]!).ToArray()),
            "RecoveryReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == ((Guid)a[0]!).ToString() && x.Key == (string)a[1]! && x.Operation == LeaveEncashmentService.RecoveryOperation)),
            "RecoveryAuditAsync" => Audit((Guid)a[0]!),
            "Add" when a[0] is LeaveRecoveryCommitment c => Add(Commitments, c),
            "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception("Recovery " + m.Name)
        });
        Service = new(Operation, Repository, Store, Leaves, Authority, Provenance, null!);
    }

    LeaveRow? Find(Guid id, Guid? employee)
    {
        var row = ParentAvailable && Parent.Request.Id == id ? Parent : OtherParents.GetValueOrDefault(id);
        return row is not null && (!employee.HasValue || row.Request.EmployeeId == employee) ? row : null;
    }
    public async Task SwitchParent()
    {
        OtherParents.Add(Parent.Request.Id, Parent);
        var previous = Parent.Request;
        var leave = new LeaveRequest(Employee, Parent.Type.Id, previous.StartDate, previous.EndDate, 420,
            "Synthetic second leave", null, null, null, null, LeavePayTreatment.PendingReview, null,
            Account.ToString(), previous.CreatedAt);
        var day = previous.Days.Single();
        leave.Days.Add(new(leave.Id, day.Date, day.StartsAtUtc, day.EndsAtUtc, day.ShiftVersionId));
        var approval = new ApprovalRequest(LeaveRules.RequestType, leave.Id, Account.ToString(),
            await Provenance.FreezeAsync(leave, default), 1, leave.CreatedAt);
        leave.Submit(approval);
        var proof = new LeaveDecisionProof(leave.Id, ApprovalState.Approved, LeavePayTreatment.Unpaid,
            Policy.Id, LeaveProvenance.SubmissionDigest(leave), Policy.RulesJson, Category, new(2020, 1, 1), [], new(0, 0));
        var reference = AttendanceOperation.Reference(await Write(JsonSerializer.SerializeToUtf8Bytes(proof), LeaveService.DecisionPurpose(leave.Id)));
        var decision = approval.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(), "Admin", "Synthetic approval",
            Clock.Now.AddDays(-9), reference);
        leave.SetApprovedTreatment(LeavePayTreatment.Unpaid, Policy.Id);
        Parent = new(leave, Parent.Type, approval, [decision], [], null);
    }
    public async Task RewriteAudit(Guid id, Action<System.Text.Json.Nodes.JsonObject> change)
    {
        var audit = Audits.Single(x => x.EventType == LeaveEncashmentService.RecoveryOperation && x.TargetId == id.ToString());
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(audit.ProtectedChangesReference!)!;
        var stored = Files[reference.StorageKey];
        var document = System.Text.Json.Nodes.JsonNode.Parse(stored.Bytes)!.AsObject();
        change(document["Before"]!.AsObject());
        Set(audit, "ProtectedChangesReference", AttendanceOperation.Reference(await Write(
            JsonSerializer.SerializeToUtf8Bytes(document), stored.Purpose)));
    }

    public async Task PendingCancellation(bool approve = false)
    {
        var cancellation = new LeaveCancellationRequest(Parent.Request.Id, Account.ToString(), "Synthetic cancellation", Clock.Now);
        var reference = AttendanceOperation.Reference(await Write(JsonSerializer.SerializeToUtf8Bytes(new LeaveService.CancellationSnapshot(
            cancellation.Id, Parent.Request.Id, Account.ToString(), cancellation.Reason, Parent.Approval!.Id)), LeaveService.CancellationPurpose(cancellation.Id)));
        var approval = new ApprovalRequest(LeaveRules.CancellationType, cancellation.Id, Account.ToString(), reference, 1, Clock.Now);
        cancellation.Submit(approval);
        var decisions = approve
            ? new[] { approval.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(), "Admin", "Synthetic cancellation approval", Clock.Now) }
            : Array.Empty<ApprovalDecision>();
        Parent = Parent with { Cancellations = [new(cancellation, approval, decisions)] };
    }
    public async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await transaction.WaitAsync(ct);
        var commitments = Commitments.ToArray(); var receipts = Receipts.ToArray(); var audits = Audits.ToArray();
        var rollback = BeginChildTransaction?.Invoke();
        try { return await action(ct); }
        catch { Commitments = commitments.ToList(); Receipts = receipts.ToList(); Audits = audits.ToList(); rollback?.Invoke(); throw; }
        finally { transaction.Release(); }
    }
    public Task<LeaveRecoveryCommitmentResponse> Create(string key) => Service.CreateRecoveryCommitmentAsync(Parent.Request.Id, Request, key, Context, default);
    public static void Set(object target, string property, object? value) => target.GetType().GetProperty(property)!.SetValue(target, value);
    IReadOnlyList<PermissionGrant> Grants()
    {
        var grants = new List<PermissionGrant>();
        if (CustomGrantFirst) grants.Add(new(LeavePermissions.Review, false, CustomRole, AccessScopeKind.All, null));
        grants.Add(new(LeavePermissions.Self, false, Role, AccessScopeKind.Self, Actor.EmployeeId));
        if (AdminGrant) grants.Add(new(LeavePermissions.Review, false, Role, AccessScopeKind.All, null));
        return grants;
    }
    Task<AuditEvent?> Audit(Guid id)
    {
        var rows = Audits.Where(x => x.EventType == LeaveEncashmentService.RecoveryOperation && x.TargetType == "Attendance" && x.TargetId == id.ToString()).ToArray();
        if (rows.Length > 1) throw AttendanceOperation.Conflict("leave_recovery_audit_conflict");
        return Task.FromResult(rows.SingleOrDefault());
    }
    public void Damage(string serialized)
    {
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(serialized)!;
        Files[reference.StorageKey] = ("{}"u8.ToArray(), Files[reference.StorageKey].Purpose);
    }
    static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
    public Task<PrivateFileReference> Write(byte[] bytes, string purpose)
    {
        OnWrite?.Invoke(purpose);
        if (WritesUntilFailure is int remaining) {
            WritesUntilFailure = remaining - 1;
            if (remaining == 1) throw new AuthenticationFailure("synthetic_storage_failure", 503);
        }
        var key = Guid.NewGuid().ToString("N"); Files.Add(key, (bytes.ToArray(), purpose));
        return Task.FromResult(new PrivateFileReference(key, "synthetic-version", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic-key", bytes.Length));
    }
    Task<byte[]> Read(PrivateFileReference reference, string purpose)
    {
        OnRead?.Invoke(purpose);
        if (!Files.TryGetValue(reference.StorageKey, out var entry) || entry.Purpose != purpose ||
            Convert.ToHexString(SHA256.HashData(entry.Bytes)) != reference.Sha256)
            throw AttendanceOperation.Conflict("private_evidence_unavailable");
        return Task.FromResult(entry.Bytes.ToArray());
    }
}




