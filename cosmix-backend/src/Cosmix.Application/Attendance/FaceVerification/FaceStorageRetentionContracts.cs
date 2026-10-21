using Cosmix.Application.Common.Files;
using System.Text.Json.Serialization;

namespace Cosmix.Application.Attendance.FaceVerification;

/// <summary>Server-provisioned authority to limit biometric use, never authority to destroy evidence.
/// Lifetimes have no defaults and must be taken from the cited approval, not capture-token expiry.</summary>
public sealed record FaceStorageRetentionOptions
{
    public const string SectionName = "FaceStorageRetention";
    public bool Enabled { get; init; }
    public bool AuthorityConfirmed { get; init; }
    public string ApprovalReference { get; init; } = "";
    public int? CaptureUseLifetimeSeconds { get; init; }
    public int? EnrollmentUseLifetimeSeconds { get; init; }
}

public enum FaceStorageRetentionEventKind { Stored, UseReleasePrepared, Retired }
public enum FaceStorageRetentionReason { EnrollmentRevoked, EnrollmentReplaced, CaptureCompleted }

/// <summary>Internal encrypted object audit. No image, token, challenge, key material or actor authority.
/// Existing attendance audit remains responsible for the authorized actor and business transaction.</summary>
public sealed record FaceStorageRetentionEvent(
    [property: JsonRequired] int Sequence,
    [property: JsonRequired] PrivateFileReference Reference,
    [property: JsonRequired] string Purpose,
    [property: JsonRequired] FaceStorageRetentionEventKind Kind,
    [property: JsonRequired] DateTimeOffset OccurredAtUtc,
    [property: JsonRequired] DateTimeOffset UseUntilUtc,
    [property: JsonRequired] string ApprovalReference,
    [property: JsonRequired] FaceStorageRetentionReason? RetirementReason = null,
    [property: JsonRequired] string? OperationKey = null);

/// <summary>All operations for an object, including byte release, use one cross-process lock.
/// Append must persist immutable encrypted metadata before returning. Missing, partial, malformed,
/// substituted or non-contiguous records fail closed; no method removes ciphertext or records.</summary>
public interface IFaceStorageRetentionJournal
{
    bool IsConfigured { get; }
    Task<T> WithObjectLockAsync<T>(PrivateFileReference reference,
        Func<CancellationToken, Task<T>> action, CancellationToken ct);
    Task<IReadOnlyList<FaceStorageRetentionEvent>> ReadAsync(PrivateFileReference reference, CancellationToken ct);
    Task AppendAsync(FaceStorageRetentionEvent value, CancellationToken ct);
}
