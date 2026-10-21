using System.Data;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.HR.Policies;
using Cosmix.Application.HR.Policies.Advanced;
using Cosmix.Domain.HR.Policies;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class AdvancedPolicyRepository(CosmixDbContext db) : IAdvancedPolicyPublicationRepository
{
    private static readonly string[] Codes = Enum.GetValues<AdvancedAttendanceFamily>().Select(AdvancedAttendanceRulesCodec.Code).ToArray();
    public Task<Policy?> FindAsync(AdvancedAttendanceFamily family, CancellationToken ct)
    {
        var code = AdvancedAttendanceRulesCodec.Code(family);
        return db.Policies.SingleOrDefaultAsync(x => x.Code == code && x.Category == PolicyCategory.EmployeeException, ct);
    }

    internal IQueryable<PolicyVersion> VersionQuery(Guid policyId) =>
        from version in db.PolicyVersions.AsNoTracking()
        join policy in db.Policies.AsNoTracking() on version.PolicyId equals policy.Id
        where policy.Id == policyId && policy.Category == PolicyCategory.EmployeeException && Codes.Contains(policy.Code)
        select version;

    public async Task<IReadOnlyList<PolicyVersion>> VersionsAsync(Guid policyId, CancellationToken ct) =>
        Bounded(await VersionQuery(policyId).OrderBy(x => x.VersionNumber).Take(1025).ToArrayAsync(ct), 1024);

    internal IQueryable<AttendancePolicyCandidate> AssignmentQuery() =>
        from assignment in db.PolicyAssignments.AsNoTracking()
        join version in db.PolicyVersions.AsNoTracking() on assignment.PolicyVersionId equals version.Id
        join policy in db.Policies.AsNoTracking() on version.PolicyId equals policy.Id
        join end in db.PolicyAssignmentEnds.AsNoTracking() on assignment.Id equals end.PolicyAssignmentId into ends
        from ending in ends.DefaultIfEmpty()
        where policy.Category == PolicyCategory.EmployeeException && Codes.Contains(policy.Code)
        select new AttendancePolicyCandidate {
            Assignment = assignment, Version = version, End = ending,
            AuthorityValid = assignment.Authority == PolicyAuthority.DirectHr && assignment.ApprovalRequestId == null &&
                version.Authority == PolicyAuthority.DirectHr && version.ApprovalRequestId == null &&
                (ending == null || ending.Authority == PolicyAuthority.DirectHr && ending.ApprovalRequestId == null)
        };

    public async Task<IReadOnlyList<AttendancePolicyCandidate>> AssignmentsAsync(Guid policyId, CancellationToken ct) =>
        Bounded(await AssignmentQuery().Where(x => x.Version.PolicyId == policyId).OrderBy(x => x.Assignment.EffectiveFrom)
            .ThenBy(x => x.Assignment.Id).Take(4097).ToArrayAsync(ct), 4096);

    internal IQueryable<AttendancePolicyCandidate> FamilyQuery(AdvancedAttendanceFamily family)
    {
        var code = AdvancedAttendanceRulesCodec.Code(family);
        return AssignmentQuery().Where(x => db.Policies.Any(p => p.Id == x.Version.PolicyId && p.Code == code && p.Category == PolicyCategory.EmployeeException));
    }

    internal IQueryable<AttendancePolicyMembership> MembershipQuery(Guid employeeId, DateOnly date) =>
        db.EmployeeAssignments.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.EffectiveFrom <= date &&
            (x.EffectiveTo == null || x.EffectiveTo > date)).OrderBy(x => x.Id).Take(3)
            .Select(x => new AttendancePolicyMembership(x.Id, x.EmployeeId, x.DepartmentId, x.StaffCategoryId, x.SiteId, x.EffectiveFrom, x.EffectiveTo));

    internal IQueryable<AdvancedPolicyStaffCategorySource> StaffCategoryQuery(Guid employeeId, DateOnly date) =>
        (from membership in db.EmployeeAssignments.AsNoTracking()
         join category in db.StaffCategories.AsNoTracking() on membership.StaffCategoryId equals category.Id
         where membership.EmployeeId == employeeId && membership.EffectiveFrom <= date &&
            (membership.EffectiveTo == null || membership.EffectiveTo > date)
         orderby membership.Id
         select new AdvancedPolicyStaffCategorySource(membership.Id, category.Id, category.Code)).Take(3);

    internal IQueryable<AttendancePolicyCandidate> ApplicableQuery(Guid employeeId, AdvancedAttendanceFamily family, DateOnly date) =>
        FamilyQuery(family).Where(x => x.Assignment.EffectiveFrom <= date &&
            (x.Assignment.EffectiveTo == null || x.Assignment.EffectiveTo > date) &&
            (x.End == null || x.End.EffectiveTo > date || !x.AuthorityValid ||
                x.End.EffectiveTo <= x.Assignment.EffectiveFrom ||
                x.Assignment.EffectiveTo != null && x.End.EffectiveTo > x.Assignment.EffectiveTo) &&
            (x.Assignment.EmployeeId == employeeId || db.EmployeeAssignments.Any(m => m.EmployeeId == employeeId &&
                m.EffectiveFrom <= date && (m.EffectiveTo == null || m.EffectiveTo > date) &&
                (x.Assignment.DepartmentId == m.DepartmentId || x.Assignment.StaffCategoryId == m.StaffCategoryId ||
                    x.Assignment.SiteId != null && x.Assignment.SiteId == m.SiteId))));

    public async Task<AttendancePolicySource> SourceAsync(Guid employeeId, AdvancedAttendanceFamily family, DateOnly date, CancellationToken ct) =>
        new(await MembershipQuery(employeeId, date).ToArrayAsync(ct),
            Bounded(await ApplicableQuery(employeeId, family, date).OrderBy(x => x.Assignment.Id).Take(129).ToArrayAsync(ct), 128));

    public async Task<AdvancedPolicyDatedSource> DatedSourceAsync(Guid employeeId, AdvancedAttendanceFamily family, DateOnly date, CancellationToken ct) =>
        new(await FindAsync(family, ct), await SourceAsync(employeeId, family, date, ct), await StaffCategoryQuery(employeeId, date).ToArrayAsync(ct));

    public Task<bool> TargetExistsAsync(AttendancePolicyTarget target, Guid id, CancellationToken ct) => target switch {
        AttendancePolicyTarget.Employee => db.Employees.AnyAsync(x => x.Id == id, ct),
        AttendancePolicyTarget.Department => db.Departments.AnyAsync(x => x.Id == id && x.IsActive, ct),
        AttendancePolicyTarget.StaffCategory => db.StaffCategories.AnyAsync(x => x.Id == id && x.IsActive, ct),
        AttendancePolicyTarget.Site => db.Sites.AnyAsync(x => x.Id == id && x.IsActive, ct),
        _ => Task.FromResult(false)
    };

    public async Task<bool> StaffCategoryCodesExistAsync(IReadOnlyList<string> codes, CancellationToken ct) =>
        codes.Count is > 0 and <= 32 && codes.Distinct(StringComparer.Ordinal).Count() == codes.Count &&
        await db.StaffCategories.CountAsync(x => x.IsActive && codes.Contains(x.Code), ct) == codes.Count;

    public Task<bool> HasLockedPeriodAsync(DateOnly from, DateOnly? to, CancellationToken ct) =>
        db.AttendancePeriodLocks.AnyAsync(x => x.IsLocked && x.PeriodEndExclusive > from && (to == null || x.PeriodStart < to), ct);

    internal IQueryable<AttendancePolicyCandidate> OverlapQuery(AdvancedAttendanceFamily family, AttendancePolicyTarget target,
        Guid targetId, DateOnly from, DateOnly? to, Guid? exceptAssignmentId) => FamilyQuery(family).Where(x =>
            x.Assignment.Id != exceptAssignmentId && (to == null || x.Assignment.EffectiveFrom < to) &&
            (x.Assignment.EffectiveTo == null || x.Assignment.EffectiveTo > from) &&
            (x.End == null || x.End.EffectiveTo > from || !x.AuthorityValid || x.End.EffectiveTo <= x.Assignment.EffectiveFrom ||
                x.Assignment.EffectiveTo != null && x.End.EffectiveTo > x.Assignment.EffectiveTo) &&
            (target == AttendancePolicyTarget.Employee && x.Assignment.EmployeeId == targetId ||
                target == AttendancePolicyTarget.Department && x.Assignment.DepartmentId == targetId ||
                target == AttendancePolicyTarget.StaffCategory && x.Assignment.StaffCategoryId == targetId ||
                target == AttendancePolicyTarget.Site && x.Assignment.SiteId == targetId));

    public Task<bool> HasTargetOverlapAsync(AdvancedAttendanceFamily family, AttendancePolicyTarget target, Guid targetId,
        DateOnly from, DateOnly? to, Guid? exceptAssignmentId, CancellationToken ct)
    {
        RequireSerializable();
        return OverlapQuery(family, target, targetId, from, to, exceptAssignmentId).AnyAsync(ct);
    }

    public void Add(Policy policy) { RequireSerializable(); db.Policies.Add(policy); }
    public void Add(PolicyVersion version) { RequireSerializable(); db.PolicyVersions.Add(version); }
    public void Add(PolicyAssignment assignment) { RequireSerializable(); db.PolicyAssignments.Add(assignment); }
    public void Add(PolicyAssignmentEnd end) { RequireSerializable(); db.PolicyAssignmentEnds.Add(end); }
    public void ExpectVersion(Policy policy, byte[] version)
    { RequireSerializable(); db.Entry(policy).Property(x => x.RowVersion).OriginalValue = version; }
    public async Task SaveChangesAsync(CancellationToken ct) { RequireSerializable(); await db.SaveChangesAsync(ct); }

    private void RequireSerializable()
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Advanced policy mutations require the outer serializable AttendanceOperation transaction.");
    }

    private static T[] Bounded<T>(T[] rows, int maximum) => rows.Length <= maximum ? rows : throw AttendanceOperation.Conflict("advanced_policy_limit");
}
