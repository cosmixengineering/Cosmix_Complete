using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.Common.BackgroundJobs;

public sealed class DurableJobProcessor(IDurableJobRepository jobs, IEnumerable<IDurableJobHandler> handlers,
    AttendanceJobOptions options, TimeProvider clock)
{
    public async Task<bool> DispatchOneAsync(CancellationToken ct)
    {
        if (!options.IsValid()) throw new InvalidOperationException("Invalid AttendanceJobs settings.");
        if (!options.Enabled) return false;
        var registered = handlers.Select(handler => new { Handler = handler, handler.MessageType }).ToArray();
        if (registered.GroupBy(x => x.MessageType, StringComparer.Ordinal).Any(x => x.Count() != 1))
            throw new InvalidOperationException("Durable job message handlers must be unique.");
        var ownedTypes = DurableJobMessageTypes.RequireAllowed(registered.Select(x => x.MessageType).ToArray());
        var claim = await jobs.ClaimNextAsync(ownedTypes, clock.GetUtcNow(), TimeSpan.FromSeconds(options.LeaseSeconds), ct);
        if (claim is null) return false;
        try
        {
            // Retain the claim binding independently of handler-owned preparation state.
            var preparation = claim.Preparation is { } metadata ? metadata with { } : null;
            IDurableJobHandler? selected = null;
            if (preparation is not null)
            {
                selected = registered.SingleOrDefault(x => string.Equals(x.MessageType, preparation.MessageType, StringComparison.Ordinal))?.Handler
                    ?? throw new DurableJobFailure("unsupported_message_type", false);
                if (!string.Equals(selected.MessageType, preparation.MessageType, StringComparison.Ordinal))
                    throw new DurableJobFailure("dispatch_preparation_mismatch", false);
                if (selected is IPreparedDurableJobHandler prepared)
                {
                    if (claim.Attempt > options.MaximumAttempts) throw new DurableJobFailure("dispatch_attempts_exhausted", false);
                    if (claim.LeaseExpiresAt <= clock.GetUtcNow()) return false;
                    ct.ThrowIfCancellationRequested();
                    await prepared.PrepareAsync(claim, ct);
                    ct.ThrowIfCancellationRequested();
                }
            }
            return await jobs.CompleteAsync(claim, async (message, token) =>
            {
                if (claim.Attempt > options.MaximumAttempts) throw new DurableJobFailure("dispatch_attempts_exhausted", false);
                var handler = registered.SingleOrDefault(x => string.Equals(x.MessageType, message.MessageType, StringComparison.Ordinal))?.Handler;
                if (preparation is not null && (claim.Preparation != preparation || message.Id != claim.MessageId || message.Attempts != claim.Attempt ||
                    !string.Equals(message.MessageType, preparation.MessageType, StringComparison.Ordinal) ||
                    !string.Equals(message.EncryptedPayloadReference, preparation.EncryptedPayloadReference, StringComparison.Ordinal) ||
                    !string.Equals(message.CorrelationId, preparation.CorrelationId, StringComparison.Ordinal) ||
                    !string.Equals(message.DeduplicationKey, preparation.DeduplicationKey, StringComparison.Ordinal) ||
                    !ReferenceEquals(handler, selected) || !string.Equals(selected!.MessageType, preparation.MessageType, StringComparison.Ordinal)))
                    throw new DurableJobFailure("dispatch_preparation_mismatch", false);
                if (handler is null) throw new DurableJobFailure("unsupported_message_type", false);
                if (handler is IPreparedDurableJobHandler && preparation is null)
                    throw new DurableJobFailure("dispatch_preparation_missing", false);
                await handler.ApplyAsync(message, token);
                jobs.AddAudit(new AuditEvent("outbox.completed", "attendance-job-worker", "system", "OutboxMessage",
                    message.Id.ToString(), clock.GetUtcNow(), message.CorrelationId));
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            var (code, retryable) = error switch
            {
                DurableJobFailure failure => (failure.Code, failure.Retryable),
                AuthenticationFailure failure when failure.StatusCode == 503 => ("private_storage_unavailable", true),
                _ => ("dispatch_unavailable", true)
            };
            var retryAt = retryable && claim.Attempt < options.MaximumAttempts
                ? clock.GetUtcNow().AddSeconds(Math.Min(options.RetryMaximumSeconds,
                    options.RetryBaseSeconds * Math.Pow(2, claim.Attempt - 1))) : (DateTimeOffset?)null;
            await jobs.FailAsync(claim, code, retryAt, ct);
            return true;
        }
    }
}
