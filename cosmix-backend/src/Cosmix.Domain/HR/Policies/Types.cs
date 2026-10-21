namespace Cosmix.Domain.HR.Policies;

public enum PolicyCategory { Attendance = 1, Leave = 2, Allowance = 3, VehicleFuel = 4, EmployeeException = 5 }

public enum PolicyAuthority { ApprovedWorkflow = 1, DirectHr = 2 }

internal static class PolicyPublicationAuthority
{
    public static PolicyAuthority Validate(PolicyAuthority authority, Guid? approvalRequestId)
    {
        if (!Enum.IsDefined(authority) || approvalRequestId == Guid.Empty ||
            (authority == PolicyAuthority.ApprovedWorkflow) != approvalRequestId.HasValue)
            throw new ArgumentException("Policy authority must identify either an approval or direct HR publication.");
        return authority;
    }
}
