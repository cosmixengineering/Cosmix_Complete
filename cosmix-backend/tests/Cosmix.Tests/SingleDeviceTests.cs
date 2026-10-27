using Cosmix.Application.Authentication;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Xunit;

namespace Cosmix.Tests;

public class SingleDeviceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstDeviceAndSameDeviceCanSignIn()
    {
        var account = Guid.NewGuid();
        var device = Device(account, "a");
        Assert.True(SingleDeviceRules.CanSignIn([], null));
        Assert.True(SingleDeviceRules.CanSignIn([device], device));
        Assert.True(SingleDeviceRules.CanUseSession([device], device.Id));
    }

    [Fact]
    public void DifferentOrLegacyMultipleActiveDevicesRequireRecovery()
    {
        var account = Guid.NewGuid();
        var first = Device(account, "a");
        var second = Device(account, "b");
        Assert.False(SingleDeviceRules.CanSignIn([first], second));
        Assert.False(SingleDeviceRules.CanSignIn([first, second], first));
        Assert.False(SingleDeviceRules.CanUseSession([first, second], first.Id));
        Assert.False(SingleDeviceRules.CanUseSession([first], second.Id));
    }

    [Fact]
    public void OnlyExplicitSuccessfulReplacementProofIsRecognized()
    {
        var denied = Challenge();
        denied.Consume(Now.AddMinutes(1));
        Assert.False(denied.HasReplacementProof);

        var proven = Challenge();
        proven.MarkReplacementProof(Now.AddMinutes(1));
        Assert.True(proven.HasReplacementProof);
        Assert.False(proven.IsUsableAt(Now.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => proven.MarkReplacementProof(Now.AddMinutes(2)));
        proven.Revoke(Now.AddMinutes(2));
        Assert.NotNull(proven.RevokedAt);
    }

    [Fact]
    public void ApprovalProofRejectsWrongAccountPhoneVersionExpiryAndRevocation()
    {
        var proof = Challenge();
        proof.MarkReplacementProof(Now.AddMinutes(1));
        var account = proof.UserAccountId;
        var phone = proof.PhoneBindingId;
        Assert.True(SingleDeviceRules.CanApproveReplacement(proof, account, phone, 3, Now.AddMinutes(2)));
        Assert.False(SingleDeviceRules.CanApproveReplacement(proof, Guid.NewGuid(), phone, 3, Now.AddMinutes(2)));
        Assert.False(SingleDeviceRules.CanApproveReplacement(proof, account, Guid.NewGuid(), 3, Now.AddMinutes(2)));
        Assert.False(SingleDeviceRules.CanApproveReplacement(proof, account, phone, 4, Now.AddMinutes(2)));
        Assert.False(SingleDeviceRules.CanApproveReplacement(proof, account, phone, 3, Now.AddMinutes(5)));
        proof.Revoke(Now.AddMinutes(2));
        Assert.False(SingleDeviceRules.CanApproveReplacement(proof, account, phone, 3, Now.AddMinutes(3)));
    }

    [Fact]
    public void ExpiredChallengeCannotBecomeReplacementProof()
    {
        var expired = Challenge();
        Assert.Throws<InvalidOperationException>(() => expired.MarkReplacementProof(Now.AddMinutes(5)));
        Assert.False(expired.HasReplacementProof);
    }

    [Fact]
    public void RecoveryRevokesEveryActiveDeviceAndSessionWhileKeepingHistory()
    {
        var account = Guid.NewGuid();
        var first = Device(account, "a");
        var second = Device(account, "b");
        first.MarkVerified(Now);
        second.MarkVerified(Now);
        var session = (UserSession)Activator.CreateInstance(typeof(UserSession), nonPublic: true)!;
        Set(session, nameof(UserSession.CreatedAt), Now);
        Set(session, nameof(UserSession.ExpiresAt), Now.AddHours(1));
        Assert.True(session.IsActiveAt(Now.AddMinutes(1)));

        DeviceRecoveryRevocations.Apply([first, second], [session], Now.AddMinutes(2));

        Assert.Equal("device-recovery", first.RevocationReason);
        Assert.Equal("device-recovery", second.RevocationReason);
        Assert.Equal("device-recovery", session.RevocationReason);
        Assert.False(session.IsActiveAt(Now.AddMinutes(3)));
        Assert.False(SingleDeviceRules.CanUseSession([], first.Id));
        Assert.Equal(account, first.UserAccountId);
        Assert.Equal(account, second.UserAccountId);
        Assert.NotEqual(first.Id, second.Id);
    }

    private static MobileOtpChallenge Challenge()
    {
        // The rule requires only persisted challenge fields; onboarding an account is outside this unit test.
        var challenge = (MobileOtpChallenge)Activator.CreateInstance(typeof(MobileOtpChallenge), nonPublic: true)!;
        Set(challenge, nameof(MobileOtpChallenge.Purpose), "MobileLogin");
        Set(challenge, nameof(MobileOtpChallenge.UserAccountId), Guid.NewGuid());
        Set(challenge, nameof(MobileOtpChallenge.PhoneBindingId), Guid.NewGuid());
        Set(challenge, nameof(MobileOtpChallenge.SecurityVersion), 3);
        Set(challenge, nameof(MobileOtpChallenge.DeviceHash), new string('b', 64));
        Set(challenge, nameof(MobileOtpChallenge.Platform), "android");
        Set(challenge, nameof(MobileOtpChallenge.DeviceName), "Replacement");
        Set(challenge, nameof(MobileOtpChallenge.Verifier), new string('a', 64));
        Set(challenge, nameof(MobileOtpChallenge.IssuedAt), Now);
        Set(challenge, nameof(MobileOtpChallenge.ExpiresAt), Now.AddMinutes(5));
        Set(challenge, nameof(MobileOtpChallenge.MaximumAttempts), 5);
        return challenge;
    }

    private static void Set<T>(T target, string property, object value) =>
        typeof(T).GetProperty(property)!.SetValue(target, value);

    private static RegisteredDevice Device(Guid account, string suffix) =>
        new(account, new string(suffix[0], 64), "android", "Phone", Now);
}
