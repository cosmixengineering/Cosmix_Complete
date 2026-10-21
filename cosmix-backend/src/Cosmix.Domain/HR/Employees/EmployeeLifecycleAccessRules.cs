namespace Cosmix.Domain.HR.Employees;

/// <summary>An effective return to Active cannot expose old access before chronological lifecycle cleanup commits.</summary>
public static class EmployeeLifecycleAccessRules
{
    public static bool IsReconciled(EmployeeStatusChange? latest, Guid? appliedStatusChangeId) =>
        latest is null || latest.NewStatus != EmployeeStatus.Active ||
        latest.PreviousStatus is EmployeeStatus.Draft or EmployeeStatus.PendingApproval || latest.Id == appliedStatusChangeId;
}
