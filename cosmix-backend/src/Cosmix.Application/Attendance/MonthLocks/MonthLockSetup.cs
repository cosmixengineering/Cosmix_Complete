using Cosmix.Application.Authentication;

namespace Cosmix.Application.Attendance.MonthLocks;

/// <summary>Known missing setup is a blocking readiness issue, not a failed month read.
/// Integrity, authorization, storage and infrastructure failures must still propagate.</summary>
public static class MonthLockSetup
{
    public static bool IsMissingConfiguration(AuthenticationFailure failure) =>
        failure.StatusCode == 409 && failure.Code == "employee_assignment_unavailable" ||
        failure.StatusCode == 503 && failure.Code is "staff_taxonomy_category_not_configured" or
            "staff_taxonomy_taxonomy_not_effective";
}
