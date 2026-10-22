using Cosmix.Application.Attendance.PunchRetention;
using Cosmix.Domain.Attendance.LocationConsent;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class PunchRetentionRepository(CosmixDbContext db) : IPunchRetentionRepository
{
    public async Task<IReadOnlyList<Guid>> GetUnredactedPunchIdsOlderThanAsync(DateTimeOffset thresholdUtc, int batchSize, CancellationToken ct)
    {
        var redactedIds = db.PunchLocationRedactions.Select(r => r.PunchEventId);

        return await db.RawPunchEvents
            .AsNoTracking()
            .Where(p => p.ReceivedAtUtc < thresholdUtc &&
                        (p.Latitude != null || p.Longitude != null) &&
                        !redactedIds.Contains(p.Id))
            .OrderBy(p => p.ReceivedAtUtc)
            .Select(p => p.Id)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task AddRedactionsAsync(IReadOnlyList<PunchLocationRedaction> redactions, CancellationToken ct) =>
        await db.PunchLocationRedactions.AddRangeAsync(redactions, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
