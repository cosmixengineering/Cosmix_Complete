namespace NOIR.Infrastructure.Lifecycle;

/// <summary>
/// Represents a shutdown marker file written during graceful shutdown.
/// Used by <see cref="DeployRecoveryService"/> to detect clean vs crash restarts.
/// </summary>
public sealed record ShutdownMarker
{
    public DateTimeOffset Timestamp { get; init; }
    public string Reason { get; init; } = string.Empty;
    public bool IsClean { get; init; }
}
