using System.Net;
using Cosmix.Application.Authentication;
using Cosmix.Infrastructure.FaceVerification;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Cosmix.Tests;

public sealed class FaceTransportDiagnosticsTests
{
    private static readonly byte[] Image = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAFElEQVR4nGNkr/7FgA0wYRUdtBIALAwBjB5fnwEAAAAASUVORK5CYII=");
    private static readonly CompreFaceOptions Options = new()
    {
        BaseUri = new("https://192.0.2.1/"), DetectionApiKey = "synthetic-detection", VerificationApiKey = "synthetic-verification",
        DeploymentVersion = "synthetic-transport", MinimumDetectionProbability = .9, TimeoutSeconds = 1
    };

    [Fact]
    public async Task Provider_timeout_has_one_submission_and_a_redacted_stage_diagnostic()
    {
        var transport = new Transport(async ct => { await Task.Delay(Timeout.Infinite, ct); return new(HttpStatusCode.OK); });
        var log = new RecordingLogger();
        using var client = Client(transport, log);
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => client.DetectAsync(Image));
        Assert.Equal("face_provider_timeout", error.Code);
        Assert.Equal(1, transport.Calls);
        var message = Assert.Single(log.Messages);
        Assert.Contains("detection", message);
        Assert.Contains("timeout", message);
        Assert.DoesNotContain(Options.DetectionApiKey, message);
        Assert.DoesNotContain("10.20.30.40", message);
        Assert.DoesNotContain(Convert.ToBase64String(Image), message);
    }

    [Fact]
    public async Task Caller_cancellation_stays_cancellation_and_is_not_reported_as_provider_timeout()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new Transport(async ct => { started.SetResult(); await Task.Delay(Timeout.Infinite, ct); return new(HttpStatusCode.OK); });
        var log = new RecordingLogger();
        using var client = Client(transport, log);
        using var cancelled = new CancellationTokenSource();
        var request = client.DetectAsync(Image, cancelled.Token);
        await started.Task;
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.Contains("cancelled", Assert.Single(log.Messages));
        Assert.Equal(1, transport.Calls);
    }

    private static CompreFaceClient Client(Transport transport, RecordingLogger log) =>
        new(Options with { BaseUri = new("https://10.20.30.40/") }, new HttpClient(transport), log);
    private sealed class Transport(Func<CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Interlocked.Increment(ref Calls); return send(cancellationToken); }
    }
    private sealed class RecordingLogger : ILogger<CompreFaceClient>
    {
        public readonly List<string> Messages = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
