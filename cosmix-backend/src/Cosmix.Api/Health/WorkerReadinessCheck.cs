using Cosmix.Application.Common.BackgroundJobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cosmix.Api.Health;

public sealed class WorkerReadinessCheck(WorkerHealth health) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var workers = health.Snapshot();
        var ready = workers.Any(x => x.Worker == "gps-retention") && workers.Any(x => x.Worker == "employee-lifecycle") &&
            workers.All(x => x.Status == "healthy");
        return Task.FromResult(new HealthCheckResult(ready ? HealthStatus.Healthy : HealthStatus.Unhealthy,
            "Background worker execution freshness only.", data: new Dictionary<string, object> { ["workers"] = workers }));
    }
}
