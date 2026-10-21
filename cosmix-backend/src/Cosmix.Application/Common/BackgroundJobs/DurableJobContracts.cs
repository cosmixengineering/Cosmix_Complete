using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Jobs;

namespace Cosmix.Application.Common.BackgroundJobs;

public sealed record DurableJobClaim(Guid MessageId, Guid LeaseToken, DateTimeOffset LeaseExpiresAt, int Attempt)
{
    public DurableJobPreparation? Preparation { get; init; }
}

public sealed record DurableJobPreparation(string MessageType, string EncryptedPayloadReference,
    string CorrelationId, string? DeduplicationKey);

public interface IDurableJobHandler
{
    string MessageType { get; }
    /// <summary>
    /// Called inside the outbox completion transaction. Stage only in the same scoped unit of work;
    /// never commit, start a nested transaction or call external messaging. Completion and changes save atomically.
    /// Throw DurableJobFailure for an expected permanent/retryable failure. Never report unsupported work as success.
    /// </summary>
    Task ApplyAsync(OutboxMessage message, CancellationToken ct);
}

public interface IPreparedDurableJobHandler : IDurableJobHandler
{
    /// <summary>
    /// Optional preparation outside the completion transaction, bound to the claimed message metadata.
    /// Read-only snapshots and private IO belong to the handler; never change business rows here.
    /// ApplyAsync must reload authoritative current state and permissions inside completion before staging changes.
    /// Preparation may repeat after cancellation, failure or lease expiry and must not imply completion.
    /// </summary>
    Task PrepareAsync(DurableJobClaim claim, CancellationToken ct);
}

public sealed class DurableJobFailure(string code, bool retryable) : Exception(code)
{
    public string Code { get; } = code;
    public bool Retryable { get; } = retryable;
}

public interface IDurableJobRepository
{
    void RequireProducerTransaction();
    Task<OutboxMessage?> FindByIdentityAsync(string messageType, string deduplicationKey, CancellationToken ct);
    Task<bool> RecipientExistsAsync(Guid accountId, CancellationToken ct);
    /// <summary>Requires an active producer transaction; does not save or commit.</summary>
    void Stage(OutboxMessage message);
    /// <summary>Claims only an exact registered handler type; unrelated pending/expired jobs remain untouched.</summary>
    Task<DurableJobClaim?> ClaimNextAsync(IReadOnlyCollection<string> allowedMessageTypes, DateTimeOffset now, TimeSpan lease, CancellationToken ct);
    /// <summary>Reloads/locks the message and checks the lease before and after the action; saves all changes atomically.</summary>
    Task<bool> CompleteAsync(DurableJobClaim claim, Func<OutboxMessage, CancellationToken, Task> action, CancellationToken ct);
    Task<bool> FailAsync(DurableJobClaim claim, string code, DateTimeOffset? retryAt, CancellationToken ct);
    void AddAudit(AuditEvent audit);
}

public static class DurableJobMessageTypes
{
    public const int MaximumTypes = 32;

    /// <summary>Copies and bounds the explicit worker ownership list. There is no wildcard or prefix ownership.</summary>
    public static string[] RequireAllowed(IReadOnlyCollection<string>? types)
    {
        if (types is null || types.Count is < 1 or > MaximumTypes || types.Any(x => x is null || x.Length is < 1 or > 100 ||
                x.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-' or '_'))) ||
            types.Distinct(StringComparer.Ordinal).Count() != types.Count)
            throw new InvalidOperationException("Require one to 32 unique lower-case durable job type identifiers, at most 100 characters each.");
        return types.Order(StringComparer.Ordinal).ToArray();
    }
}

/// <summary>Operational retry settings, not attendance or pay policy. Explicit opt-in prevents unexpected startup writes.</summary>
public sealed class AttendanceJobOptions
{
    public bool Enabled { get; set; }
    public int PollSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 10;
    public int LeaseSeconds { get; set; } = 120;
    public int MaximumAttempts { get; set; } = 5;
    public int RetryBaseSeconds { get; set; } = 5;
    public int RetryMaximumSeconds { get; set; } = 300;
    public bool IsValid() => PollSeconds is >= 1 and <= 300 && BatchSize is >= 1 and <= 100 &&
        LeaseSeconds is >= 30 and <= 900 && MaximumAttempts is >= 1 and <= 20 &&
        RetryBaseSeconds is >= 1 and <= 300 && RetryMaximumSeconds >= RetryBaseSeconds && RetryMaximumSeconds <= 3600;
}
