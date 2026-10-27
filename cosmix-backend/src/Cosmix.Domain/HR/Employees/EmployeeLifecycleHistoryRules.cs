namespace Cosmix.Domain.HR.Employees;

/// <summary>Supported recorded transitions. Callers must separately verify the exact
/// independent approval, chronological history and effective eligibility interval.</summary>
public static class EmployeeLifecycleHistoryRules
{
    public static bool Supports(EmployeeStatus previous, EmployeeStatus next, string? approvedPolicyVersion)
    {
        if (previous == EmployeeStatus.Active && next is EmployeeStatus.Suspended or EmployeeStatus.Resigned or EmployeeStatus.Terminated ||
            previous == EmployeeStatus.Suspended && next is EmployeeStatus.Resigned or EmployeeStatus.Terminated) return true;
        var validPolicy = !string.IsNullOrWhiteSpace(approvedPolicyVersion) &&
            approvedPolicyVersion == approvedPolicyVersion.Trim() && approvedPolicyVersion.Length <= 100 &&
            !approvedPolicyVersion.Any(char.IsControl);
        return validPolicy && (previous is EmployeeStatus.Active or EmployeeStatus.Suspended && next == EmployeeStatus.Retired ||
            previous == EmployeeStatus.Suspended && next == EmployeeStatus.Active);
    }
}
