using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.FoundationPolicies;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Policies;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

/// <summary>Only the three reserved foundation policy codes and their dedicated approval requests belong to this module.</summary>
public sealed class FoundationPolicyRepository(CosmixDbContext db) : IHrFoundationPolicyRepository
{
    public Task<Policy?> PolicyAsync(HrFoundationPolicyKind kind, CancellationToken ct)
    {
        var code = HrFoundationPolicyRulesCodec.Code(kind);
        // Preserve a reserved-code/category collision as invalid persisted state rather than pretending there is no policy.
        return db.Policies.SingleOrDefaultAsync(x => x.Code == code, ct);
    }
    internal IQueryable<HrFoundationPolicyCandidate> VersionQuery(Guid policyId, HrFoundationPolicyKind kind)
    {
        var type = HrFoundationPolicyRulesCodec.RequestType(kind);
        return db.PolicyVersions.AsNoTracking().Where(x => x.PolicyId == policyId)
            .OrderBy(x => x.VersionNumber).Take(1025).Select(v => new HrFoundationPolicyCandidate(v,
                v.Authority == PolicyAuthority.ApprovedWorkflow && v.ApprovalRequestId != null &&
                v.PolicyDocumentPrivateReference != null &&
                db.ApprovalRequests.Any(r => r.Id == v.ApprovalRequestId && r.RequestType == type && r.SubjectId == policyId &&
                    r.Stage == 1 && r.RouteRuleId == null && r.State == ApprovalState.Approved &&
                    r.SnapshotPrivateReference == v.PolicyDocumentPrivateReference && r.RequesterUserId != v.PublishedByUserId &&
                    r.RequestedAt <= v.PublishedAt && r.ClosedAt == v.PublishedAt) &&
                db.ApprovalDecisions.Count(d => d.ApprovalRequestId == v.ApprovalRequestId) == 1 &&
                db.ApprovalDecisions.Any(d => d.ApprovalRequestId == v.ApprovalRequestId && d.Decision == ApprovalState.Approved &&
                    d.ActorUserId == v.PublishedByUserId && d.DecidedAt == v.PublishedAt && d.ProtectedChangeReference != null &&
                    (d.ActorRoleCode == "ADMIN" || d.ActorRoleCode == "SUPERADMIN")), v.ApprovalRequest,
                db.ApprovalDecisions.Where(d => d.ApprovalRequestId == v.ApprovalRequestId).OrderBy(d => d.DecidedAt).ThenBy(d => d.Id).FirstOrDefault()));
    }
    public async Task<IReadOnlyList<HrFoundationPolicyCandidate>> VersionsAsync(Guid policyId, HrFoundationPolicyKind kind, CancellationToken ct)
    {
        var rows = await VersionQuery(policyId, kind).ToArrayAsync(ct);
        if (rows.Length > 1024) throw new AuthenticationFailure("hr_foundation_policy_history_limit", 409);
        return rows;
    }
    public Task<bool> PendingAsync(Guid policyId, HrFoundationPolicyKind kind, CancellationToken ct)
    {
        var type = HrFoundationPolicyRulesCodec.RequestType(kind);
        return db.ApprovalRequests.AnyAsync(x => x.SubjectId == policyId && x.RequestType == type && x.State == ApprovalState.Pending, ct);
    }
    public Task<bool> StaffCategoryCodeExistsAsync(string code, CancellationToken ct) =>
        db.StaffCategories.AnyAsync(x => x.Code == code, ct);
    public Task<ApprovalRequest?> RequestAsync(Guid policyId, HrFoundationPolicyKind kind, Guid requestId, CancellationToken ct)
    {
        var type = HrFoundationPolicyRulesCodec.RequestType(kind);
        return db.ApprovalRequests.SingleOrDefaultAsync(x => x.Id == requestId && x.SubjectId == policyId && x.RequestType == type, ct);
    }
    public async Task<PagedResult<HrFoundationPolicyRequestView>> RequestsAsync(Guid policyId, HrFoundationPolicyKind kind,
        int page, int pageSize, ApprovalState? state, CancellationToken ct)
    {
        var type = HrFoundationPolicyRulesCodec.RequestType(kind);
        var query = db.ApprovalRequests.AsNoTracking().Where(x => x.SubjectId == policyId && x.RequestType == type);
        if (state.HasValue) query = query.Where(x => x.State == state);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(rows.Select(x => new HrFoundationPolicyRequestView(x.Id, x.SubjectId, kind, x.State, x.RequesterUserId,
            x.RequestedAt, x.ClosedAt, Convert.ToBase64String(x.RowVersion))).ToArray(), count, page, pageSize);
    }
    public async Task<IReadOnlyList<ApprovalDecision>> DecisionsAsync(Guid requestId, CancellationToken ct) =>
        await db.ApprovalDecisions.AsNoTracking().Where(x => x.ApprovalRequestId == requestId)
            .OrderBy(x => x.DecidedAt).ThenBy(x => x.Id).Take(2).ToArrayAsync(ct);
    public Task<IdempotencyReceipt?> ReceiptAsync(string actor, string operation, string key, CancellationToken ct) =>
        db.IdempotencyReceipts.SingleOrDefaultAsync(x => x.ActorUserId == actor && x.Operation == operation && x.Key == key, ct);
    public void ExpectVersion(Policy row, byte[] version) => db.Entry(row).Property(x => x.RowVersion).OriginalValue = version;
    public void ExpectVersion(ApprovalRequest row, byte[] version) => db.Entry(row).Property(x => x.RowVersion).OriginalValue = version;
    public void Add(Policy row) => db.Policies.Add(row);
    public void Add(PolicyVersion row) => db.PolicyVersions.Add(row);
    public void Add(ApprovalRequest row) => db.ApprovalRequests.Add(row);
    public void Add(ApprovalDecision row) => db.ApprovalDecisions.Add(row);
    public void Add(AuditEvent row) => db.AuditEvents.Add(row);
    public void Add(IdempotencyReceipt row) => db.IdempotencyReceipts.Add(row);
    public async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}
