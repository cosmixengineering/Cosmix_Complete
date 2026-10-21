using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.FaceVerification;

public enum FaceVerificationOutcome { Pending = 1, Passed = 2, Failed = 3, Unavailable = 4, Expired = 5 }

/// <summary>Single-use challenge/result bound to one employee, device and punch request. No raw capture is stored.</summary>
public sealed class FaceVerificationAttempt
{
    private FaceVerificationAttempt() { }
    public FaceVerificationAttempt(FaceEnrollment enrollment, UserAccount account, RegisteredDevice device, string punchIdempotencyKey,
        string challengeSha256, string provider, string providerVersion, DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc, string correlationId)
    {
        if (!enrollment.IsActive || enrollment.EmployeeId != account.EmployeeId || account.Status != AccountStatus.Active
            || device.UserAccountId != account.Id || device.RevokedAt.HasValue || !device.VerifiedAt.HasValue || device.VerifiedAt > issuedAtUtc)
            throw new InvalidOperationException("The active enrollment and registered device must belong to this employee account.");
        if (expiresAtUtc <= issuedAtUtc) throw new ArgumentException("The challenge must expire after issue.");
        challengeSha256 = ModelGuard.Required(challengeSha256, 64, nameof(challengeSha256)).ToUpperInvariant();
        if (challengeSha256.Length != 64 || !challengeSha256.All(Uri.IsHexDigit)) throw new ArgumentException("A SHA-256 challenge digest is required.");
        Id = Guid.NewGuid(); EmployeeId = enrollment.EmployeeId; FaceEnrollmentId = enrollment.Id;
        UserAccountId = account.Id; RegisteredDeviceKey = device.Id; RegisteredDeviceId = device.Id.ToString("D");
        PunchIdempotencyKey = ModelGuard.Required(punchIdempotencyKey, 100, nameof(punchIdempotencyKey));
        ChallengeSha256 = challengeSha256;
        Provider = ModelGuard.Required(provider, 100, nameof(provider));
        ProviderVersion = ModelGuard.Required(providerVersion, 100, nameof(providerVersion));
        CorrelationId = ModelGuard.Required(correlationId, 100, nameof(correlationId));
        IssuedAtUtc = ModelGuard.Utc(issuedAtUtc); ExpiresAtUtc = ModelGuard.Utc(expiresAtUtc);
        Outcome = FaceVerificationOutcome.Pending;
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid FaceEnrollmentId { get; private set; }
    public Guid UserAccountId { get; private set; }
    public Guid RegisteredDeviceKey { get; private set; }
    public RegisteredDevice Device { get; private set; } = null!;
    public FaceEnrollment Enrollment { get; private set; } = null!;
    public string RegisteredDeviceId { get; private set; } = string.Empty;
    public string PunchIdempotencyKey { get; private set; } = string.Empty;
    public string ChallengeSha256 { get; private set; } = string.Empty;
    public string Provider { get; private set; } = string.Empty;
    public string ProviderVersion { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public FaceVerificationOutcome Outcome { get; private set; }
    public bool? LivenessPassed { get; private set; }
    public bool? MatchPassed { get; private set; }
    public string? ResultReasonCode { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }
    public Guid? ConsumedByPunchId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Expire(DateTimeOffset atUtc)
    {
        if (Outcome != FaceVerificationOutcome.Pending || atUtc < ExpiresAtUtc)
            throw new InvalidOperationException("Only an expired pending challenge can be marked expired.");
        Outcome = FaceVerificationOutcome.Expired; LivenessPassed = false; MatchPassed = false;
        ResultReasonCode = "CHALLENGE_EXPIRED"; CompletedAtUtc = ModelGuard.Utc(atUtc);
    }
    public void RecordProviderResult(bool available, bool livenessPassed, bool matchPassed, string reasonCode, DateTimeOffset atUtc)
    {
        if (Outcome != FaceVerificationOutcome.Pending || atUtc < IssuedAtUtc || atUtc >= ExpiresAtUtc)
            throw new InvalidOperationException("The challenge is already completed or outside its validity window.");
        ResultReasonCode = ModelGuard.Code(reasonCode, 100);
        Outcome = !available ? FaceVerificationOutcome.Unavailable : livenessPassed && matchPassed ? FaceVerificationOutcome.Passed : FaceVerificationOutcome.Failed;
        LivenessPassed = available && livenessPassed; MatchPassed = available && matchPassed;
        CompletedAtUtc = ModelGuard.Utc(atUtc);
    }
    public void Consume(RawPunchEvent punch, FaceEnrollment enrollment, UserAccount account, RegisteredDevice device, DateTimeOffset atUtc)
    {
        if (Outcome != FaceVerificationOutcome.Passed || LivenessPassed != true || MatchPassed != true || !CompletedAtUtc.HasValue
            || ConsumedAtUtc.HasValue || atUtc < CompletedAtUtc || atUtc >= ExpiresAtUtc
            || account.Id != UserAccountId || account.EmployeeId != EmployeeId || account.Status != AccountStatus.Active
            || device.Id != RegisteredDeviceKey || device.UserAccountId != UserAccountId || device.RevokedAt.HasValue
            || !enrollment.IsActive || enrollment.Id != FaceEnrollmentId || enrollment.EmployeeId != EmployeeId
            || punch.Action != PunchAction.CheckIn || punch.EmployeeId != EmployeeId
            || punch.RegisteredDeviceId != RegisteredDeviceId || punch.IdempotencyKey != PunchIdempotencyKey
            || punch.ReceivedAtUtc < IssuedAtUtc || punch.ReceivedAtUtc >= ExpiresAtUtc || atUtc < punch.ReceivedAtUtc)
            throw new InvalidOperationException("Verification is not valid for this Check In.");
        ConsumedByPunchId = punch.Id; ConsumedAtUtc = ModelGuard.Utc(atUtc);
    }
}
