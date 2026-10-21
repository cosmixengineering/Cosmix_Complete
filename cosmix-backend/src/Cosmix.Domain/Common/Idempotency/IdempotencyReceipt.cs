namespace Cosmix.Domain.Common.Idempotency;

public sealed class IdempotencyReceipt
{
    private IdempotencyReceipt() { }
    public IdempotencyReceipt(string actorUserId, string operation, string key, string requestSha256,
        DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        if (expiresAt <= createdAt) throw new ArgumentException("Idempotency expiry must follow creation.");
        var fingerprint = ModelGuard.Required(requestSha256, 64, nameof(requestSha256)).ToUpperInvariant();
        if (fingerprint.Length != 64 || !fingerprint.All(Uri.IsHexDigit)) throw new ArgumentException("A SHA-256 request digest is required.");
        Id = Guid.NewGuid(); ActorUserId = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        Operation = ModelGuard.Required(operation, 100, nameof(operation)); Key = ModelGuard.Required(key, 100, nameof(key));
        RequestSha256 = fingerprint; CreatedAt = ModelGuard.Utc(createdAt); ExpiresAt = ModelGuard.Utc(expiresAt);
    }
    public Guid Id { get; private set; }
    public string ActorUserId { get; private set; } = string.Empty;
    public string Operation { get; private set; } = string.Empty;
    public string Key { get; private set; } = string.Empty;
    public string RequestSha256 { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? ResultCode { get; private set; }
    public string? ProtectedResultReference { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Complete(string resultCode, string protectedResultReference, DateTimeOffset at)
    {
        if (CompletedAt.HasValue || at < CreatedAt || at >= ExpiresAt) throw new InvalidOperationException("Receipt is complete or outside its valid interval.");
        var code = ModelGuard.Required(resultCode, 100, nameof(resultCode));
        var reference = ModelGuard.Required(protectedResultReference, 500, nameof(protectedResultReference));
        ResultCode = code; ProtectedResultReference = reference; CompletedAt = ModelGuard.Utc(at);
    }
}
