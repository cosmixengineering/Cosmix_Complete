namespace Cosmix.Domain.Common.Jobs;

public enum DispatchState { Pending = 0, Processing = 1, Completed = 2, Failed = 3 }

/// <summary>Transactional dispatch metadata. A worker/provider is deliberately not implemented.</summary>
public sealed class OutboxMessage
{
    private OutboxMessage() { }
    public OutboxMessage(string messageType, string encryptedPayloadReference, DateTimeOffset createdAt,
        string correlationId, string deduplicationKey)
    {
        Id = Guid.NewGuid(); MessageType = ModelGuard.Required(messageType, 100, nameof(messageType));
        EncryptedPayloadReference = ModelGuard.Required(encryptedPayloadReference, 500, nameof(encryptedPayloadReference));
        CreatedAt = ModelGuard.Utc(createdAt); AvailableAt = CreatedAt;
        CorrelationId = ModelGuard.Required(correlationId, 100, nameof(correlationId));
        DeduplicationKey = ModelGuard.Required(deduplicationKey, 150, nameof(deduplicationKey));
    }
    public Guid Id { get; private set; }
    public string MessageType { get; private set; } = string.Empty;
    public string EncryptedPayloadReference { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public string DeduplicationKey { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset AvailableAt { get; private set; }
    public DispatchState State { get; private set; }
    public int Attempts { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? LastFailureCode { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public Guid Claim(DateTimeOffset now, DateTimeOffset leaseExpiresAt)
    {
        if (State is DispatchState.Completed or DispatchState.Failed || now < AvailableAt || leaseExpiresAt <= now
            || (State == DispatchState.Processing && LeaseExpiresAt > now))
            throw new InvalidOperationException("Message is not available for dispatch.");
        LeaseToken = Guid.NewGuid(); LeaseExpiresAt = ModelGuard.Utc(leaseExpiresAt);
        State = DispatchState.Processing; Attempts = checked(Attempts + 1); return LeaseToken.Value;
    }
    public void Complete(Guid leaseToken, DateTimeOffset now)
    {
        EnsureLease(leaseToken, now); State = DispatchState.Completed; CompletedAt = ModelGuard.Utc(now);
        LeaseToken = null; LeaseExpiresAt = null; LastFailureCode = null;
    }
    public void RecordFailure(Guid leaseToken, DateTimeOffset now, string failureCode, DateTimeOffset? retryAt)
    {
        EnsureLease(leaseToken, now);
        if (retryAt.HasValue && retryAt < now) throw new ArgumentException("Retry cannot be in the past.");
        var code = ModelGuard.Required(failureCode, 100, nameof(failureCode));
        LastFailureCode = code; State = retryAt.HasValue ? DispatchState.Pending : DispatchState.Failed;
        AvailableAt = retryAt?.ToUniversalTime() ?? now.ToUniversalTime(); LeaseToken = null; LeaseExpiresAt = null;
    }
    private void EnsureLease(Guid token, DateTimeOffset now)
    {
        if (State != DispatchState.Processing || LeaseToken != token || token == Guid.Empty || !LeaseExpiresAt.HasValue || now >= LeaseExpiresAt || now < CreatedAt)
            throw new InvalidOperationException("Dispatch lease is invalid or expired.");
    }
}
