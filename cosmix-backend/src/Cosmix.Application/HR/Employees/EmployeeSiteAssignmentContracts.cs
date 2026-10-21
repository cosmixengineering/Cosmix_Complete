using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.HR.Employees;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssignEmployeeSiteRequest(
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    Guid SiteId, DateOnly EffectiveFrom,
    [Required, StringLength(1000)] string Reason);

public sealed record EmployeeAssignmentListRequest(
    [Range(1, 1000000)] int Page = 1, [Range(1, 100)] int PageSize = 25);

public sealed record EmployeeAssignmentResponse(Guid Id, Guid EmployeeId, Guid DepartmentId,
    Guid DesignationId, Guid StaffCategoryId, Guid EmploymentTypeId, Guid? BranchId, Guid? SiteId,
    Guid? ReportingManagerId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Version);

/// <summary>Version is the employee aggregate version required by assignment writes.</summary>
public sealed record EmployeeAssignmentPage(Guid EmployeeId, string Version,
    PagedResult<EmployeeAssignmentResponse> Assignments);

public interface IEmployeeSiteAssignmentRepository
{
    Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken ct);
    Task<bool> HasApprovedDecisionAsync(Employee employee, CancellationToken ct);
    Task<bool> HasEligibleEmploymentHistoryAsync(Guid employeeId, DateTimeOffset now, CancellationToken ct);
    Task<PagedResult<EmployeeAssignmentResponse>> ListAsync(Guid employeeId,
        EmployeeAssignmentListRequest request, DateOnly? activeOnOrAfter, CancellationToken ct);
    /// <summary>At most two tracked rows, sufficient to reject multiple open assignments.</summary>
    Task<IReadOnlyList<EmployeeAssignment>> OpenAssignmentsAsync(Guid employeeId, CancellationToken ct);
    Task<bool> HasConflictingAssignmentAsync(Guid employeeId, Guid currentAssignmentId,
        DateOnly currentEffectiveFrom, CancellationToken ct);
    Task<string?> ActiveStaffCategoryCodeAsync(Guid staffCategoryId, CancellationToken ct);
    Task<Site?> FindSiteAsync(Guid siteId, CancellationToken ct);
    void ExpectVersion(Employee employee, byte[] version);
    void AddAssignment(EmployeeAssignment assignment);
    Task SaveChangesAsync(CancellationToken ct);
}
