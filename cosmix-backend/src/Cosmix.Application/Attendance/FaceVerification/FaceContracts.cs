using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Authentication;

namespace Cosmix.Application.Attendance.FaceVerification;

/// <summary>Management-recorded settings. There are intentionally no default approval or biometric thresholds.</summary>
public sealed class FaceVerificationPolicy
{
    public const string SectionName = "FaceVerificationPolicy";
    public bool AuthorityConfirmed { get; set; }
    public string? ApprovalReference { get; set; }
    public int? ChallengeLifetimeSeconds { get; set; }
    public int? MaximumAttemptsPerPunch { get; set; }
    public string? RetentionPolicyReference { get; set; }
    public bool IsConfirmed => AuthorityConfirmed && !string.IsNullOrWhiteSpace(ApprovalReference) &&
        !string.IsNullOrWhiteSpace(RetentionPolicyReference) && ChallengeLifetimeSeconds is > 0 and <= 3600 &&
        MaximumAttemptsPerPunch is > 0 and <= 100;
}

public sealed record FaceProviderCapability(bool Available, string Provider, string Version, string ReasonCode);
public sealed record FaceProviderEnrollmentInput(Guid EmployeeId, Guid DeviceId, string EvidenceToken, string CorrelationId,
    string IdempotencyKey, Guid? ActorAccountId = null, Guid? ActorSessionId = null);
public sealed record FaceProviderEnrollmentResult(bool Success, string Provider, string Version, Guid EmployeeId,
    Guid DeviceId, byte[]? Template, string ReasonCode);
public sealed record FaceProviderVerificationInput(Guid AttemptId, Guid EmployeeId, Guid DeviceId,
    string Challenge, string EvidenceToken, byte[] Template, string CorrelationId,
    Guid? ActorAccountId = null, Guid? ActorSessionId = null);
public sealed record FaceProviderVerificationResult(bool Available, bool LivenessPassed, bool MatchPassed,
    string Provider, string Version, Guid AttemptId, Guid EmployeeId, Guid DeviceId, string ChallengeSha256,
    string ReasonCode);

/// <summary>Only an approved, cryptographically validating adapter may return verified results.
/// No client boolean or ordinary image comparison constitutes a live-person verification.</summary>
public interface IFaceVerificationProvider
{
    FaceProviderCapability Capability { get; }
    Task<FaceProviderEnrollmentResult> EnrollAsync(FaceProviderEnrollmentInput input, CancellationToken ct);
    Task<FaceProviderVerificationResult> VerifyAsync(FaceProviderVerificationInput input, CancellationToken ct);
}

/// <summary>Biometric templates use separate private storage and keys, never the ordinary document/audit store.</summary>
public interface IFaceTemplateStore
{
    bool IsConfigured { get; }
    Task<PrivateFileReference> WriteAsync(byte[] template, string purpose, CancellationToken ct);
    Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FaceEnrollmentRequest(Guid RegisteredDeviceId,
    [Required, StringLength(4096)] string ProviderEvidenceToken,
    Guid? ExpectedActiveEnrollmentId, [StringLength(12, MinimumLength = 12)] string? ExpectedActiveVersion,
    [Required, StringLength(1000)] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RevokeFaceEnrollmentRequest([Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    [Required, StringLength(1000)] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FaceChallengeRequest([Required, RegularExpression("^[A-Za-z0-9_.:-]{8,100}$")] string PunchIdempotencyKey);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VerifyFaceRequest([Required, StringLength(128)] string Challenge,
    [Required, StringLength(4096)] string ProviderEvidenceToken,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion);
public sealed record FaceEnrollmentResponse(Guid Id, Guid EmployeeId, string Provider, string ProviderVersion,
    DateTimeOffset EnrolledAtUtc, bool IsActive, DateTimeOffset? RevokedAtUtc, Guid? ReplacesEnrollmentId, string Version);
public sealed record FaceChallengeResponse(Guid Id, string Challenge, DateTimeOffset ExpiresAtUtc, string Version);
public sealed record FaceVerificationResponse(Guid Id, string Outcome, string ReasonCode, DateTimeOffset ExpiresAtUtc,
    bool CanSubmitCheckIn, string Version);
public sealed record FaceAttemptResponse(Guid Id, Guid EnrollmentId, string Outcome, DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc, DateTimeOffset? CompletedAtUtc, DateTimeOffset? ConsumedAtUtc,
    string Provider, string ProviderVersion, string? ReasonCode);
public sealed record FaceCapabilityResponse(bool Available, string ReasonCode, bool EnrollmentActive,
    bool CheckOutRequiresFace = false, bool EnrollmentCompatible = false, bool CheckInFaceReady = false,
    string AvailabilityKind = "configuration_only");
public sealed record FaceEmployeeDevice(UserAccount Account, RegisteredDevice Device);

public interface IFaceRepository
{
    Task<FaceEmployeeDevice> EmployeeDeviceAsync(Guid employeeId, Guid deviceId, CancellationToken ct);
    Task<IReadOnlyList<AccessResource>> ResourcesAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<FaceEnrollment?> ActiveAsync(Guid employeeId, CancellationToken ct);
    Task<FaceEnrollment?> EnrollmentAsync(Guid employeeId, Guid id, CancellationToken ct);
    Task<FaceVerificationAttempt?> AttemptAsync(Guid employeeId, Guid id, CancellationToken ct);
    Task<int> AttemptCountAsync(Guid employeeId, string punchKey, CancellationToken ct);
    Task<PagedResult<FaceEnrollmentResponse>> EnrollmentsAsync(Guid employeeId, int page, int pageSize, CancellationToken ct);
    Task<PagedResult<FaceAttemptResponse>> AttemptsAsync(Guid employeeId, int page, int pageSize, CancellationToken ct);
    void Add(FaceEnrollment enrollment);
    void Add(FaceVerificationAttempt attempt);
    Task SaveChangesAsync(CancellationToken ct);
}
