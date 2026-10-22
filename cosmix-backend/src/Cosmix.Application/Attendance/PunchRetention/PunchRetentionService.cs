using Cosmix.Domain.Attendance.LocationConsent;

namespace Cosmix.Application.Attendance.PunchRetention;

public sealed class PunchRetentionService(
    IPunchRetentionRepository repository,
    PunchRetentionOptions options,
    TimeProvider clock)
{
    public async Task<PunchRetentionRunResult> RunRetentionAsync(CancellationToken ct)
    {
        if (!options.RetentionDays.HasValue || options.RetentionDays.Value <= 0)
        {
            throw new InvalidOperationException("PunchRetention:RetentionDays is not configured or invalid. Automatic retention cleanup halted.");
        }

        var retentionDays = options.RetentionDays.Value;

        var now = clock.GetUtcNow();
        var threshold = now.AddDays(-retentionDays);

        int totalRedacted = 0;
        const int batchSize = 500;

        while (!ct.IsCancellationRequested)
        {
            var punchIds = await repository.GetUnredactedPunchIdsOlderThanAsync(threshold, batchSize, ct);
            if (punchIds.Count == 0) break;

            var redactions = punchIds.Select(id => new PunchLocationRedaction(
                id,
                now,
                "system",
                retentionDays,
                $"Automated retention cleanup older than {retentionDays} days"
            )).ToList();

            await repository.AddRedactionsAsync(redactions, ct);
            await repository.SaveChangesAsync(ct);

            totalRedacted += redactions.Count;
            if (punchIds.Count < batchSize) break;
        }

        return new PunchRetentionRunResult(totalRedacted, now, $"Redacted {totalRedacted} GPS punch coordinates older than {retentionDays} days.");
    }
}
