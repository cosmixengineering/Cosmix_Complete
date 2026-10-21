namespace Cosmix.Application.HR.Leaves;

internal static class LeaveRoleCodes
{
    internal const string Admin = "ADMIN";
    internal const string SuperAdmin = "SUPERADMIN";

    // Historical evidence keeps its exact bytes and crossbindings; these aliases never grant live authority.
    internal static bool IsRetainedAdmin(string? role) => role is Admin or "Admin";
    internal static bool IsRetainedReviewer(string? role) => role is Admin or SuperAdmin or "Admin" or "SuperAdmin";
}
