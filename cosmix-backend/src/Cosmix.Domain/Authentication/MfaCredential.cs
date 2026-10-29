using Cosmix.Domain.Common;

namespace Cosmix.Domain.Authentication;

/// <summary>Protected secret reference for an approved method, or a hash of one single-use recovery code.</summary>
public sealed class MfaCredential
{
    private MfaCredential() { }
    public MfaCredential(Guid userAccountId, MfaMethod method, string? protectedSecretReference,
        string? recoveryCodeHash, DateTimeOffset enrolledAt)
    {
        if (!Enum.IsDefined(method)) throw new ArgumentOutOfRangeException(nameof(method));
        if ((method == MfaMethod.RecoveryCode && (string.IsNullOrWhiteSpace(recoveryCodeHash) || protectedSecretReference is not null)) ||
            (method != MfaMethod.RecoveryCode && (string.IsNullOrWhiteSpace(protectedSecretReference) || recoveryCodeHash is not null)))
            throw new ArgumentException("Method requires either a protected secret reference or a recovery-code digest.");
        Id = Guid.NewGuid(); UserAccountId = ModelGuard.Id(userAccountId, nameof(userAccountId)); Method = method;
        ProtectedSecretReference = ModelGuard.Optional(protectedSecretReference, 500, nameof(protectedSecretReference));
        RecoveryCodeHash = ModelGuard.Optional(recoveryCodeHash, 256, nameof(recoveryCodeHash)); EnrolledAt = ModelGuard.Utc(enrolledAt);
    }
    public Guid Id { get; private set; }
    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;
    public MfaMethod Method { get; private set; }
    public string? ProtectedSecretReference { get; private set; }
    public string? RecoveryCodeHash { get; private set; }
    public DateTimeOffset EnrolledAt { get; private set; }
    public Guid? EnrollmentSessionId { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public long? LastAcceptedCounter { get; private set; }
    public DateTimeOffset? RecoveryCodeConsumedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void BindEnrollment(Guid sessionId)
    {
        if (Method != MfaMethod.Totp || ConfirmedAt.HasValue || RevokedAt.HasValue || EnrollmentSessionId.HasValue)
            throw new InvalidOperationException("Only a new authenticator can bind its enrollment session.");
        EnrollmentSessionId = ModelGuard.Id(sessionId, nameof(sessionId));
    }
    public void Confirm(DateTimeOffset at)
    {
        if (at < EnrolledAt || RevokedAt.HasValue || ConfirmedAt.HasValue) throw new InvalidOperationException("Invalid MFA confirmation.");
        ConfirmedAt = ModelGuard.Utc(at);
    }
    public void RecordAcceptedProof(DateTimeOffset at, long? counter)
    {
        if (!ConfirmedAt.HasValue || at < ConfirmedAt || RevokedAt.HasValue || RecoveryCodeConsumedAt.HasValue)
            throw new InvalidOperationException("MFA credential is not active.");
        if (Method == MfaMethod.Totp && (!counter.HasValue || counter < 0 || counter <= LastAcceptedCounter))
            throw new InvalidOperationException("MFA counter must be new.");
        if (Method == MfaMethod.Totp) LastAcceptedCounter = counter;
        if (Method == MfaMethod.RecoveryCode) RecoveryCodeConsumedAt = ModelGuard.Utc(at);
    }
    public void Revoke(DateTimeOffset at)
    {
        if (at < EnrolledAt || RevokedAt.HasValue) throw new InvalidOperationException("Invalid credential revocation.");
        RevokedAt = ModelGuard.Utc(at);
    }
}
