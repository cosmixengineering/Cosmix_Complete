using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Departments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.StaffCategories;

namespace Cosmix.Domain.HR.Policies;

/// <summary>Approved individual or population assignment, including early-reporting/study exceptions. The published schema identifies the actual exception rules.</summary>
public sealed class PolicyAssignment : IAppendOnlyEntity
{
    private PolicyAssignment() { }

    public PolicyAssignment(Guid policyVersionId, Guid? roleId, Guid? employeeId, Guid? departmentId, Guid? staffCategoryId, Guid? siteId, DateOnly effectiveFrom, DateOnly? effectiveTo, string reason, string? instructionPrivateReference, Guid? approvalRequestId, string recordedByUserId, DateTimeOffset recordedAt, PolicyAuthority authority = PolicyAuthority.ApprovedWorkflow)
    {
        ModelGuard.EffectiveDates(effectiveFrom, effectiveTo);
        if (new Guid?[] { roleId, employeeId, departmentId, staffCategoryId, siteId }.Count(x => x.HasValue) != 1) throw new ArgumentException("One explicit policy-assignment target is required.");
        Id = Guid.NewGuid();
        PolicyVersionId = ModelGuard.Id(policyVersionId, nameof(policyVersionId));
        RoleId = roleId.HasValue ? ModelGuard.Id(roleId.Value, nameof(roleId)) : null;
        EmployeeId = employeeId.HasValue ? ModelGuard.Id(employeeId.Value, nameof(employeeId)) : null;
        DepartmentId = departmentId.HasValue ? ModelGuard.Id(departmentId.Value, nameof(departmentId)) : null;
        StaffCategoryId = staffCategoryId.HasValue ? ModelGuard.Id(staffCategoryId.Value, nameof(staffCategoryId)) : null;
        SiteId = siteId.HasValue ? ModelGuard.Id(siteId.Value, nameof(siteId)) : null;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        Reason = ModelGuard.Required(reason, 1000, nameof(reason));
        InstructionPrivateReference = ModelGuard.Optional(instructionPrivateReference, 500, nameof(instructionPrivateReference));
        Authority = PolicyPublicationAuthority.Validate(authority, approvalRequestId);
        ApprovalRequestId = approvalRequestId;
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid PolicyVersionId { get; private set; }
    public Guid? RoleId { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? StaffCategoryId { get; private set; }
    public Guid? SiteId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? InstructionPrivateReference { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
    public PolicyAuthority Authority { get; private set; } = PolicyAuthority.ApprovedWorkflow;
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public PolicyVersion PolicyVersion { get; private set; } = null!;
    public Role? Role { get; private set; }
    public Employee? Employee { get; private set; }
    public Department? Department { get; private set; }
    public StaffCategory? StaffCategory { get; private set; }
    public Site? Site { get; private set; }
    public ApprovalRequest? ApprovalRequest { get; private set; }
}
