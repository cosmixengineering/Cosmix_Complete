namespace Cosmix.Application.Common.BackgroundJobs;

public sealed record WorkerHealthStatus(string Worker, string Status, DateTimeOffset? LastCompletedAt);

/// <summary>Process-local execution signals, never a substitute for durable job or provider delivery evidence.</summary>
public sealed class WorkerHealth(TimeProvider clock)
{
    private readonly object gate = new();
    private readonly Dictionary<string, State> workers = new(StringComparer.Ordinal);
    private sealed record State(TimeSpan MaximumAge, DateTimeOffset? CompletedAt, bool Failed);

    public void Register(string worker, TimeSpan maximumAge)
    {
        if (worker.Length is < 1 or > 40 || worker.Any(c => !char.IsAsciiLetterLower(c) && c != '-') ||
            maximumAge <= TimeSpan.Zero || maximumAge > TimeSpan.FromDays(32))
            throw new ArgumentException("Invalid worker health registration.");
        lock (gate)
        {
            if (workers.TryGetValue(worker, out var existing) && existing.MaximumAge != maximumAge)
                throw new InvalidOperationException("Worker health registration changed.");
            workers.TryAdd(worker, new(maximumAge, null, false));
        }
    }

    public void Complete(string worker) => Update(worker, false);
    public void Fail(string worker) => Update(worker, true);
    private void Update(string worker, bool failed)
    {
        lock (gate)
        {
            if (!workers.TryGetValue(worker, out var state)) throw new InvalidOperationException("Worker is not registered.");
            workers[worker] = state with { Failed = failed, CompletedAt = failed ? state.CompletedAt : clock.GetUtcNow() };
        }
    }

    public IReadOnlyList<WorkerHealthStatus> Snapshot()
    {
        lock (gate)
        {
            var now = clock.GetUtcNow();
            return workers.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new WorkerHealthStatus(x.Key,
                x.Value.Failed ? "failed" : x.Value.CompletedAt is not { } completed ? "pending" :
                now < completed || now - completed > x.Value.MaximumAge ? "stale" : "healthy", x.Value.CompletedAt)).ToArray();
        }
    }
}
