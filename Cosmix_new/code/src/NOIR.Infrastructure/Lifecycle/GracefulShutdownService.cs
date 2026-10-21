namespace NOIR.Infrastructure.Lifecycle;

using Microsoft.AspNetCore.SignalR;

/// <summary>
/// Hosted service that handles application shutdown gracefully.
/// Writes a shutdown marker file, notifies connected SignalR clients,
/// and waits for in-flight operations to complete.
/// </summary>
public sealed class GracefulShutdownService : IHostedService
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IHubContext<NotificationHub, INotificationClient> _hubContext;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<GracefulShutdownService> _logger;

    private const int DrainDelaySeconds = 5;

    public GracefulShutdownService(
        IHostApplicationLifetime lifetime,
        IHubContext<NotificationHub, INotificationClient> hubContext,
        IWebHostEnvironment env,
        ILogger<GracefulShutdownService> logger)
    {
        _lifetime = lifetime;
        _hubContext = hubContext;
        _env = env;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _lifetime.ApplicationStopping.Register(OnStopping);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void OnStopping()
    {
        try
        {
            _logger.LogInformation("Graceful shutdown initiated — writing marker and notifying clients");

            // 1. Write shutdown marker file
            var markerPath = GetMarkerPath();
            var marker = new ShutdownMarker
            {
                Timestamp = DateTimeOffset.UtcNow,
                Reason = "Server is restarting",
                IsClean = true
            };

            var json = JsonSerializer.Serialize(marker, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            File.WriteAllText(markerPath, json);

            _logger.LogInformation("Shutdown marker written to {MarkerPath}", markerPath);

            // 2. Notify all connected SignalR clients
            _hubContext.Clients.All
                .ReceiveServerShutdown("Server is restarting")
                .GetAwaiter()
                .GetResult();

            _logger.LogInformation("SignalR shutdown notification sent to all clients");

            // 3. Wait for in-flight requests to drain
            Thread.Sleep(TimeSpan.FromSeconds(DrainDelaySeconds));

            _logger.LogInformation("Graceful shutdown complete after {DrainDelaySeconds}s drain period", DrainDelaySeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during graceful shutdown");
        }
    }

    private string GetMarkerPath() =>
        Path.Combine(_env.ContentRootPath, ".shutdown-marker.json");
}
