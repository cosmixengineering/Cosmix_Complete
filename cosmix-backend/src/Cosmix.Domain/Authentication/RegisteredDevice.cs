using Cosmix.Domain.Common;

namespace Cosmix.Domain.Authentication;

public sealed class RegisteredDevice
{
    private RegisteredDevice() { }
    public RegisteredDevice(Guid userAccountId, string deviceIdentifierHash, string platform, string displayName,
        DateTimeOffset registeredAt)
    {
        Id = Guid.NewGuid(); UserAccountId = ModelGuard.Id(userAccountId, nameof(userAccountId));
        DeviceIdentifierHash = ModelGuard.Required(deviceIdentifierHash, 256, nameof(deviceIdentifierHash));
        Platform = ModelGuard.Required(platform, 40, nameof(platform)); DisplayName = ModelGuard.Required(displayName, 150, nameof(displayName));
        RegisteredAt = ModelGuard.Utc(registeredAt);
    }
    public Guid Id { get; private set; }
    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;
    public string DeviceIdentifierHash { get; private set; } = string.Empty;
    public string Platform { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public DateTimeOffset RegisteredAt { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset? LastSeenAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }
    public string? IntegrityAssessmentCode { get; private set; }
    public DateTimeOffset? IntegrityAssessedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void MarkVerified(DateTimeOffset at)
    {
        if (RevokedAt.HasValue || at < RegisteredAt || VerifiedAt.HasValue) throw new InvalidOperationException("Invalid device verification.");
        VerifiedAt = ModelGuard.Utc(at);
    }
    public void RecordSeen(DateTimeOffset at, string? integrityAssessmentCode)
    {
        if (RevokedAt.HasValue || at < RegisteredAt || LastSeenAt > at) throw new InvalidOperationException("Invalid device observation.");
        var result = ModelGuard.Optional(integrityAssessmentCode, 100, nameof(integrityAssessmentCode));
        LastSeenAt = ModelGuard.Utc(at); IntegrityAssessmentCode = result; IntegrityAssessedAt = result is null ? null : ModelGuard.Utc(at);
    }
    public void Revoke(string reason, DateTimeOffset at)
    {
        if (RevokedAt.HasValue || at < RegisteredAt) throw new InvalidOperationException("Invalid device revocation.");
        RevocationReason = ModelGuard.Required(reason, 1000, nameof(reason)); RevokedAt = ModelGuard.Utc(at);
    }
}
