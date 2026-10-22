using Cosmix.Domain.Attendance.LocationConsent;

namespace Cosmix.Application.Attendance.PunchRetention;

public sealed record PunchRetentionRunResult(
    int RecordsRedacted,
    DateTimeOffset RunAt,
    string Message);

public interface IPunchRetentionRepository
{
    Task<IReadOnlyList<Guid>> GetUnredactedPunchIdsOlderThanAsync(DateTimeOffset thresholdUtc, int batchSize, CancellationToken ct);
    Task AddRedactionsAsync(IReadOnlyList<PunchLocationRedaction> redactions, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
