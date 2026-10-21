using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.HR.Leaves;

public sealed class LeaveRecoveryWorkEvidence(ILeaveRecoveryWorkRepository repository, AttendanceOperation operation, IPrivateFileStore files)
{
    public const string RequestType = "leave-recovery-work";
    public const string FactTarget = "LeaveRecoveryWork";
    public const string Submitted = "attendance.leave.recovery.work.submitted";
    public const string Decided = "attendance.leave.recovery.work.decided";
    public static string Purpose(Guid employee, Guid id, string kind) => $"hr:leave-recovery:{employee:N}:{id:N}:{kind}:v1";
    public async Task<string> StoreAsync<T>(Guid employee, Guid id, string kind, T value, CancellationToken ct) =>
        AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(value), Purpose(employee, id, kind), ct));

    public async Task<LeaveRecoveryWorkEvidenceRow> ReadAsync(Guid employee, Guid id, CancellationToken ct)
    {
        repository.RequireTransaction();
        var row = await repository.FindAsync(employee, id, ct) ?? throw AttendanceOperation.Missing("leave_recovery_work_not_found");
        var facts = await repository.FactsAsync(employee, Submitted, ct);
        var fact = facts.Where(x => x.TargetId == id.ToString()).ToArray();
        var approval = row.Approval;
        if (fact.Length != 1 || fact[0].ProtectedChangesReference != approval.SnapshotPrivateReference)
            throw AttendanceOperation.Conflict("leave_recovery_work_evidence_unavailable");
        var s = await operation.ReadPrivateAsync<LeaveRecoveryWorkSubmission>(approval.SnapshotPrivateReference, Purpose(employee, id, "submission"), ct);
        if (s.Schema != 1 || s.Id != id || s.EmployeeId != employee || s.AccountId == Guid.Empty || s.SessionId == Guid.Empty ||
            s.Source?.Parent?.Commitment is null || s.Source.Policy?.Settings is null || s.Source.Employment is null ||
            s.Source.Sessions is null || s.Source.Rosters is not { Count: 3 } || s.Request is null || s.Ranges is null ||
            s.Source.Parent.EmployeeId != employee || s.Source.Parent.Commitment.Id == Guid.Empty ||
            !WorkTreatmentRules.HashValid(s.Source.Parent.Fingerprint) ||
            approval.SubjectId != id || approval.RequestType != RequestType || approval.Stage != 1 || approval.RouteRuleId is not null ||
            approval.RequesterUserId != s.AccountId.ToString() || approval.RequestedAt != s.SubmittedAt ||
            !FactValid(fact[0], employee, id, Submitted, s.AccountId, s.SessionId, s.SubmittedAt) ||
            string.IsNullOrWhiteSpace(s.SubmissionKey) || string.IsNullOrWhiteSpace(s.Request.Reason) ||
            s.Request.Reason.Length > 1000 || s.Request.Reason.Any(char.IsControl))
            throw AttendanceOperation.Conflict("leave_recovery_work_evidence_unavailable");
        var ranges = LeaveRecoveryWorkRules.Evaluate(id, s.Request, s.Source, s.SubmittedAt);
        if (WorkTreatmentRules.Hash(ranges) != WorkTreatmentRules.Hash(s.Ranges))
            throw AttendanceOperation.Conflict("leave_recovery_work_evidence_unavailable");
        LeaveRecoveryWorkDecision? d = null;
        if (approval.State == ApprovalState.Pending)
        {
            if (row.Decision is not null || approval.ClosedAt.HasValue ||
                (await repository.FactsAsync(employee, Decided, ct)).Any(x => x.TargetId == id.ToString()))
                throw AttendanceOperation.Conflict("leave_recovery_work_decision_unavailable");
        }
        else
        {
            var decision = row.Decision;
            if (decision?.ProtectedChangeReference is null || decision.ApprovalRequestId != approval.Id ||
                decision.Decision != approval.State || approval.State is not (ApprovalState.Approved or ApprovalState.Rejected or ApprovalState.Returned) ||
                decision.DecidedAt != approval.ClosedAt || decision.DecidedAt < s.SubmittedAt ||
                decision.ActorUserId == approval.RequesterUserId || !LeaveRoleCodes.IsRetainedReviewer(decision.ActorRoleCode))
                throw AttendanceOperation.Conflict("leave_recovery_work_decision_unavailable");
            d = await operation.ReadPrivateAsync<LeaveRecoveryWorkDecision>(decision.ProtectedChangeReference, Purpose(employee, id, "decision"), ct);
            var decided = (await repository.FactsAsync(employee, Decided, ct)).Where(x => x.TargetId == id.ToString()).ToArray();
            if (d.Schema != 1 || d.Id != id || d.EmployeeId != employee || d.ReviewerAccountId == s.AccountId ||
                d.ReviewerAccountId.ToString() != decision.ActorUserId || d.ReviewerEmployeeId == employee || d.ReviewerEmployeeId == Guid.Empty ||
                d.ReviewerRole != decision.ActorRoleCode || d.State != approval.State || d.DecidedAt != decision.DecidedAt ||
                string.IsNullOrWhiteSpace(d.DecisionKey) || d.Request is null || d.Request.Decision != d.State ||
                d.Request.Reason?.Trim() != decision.Reason || string.IsNullOrWhiteSpace(d.Request.ExpectedVersion) ||
                d.SubmissionFingerprint != WorkTreatmentRules.Hash(s) ||
                d.ApprovedMinutes != (d.State == ApprovalState.Approved ? LeaveRecoveryWorkRules.Minutes(s.Ranges) : 0) ||
                decided.Length != 1 || !FactValid(decided[0], employee, id, Decided, d.ReviewerAccountId, d.ReviewerSessionId, d.DecidedAt) ||
                decided[0].ProtectedChangesReference != decision.ProtectedChangeReference || decided[0].ActorRoleCode != d.ReviewerRole)
                throw AttendanceOperation.Conflict("leave_recovery_work_decision_unavailable");
        }
        return new(row, s, d);
    }

    public async Task<IReadOnlyList<LeaveRecoveryWorkEvidenceRow>> ListAsync(Guid employee, CancellationToken ct)
    {
        var facts = await repository.FactsAsync(employee, Submitted, ct);
        if (facts.Count > 1000 || facts.Select(x => x.TargetId).Distinct().Count() != facts.Count)
            throw AttendanceOperation.Conflict("leave_recovery_work_history_limit");
        var result = new List<LeaveRecoveryWorkEvidenceRow>();
        foreach (var fact in facts)
        {
            if (!Guid.TryParse(fact.TargetId, out var id)) throw AttendanceOperation.Conflict("leave_recovery_work_evidence_unavailable");
            result.Add(await ReadAsync(employee, id, ct));
        }
        return result;
    }

    public static AuditEvent Fact(string eventType, Guid employee, Guid id, AttendanceActor actor, string reference, string correlationId) =>
        new(eventType, actor.Identity.AccountId.ToString(), actor.RoleCode, FactTarget, id.ToString(), actor.Now,
            correlationId, employee, reference, deviceReference: actor.Identity.SessionId.ToString());
    private static bool FactValid(AuditEvent fact, Guid employee, Guid id, string eventType, Guid account, Guid session, DateTimeOffset at) =>
        fact.EventType == eventType && fact.TargetType == FactTarget && fact.EmployeeScopeId == employee && fact.TargetId == id.ToString() &&
        fact.ActorUserId == account.ToString() && session != Guid.Empty && fact.DeviceReference == session.ToString() && fact.OccurredAt == at;
}

public sealed class LeaveRecoveryCreditReader(LeaveRecoveryWorkEvidence evidence) : ILeaveRecoveryCreditReader
{
    public async Task<IReadOnlyList<WorkTreatmentCreditEvidence>> ReadReservedAsync(Guid employeeId, CancellationToken ct)
    {
        var result = new List<WorkTreatmentCreditEvidence>();
        foreach (var row in await evidence.ListAsync(employeeId, ct))
        {
            if (row.Row.Approval.State != ApprovalState.Approved) continue;
            var s = row.Submission;
            foreach (var range in s.Ranges)
                result.Add(new(s.Id, row.Row.Approval.Id, row.Row.Decision!.Id, row.Decision!.ReviewerAccountId,
                    s.Source.Fingerprint, WorkTreatmentRules.Hash(s.Source.Policy), range, s.Source.Parent.Commitment.Id) {
                    PolicyVersionId = s.Source.Policy.VersionId,
                    OriginalScopes = [new([new(EmployeeId: employeeId, SiteId: s.Source.Employment.SiteId)])]
                });
        }
        // Source correction or leave cancellation cannot silently release an immutable allocation.
        WorkTreatmentRules.Exclusive(result.Select(x => x.Range).ToArray(), []);
        return result;
    }
}
