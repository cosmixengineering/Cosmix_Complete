using Cosmix.Application.Attendance.Common;
using Cosmix.Application.HR.Policies;
using Cosmix.Domain.HR.Policies;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class AttendancePolicyRepository(CosmixDbContext db) : IAttendancePolicyRepository
{
    public async Task<IReadOnlyList<Policy>> ListAsync(CancellationToken ct) =>
        Bounded(await db.Policies.AsNoTracking().Where(x => x.Category == PolicyCategory.Attendance)
            .OrderBy(x => x.Code).Take(513).ToArrayAsync(ct), 512);
    public Task<Policy?> FindAsync(Guid id, CancellationToken ct) =>
        db.Policies.SingleOrDefaultAsync(x => x.Id == id && x.Category == PolicyCategory.Attendance, ct);
    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) => db.Policies.AnyAsync(x => x.Code == code, ct);
    public async Task<IReadOnlyList<PolicyVersion>> VersionsAsync(Guid policyId, CancellationToken ct) =>
        Bounded(await db.PolicyVersions.AsNoTracking().Where(x => x.PolicyId == policyId)
            .OrderBy(x => x.VersionNumber).Take(1025).ToArrayAsync(ct), 1024);

    internal IQueryable<AttendancePolicyCandidate> AssignmentQuery() =>
        from a in db.PolicyAssignments.AsNoTracking()
        join v in db.PolicyVersions.AsNoTracking() on a.PolicyVersionId equals v.Id
        join p in db.Policies.AsNoTracking() on v.PolicyId equals p.Id
        join end in db.PolicyAssignmentEnds.AsNoTracking() on a.Id equals end.PolicyAssignmentId into ends
        from e in ends.DefaultIfEmpty()
        where p.Category == PolicyCategory.Attendance
        select new AttendancePolicyCandidate
        {
            Assignment = a, Version = v, End = e,
            // The former foundation did not define a policy approval producer or request-type/subject
            // contract. A generic approved request cannot establish policy authority. Preserve its
            // history but fail closed until that legacy authority can be explicitly verified.
            AuthorityValid = a.Authority == PolicyAuthority.DirectHr && a.ApprovalRequestId == null &&
                v.Authority == PolicyAuthority.DirectHr && v.ApprovalRequestId == null &&
                (e == null || e.Authority == PolicyAuthority.DirectHr && e.ApprovalRequestId == null)
        };

    public async Task<IReadOnlyList<AttendancePolicyCandidate>> AssignmentsAsync(Guid policyId, CancellationToken ct) =>
        Bounded(await AssignmentQuery().Where(x => x.Version.PolicyId == policyId).OrderBy(x => x.Assignment.EffectiveFrom)
            .ThenBy(x => x.Assignment.Id).Take(4097).ToArrayAsync(ct), 4096);

    internal IQueryable<AttendancePolicyCandidate> ApplicableQuery(Guid employeeId, DateOnly date) =>
        AssignmentQuery().Where(x => x.Assignment.EffectiveFrom <= date &&
            (x.Assignment.EffectiveTo == null || x.Assignment.EffectiveTo > date) &&
            (x.End == null || x.End.EffectiveTo > date || !x.AuthorityValid) &&
            (x.Assignment.EmployeeId == employeeId || db.EmployeeAssignments.Any(m => m.EmployeeId == employeeId &&
                m.EffectiveFrom <= date && (m.EffectiveTo == null || m.EffectiveTo > date) &&
                (x.Assignment.DepartmentId == m.DepartmentId || x.Assignment.StaffCategoryId == m.StaffCategoryId ||
                 x.Assignment.SiteId != null && x.Assignment.SiteId == m.SiteId))));

    public async Task<AttendancePolicySource> SourceAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        var memberships = await db.EmployeeAssignments.AsNoTracking().Where(m => m.EmployeeId == employeeId &&
            m.EffectiveFrom <= date && (m.EffectiveTo == null || m.EffectiveTo > date)).OrderBy(m => m.Id).Take(3)
            .Select(m => new AttendancePolicyMembership(m.Id, m.EmployeeId, m.DepartmentId, m.StaffCategoryId,
                m.SiteId, m.EffectiveFrom, m.EffectiveTo)).ToArrayAsync(ct);
        var candidates = Bounded(await ApplicableQuery(employeeId, date).OrderBy(x => x.Assignment.Id).Take(129).ToArrayAsync(ct), 128);
        return new(memberships, candidates);
    }
    public Task<bool> TargetExistsAsync(AttendancePolicyTarget target, Guid id, CancellationToken ct) => target switch
    {
        AttendancePolicyTarget.Employee => db.Employees.AnyAsync(x => x.Id == id, ct),
        AttendancePolicyTarget.Department => db.Departments.AnyAsync(x => x.Id == id && x.IsActive, ct),
        AttendancePolicyTarget.StaffCategory => db.StaffCategories.AnyAsync(x => x.Id == id && x.IsActive, ct),
        AttendancePolicyTarget.Site => db.Sites.AnyAsync(x => x.Id == id && x.IsActive, ct),
        _ => Task.FromResult(false)
    };
    public Task<bool> HasLockedPeriodAsync(DateOnly from, DateOnly? to, CancellationToken ct) =>
        db.AttendancePeriodLocks.AnyAsync(x => x.IsLocked && x.PeriodEndExclusive > from &&
            (to == null || x.PeriodStart < to), ct);
    internal IQueryable<AttendancePolicyCandidate> OverlapQuery(AttendancePolicyTarget target, Guid targetId, DateOnly from,
        DateOnly? to, Guid? exceptAssignmentId) => AssignmentQuery().Where(x =>
        x.Assignment.Id != exceptAssignmentId && (to == null || x.Assignment.EffectiveFrom < to) &&
        (x.Assignment.EffectiveTo == null || x.Assignment.EffectiveTo > from) && (x.End == null || x.End.EffectiveTo > from) &&
        (target == AttendancePolicyTarget.Employee && x.Assignment.EmployeeId == targetId ||
         target == AttendancePolicyTarget.Department && x.Assignment.DepartmentId == targetId ||
         target == AttendancePolicyTarget.StaffCategory && x.Assignment.StaffCategoryId == targetId ||
         target == AttendancePolicyTarget.Site && x.Assignment.SiteId == targetId));
    public Task<bool> HasTargetOverlapAsync(AttendancePolicyTarget target, Guid targetId, DateOnly from,
        DateOnly? to, Guid? exceptAssignmentId, CancellationToken ct) =>
        OverlapQuery(target, targetId, from, to, exceptAssignmentId).AnyAsync(ct);
    public void Add(Policy policy) => db.Policies.Add(policy);
    public void Add(PolicyVersion version) => db.PolicyVersions.Add(version);
    public void Add(PolicyAssignment assignment) => db.PolicyAssignments.Add(assignment);
    public void Add(PolicyAssignmentEnd end) => db.PolicyAssignmentEnds.Add(end);
    public void ExpectVersion(Policy policy, byte[] version) => db.Entry(policy).Property(x => x.RowVersion).OriginalValue = version;
    public async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
    private static T[] Bounded<T>(T[] rows, int limit) => rows.Length <= limit ? rows : throw AttendanceOperation.Conflict("attendance_policy_limit");
}
