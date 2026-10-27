namespace Cosmix.Application.Attendance.PunchRetention;

public sealed class PunchRetentionService(
    IPunchRetentionRepository repository,
    PunchRetentionOptions options,
    TimeProvider clock)
{
    public async Task<PunchRetentionRunResult> RunRetentionAsync(CancellationToken ct)
    {
        if (!options.RetentionDays.HasValue || options.RetentionDays.Value <= 0 || !options.IsPolicyApproved)
        {
            throw new InvalidOperationException("PunchRetention:RetentionDays must be configured and explicitly approved by company management (PunchRetention:IsPolicyApproved = true). Automatic destructive retention cleanup halted.");
        }
        if (options.BatchSize is < 1 or > 1000 || options.MaxBatchesPerRun is < 1 or > 1000)
            throw new InvalidOperationException("PunchRetention batch configuration is invalid.");

        var retentionDays = options.RetentionDays.Value;

        var now = clock.GetUtcNow();
        var threshold = now.AddDays(-retentionDays);

        int totalRedacted = 0;
        for (var batch = 0; batch < options.MaxBatchesPerRun; batch++)
        {
            ct.ThrowIfCancellationRequested();
            var redacted = await repository.RedactBatchAsync(threshold, now, retentionDays, options.BatchSize, ct);
            if (redacted is < 0 || redacted > options.BatchSize)
                throw new InvalidOperationException("PunchRetention repository returned an invalid batch count.");
            totalRedacted += redacted;
            if (redacted < options.BatchSize) break;
        }

        // Legacy attendance.punch.submit protected audit payloads may contain a second GPS copy.
        // Continue the bounded audit cleanup even when no SQL coordinate rows remain.
        var auditsProcessed = 0;
        for (var batch = 0; batch < options.MaxBatchesPerRun; batch++)
        {
            ct.ThrowIfCancellationRequested();
            var processed = await repository.ExpireLegacyAuditBatchAsync(threshold, now, options.BatchSize, ct);
            if (processed is < 0 || processed > options.BatchSize)
                throw new InvalidOperationException("PunchRetention repository returned an invalid audit batch count.");
            auditsProcessed += processed;
            if (processed < options.BatchSize) break;
        }

        return new PunchRetentionRunResult(totalRedacted, auditsProcessed, now,
            $"Redacted {totalRedacted} GPS punch coordinates and processed {auditsProcessed} legacy punch audit payloads older than {retentionDays} days.");
    }
}
