using Cosmix.Application.Authentication.Access;

namespace Cosmix.Application.Attendance.Shifts;

public static class SchedulingPermissions
{
    public const string ShiftsRead = "attendance.shifts.read";
    public const string ShiftsManage = "attendance.shifts.manage";
    public const string RostersRead = "attendance.rosters.read";
    public const string RostersManage = "attendance.rosters.manage";
    public const string MyShift = "attendance.rosters.self";

    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = Array.AsReadOnly<AccessPermissionDefinition>([
        new(ShiftsRead, "Read shift masters and immutable schedule versions", false, true, false),
        new(ShiftsManage, "Manage shift masters and publish immutable schedule versions", false, true, false),
        new(RostersRead, "Read site schedules and employee roster history within granted scopes", false, true, false),
        new(RostersManage, "Directly assign, replace, end and temporarily change site shifts within granted site scopes", false, true, false),
        new(MyShift, "Read only the authenticated employee's own shift", false, false, true)
    ]);
}
