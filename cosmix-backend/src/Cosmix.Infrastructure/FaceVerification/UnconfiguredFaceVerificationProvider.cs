using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Authentication;

namespace Cosmix.Infrastructure.FaceVerification;

/// <summary>No biometric provider has been approved. Never generates a successful enrollment or proof.</summary>
public sealed class UnconfiguredFaceVerificationProvider : IFaceVerificationProvider
{
    public FaceProviderCapability Capability => new(false, "unconfigured", "unconfigured", "face_provider_not_configured");
    public Task<FaceProviderEnrollmentResult> EnrollAsync(FaceProviderEnrollmentInput input, CancellationToken ct) =>
        throw new AuthenticationFailure("face_provider_not_configured", 503);
    public Task<FaceProviderVerificationResult> VerifyAsync(FaceProviderVerificationInput input, CancellationToken ct) =>
        throw new AuthenticationFailure("face_provider_not_configured", 503);
}
