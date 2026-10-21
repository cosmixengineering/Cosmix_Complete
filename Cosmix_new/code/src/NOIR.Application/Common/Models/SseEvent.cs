namespace NOIR.Application.Common.Models;

/// <summary>
/// Represents a Server-Sent Event to push to connected clients.
/// </summary>
public sealed record SseEvent
{
    /// <summary>
    /// The event type (maps to the SSE "event:" field).
    /// Clients use this to dispatch to specific event handlers.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// The JSON-serialized event payload (maps to the SSE "data:" field).
    /// </summary>
    public required string Data { get; init; }

    /// <summary>
    /// Optional event ID (maps to the SSE "id:" field).
    /// Clients can use this for last-event-id reconnection.
    /// </summary>
    public string? Id { get; init; }
}
