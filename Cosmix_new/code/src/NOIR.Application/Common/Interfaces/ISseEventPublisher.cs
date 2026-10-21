namespace NOIR.Application.Common.Interfaces;

/// <summary>
/// Publishes SSE events to connected clients.
/// Provides one-way server-to-client push for job progress and operation status.
/// Channel IDs are tenant-scoped at the endpoint level (format: "{tenantId}:{channelId}").
/// When publishing from backend services, use <see cref="SseEventPublisherExtensions.PublishForTenantAsync"/>
/// to ensure correct tenant scoping.
/// </summary>
public interface ISseEventPublisher
{
    /// <summary>
    /// Publishes an event to a specific channel (raw channel ID, including tenant prefix).
    /// If no subscribers are listening on the channel, the event is silently dropped.
    /// </summary>
    Task PublishAsync(string channelId, SseEvent sseEvent, CancellationToken ct = default);

    /// <summary>
    /// Subscribes to a channel and yields events as they arrive.
    /// The channel is created on first subscribe and removed when the subscriber disconnects.
    /// </summary>
    IAsyncEnumerable<SseEvent> SubscribeAsync(string channelId, CancellationToken ct = default);
}

/// <summary>
/// Extension methods for tenant-scoped SSE publishing.
/// </summary>
public static class SseEventPublisherExtensions
{
    /// <summary>
    /// Publishes an event to a tenant-scoped channel.
    /// Automatically prefixes the channel ID with the tenant ID.
    /// </summary>
    public static Task PublishForTenantAsync(
        this ISseEventPublisher publisher,
        string tenantId,
        string channelId,
        SseEvent sseEvent,
        CancellationToken ct = default)
    {
        var scopedChannelId = $"{tenantId}:{channelId}";
        return publisher.PublishAsync(scopedChannelId, sseEvent, ct);
    }
}
