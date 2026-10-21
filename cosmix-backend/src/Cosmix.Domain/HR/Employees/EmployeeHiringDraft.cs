using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.Employees;

/// <summary>Proposed hiring data, not an effective employment, salary or site assignment.</summary>
public sealed class EmployeeHiringDraft : IEmployeeProfileRecord
{
    private EmployeeHiringDraft() { }
    public EmployeeHiringDraft(Guid employeeId) => EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
    public Guid EmployeeId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? DesignationId { get; private set; }
    public Guid? StaffCategoryId { get; private set; }
    public Guid? EmploymentTypeId { get; private set; }
    public Guid? BranchId { get; private set; }
    public Guid? ReportingManagerEmployeeId { get; private set; }
    public Guid? ProposedShiftId { get; private set; }
    public string? ProfileReference { get; private set; }
    public string? FinancialReference { get; private set; }
    public string? MedicalReference { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void SetProfile(string protectedReference, Guid? department, Guid? designation, Guid? category,
        Guid? employmentType, Guid? branch, Guid? manager, Guid? shift)
    {
        var reference = ModelGuard.Required(protectedReference, 500, nameof(protectedReference));
        Guid?[] ids = [department, designation, category, employmentType, branch, manager, shift];
        if (ids.Any(x => x == Guid.Empty) || manager == EmployeeId)
            throw new ArgumentException("Valid master references and a different reporting manager are required.");
        ProfileReference = reference; DepartmentId = department; DesignationId = designation;
        StaffCategoryId = category; EmploymentTypeId = employmentType; BranchId = branch;
        ReportingManagerEmployeeId = manager; ProposedShiftId = shift;
    }
    public void SetFinancial(string reference) => FinancialReference = ModelGuard.Required(reference, 500, nameof(reference));
    public void SetMedical(string reference) => MedicalReference = ModelGuard.Required(reference, 500, nameof(reference));
}
