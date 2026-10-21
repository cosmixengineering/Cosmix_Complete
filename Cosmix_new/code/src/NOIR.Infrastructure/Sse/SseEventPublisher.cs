namespace NOIR.Infrastructure.Sse;

/// <summary>
/// In-memory SSE event publisher using System.Threading.Channels.
/// Each channel (identified by channelId) has one bounded Channel with capacity 100.
/// Registered as a Singleton so all scoped endpoints share the same instance.
/// </summary>
public sealed class SseEventPublisher : ISseEventPublisher, ISingletonService
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);
    private const int ChannelCapacity = 100;

    private readonly ConcurrentDictionary<string, Channel<SseEvent>> _channels = new();
    private readonly ILogger<SseEventPublisher> _logger;

    public SseEventPublisher(ILogger<SseEventPublisher> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task PublishAsync(string channelId, SseEvent sseEvent, CancellationToken ct = default)
    {
        if (_channels.TryGetValue(channelId, out var channel))
        {
            if (!channel.Writer.TryWrite(sseEvent))
            {
                _logger.LogWarning("SSE channel {ChannelId} is full, dropping event {EventType}", channelId, sseEvent.EventType);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SseEvent> SubscribeAsync(
        string channelId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = _channels.GetOrAdd(channelId, _ =>
            Channel.CreateBounded<SseEvent>(new BoundedChannelOptions(ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            }));

        _logger.LogDebug("SSE client subscribed to channel {ChannelId}", channelId);

        // Heartbeat task keeps the connection alive by writing periodic events
        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _ = Task.Run(async () =>
        {
            try
            {
                while (!heartbeatCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(HeartbeatInterval, heartbeatCts.Token);
                    channel.Writer.TryWrite(new SseEvent { EventType = "heartbeat", Data = "" });
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when client disconnects
            }
        }, heartbeatCts.Token);

        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(ct))
            {
                yield return evt;
            }
        }
        finally
        {
            // Cancel heartbeat and remove the channel on disconnect
            await heartbeatCts.CancelAsync();
            _channels.TryRemove(channelId, out _);
            _logger.LogDebug("SSE client disconnected from channel {ChannelId}", channelId);
        }
    }
}
