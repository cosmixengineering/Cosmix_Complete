using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class StaffTaxonomyRepository(CosmixDbContext db) : IStaffTaxonomyRepository
{
    // Detect reserved-code collisions/unknown categories too; filtering them out would resurrect legacy fallback.
    // Writes still require the exact code and EmployeeException category in the service.
    private IQueryable<Policy> Policies => db.Policies.Where(p => p.Code.StartsWith(StaffTaxonomyRulesCodec.PolicyCodePrefix));
    private IQueryable<ApprovalRequest> Requests => db.ApprovalRequests.Where(r =>
        r.RequestType == StaffTaxonomyRulesCodec.RequestType && r.Stage == 1 && r.RouteRuleId == null &&
        Policies.Any(p => p.Id == r.SubjectId));

    // Reserving the policy for a pending proposal does not activate rules or interrupt the confirmed baseline.
    // Any persisted publication, including future/inactive/invalid versions, suppresses fallback.
    public Task<bool> NamespaceExistsAsync(CancellationToken ct) =>
        db.PolicyVersions.AnyAsync(v => Policies.Any(p => p.Id == v.PolicyId), ct);
    public Task<Policy?> FindPolicyAsync(CancellationToken ct) =>
        Policies.SingleOrDefaultAsync(p => p.Code == StaffTaxonomyRulesCodec.PolicyCode, ct);
    public async Task<IReadOnlyList<StaffTaxonomyVersionSource>> TimelineAsync(CancellationToken ct)
    {
        var policies = await Policies.AsNoTracking().Take(17).ToArrayAsync(ct);
        if (policies.Length > 16) throw Conflict("staff_taxonomy_history_limit");
        var ids = policies.Select(p => p.Id).ToArray();
        var versions = await db.PolicyVersions.AsNoTracking().Where(v => ids.Contains(v.PolicyId))
            .OrderBy(v => v.VersionNumber).ThenBy(v => v.Id).Take(513).ToArrayAsync(ct);
        if (versions.Length > 512) throw Conflict("staff_taxonomy_history_limit");
        var requestIds = versions.Where(v => v.ApprovalRequestId.HasValue).Select(v => v.ApprovalRequestId!.Value).Distinct().ToArray();
        var requests = await db.ApprovalRequests.AsNoTracking().Where(r => requestIds.Contains(r.Id)).ToArrayAsync(ct);
        var decisions = await db.ApprovalDecisions.AsNoTracking().Where(d => requestIds.Contains(d.ApprovalRequestId)).Take(1025).ToArrayAsync(ct);
        if (decisions.Length > 1024) throw Conflict("staff_taxonomy_history_limit");
        return versions.Select(v => new StaffTaxonomyVersionSource(policies.Single(p => p.Id == v.PolicyId), v,
            requests.SingleOrDefault(r => r.Id == v.ApprovalRequestId), decisions.Where(d => d.ApprovalRequestId == v.ApprovalRequestId).ToArray())).ToArray();
    }
    public Task<ApprovalRequest?> FindRequestAsync(Guid id, CancellationToken ct) => Requests.SingleOrDefaultAsync(r => r.Id == id, ct);
    public async Task<PagedResult<StaffTaxonomyRequestView>> RequestsAsync(int page, int pageSize, CancellationToken ct)
    {
        var total = await Requests.CountAsync(ct);
        var rows = await Requests.AsNoTracking().OrderByDescending(r => r.RequestedAt).ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(rows.Select(r => new StaffTaxonomyRequestView(r.Id, r.SubjectId, r.State.ToString(), Convert.ToBase64String(r.RowVersion),
            r.RequesterUserId, r.RequestedAt, r.ClosedAt)).ToArray(), total, page, pageSize);
    }
    public Task<bool> HasPendingRequestAsync(Guid policyId, CancellationToken ct) =>
        db.ApprovalRequests.AnyAsync(r => r.SubjectId == policyId && r.RequestType == StaffTaxonomyRulesCodec.RequestType && r.State == ApprovalState.Pending, ct);
    public Task<bool> HasLockedPeriodAsync(DateOnly from, DateOnly? to, CancellationToken ct) =>
        db.AttendancePeriodLocks.AnyAsync(l => l.IsLocked && l.PeriodEndExclusive > from && (to == null || l.PeriodStart < to), ct);

    public async Task ValidatePolicyReferencesAsync(StaffTaxonomyRules rules, DateOnly from, DateOnly? to, CancellationToken ct)
    {
        foreach (var reference in rules.Categories.SelectMany(c => c.PolicyReferences).Where(r => r.PolicyVersionId.HasValue).Distinct())
        {
            var expectedCategory = reference.Use switch
            {
                StaffTaxonomyPolicyUse.Attendance => PolicyCategory.Attendance,
                StaffTaxonomyPolicyUse.Leave => PolicyCategory.Leave,
                StaffTaxonomyPolicyUse.Meal or StaffTaxonomyPolicyUse.Deployment => PolicyCategory.Allowance,
                StaffTaxonomyPolicyUse.Fuel => PolicyCategory.VehicleFuel,
                _ => throw Conflict("staff_taxonomy_policy_reference_invalid")
            };
            if (!await db.PolicyVersions.AsNoTracking().AnyAsync(v => v.Id == reference.PolicyVersionId &&
                v.Policy.Category == expectedCategory && v.Policy.IsActive && v.EffectiveFrom <= from &&
                (v.EffectiveTo == null || to != null && v.EffectiveTo >= to), ct))
                throw Conflict("staff_taxonomy_policy_reference_unavailable");
        }
        // Existence/category/date is a reference check only. Claim/attendance consumers must separately verify exact policy authority and eligibility.
    }
    public void Add(Policy policy) => db.Policies.Add(policy);
    public void Add(ApprovalRequest request) => db.ApprovalRequests.Add(request);
    public void Add(ApprovalDecision decision) => db.ApprovalDecisions.Add(decision);
    public void Add(PolicyVersion version) => db.PolicyVersions.Add(version);
    public void ExpectVersion(Policy policy, byte[] version)
    {
        if (!policy.RowVersion.SequenceEqual(version)) throw Conflict("staff_taxonomy_policy_version_conflict");
        db.Entry(policy).Property(p => p.RowVersion).OriginalValue = version;
    }
    public void ExpectVersion(ApprovalRequest request, byte[] version)
    {
        if (!request.RowVersion.SequenceEqual(version)) throw Conflict("staff_taxonomy_request_version_conflict");
        db.Entry(request).Property(r => r.RowVersion).OriginalValue = version;
    }
    public async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
    private static AuthenticationFailure Conflict(string code) => new(code, 409);
}
