using Cosmix.Domain.Attendance.FaceVerification;

namespace Cosmix.Application.Attendance.FaceVerification;

public sealed record FaceReadinessResult(bool SystemAvailable, bool EnrollmentActive, bool EnrollmentCompatible,
    bool CheckInFaceReady, string ReasonCode);

/// <summary>Configuration and own-enrollment readiness only; no operational/provider acceptance is implied.</summary>
public static class FaceReadiness
{
    public static FaceReadinessResult Evaluate(FaceVerificationPolicy policy, FaceProviderCapability capability,
        bool templateStoreConfigured, FaceEnrollment? enrollment)
    {
        var configured = capability.Available && ValidIdentity(capability.Provider) && ValidIdentity(capability.Version);
        var active = enrollment?.IsActive == true;
        var compatible = active && configured && enrollment!.Provider == capability.Provider && enrollment.ProviderVersion == capability.Version;
        var system = policy.IsConfirmed && configured && templateStoreConfigured;
        var reason = !policy.IsConfirmed ? "face_policy_not_confirmed" : !configured ? "face_provider_not_configured" :
            !templateStoreConfigured ? "face_template_storage_unavailable" : !active ? "face_enrollment_required" :
            !compatible ? "face_enrollment_provider_mismatch" : "ready";
        return new(system, active, compatible, system && compatible, reason);
    }
    public static bool ValidIdentity(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 100 && !value.Any(char.IsControl);
}
