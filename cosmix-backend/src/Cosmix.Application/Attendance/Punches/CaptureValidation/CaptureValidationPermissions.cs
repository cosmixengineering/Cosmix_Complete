using Cosmix.Application.Authentication.Access;

namespace Cosmix.Application.Attendance.Punches.CaptureValidation;

public static class CaptureValidationPermissions
{
    public const string Read = "attendance.capture.validation.read";
    public const string Manage = "attendance.capture.validation.manage";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = Array.AsReadOnly<AccessPermissionDefinition>([
        new(Read, "Read global approved capture-validation policy and immutable history", true, true, false),
        new(Manage, "Publish future global capture-validation policy; fixed Admin/SuperAdmin required", true, true, false)
    ]);
}
