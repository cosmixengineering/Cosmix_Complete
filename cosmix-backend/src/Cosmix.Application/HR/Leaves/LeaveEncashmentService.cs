using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

public sealed partial class LeaveEncashmentService(
    AttendanceOperation operation,
    ILeaveEncashmentRepository repository,
    IPrivateFileStore files,
    ILeaveRepository leaves,
    LeaveReviewAuthority authority,
    LeaveProvenance provenance,
    LeaveEncashmentValuation valuation
)
{
    public const string EncashmentRequestType = "leave-encashment";

    public Task<LeaveEncashmentResponse> SubmitAsync(
        SubmitLeaveEncashmentRequest request,
        string key,
        AuthRequestContext context,
        CancellationToken ct)
    {
        if (request is null || request.RequestedMinutes is < 60 or > 43200 || request.LeaveYearEnd <= request.LeaveYearStart ||
            request.LeaveTypeId == Guid.Empty || request.PolicyVersionId == Guid.Empty)
            throw AttendanceOperation.Invalid("invalid_leave_encashment_request");

        return operation.WriteAsync<LeaveEncashmentResponse>(
            LeavePermissions.Self,
            "attendance.leave.encashment.submit",
            null,
            key,
            request,
            request.Reason,
            context,
            async (actor, token) =>
            {
                var employeeId = actor.Identity.EmployeeId;
                if (employeeId == Guid.Empty) throw AttendanceOperation.Denied();

                var snapshot = await valuation.CalculateAsync(employeeId, actor.Identity.AccountId, request, actor.Now, token);
                var calculationRef = AttendanceOperation.Reference(await files.WriteAsync(
                    JsonSerializer.SerializeToUtf8Bytes(snapshot), LeaveEncashmentValuation.Purpose(employeeId), token));
                var encashment = new LeaveEncashmentRequest(employeeId, request.LeaveYearStart, request.LeaveYearEnd,
                    request.RequestedMinutes, LeaveEncashmentValuation.PersistedKpiPercent(snapshot.Kpi.ExactScore), snapshot.Amount, snapshot.Salary.Terms.Currency,
                    snapshot.Policy.VersionId, calculationRef, actor.Identity.AccountId.ToString(), actor.Now);

                var approval = new ApprovalRequest(
                    EncashmentRequestType,
                    encashment.Id,
                    actor.Identity.AccountId.ToString(),
                    calculationRef,
                    1,
                    actor.Now
                );

                encashment.Submit(approval);
                repository.Add(encashment, approval);
                leaves.Add(new LeaveLedgerEntry(employeeId, request.LeaveTypeId!.Value, request.LeaveYearStart, request.LeaveYearEnd,
                    LeaveLedgerEntryType.Reservation, -request.RequestedMinutes, null, null, snapshot.Policy.VersionId,
                    request.Reason, LeaveEncashmentLedger.ReserveKey(encashment.Id), actor.Identity.AccountId.ToString(), actor.Now));
                await repository.SaveChangesAsync(token);

                var response = Map(encashment, approval, []);
                return new AttendanceChange<LeaveEncashmentResponse>(encashment.Id, null, response);
            }, ct, self: true);
    }

    public Task<LeaveEncashmentResponse> DecideAsync(
        Guid id,
        DecideLeaveEncashmentRequest request,
        string key,
        AuthRequestContext context,
        CancellationToken ct)
    {
        if (id == Guid.Empty || request is null ||
            request.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected))
            throw AttendanceOperation.Invalid("invalid_leave_encashment_decision");

        return operation.WriteAsync<LeaveEncashmentResponse>(
            LeavePermissions.Review,
            "attendance.leave.encashment.decide",
            null,
            key,
            new { id, request },
            request.Reason,
            context,
            async (actor, token) =>
            {
                var role = await authority.RoleAsync(actor, token);
                var encashment = await repository.FindAsync(id, null, token) ??
                    throw AttendanceOperation.Missing("leave_encashment_request_not_found");

                var approval = encashment.ApprovalRequest ??
                    throw AttendanceOperation.Conflict("encashment_approval_unavailable");

                var history = await repository.DecisionsAsync(approval.Id, token);
                if (!LeaveRules.ApprovalValid(approval, history, encashment.Id, encashment.RequestedByUserId, EncashmentRequestType) ||
                    encashment.ApprovalRequestId != approval.Id || encashment.SubmittedAt != approval.RequestedAt ||
                    approval.SnapshotPrivateReference != encashment.CalculationPrivateReference)
                    throw AttendanceOperation.Conflict("encashment_approval_unavailable");
                if (encashment.EmployeeId == actor.Identity.EmployeeId || approval.RequesterUserId == actor.Identity.AccountId.ToString())
                    throw AttendanceOperation.Denied();

                leaves.ExpectVersion(approval, LeaveRules.Version(approval, request.ExpectedApprovalVersion));

                if (approval.State != ApprovalState.Pending)
                    throw AttendanceOperation.Conflict("leave_encashment_not_pending");

                if (request.ApprovedAmount.HasValue &&
                    (request.Decision != ApprovalState.Approved || request.ApprovedAmount != encashment.ProposedAmount))
                    throw AttendanceOperation.Invalid("encashment_amount_override_not_supported");

                var ledger = await repository.LedgerAsync(encashment.EmployeeId, encashment.LeaveYearStart, encashment.LeaveYearEnd, token);
                var reservation = LeaveEncashmentLedger.Verify(encashment, ledger, allowLegacy: true);
                if (request.Decision == ApprovalState.Approved)
                    await valuation.VerifyApprovalAsync(encashment, reservation, actor.Now, token);
                var decisionReference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new {
                    schemaVersion = 2, encashmentId = id, calculationReference = encashment.CalculationPrivateReference,
                    reservationId = reservation?.Id, request.Decision, request.Reason, proposedAmount = encashment.ProposedAmount,
                    financialStatus = "not_posted_or_settled", reviewedAt = actor.Now
                }), $"leave:encashment:{id:N}:decision", token));
                var decision = approval.Decide(
                    request.Decision,
                    actor.Identity.AccountId.ToString(),
                    role,
                    request.Reason,
                    actor.Now,
                    decisionReference
                );
                leaves.Add(decision);
                if (reservation is not null)
                {
                    leaves.Add(new LeaveLedgerEntry(encashment.EmployeeId, reservation.LeaveTypeId, encashment.LeaveYearStart, encashment.LeaveYearEnd,
                        LeaveLedgerEntryType.Release, encashment.RequestedMinutes, null, reservation.Id, encashment.PolicyVersionId,
                        request.Reason, LeaveEncashmentLedger.ReleaseKey(id), actor.Identity.AccountId.ToString(), actor.Now));
                    if (request.Decision == ApprovalState.Approved)
                        leaves.Add(new LeaveLedgerEntry(encashment.EmployeeId, reservation.LeaveTypeId, encashment.LeaveYearStart, encashment.LeaveYearEnd,
                            LeaveLedgerEntryType.Encashment, -encashment.RequestedMinutes, null, null, encashment.PolicyVersionId,
                            request.Reason, LeaveEncashmentLedger.ConsumeKey(id), actor.Identity.AccountId.ToString(), actor.Now));
                }
                await repository.SaveChangesAsync(token);

                var response = Map(encashment, approval, [new LeaveDecisionResponse(
                    decision.Id,
                    decision.ApprovalRequestId,
                    decision.Decision,
                    decision.ActorRoleCode,
                    decision.Reason,
                    decision.DecidedAt
                )]);
                return new AttendanceChange<LeaveEncashmentResponse>(id, null, response);
            }, ct, resources: authority.ScopeAsync);
    }

    public Task<IReadOnlyList<LeaveEncashmentResponse>> MineAsync(AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync<IReadOnlyList<LeaveEncashmentResponse>>(
            LeavePermissions.Self,
            "attendance.leave.encashment.mine",
            null,
            context,
            async (actor, token) =>
            {
                var list = await repository.ListAsync(actor.Identity.EmployeeId, token);
                return await MapList(list, token);
            }, ct, self: true);

    public Task<IReadOnlyList<LeaveEncashmentResponse>> QueueAsync(AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync<IReadOnlyList<LeaveEncashmentResponse>>(
            LeavePermissions.Review,
            "attendance.leave.encashment.queue",
            null,
            context,
            async (actor, token) =>
            {
                var list = await repository.ListAsync(null, token);
                return await MapList(list, token);
            }, ct, resources: authority.ScopeAsync);

    public Task<LeaveRecoveryCommitmentResponse> CreateRecoveryCommitmentAsync(
        Guid leaveRequestId,
        CreateLeaveRecoveryCommitmentRequest request,
        string key,
        AuthRequestContext context,
        CancellationToken ct)
    {
        if (leaveRequestId == Guid.Empty || request is null || request.CommittedMinutes is < 30 or > 1440 || request.CommitmentDate.Year is < 1901 or > 9997)
            throw AttendanceOperation.Invalid("invalid_recovery_commitment_request");

        RecoveryContext? bound = null;
        return operation.WriteAsync<LeaveRecoveryCommitmentResponse>(
            LeavePermissions.Review,
            RecoveryOperation,
            null,
            key,
            new { leaveRequestId, request },
            request.Reason,
            context,
            async (actor, token) =>
            {
                var evidence = bound ?? throw AttendanceOperation.Conflict("leave_recovery_evidence_unavailable");
                var parent = evidence.Parent;
                var policyVersionId = parent.Request.PolicyVersionId!.Value;

                var recoveryEvidence = await ValidateRecoveryAsync(evidence, request, actor.Now, token);

                var commitment = new LeaveRecoveryCommitment(
                    leaveRequestId,
                    request.CommitmentDate,
                    request.CommittedMinutes,
                    request.BeforeLeave,
                    policyVersionId,
                    parent.Approval!.Id,
                    actor.Identity.AccountId.ToString(),
                    actor.Now
                );

                repository.Add(commitment);
                await repository.SaveChangesAsync(token);

                var response = new LeaveRecoveryCommitmentResponse(
                    commitment.Id,
                    commitment.LeaveRequestId,
                    commitment.CommitmentDate,
                    commitment.CommittedMinutes,
                    commitment.BeforeLeave,
                    commitment.PolicyVersionId,
                    commitment.ApprovalRequestId,
                    commitment.RecordedByUserId,
                    commitment.RecordedAt
                );
                return new AttendanceChange<LeaveRecoveryCommitmentResponse>(commitment.Id, recoveryEvidence, response);
            }, ct, resources: authority.ScopeAsync, beforeReplay: async (actor, token) =>
            {
                var parent = await RecoveryParentAsync(leaveRequestId, actor, token);
                bound = await RecoveryContextAsync(parent, actor.Now, token);
                await VerifyRecoveryReplayAsync(bound, actor, key, token);
            }, requireSystemRole: true);
    }

    public Task<IReadOnlyList<LeaveRecoveryCommitmentResponse>> GetRecoveryCommitmentsAsync(
        Guid leaveRequestId,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.ReadAsync<IReadOnlyList<LeaveRecoveryCommitmentResponse>>(
            LeavePermissions.Self,
            "attendance.leave.recovery.list",
            null,
            context,
            async (actor, token) =>
            {
                var parent = await leaves.FindAsync(leaveRequestId, actor.Identity.EmployeeId, token) ?? throw AttendanceOperation.Missing("leave_not_found");
                await provenance.VerifyAsync(parent, token);
                await VerifyRecoveryCancellationsAsync(parent, actor.Now, token);
                var list = await repository.GetRecoveryCommitmentsAsync(leaveRequestId, token);
                if (list.Count == 0) return Array.Empty<LeaveRecoveryCommitmentResponse>();
                var evidence = await RecoveryContextAsync(parent, actor.Now, token);
                await VerifyRecoveryReservationsAsync(evidence, list, actor.Now, token);
                return list.Select(c => new LeaveRecoveryCommitmentResponse(
                    c.Id,
                    c.LeaveRequestId,
                    c.CommitmentDate,
                    c.CommittedMinutes,
                    c.BeforeLeave,
                    c.PolicyVersionId,
                    c.ApprovalRequestId,
                    c.RecordedByUserId,
                    c.RecordedAt
                )).ToList();
            }, ct, self: true);

    private async Task<LeaveRow> RecoveryParentAsync(Guid leaveRequestId, AttendanceActor actor, CancellationToken ct)
    {
        var parent = await leaves.FindAsync(leaveRequestId, null, ct) ?? throw AttendanceOperation.Missing("leave_not_found");
        await provenance.VerifyAsync(parent, ct);
        if (parent.Request.EmployeeId == actor.Identity.EmployeeId || parent.Request.RequestedByUserId == actor.Identity.AccountId.ToString())
            throw AttendanceOperation.Denied();
        await VerifyRecoveryCancellationsAsync(parent, actor.Now, ct);
        if (parent.Approval!.State != ApprovalState.Approved || LeaveRules.Cancelled(parent) ||
            parent.Cancellations.Any(x => x.Approval?.State == ApprovalState.Pending) || parent.Request.PolicyVersionId is null ||
            parent.SupersededByRequestId.HasValue)
            throw AttendanceOperation.Conflict("leave_recovery_approval_unavailable");
        return parent;
    }

    private async Task<object> ValidateRecoveryAsync(RecoveryContext evidence, CreateLeaveRecoveryCommitmentRequest request,
        DateTimeOffset now, CancellationToken ct)
    {
        var (parent, proof, policy, rules) = evidence;
        var decision = parent.Decisions.Single();
        if (!await leaves.EmployeeExistsAsync(parent.Request.EmployeeId, ct))
            throw AttendanceOperation.Conflict("leave_recovery_ineligible");
        var distance = request.BeforeLeave ? parent.Request.StartDate.DayNumber - request.CommitmentDate.DayNumber :
            request.CommitmentDate.DayNumber - parent.Request.EndDate.DayNumber;
        if (request.CommitmentDate < LeaveRules.LocalDate(now) || distance <= 0 ||
            distance > (request.BeforeLeave ? rules.MaximumDaysBeforeLeave : rules.MaximumDaysAfterLeave))
            throw AttendanceOperation.Conflict("leave_recovery_date_out_of_range");
        var existing = await repository.GetRecoveryCommitmentsAsync(parent.Request.Id, ct);
        await VerifyRecoveryReservationsAsync(evidence, existing, now, ct);
        var onDate = await repository.RecoveryOnDateAsync(parent.Request.EmployeeId, request.CommitmentDate, ct);
        if (onDate.Any(x => x.CommitmentDate != request.CommitmentDate || x.CommittedMinutes is < 30 or > 1440) ||
            onDate.Select(x => x.Id).Distinct().Count() != onDate.Count)
            throw AttendanceOperation.Conflict("leave_recovery_evidence_unavailable");
        await VerifyRecoveryReservationsAsync(evidence, onDate, now, ct, otherLeaves: true);
        if (existing.Any(x => x.CommitmentDate == request.CommitmentDate) ||
            existing.Sum(x => (long)x.CommittedMinutes) + request.CommittedMinutes > Math.Min(rules.MaximumMinutesPerLeave, parent.Request.RequestedMinutes) ||
            onDate.Sum(x => (long)x.CommittedMinutes) + request.CommittedMinutes > rules.MaximumMinutesPerDay)
            throw AttendanceOperation.Conflict("leave_recovery_already_reserved_or_limit_exceeded");
        return new { schemaVersion = 1, leaveRequestId = parent.Request.Id, employeeId = parent.Request.EmployeeId,
            approvalId = parent.Approval!.Id, decisionId = decision.Id, decisionEvidence = decision.ProtectedChangeReference,
            policyVersionId = policy.Version.Id, policyRules = policy.Version.RulesJson, proof.SubmissionDigest,
            request.CommitmentDate, request.CommittedMinutes, request.BeforeLeave, request.Reason,
            existingCommitmentIds = existing.Select(x => x.Id).ToArray(), sameDateCommitmentIds = onDate.Select(x => x.Id).ToArray(),
            status = "commitment_only", attendanceFulfilled = false, salaryRecovered = false };
    }

    private async Task<IReadOnlyList<LeaveEncashmentResponse>> MapList(IReadOnlyList<LeaveEncashmentRequest> rows, CancellationToken ct)
    {
        var result = new List<LeaveEncashmentResponse>();
        foreach (var row in rows)
        {
            var history = row.ApprovalRequestId is Guid approvalId ? await repository.DecisionsAsync(approvalId, ct) : [];
            result.Add(Map(row, row.ApprovalRequest, history.Select(x => new LeaveDecisionResponse(
                x.Id, x.ApprovalRequestId, x.Decision, x.ActorRoleCode, x.Reason, x.DecidedAt)).ToArray()));
        }
        return result;
    }

    private static LeaveEncashmentResponse Map(
        LeaveEncashmentRequest encashment,
        ApprovalRequest? approval,
        IReadOnlyList<LeaveDecisionResponse> decisions) =>
        new(
            encashment.Id,
            encashment.EmployeeId,
            encashment.LeaveYearStart,
            encashment.LeaveYearEnd,
            encashment.RequestedMinutes,
            encashment.AssessedKpiPercent,
            encashment.ProposedAmount,
            encashment.Currency,
            encashment.PolicyVersionId,
            encashment.ApprovalRequestId ?? Guid.Empty,
            approval?.State ?? ApprovalState.Pending,
            Convert.ToBase64String(approval?.RowVersion ?? []),
            encashment.CreatedAt,
            decisions
        );
}
