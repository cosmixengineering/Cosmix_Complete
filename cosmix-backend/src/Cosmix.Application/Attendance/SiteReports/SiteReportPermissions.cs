using Cosmix.Application.Authentication.Access;

namespace Cosmix.Application.Attendance.SiteReports;

public static class SiteReportPermissions
{
    public const string Self = "attendance.site-reports.self";
    public const string Read = "attendance.site-reports.read";
    public const string Decide = "attendance.site-reports.decide";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = Array.AsReadOnly<AccessPermissionDefinition>([
        new(Self, "Submit and read only the signed-in employee's operational site reports", false, false, true),
        new(Read, "Read operational site reports within explicitly granted site scopes", false, true, false),
        new(Decide, "Make HR Accept/Return/Reject decisions on others' operational site reports within granted site scopes", false, true, false)
    ]);
}
