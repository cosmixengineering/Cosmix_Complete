using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.EmployeeAssignments;

/// <summary>Start-inclusive, end-exclusive employment assignment history.</summary>
public sealed class EmployeeAssignment : IEmployeeProfileRecord
{
    private EmployeeAssignment() { }

    public EmployeeAssignment(Guid employeeId, Guid departmentId, Guid designationId,
        Guid staffCategoryId, Guid employmentTypeId, Guid? branchId, Guid? siteId,
        Guid? reportingManagerId, DateOnly effectiveFrom, DateOnly? effectiveTo,
        string recordedByUserId, DateTimeOffset recordedAt)
    {
        ModelGuard.EffectiveDates(effectiveFrom, effectiveTo);
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        DepartmentId = ModelGuard.Id(departmentId, nameof(departmentId));
        DesignationId = ModelGuard.Id(designationId, nameof(designationId));
        StaffCategoryId = ModelGuard.Id(staffCategoryId, nameof(staffCategoryId));
        EmploymentTypeId = ModelGuard.Id(employmentTypeId, nameof(employmentTypeId));
        BranchId = branchId.HasValue ? ModelGuard.Id(branchId.Value, nameof(branchId)) : null;
        SiteId = siteId.HasValue ? ModelGuard.Id(siteId.Value, nameof(siteId)) : null;
        ReportingManagerId = reportingManagerId.HasValue
            ? ModelGuard.Id(reportingManagerId.Value, nameof(reportingManagerId)) : null;
        if (ReportingManagerId == EmployeeId)
            throw new ArgumentException("An employee cannot be their own manager.", nameof(reportingManagerId));
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Cosmix.Domain.HR.Employees.Employee Employee { get; private set; } = null!;
    public Guid DepartmentId { get; private set; }
    public Cosmix.Domain.HR.Departments.Department Department { get; private set; } = null!;
    public Guid DesignationId { get; private set; }
    public Cosmix.Domain.HR.Designations.Designation Designation { get; private set; } = null!;
    public Guid StaffCategoryId { get; private set; }
    public Cosmix.Domain.HR.StaffCategories.StaffCategory StaffCategory { get; private set; } = null!;
    public Guid EmploymentTypeId { get; private set; }
    public Cosmix.Domain.HR.EmploymentTypes.EmploymentType EmploymentType { get; private set; } = null!;
    public Guid? BranchId { get; private set; }
    public Cosmix.Domain.HR.Branches.Branch? Branch { get; private set; }
    public Guid? SiteId { get; private set; }
    public Cosmix.Domain.Attendance.Sites.Site? Site { get; private set; }
    public Guid? ReportingManagerId { get; private set; }
    public Cosmix.Domain.HR.Employees.Employee? ReportingManager { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void EndOn(DateOnly effectiveTo)
    {
        ModelGuard.EffectiveDates(EffectiveFrom, effectiveTo);
        if (EffectiveTo.HasValue)
            throw new InvalidOperationException("An ended assignment cannot be rewritten.");
        EffectiveTo = effectiveTo;
    }
}
