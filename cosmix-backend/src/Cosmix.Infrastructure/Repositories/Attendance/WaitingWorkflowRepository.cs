using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Waiting;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.PolicyEffects;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class WaitingWorkflowRepository(CosmixDbContext db) : IWaitingWorkflowRepository
{
    public void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Waiting reads require the caller's authorized transaction.");
    }
    internal IQueryable<WaitingStoredRecord> Records(string kind, bool tracked) =>
        from audit in db.AuditEvents.AsNoTracking()
        join approval in tracked ? db.ApprovalRequests : db.ApprovalRequests.AsNoTracking()
            on audit.ProtectedChangesReference equals approval.SnapshotPrivateReference
        where audit.EventType == WaitingWorkflowService.Event(kind) && approval.RequestType == WaitingWorkflowService.RequestType(kind) &&
            approval.Stage == 1 && audit.ActorUserId == approval.RequesterUserId && audit.ProtectedChangesReference != null
        select new WaitingStoredRecord { Audit=audit, Approval=approval,
            Decision=db.ApprovalDecisions.Where(x => x.ApprovalRequestId == approval.Id).OrderBy(x => x.Id).FirstOrDefault(),
            DecisionCount=db.ApprovalDecisions.Count(x => x.ApprovalRequestId == approval.Id) };
    public Task<WaitingStoredRecord?> FindAsync(string kind, Guid id, bool tracked, CancellationToken ct)
    {
        RequireTransaction();
        return Records(kind, tracked).Where(x => x.Approval.SubjectId == id).SingleOrDefaultAsync(ct);
    }
    internal IQueryable<WaitingStoredRecord> PairQuery(Guid waitingEmployee) => Records("pair", false)
        .Where(x => x.Audit.TargetType == "AttendanceWaitingPair" && x.Audit.TargetId == waitingEmployee.ToString("N") && x.Audit.EmployeeScopeId == waitingEmployee)
        .OrderBy(x => x.Audit.OccurredAt).ThenBy(x => x.Audit.Id).Take(129);
    public async Task<IReadOnlyList<WaitingStoredRecord>> PairsAsync(Guid waitingEmployee, CancellationToken ct)
    {
        RequireTransaction(); var result = await PairQuery(waitingEmployee).ToArrayAsync(ct);
        return result.Length <= 128 ? result : throw AttendanceOperation.Conflict("waiting_pair_history_limit");
    }
    internal IQueryable<WaitingStoredRecord> LossQuery(string key) => Records("request", false)
        .Where(x => x.Audit.TargetType == "AttendanceWaitingLoss" && x.Audit.TargetId == key)
        .OrderBy(x => x.Audit.OccurredAt).ThenBy(x => x.Audit.Id).Take(513);
    public async Task<IReadOnlyList<WaitingStoredRecord>> LossAsync(string key, CancellationToken ct)
    {
        RequireTransaction(); var result = await LossQuery(key).ToArrayAsync(ct);
        return result.Length <= 512 ? result : throw AttendanceOperation.Conflict("waiting_loss_history_limit");
    }
    internal IQueryable<WaitingStoredRecord> CauseQuery(Guid causingEmployee, DateOnly from, DateOnly to)
    {
        var lower = WaitingWorkflowService.CauseKey(causingEmployee, from);
        var upper = WaitingWorkflowService.CauseKey(causingEmployee, to);
        return from row in Records("request", false)
            join index in db.AuditEvents.AsNoTracking() on row.Audit.ProtectedChangesReference equals index.ProtectedChangesReference
            where index.EventType == "attendance.waiting.request.cause" && index.TargetType == "AttendanceWaitingCause" &&
                string.Compare(index.TargetId, lower) >= 0 && string.Compare(index.TargetId, upper) < 0 &&
                index.EmployeeScopeId == causingEmployee
            orderby index.TargetId, row.Audit.OccurredAt, row.Audit.Id
            select row;
    }
    public async Task<IReadOnlyList<WaitingStoredRecord>> CausedAsync(Guid causingEmployee, DateOnly from, DateOnly to, CancellationToken ct)
    {
        RequireTransaction(); var result = await CauseQuery(causingEmployee, from, to).Take(513).ToArrayAsync(ct);
        return result.Length <= 512 ? result : throw AttendanceOperation.Conflict("waiting_cause_history_limit");
    }
    internal IQueryable<AuditEvent> DisputeQuery(Guid root) => db.AuditEvents.AsNoTracking()
        .Where(x => x.EventType == "attendance.waiting.dispute.record" && x.TargetType == "AttendanceWaitingDispute" && x.TargetId == root.ToString("N"))
        .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Take(129);
    public async Task<IReadOnlyList<AuditEvent>> DisputesAsync(Guid root, CancellationToken ct)
    {
        RequireTransaction(); var result = await DisputeQuery(root).ToArrayAsync(ct);
        return result.Length <= 128 ? result : throw AttendanceOperation.Conflict("waiting_dispute_history_limit");
    }
    internal IQueryable<WaitingStoredRecord> EffectQuery(Guid requestId) => Records("effect", false)
        .Where(x => x.Audit.TargetType == "AttendanceWaitingEffect" && x.Audit.TargetId == requestId.ToString("N"))
        .OrderBy(x => x.Audit.OccurredAt).ThenBy(x => x.Audit.Id).Take(129);
    public async Task<IReadOnlyList<WaitingStoredRecord>> EffectsAsync(Guid requestId, CancellationToken ct)
    {
        RequireTransaction(); var result = await EffectQuery(requestId).ToArrayAsync(ct);
        return result.Length <= 128 ? result : throw AttendanceOperation.Conflict("waiting_effect_history_limit");
    }
    public async Task<WaitingDocumentProof?> DocumentAsync(Guid id, Guid employeeId, DateOnly date, CancellationToken ct)
    {
        RequireTransaction();
        var document = await db.EmployeeDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.EmployeeId == employeeId, ct);
        if (document is null || !document.IsAvailable || !document.IsCleanForReviewOn(date)) return null;
        return new(id, employeeId, WaitingAttribution.Hash(new { document.Id, document.RowVersion, document.StorageKey,
            document.ScanStatus, document.VerificationStatus, document.VerifiedAt, document.ExpiresOn, document.LifecycleStatus }));
    }
    public void Add(ApprovalRequest approval, AuditEvent audit) { RequireTransaction(); db.ApprovalRequests.Add(approval); db.AuditEvents.Add(audit); }
    public Task<WaitingTimeEvidence?> VerifiedAsync(Guid approvalRequestId, CancellationToken ct)
    {
        RequireTransaction(); return db.WaitingTimeEvidences.AsNoTracking().SingleOrDefaultAsync(x => x.ApprovalRequestId == approvalRequestId, ct);
    }
    public Task<PolicyImpactAssessment?> AssessmentAsync(Guid id, CancellationToken ct)
    {
        RequireTransaction(); return db.PolicyImpactAssessments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    }
    public void Add(AuditEvent audit) { RequireTransaction(); db.AuditEvents.Add(audit); }
    public void Add(ApprovalDecision decision) { RequireTransaction(); db.ApprovalDecisions.Add(decision); }
    public void Add(WaitingTimeEvidence evidence) { RequireTransaction(); db.WaitingTimeEvidences.Add(evidence); }
    public void Add(PolicyImpactAssessment assessment) { RequireTransaction(); db.PolicyImpactAssessments.Add(assessment); }
    public void ExpectVersion(ApprovalRequest approval, byte[] expected) => db.Entry(approval).Property(x => x.RowVersion).OriginalValue = expected;
    public Task SaveChangesAsync(CancellationToken ct) { RequireTransaction(); return db.SaveChangesAsync(ct); }
}
