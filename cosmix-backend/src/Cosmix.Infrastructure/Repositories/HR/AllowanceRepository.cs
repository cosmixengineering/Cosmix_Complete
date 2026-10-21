using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.HR.Allowances;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Allowances;
using Cosmix.Domain.HR.Policies;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

/// <summary>Module-specific allowance repository. Verified duty/employment evidence is served by the
/// shared overtime source reader; every query is category-scoped to <see cref="PolicyCategory.Allowance"/>.</summary>
public sealed class AllowanceRepository(CosmixDbContext db, IOvertimeRepository overtime) :
    IAllowanceRepository, IAllowancePolicyRepository, IAllowanceSettlementStore
{
    public async Task TrackApprovalEvidenceAsync(Guid approvalRequestId, CancellationToken ct)
    {
        var approval = await db.ApprovalRequests.FirstOrDefaultAsync(x => x.Id == approvalRequestId, ct)
            ?? throw AttendanceOperation.Missing("approval_not_found");
    }

    public void Add(AllowanceSettlementRecord record) => db.AllowanceSettlementRecords.Add(record);
    public Task<OvertimeEmploymentSource?> EmploymentAsync(Guid employeeId, DateOnly date, CancellationToken ct) =>
        overtime.EmploymentAsync(employeeId, date, ct);
    public Task<IReadOnlyList<OvertimeSessionSource>> SessionSourcesAsync(Guid employeeId, DateOnly date, CancellationToken ct) =>
        overtime.SessionSourcesAsync(employeeId, date, ct);

    public async Task<IReadOnlyList<AllowanceKind>> KindsClaimedAsync(Guid employeeId, DateOnly payableDate, CancellationToken ct) =>
        await db.AllowanceClaims.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.PayableDate == payableDate)
            .Select(x => x.Kind).Distinct().ToArrayAsync(ct);

    public Task<bool> KeyClaimExistsAsync(Guid employeeId, AllowanceKind kind, string eligibilityKey, CancellationToken ct) =>
        db.AllowanceClaims.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId && x.Kind == kind &&
            x.EligibilityKey == eligibilityKey, ct);
    public Task<bool> DateClaimExistsAsync(Guid employeeId, AllowanceKind kind, DateOnly payableDate, CancellationToken ct) =>
        db.AllowanceClaims.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId && x.Kind == kind &&
            x.PayableDate == payableDate, ct);

    public Task<bool> DeploymentClaimOverlapAsync(Guid employeeId, DateTimeOffset startsAt, DateTimeOffset endsAt, CancellationToken ct) =>
        (from claim in db.AllowanceClaims
         join approval in db.ApprovalRequests on claim.ApprovalRequestId equals approval.Id
         where claim.EmployeeId == employeeId && claim.Kind == AllowanceKind.OutstationDaily &&
             claim.EligibilityPeriodStart < endsAt && claim.EligibilityPeriodEnd > startsAt &&
             (approval.State == ApprovalState.Pending || approval.State == ApprovalState.Approved)
         select claim.Id).AnyAsync(ct);

    internal IQueryable<AllowanceRecord> Records(bool tracked) =>
        from claim in tracked ? db.AllowanceClaims : db.AllowanceClaims.AsNoTracking()
        join approval in tracked ? db.ApprovalRequests : db.ApprovalRequests.AsNoTracking()
            on claim.ApprovalRequestId equals approval.Id
        where approval.RequestType == AllowanceService.RequestType && approval.Stage == 1 &&
            approval.SubjectId == claim.Id &&
            approval.SnapshotPrivateReference == claim.CalculationPrivateReference
        select new AllowanceRecord
        {
            Claim = claim,
            Approval = approval,
            Decision = db.ApprovalDecisions.AsNoTracking().Where(x => x.ApprovalRequestId == approval.Id)
                .OrderBy(x => x.DecidedAt).FirstOrDefault()
        };

    public async Task<AllowanceRecord?> FindAsync(Guid employeeId, Guid id, CancellationToken ct)
    {
        var row = await Records(true).SingleOrDefaultAsync(x => x.Claim.Id == id && x.Claim.EmployeeId == employeeId, ct);
        if (row is null) return null;
        await db.Entry(row.Claim).Collection(x => x.Submissions).LoadAsync(ct);
        return row;
    }

    public async Task<IReadOnlyList<AllowanceRecord>> PageAsync(AllowancePageQuery query, Guid? requesterId, CancellationToken ct)
    {
        if (query.Skip < 0 || query.Limit is < 1 or > 101) throw AttendanceOperation.Invalid("invalid_allowance_page");
        var rows = from claim in db.AllowanceClaims.AsNoTracking()
                   join approval in db.ApprovalRequests.AsNoTracking() on claim.ApprovalRequestId equals approval.Id
                   where approval.RequestType == AllowanceService.RequestType && approval.Stage == 1 &&
                       approval.SubjectId == claim.Id &&
                       approval.SnapshotPrivateReference == claim.CalculationPrivateReference &&
                       (query.EmployeeId == null || claim.EmployeeId == query.EmployeeId) &&
                       (query.From == null || claim.PayableDate >= query.From) &&
                       (query.To == null || claim.PayableDate < query.To) &&
                       (query.State == null || approval.State == query.State) &&
                       (requesterId == null || approval.RequesterUserId == requesterId.Value.ToString())
                   orderby claim.CreatedAt descending, claim.Id descending
                   select new AllowanceRecord
                   {
                       Claim = claim,
                       Approval = approval,
                       Decision = db.ApprovalDecisions.AsNoTracking().Where(x => x.ApprovalRequestId == approval.Id)
                           .OrderBy(x => x.DecidedAt).FirstOrDefault()
                   };
        return await rows.Skip(query.Skip).Take(query.Limit).ToArrayAsync(ct);
    }

    public void Add(AllowanceClaim claim, ApprovalRequest approval, AllowanceClaimSubmission submission)
    {
        db.AllowanceClaims.Add(claim);
        db.ApprovalRequests.Add(approval);
        db.AllowanceClaimSubmissions.Add(submission);
    }
    public void Add(ApprovalDecision decision) => db.ApprovalDecisions.Add(decision);
    public void ExpectVersion(ApprovalRequest approval, byte[] version) =>
        db.Entry(approval).Property(x => x.RowVersion).OriginalValue = version;
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<Policy>> ListAsync(CancellationToken ct) =>
        Bounded(await db.Policies.AsNoTracking().Where(x => x.Category == PolicyCategory.Allowance)
            .OrderBy(x => x.Code).Take(513).ToArrayAsync(ct), 512);
    public Task<Policy?> FindAsync(Guid id, CancellationToken ct) =>
        db.Policies.SingleOrDefaultAsync(x => x.Id == id && x.Category == PolicyCategory.Allowance, ct);
    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) => db.Policies.AnyAsync(x => x.Code == code, ct);
    public async Task<IReadOnlyList<PolicyVersion>> VersionsAsync(Guid policyId, CancellationToken ct) =>
        Bounded(await db.PolicyVersions.AsNoTracking().Where(x => x.PolicyId == policyId)
            .OrderBy(x => x.VersionNumber).Take(1025).ToArrayAsync(ct), 1024);

    internal IQueryable<AllowancePolicyCandidate> AssignmentQuery() =>
        from a in db.PolicyAssignments.AsNoTracking()
        join v in db.PolicyVersions.AsNoTracking() on a.PolicyVersionId equals v.Id
        join p in db.Policies.AsNoTracking() on v.PolicyId equals p.Id
        join end in db.PolicyAssignmentEnds.AsNoTracking() on a.Id equals end.PolicyAssignmentId into ends
        from e in ends.DefaultIfEmpty()
        where p.Category == PolicyCategory.Allowance
        select new AllowancePolicyCandidate
        {
            Assignment = a, Version = v, End = e,
            AuthorityValid = a.Authority == PolicyAuthority.DirectHr && a.ApprovalRequestId == null &&
                v.Authority == PolicyAuthority.DirectHr && v.ApprovalRequestId == null &&
                (e == null || e.Authority == PolicyAuthority.DirectHr && e.ApprovalRequestId == null)
        };

    public async Task<IReadOnlyList<AllowancePolicyCandidate>> AssignmentsAsync(Guid policyId, CancellationToken ct) =>
        Bounded(await AssignmentQuery().Where(x => x.Version.PolicyId == policyId)
            .OrderBy(x => x.Assignment.EffectiveFrom).ThenBy(x => x.Assignment.Id).Take(4097).ToArrayAsync(ct), 4096);

    internal IQueryable<AllowancePolicyCandidate> ApplicableQuery(Guid employeeId, DateOnly date) =>
        AssignmentQuery().Where(x => x.Assignment.EffectiveFrom <= date &&
            (x.Assignment.EffectiveTo == null || x.Assignment.EffectiveTo > date) &&
            (x.End == null || x.End.EffectiveTo > date || !x.AuthorityValid) &&
            (x.Assignment.EmployeeId == employeeId || db.EmployeeAssignments.Any(m => m.EmployeeId == employeeId &&
                m.EffectiveFrom <= date && (m.EffectiveTo == null || m.EffectiveTo > date) &&
                (x.Assignment.DepartmentId == m.DepartmentId || x.Assignment.StaffCategoryId == m.StaffCategoryId ||
                 x.Assignment.SiteId != null && x.Assignment.SiteId == m.SiteId))));

    public async Task<AllowancePolicySource> SourceAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        var memberships = await db.EmployeeAssignments.AsNoTracking().Where(m => m.EmployeeId == employeeId &&
            m.EffectiveFrom <= date && (m.EffectiveTo == null || m.EffectiveTo > date)).OrderBy(m => m.Id).Take(3)
            .Select(m => new AllowancePolicyMembership(m.Id, m.EmployeeId, m.DepartmentId, m.StaffCategoryId,
                m.SiteId, m.EffectiveFrom, m.EffectiveTo)).ToArrayAsync(ct);
        var candidates = Bounded(await ApplicableQuery(employeeId, date).OrderBy(x => x.Assignment.Id)
            .Take(129).ToArrayAsync(ct), 128);
        return new(memberships, candidates);
    }

    public Task<bool> TargetExistsAsync(AllowancePolicyTarget target, Guid id, CancellationToken ct) => target switch
    {
        AllowancePolicyTarget.Employee => db.Employees.AnyAsync(x => x.Id == id, ct),
        AllowancePolicyTarget.Department => db.Departments.AnyAsync(x => x.Id == id && x.IsActive, ct),
        AllowancePolicyTarget.StaffCategory => db.StaffCategories.AnyAsync(x => x.Id == id && x.IsActive, ct),
        AllowancePolicyTarget.Site => db.Sites.AnyAsync(x => x.Id == id && x.IsActive, ct),
        _ => Task.FromResult(false)
    };

    internal IQueryable<AllowancePolicyCandidate> OverlapQuery(AllowancePolicyTarget target, Guid targetId, DateOnly from,
        DateOnly? to, Guid? exceptAssignmentId) => AssignmentQuery().Where(x =>
        x.Assignment.Id != exceptAssignmentId && (to == null || x.Assignment.EffectiveFrom < to) &&
        (x.Assignment.EffectiveTo == null || x.Assignment.EffectiveTo > from) &&
        (x.End == null || x.End.EffectiveTo > from) &&
        (target == AllowancePolicyTarget.Employee && x.Assignment.EmployeeId == targetId ||
         target == AllowancePolicyTarget.Department && x.Assignment.DepartmentId == targetId ||
         target == AllowancePolicyTarget.StaffCategory && x.Assignment.StaffCategoryId == targetId ||
         target == AllowancePolicyTarget.Site && x.Assignment.SiteId == targetId));

    public Task<bool> HasTargetOverlapAsync(AllowancePolicyTarget target, Guid targetId, DateOnly from,
        DateOnly? to, Guid? exceptAssignmentId, CancellationToken ct) =>
        OverlapQuery(target, targetId, from, to, exceptAssignmentId).AnyAsync(ct);

    public void Add(Policy policy) => db.Policies.Add(policy);
    public void Add(PolicyVersion version) => db.PolicyVersions.Add(version);
    public void Add(PolicyAssignment assignment) => db.PolicyAssignments.Add(assignment);
    public void Add(PolicyAssignmentEnd end) => db.PolicyAssignmentEnds.Add(end);
    public void ExpectVersion(Policy policy, byte[] version) =>
        db.Entry(policy).Property(x => x.RowVersion).OriginalValue = version;

    private static T[] Bounded<T>(T[] rows, int limit) => rows.Length <= limit ? rows :
        throw AttendanceOperation.Conflict("allowance_policy_limit");
}
