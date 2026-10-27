using System.Data;
using Cosmix.Application.Attendance.PunchRetention;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.LocationConsent;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed partial class PunchRetentionRepository(CosmixDbContext db, IPrivateFileStore files,
    IPunchRetentionPrivateFiles retentionFiles) : IPunchRetentionRepository
{
    public async Task<int> RedactBatchAsync(DateTimeOffset thresholdUtc, DateTimeOffset redactedAtUtc,
        int retentionDays, int batchSize, CancellationToken ct)
    {
        if (batchSize is < 1 or > 1000 || retentionDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // Transaction-owned SQL Server application lock serializes worker replicas.
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock " +
                "@Resource=N'Cosmix.PunchLocationRetention', @LockMode=N'Exclusive', " +
                "@LockOwner=N'Transaction', @LockTimeout=10000; SELECT @result;";
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) < 0)
                throw new InvalidOperationException("Punch retention lock is unavailable.");
        }
        // Include accuracy-only legacy rows and rows whose receipt survived older cleanup code.
        var ids = await db.RawPunchEvents.AsNoTracking()
            .Where(p => p.ReceivedAtUtc < thresholdUtc &&
                (p.Latitude != null || p.Longitude != null || p.AccuracyMeters != null))
            .OrderBy(p => p.ReceivedAtUtc).ThenBy(p => p.Id)
            .Select(p => p.Id).Take(batchSize).ToListAsync(ct);
        if (ids.Count == 0) { await transaction.CommitAsync(ct); return 0; }
        var recordedIds = (await db.PunchLocationRedactions.AsNoTracking()
            .Where(r => ids.Contains(r.PunchEventId)).Select(r => r.PunchEventId).ToListAsync(ct)).ToHashSet();
        foreach (var id in ids.Where(id => !recordedIds.Contains(id)))
            db.PunchLocationRedactions.Add(new PunchLocationRedaction(id, redactedAtUtc, "system",
                retentionDays, $"Automated retention cleanup older than {retentionDays} days"));
        await db.SaveChangesAsync(ct);
        var updated = await db.RawPunchEvents.Where(p => ids.Contains(p.Id) && p.ReceivedAtUtc < thresholdUtc)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Latitude, (decimal?)null)
                .SetProperty(p => p.Longitude, (decimal?)null)
                .SetProperty(p => p.AccuracyMeters, (decimal?)null), ct);
        if (updated != ids.Count)
            throw new DbUpdateConcurrencyException("Punch retention batch changed before coordinates were cleared.");
        await transaction.CommitAsync(ct);
        return updated;
    }
}
