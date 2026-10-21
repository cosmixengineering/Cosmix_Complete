namespace NOIR.Infrastructure.Lifecycle;

using Microsoft.AspNetCore.SignalR;

/// <summary>
/// Background service that checks for shutdown markers on startup.
/// If a clean marker exists, removes it and notifies clients of recovery.
/// If no marker exists after a previous run, this was a first start or marker already cleaned.
/// </summary>
public sealed class DeployRecoveryService : BackgroundService
{
    private readonly IHubContext<NotificationHub, INotificationClient> _hubContext;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DeployRecoveryService> _logger;

    /// <summary>
    /// Delay before sending recovery notification to give clients time to connect.
    /// </summary>
    private const int ClientConnectDelaySeconds = 5;

    public DeployRecoveryService(
        IHubContext<NotificationHub, INotificationClient> hubContext,
        IWebHostEnvironment env,
        ILogger<DeployRecoveryService> logger)
    {
        _hubContext = hubContext;
        _env = env;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var markerPath = GetMarkerPath();

        if (!File.Exists(markerPath))
        {
            _logger.LogInformation("No shutdown marker found — first start or marker already cleaned");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(markerPath, stoppingToken);
            var marker = JsonSerializer.Deserialize<ShutdownMarker>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (marker is null)
            {
                _logger.LogWarning("Shutdown marker file was empty or invalid — deleting");
                DeleteMarker(markerPath);
                return;
            }

            if (marker.IsClean)
            {
                _logger.LogInformation(
                    "Clean restart detected. Previous shutdown at {Timestamp} — reason: {Reason}",
                    marker.Timestamp,
                    marker.Reason);
            }
            else
            {
                _logger.LogWarning(
                    "Crash recovery detected. Previous unclean shutdown at {Timestamp} — reason: {Reason}",
                    marker.Timestamp,
                    marker.Reason);
            }

            // Delete marker after reading
            DeleteMarker(markerPath);

            // Wait for clients to connect before sending recovery notification
            await Task.Delay(TimeSpan.FromSeconds(ClientConnectDelaySeconds), stoppingToken);

            // Notify all connected clients of recovery
            await _hubContext.Clients.All.ReceiveServerRecovery();
            _logger.LogInformation("Server recovery notification sent to all clients");
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during deploy recovery check");
        }
    }

    private void DeleteMarker(string markerPath)
    {
        try
        {
            File.Delete(markerPath);
            _logger.LogDebug("Shutdown marker deleted: {MarkerPath}", markerPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete shutdown marker: {MarkerPath}", markerPath);
        }
    }

    private string GetMarkerPath() =>
        Path.Combine(_env.ContentRootPath, ".shutdown-marker.json");
}
