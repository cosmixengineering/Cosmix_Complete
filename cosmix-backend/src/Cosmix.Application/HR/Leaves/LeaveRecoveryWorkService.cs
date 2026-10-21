using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.HR.Leaves;

public sealed class LeaveRecoveryWorkService(AttendanceOperation operation, ILeaveRecoveryWorkRepository repository,
    LeaveRecoveryWorkEvidence evidence, LeaveRecoveryWorkSources sources, LeaveEncashmentService parents,
    LeaveReviewAuthority authority, IWorkTreatmentCreditReader credits, ILeaveRecoveryCreditReader recoveryCredits,
    ISchedulingJournalRepository journal, TimeProvider clock, INotificationOutbox notifications)
{
    public Task<LeaveRecoveryWorkSource> SourceAsync(Guid leaveId, Guid commitmentId, Guid policyId,
        AuthRequestContext context, CancellationToken ct) => operation.ReadAsync(LeavePermissions.Self,
        "attendance.leave.recovery.work.source", null, context, async (actor, token) =>
            await sources.LoadAsync(leaveId, commitmentId, actor.Identity.EmployeeId, policyId, actor.Now, token), ct, self: true);

    public Task<LeaveRecoveryWorkView> SubmitAsync(Guid leaveId, Guid commitmentId, SubmitLeaveRecoveryWorkRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        if (request is null) throw AttendanceOperation.Invalid();
        return operation.WriteAsync<LeaveRecoveryWorkView>(LeavePermissions.Self, LeaveRecoveryWorkEvidence.Submitted, null, key,
            new { leaveId, commitmentId, request }, request.Reason, context, async (actor, token) =>
            {
                var rows = await evidence.ListAsync(actor.Identity.EmployeeId, token);
                var prior = Retry(rows, actor, leaveId, commitmentId, request, key);
                if (prior is not null)
                {
                    await CurrentAsync(prior.Submission, actor, token);
                    return new(prior.Submission.Id, null, View(prior));
                }
                var source = await sources.LoadAsync(leaveId, commitmentId, actor.Identity.EmployeeId, request.PolicyVersionId, actor.Now, token);
                if (LeaveRules.LocalDate(actor.Now).DayNumber - source.Parent.Commitment.CommitmentDate.DayNumber > source.Policy.Settings.SubmissionWindowDays)
                    throw AttendanceOperation.Conflict("leave_recovery_submission_window_closed");
                var id = Guid.NewGuid();
                var ranges = LeaveRecoveryWorkRules.Evaluate(id, request, source, actor.Now);
                Capacity(rows, source, id, LeaveRecoveryWorkRules.Minutes(ranges), true);
                var submission = new LeaveRecoveryWorkSubmission(1, id, actor.Identity.EmployeeId, actor.Identity.AccountId,
                    actor.Identity.SessionId, key, actor.Now, request, source, ranges);
                await AvailableAsync(submission, token);
                var reference = await evidence.StoreAsync(actor.Identity.EmployeeId, id, "submission", submission, token);
                await CurrentAsync(submission, actor, token);
                Capacity(await evidence.ListAsync(actor.Identity.EmployeeId, token), source, id, LeaveRecoveryWorkRules.Minutes(ranges), true);
                await AvailableAsync(submission, token);
                var recipients = await ReviewRecipientsAsync(actor.Identity.EmployeeId, actor.Identity.AccountId, token);
                foreach (var recipient in recipients)
                    await notifications.StageAsync(new(id, recipient, NotificationTemplates.Decision, id), context, token);
                if (!recipients.SequenceEqual(await ReviewRecipientsAsync(actor.Identity.EmployeeId, actor.Identity.AccountId, token)))
                    throw AttendanceOperation.Conflict("leave_recovery_review_recipients_changed");
                await CurrentAsync(submission, actor, token);
                Capacity(await evidence.ListAsync(actor.Identity.EmployeeId, token), source, id, LeaveRecoveryWorkRules.Minutes(ranges), true);
                await AvailableAsync(submission, token);
                var approval = new ApprovalRequest(LeaveRecoveryWorkEvidence.RequestType, id, actor.Identity.AccountId.ToString(), reference, 1, actor.Now);
                repository.Add(approval);
                repository.Add(LeaveRecoveryWorkEvidence.Fact(LeaveRecoveryWorkEvidence.Submitted, actor.Identity.EmployeeId, id, actor, reference, context.CorrelationId));
                await repository.SaveAsync(token);
                return new(id, null, View(new(new(approval, null), submission, null)));
            }, ct, self: true, beforeReplay: async (actor, token) =>
            {
                var rows = await evidence.ListAsync(actor.Identity.EmployeeId, token);
                var prior = Retry(rows, actor, leaveId, commitmentId, request, key);
                if (prior is not null)
                {
                    await CurrentAsync(prior.Submission, actor, token);
                    Capacity(rows, prior.Submission.Source, prior.Submission.Id, LeaveRecoveryWorkRules.Minutes(prior.Submission.Ranges), false);
                    await AvailableAsync(prior.Submission, token);
                    await SessionAsync(actor, token);
                }
                else if (await journal.FindReceiptAsync(actor.Identity.AccountId.ToString(), LeaveRecoveryWorkEvidence.Submitted, key, token) is not null)
                    throw AttendanceOperation.Conflict("leave_recovery_work_evidence_unavailable");
            }, beforeCommit: async (actor, token) =>
            {
                var rows = await evidence.ListAsync(actor.Identity.EmployeeId, token);
                var row = Retry(rows, actor, leaveId, commitmentId, request, key) ??
                    throw AttendanceOperation.Conflict("leave_recovery_work_evidence_unavailable");
                await CurrentAsync(row.Submission, actor, token);
                Capacity(rows, row.Submission.Source, row.Submission.Id, LeaveRecoveryWorkRules.Minutes(row.Submission.Ranges), false);
                await AvailableAsync(row.Submission, token);
                await SessionAsync(actor, token);
            });
    }

    public Task<LeaveRecoveryWorkView> DecideAsync(Guid employeeId, Guid id, DecideLeaveRecoveryWorkRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        if (request is null || request.Decision is not (ApprovalState.Approved or ApprovalState.Rejected or ApprovalState.Returned))
            throw AttendanceOperation.Invalid("invalid_leave_recovery_decision");
        return operation.WriteAsync<LeaveRecoveryWorkView>(LeavePermissions.Review, LeaveRecoveryWorkEvidence.Decided, employeeId, key,
            new { id, request }, request.Reason, context, async (actor, token) =>
            {
                var row = await evidence.ReadAsync(employeeId, id, token);
                await ReviewerAsync(actor, row, token);
                if (row.Decision is not null)
                {
                    if (row.Decision.DecisionKey != key || row.Decision.ReviewerAccountId != actor.Identity.AccountId ||
                        WorkTreatmentRules.Hash(row.Decision.Request) != WorkTreatmentRules.Hash(request))
                        throw AttendanceOperation.Conflict("leave_recovery_work_already_decided");
                    if (row.Decision.State == ApprovalState.Approved) await CurrentAsync(row.Submission, actor, token);
                    return new(id, null, View(row));
                }
                var version = LeaveRules.Version(row.Row.Approval, request.ExpectedVersion);
                await ReviewSourcesAsync(row, actor, request.Decision, token);
                var role = await authority.RoleAsync(actor with { Now = clock.GetUtcNow() }, token);
                var proof = new LeaveRecoveryWorkDecision(1, id, employeeId, actor.Identity.AccountId, actor.Identity.EmployeeId,
                    actor.Identity.SessionId, role, request.Decision, actor.Now, WorkTreatmentRules.Hash(row.Submission),
                    request.Decision == ApprovalState.Approved ? LeaveRecoveryWorkRules.Minutes(row.Submission.Ranges) : 0, key, request);
                var reference = await evidence.StoreAsync(employeeId, id, "decision", proof, token);
                await ReviewerAsync(actor, row, token);
                await ReviewSourcesAsync(row, actor, request.Decision, token);
                repository.ExpectVersion(row.Row.Approval, version);
                var before = View(row);
                var decision = row.Row.Approval.Decide(request.Decision, actor.Identity.AccountId.ToString(), role, request.Reason, actor.Now, reference);
                repository.Add(decision);
                repository.Add(LeaveRecoveryWorkEvidence.Fact(LeaveRecoveryWorkEvidence.Decided, employeeId, id, actor with { RoleCode = role }, reference, context.CorrelationId));
                await repository.SaveAsync(token);
                await notifications.StageAsync(new(decision.Id, row.Submission.AccountId, NotificationTemplates.Decision, id), context, token);
                await ReviewerAsync(actor, row, token);
                await ReviewSourcesAsync(row, actor, request.Decision, token);
                return new(id, before, View(new(row.Row with { Decision = decision }, row.Submission, proof)));
            }, ct, resources: authority.ScopeAsync, requireSystemRole: true, beforeReplay: async (actor, token) =>
            {
                var row = await evidence.ReadAsync(employeeId, id, token);
                await ReviewerAsync(actor, row, token);
                if (row.Decision is null && await journal.FindReceiptAsync(actor.Identity.AccountId.ToString(),
                    LeaveRecoveryWorkEvidence.Decided, key, token) is not null)
                    throw AttendanceOperation.Conflict("leave_recovery_work_decision_unavailable");
                if (row.Row.Approval.State == ApprovalState.Approved)
                {
                    await CurrentAsync(row.Submission, actor, token);
                    await AvailableAsync(row.Submission, token);
                }
                await ReviewerAsync(actor, row, token);
            }, beforeCommit: async (actor, token) =>
            {
                var row = await evidence.ReadAsync(employeeId, id, token);
                await ReviewerAsync(actor, row, token);
                await ReviewSourcesAsync(row, actor, row.Row.Approval.State, token);
                await ReviewerAsync(actor, row, token);
            });
    }

    public Task<LeaveRecoveryWorkProgress> ProgressAsync(Guid leaveId, Guid commitmentId, bool self,
        AuthRequestContext context, CancellationToken ct) => operation.ReadAsync(self ? LeavePermissions.Self : LeavePermissions.Review,
        "attendance.leave.recovery.work.progress", null, context, async (actor, token) =>
        {
            var parent = await parents.RecoveryWorkParentAsync(leaveId, commitmentId, self ? actor.Identity.EmployeeId : null, actor.Now, token);
            var rows = (await evidence.ListAsync(parent.EmployeeId, token)).Where(x => x.Submission.Source.Parent.Commitment.Id == commitmentId).ToArray();
            foreach (var row in rows.Where(x => x.Row.Approval.State == ApprovalState.Approved))
            {
                await CurrentAsync(row.Submission, actor, token);
                await AvailableAsync(row.Submission, token);
            }
            var fulfilled = rows.Sum(x => x.Decision?.ApprovedMinutes ?? 0);
            if (fulfilled > parent.Commitment.CommittedMinutes) throw AttendanceOperation.Conflict("leave_recovery_obligation_overallocated");
            if (!self) await authority.RoleAsync(actor with { Now = clock.GetUtcNow() }, token);
            await SessionAsync(actor, token);
            return new LeaveRecoveryWorkProgress(leaveId, commitmentId, parent.Commitment.CommittedMinutes, fulfilled,
                parent.Commitment.CommittedMinutes - fulfilled, fulfilled == parent.Commitment.CommittedMinutes ? "fulfilled" :
                fulfilled > 0 ? "partially_fulfilled" : "commitment_only", rows.Select(View).ToArray());
        }, ct, self: self, resources: self ? null : authority.ScopeAsync, requireSystemRole: !self);

    public Task<IReadOnlyList<LeaveRecoveryWorkView>> QueueAsync(AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync<IReadOnlyList<LeaveRecoveryWorkView>>(LeavePermissions.Review, "attendance.leave.recovery.work.queue", null,
            context, async (actor, token) =>
            {
                var result = new List<LeaveRecoveryWorkView>();
                foreach (var item in await repository.PendingAsync(token)) result.Add(View(await evidence.ReadAsync(item.EmployeeId, item.Id, token)));
                await authority.RoleAsync(actor with { Now = clock.GetUtcNow() }, token);
                await SessionAsync(actor, token);
                return result;
            }, ct, resources: authority.ScopeAsync, requireSystemRole: true);

    public Task<LeaveRecoveryWorkDetail> DetailAsync(Guid? employeeId, Guid id, bool self,
        AuthRequestContext context, CancellationToken ct) => operation.ReadAsync(self ? LeavePermissions.Self : LeavePermissions.Review,
        "attendance.leave.recovery.work.detail", self ? null : employeeId, context, async (actor, token) =>
        {
            var row = await evidence.ReadAsync(self ? actor.Identity.EmployeeId : employeeId ?? throw AttendanceOperation.Invalid(), id, token);
            if (!self) await authority.RoleAsync(actor with { Now = clock.GetUtcNow() }, token);
            return new LeaveRecoveryWorkDetail(View(row), row.Submission.Request, row.Submission.Source);
        }, ct, self: self, resources: self ? null : authority.ScopeAsync, requireSystemRole: !self);

    private async Task CurrentAsync(LeaveRecoveryWorkSubmission submission, AttendanceActor actor, CancellationToken ct)
    {
        var p = submission.Source.Parent;
        var current = await sources.LoadAsync(p.Commitment.LeaveRequestId, p.Commitment.Id, p.EmployeeId,
            submission.Source.Policy.VersionId, clock.GetUtcNow(), ct);
        if (current.Fingerprint != submission.Source.Fingerprint) throw AttendanceOperation.Conflict("leave_recovery_work_source_changed");
        await SessionAsync(actor, ct);
    }
    private async Task SessionAsync(AttendanceActor actor, CancellationToken ct)
    {
        if (!await journal.IsCurrentSessionAsync(actor.Identity, ct)) throw new AuthenticationFailure("invalid_authentication", 401);
    }
    private async Task<IReadOnlyList<Guid>> ReviewRecipientsAsync(Guid employeeId, Guid requesterAccountId, CancellationToken ct)
    {
        var recipients = await repository.ReviewRecipientAccountIdsAsync(employeeId, requesterAccountId, clock.GetUtcNow(), ct);
        if (recipients.Count is < 1 or > 256 || recipients.Any(x => x == Guid.Empty || x == requesterAccountId) ||
            recipients.Distinct().Count() != recipients.Count)
            throw AttendanceOperation.Conflict("leave_recovery_review_recipient_unavailable");
        return recipients.Order().ToArray();
    }
    private async Task ReviewerAsync(AttendanceActor actor, LeaveRecoveryWorkEvidenceRow row, CancellationToken ct)
    {
        if (actor.Identity.EmployeeId == row.Submission.EmployeeId || actor.Identity.AccountId == row.Submission.AccountId)
            throw AttendanceOperation.Denied();
        await authority.RoleAsync(actor with { Now = clock.GetUtcNow() }, ct);
        await SessionAsync(actor, ct);
    }
    private async Task ReviewSourcesAsync(LeaveRecoveryWorkEvidenceRow row, AttendanceActor actor, ApprovalState state, CancellationToken ct)
    {
        if (state != ApprovalState.Approved) return;
        await CurrentAsync(row.Submission, actor, ct);
        Capacity(await evidence.ListAsync(row.Submission.EmployeeId, ct), row.Submission.Source, row.Submission.Id,
            LeaveRecoveryWorkRules.Minutes(row.Submission.Ranges), false);
        await AvailableAsync(row.Submission, ct);
    }
    private async Task AvailableAsync(LeaveRecoveryWorkSubmission submission, CancellationToken ct)
    {
        var reserved = (await recoveryCredits.ReadReservedAsync(submission.EmployeeId, ct)).Where(x => x.Range.SourceId != submission.Id)
            .Select(x => x.Range).ToArray();
        // The shared reader must exclude only this claim's retained allocation during replay.
        await credits.CheckExclusiveAsync(submission.EmployeeId, submission.Ranges, reserved, ct);
    }
    private static void Capacity(IReadOnlyList<LeaveRecoveryWorkEvidenceRow> rows, LeaveRecoveryWorkSource source, Guid id, int minutes, bool adding)
    {
        var siblings = rows.Where(x => x.Submission.Source.Parent.Commitment.Id == source.Parent.Commitment.Id).ToArray();
        if (siblings.Any(x => x.Submission.Source.Policy.VersionId != source.Policy.VersionId))
            throw AttendanceOperation.Conflict("leave_recovery_fulfilment_policy_changed");
        if (adding && siblings.Length >= source.Policy.Settings.MaximumClaimsPerCommitment)
            throw AttendanceOperation.Conflict("leave_recovery_claim_count_limit");
        var used = siblings.Where(x => x.Submission.Id != id).Sum(x => x.Decision?.ApprovedMinutes ?? 0);
        var remaining = source.Parent.Commitment.CommittedMinutes - used;
        if (minutes > remaining) throw AttendanceOperation.Conflict("leave_recovery_obligation_overallocated");
        if (!source.Policy.Settings.AllowPartialFulfilment && minutes != remaining)
            throw AttendanceOperation.Conflict("leave_recovery_partial_fulfilment_disabled");
    }
    private static LeaveRecoveryWorkEvidenceRow? Retry(IReadOnlyList<LeaveRecoveryWorkEvidenceRow> rows, AttendanceActor actor,
        Guid leaveId, Guid commitmentId, SubmitLeaveRecoveryWorkRequest request, string key)
    {
        var matches = rows.Where(x => x.Submission.AccountId == actor.Identity.AccountId && x.Submission.SubmissionKey == key).ToArray();
        if (matches.Length > 1) throw AttendanceOperation.Conflict("leave_recovery_work_evidence_unavailable");
        var prior = matches.SingleOrDefault();
        if (prior is not null && (prior.Submission.Source.Parent.Commitment.Id != commitmentId ||
            prior.Submission.Source.Parent.Commitment.LeaveRequestId != leaveId || WorkTreatmentRules.Hash(prior.Submission.Request) != WorkTreatmentRules.Hash(request)))
            throw AttendanceOperation.Conflict("leave_recovery_submission_key_reused");
        return prior;
    }
    private static LeaveRecoveryWorkView View(LeaveRecoveryWorkEvidenceRow row) => new(row.Submission.Id, row.Submission.EmployeeId,
        row.Submission.Source.Parent.Commitment.LeaveRequestId, row.Submission.Source.Parent.Commitment.Id, row.Submission.Source.Policy.VersionId,
        row.Row.Approval.State, Convert.ToBase64String(row.Row.Approval.RowVersion), LeaveRecoveryWorkRules.Minutes(row.Submission.Ranges),
        row.Decision?.ApprovedMinutes, row.Submission.SubmittedAt, row.Decision?.DecidedAt);
}
