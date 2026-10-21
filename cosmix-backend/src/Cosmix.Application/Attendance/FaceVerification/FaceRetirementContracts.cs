using System.Text.Json.Serialization;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.Attendance.FaceVerification;

public enum FaceRetirementProof { Pending, Committed, Rejected }
public enum FaceRetirementState
{
    AwaitingTerminal, PendingPolicy, PendingHold, PendingBoundary, PendingDisposalAdapter,
    RetryRequired, ScopeRejected, Disposed
}

// Internal server evidence. These records are never HTTP request contracts or EF entities.
public sealed record FaceRetirementIntent(
    [property: JsonRequired] int Version,
    [property: JsonRequired] Guid EmployeeId,
    [property: JsonRequired] Guid SubjectId,
    [property: JsonRequired] PrivateFileReference Reference,
    [property: JsonRequired] string Purpose,
    [property: JsonRequired] FaceStorageRetentionReason Reason,
    [property: JsonRequired] string OperationKey,
    [property: JsonRequired] DateTimeOffset? RevokedAtUtc,
    [property: JsonRequired] Guid? ReplacementEnrollmentId,
    [property: JsonRequired] FaceCaptureEnvelope? Capture,
    [property: JsonRequired] string? TokenSha256);

public sealed record FaceRetirementOutcome(
    [property: JsonRequired] int Version,
    [property: JsonRequired] Guid IntentId,
    [property: JsonRequired] FaceRetirementState State,
    [property: JsonRequired] bool UseClosed,
    [property: JsonRequired] string Code,
    [property: JsonRequired] DateTimeOffset ObservedAtUtc);
public sealed record FaceRetirementCursor(DateTimeOffset OccurredAtUtc, Guid Id);
public sealed record FaceRetirementBatch(int Examined, int UseClosed, int Pending, int Failed,
    FaceRetirementCursor? NextCursor, int RecoveryPending, int RecoveryFailed, FaceRetirementCursor? NextRevocationCursor);
public sealed record FaceRetirementRecovery(int Recovered, int Pending, int Failed, FaceRetirementCursor? NextCursor);
public sealed record FaceRevocationSource(FaceEnrollment Enrollment, Guid? ReplacementEnrollmentId,
    AuditEvent? AuthorityAudit = null);

public interface IFaceRetirementRepository
{
    void RequireTransaction();
    void RequireOutsideTransaction();
    Task<AuditEvent?> FindIntentAsync(string targetType, Guid subjectId, CancellationToken ct);
    Task<AuditEvent?> LatestOutcomeAsync(Guid intentId, CancellationToken ct);
    Task<IReadOnlyList<AuditEvent>> IntentsAsync(FaceRetirementCursor? after, int size, CancellationToken ct);
    Task<IReadOnlyList<FaceRevocationSource>> UnstagedRevocationsAsync(FaceRetirementCursor? after, int size, CancellationToken ct);
    Task<FaceRetirementProof> ValidateCommittedAsync(FaceRetirementIntent intent, CancellationToken ct);
    void Add(AuditEvent audit);
    Task SaveChangesAsync(CancellationToken ct);
    Task<T> InAuditTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}

public interface IFaceEvidenceRetirement
{
    bool IsConfigured { get; }
    Task RetireAsync(PrivateFileReference reference, string purpose, FaceStorageRetentionReason reason,
        string operationKey, CancellationToken ct);
}

public sealed class UnavailableFaceEvidenceRetirement : IFaceEvidenceRetirement
{
    public bool IsConfigured => false;
    public Task RetireAsync(PrivateFileReference reference, string purpose, FaceStorageRetentionReason reason,
        string operationKey, CancellationToken ct) => throw FaceRetirementEvidence.Unavailable();
}

public sealed record FaceRetirementWorkerOptions
{
    public const string SectionName = "FaceRetirementWorker";
    public bool Enabled { get; init; }
    public int BatchSize { get; init; } = 100;
    public int PollSeconds { get; init; } = 30;
}

public sealed record FaceDisposalOptions
{
    public const string SectionName = "FaceDisposal";
    public bool Enabled { get; init; }
}

// An approved adapter must revalidate current holds and exact object identity atomically with
// disposal, and make the live-object/backup/key outcome durable under this operation identity.
public sealed record FaceDisposalAuthorization(Guid IntentId, FaceRetirementIntent Intent,
    DateTimeOffset NotBeforeUtc, string RetentionApproval, string HoldApproval,
    string BackupApproval, string KeyApproval);
public sealed record FaceDisposalDecision(FaceRetirementState State, FaceDisposalAuthorization? Authorization = null);
public interface IFaceDisposalPolicy
{
    Task<FaceDisposalDecision> EvaluateAsync(Guid intentId, FaceRetirementIntent intent,
        DateTimeOffset now, CancellationToken ct);
}
public interface IFacePhysicalDisposal
{
    bool IsConfigured { get; }
    Task DisposeAsync(FaceDisposalAuthorization authorization, CancellationToken ct);
}

public sealed class PendingFaceDisposalPolicy : IFaceDisposalPolicy
{
    public Task<FaceDisposalDecision> EvaluateAsync(Guid intentId, FaceRetirementIntent intent,
        DateTimeOffset now, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult(new FaceDisposalDecision(FaceRetirementState.PendingPolicy)); }
}
public sealed class UnavailableFacePhysicalDisposal : IFacePhysicalDisposal
{
    public bool IsConfigured => false;
    public Task DisposeAsync(FaceDisposalAuthorization authorization, CancellationToken ct) =>
        throw new InvalidOperationException("Approved face disposal adapter is unavailable.");
}
