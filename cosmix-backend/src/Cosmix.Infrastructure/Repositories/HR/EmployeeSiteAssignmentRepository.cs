using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Infrastructure.Authentication.Access;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class EmployeeSiteAssignmentRepository(CosmixDbContext db) : IEmployeeSiteAssignmentRepository
{
    public Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken ct) =>
        db.Employees.SingleOrDefaultAsync(x => x.Id == employeeId, ct);

    public Task<bool> HasApprovedDecisionAsync(Employee employee, CancellationToken ct) =>
        db.Set<EmployeeOnboardingDecision>().AnyAsync(x => x.Id == employee.ApprovedOnboardingDecisionId &&
            x.EmployeeId == employee.Id && x.Decision == OnboardingDecision.Approve, ct);

    public async Task<bool> HasEligibleEmploymentHistoryAsync(Guid employeeId, DateTimeOffset now, CancellationToken ct)
    {
        var history = await db.Set<EmployeeStatusChange>().AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.EffectiveAt <= now)
            .OrderByDescending(x => x.EffectiveAt).Take(2).ToArrayAsync(ct);
        return AccessManagementRepository.HasEligibleEmploymentHistory(history, now);
    }

    public async Task<PagedResult<EmployeeAssignmentResponse>> ListAsync(Guid employeeId,
        EmployeeAssignmentListRequest request, DateOnly? activeOnOrAfter, CancellationToken ct)
    {
        var query = db.EmployeeAssignments.AsNoTracking().Where(x => x.EmployeeId == employeeId);
        if (activeOnOrAfter.HasValue)
            query = query.Where(x => x.EffectiveTo == null || x.EffectiveTo > activeOnOrAfter.Value);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.EffectiveFrom).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArrayAsync(ct);
        return new(rows.Select(x => new EmployeeAssignmentResponse(x.Id, x.EmployeeId, x.DepartmentId,
            x.DesignationId, x.StaffCategoryId, x.EmploymentTypeId, x.BranchId, x.SiteId, x.ReportingManagerId,
            x.EffectiveFrom, x.EffectiveTo, Convert.ToBase64String(x.RowVersion))).ToArray(), count, request.Page, request.PageSize);
    }

    public async Task<IReadOnlyList<EmployeeAssignment>> OpenAssignmentsAsync(Guid employeeId, CancellationToken ct) =>
        await db.EmployeeAssignments.Where(x => x.EmployeeId == employeeId && x.EffectiveTo == null)
            .OrderByDescending(x => x.EffectiveFrom).ThenBy(x => x.Id).Take(2).ToArrayAsync(ct);

    public Task<bool> HasConflictingAssignmentAsync(Guid employeeId, Guid currentAssignmentId,
        DateOnly currentEffectiveFrom, CancellationToken ct) =>
        db.EmployeeAssignments.AnyAsync(x => x.EmployeeId == employeeId && x.Id != currentAssignmentId &&
            (x.EffectiveTo == null || x.EffectiveTo > currentEffectiveFrom), ct);

    public Task<string?> ActiveStaffCategoryCodeAsync(Guid staffCategoryId, CancellationToken ct) =>
        db.StaffCategories.Where(x => x.Id == staffCategoryId && x.IsActive).Select(x => x.Code).SingleOrDefaultAsync(ct);

    public Task<Site?> FindSiteAsync(Guid siteId, CancellationToken ct) =>
        db.Sites.AsNoTracking().SingleOrDefaultAsync(x => x.Id == siteId, ct);

    public void ExpectVersion(Employee employee, byte[] version)
    {
        if (!employee.RowVersion.SequenceEqual(version)) throw EmployeeOperation.Conflict("employee_version_conflict");
        db.Entry(employee).Property(x => x.RowVersion).OriginalValue = version;
    }

    public void AddAssignment(EmployeeAssignment assignment) => db.EmployeeAssignments.Add(assignment);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
