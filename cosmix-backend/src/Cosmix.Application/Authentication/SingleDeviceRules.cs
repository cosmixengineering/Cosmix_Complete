using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;

namespace Cosmix.Application.Authentication;

/// <summary>Evaluate the complete current device set; historical revoked rows are excluded by the repository.</summary>
public static class SingleDeviceRules
{
    public static bool CanSignIn(IReadOnlyList<RegisteredDevice> activeDevices, RegisteredDevice? presented) =>
        activeDevices.Count == 0 && presented is null ||
        activeDevices.Count == 1 && presented is not null && activeDevices[0].Id == presented.Id;

    public static bool CanUseSession(IReadOnlyList<RegisteredDevice> activeDevices, Guid? sessionDeviceId) =>
        activeDevices.Count == 1 && activeDevices[0].Id == sessionDeviceId;

    public static bool CanApproveReplacement(MobileOtpChallenge? proof, Guid accountId, Guid? phoneBindingId,
        int securityVersion, DateTimeOffset now) =>
        proof is not null && proof.HasReplacementProof && proof.UserAccountId == accountId &&
        proof.PhoneBindingId == phoneBindingId && proof.SecurityVersion == securityVersion &&
        (proof.Platform is "android" or "ios") && proof.DeviceHash.Length == 64 &&
        proof.DeviceHash.All(char.IsAsciiHexDigit) && proof.ConsumedAt <= now &&
        !proof.RevokedAt.HasValue && proof.IssuedAt <= now && proof.ExpiresAt > now &&
        proof.DeviceName.Length is >= 1 and <= 150;
}
