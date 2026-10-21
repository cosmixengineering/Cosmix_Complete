using Cosmix.Application.Authentication.Access;

namespace Cosmix.Application.Attendance.Punches;

public static class AttendanceCapturePermissions
{
    public const string Capture = "attendance.punches.self";
    public const string SelfRead = "attendance.history.self";
    public const string FaceSelf = "attendance.face.self";
    public const string FaceManage = "attendance.face.manage";
    public const string FaceRead = "attendance.face.read";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = Array.AsReadOnly<AccessPermissionDefinition>([
        new(Capture, "Submit own mobile attendance evidence with registered device", false, false, true),
        new(SelfRead, "Read own attendance state, punches and work sessions", false, false, true),
        new(FaceSelf, "Run own Check In face challenge", false, false, true),
        new(FaceManage, "Enroll or revoke employee face enrollment subject to approved biometric authority", true, true, false),
        new(FaceRead, "Read employee face enrollment and verification attempt metadata", true, true, false)
    ]);
}
