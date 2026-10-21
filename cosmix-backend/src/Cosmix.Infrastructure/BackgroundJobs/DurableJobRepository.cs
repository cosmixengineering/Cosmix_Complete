using System.Data;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Jobs;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.BackgroundJobs;

public sealed class DurableJobRepository(CosmixDbContext db, TimeProvider clock) : IDurableJobRepository
{
    public void RequireProducerTransaction()
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Stage outbox work in the business transaction before its atomic save.");
    }

    public async Task<OutboxMessage?> FindByIdentityAsync(string messageType, string deduplicationKey, CancellationToken ct) =>
        db.OutboxMessages.Local.SingleOrDefault(x => x.MessageType == messageType && x.DeduplicationKey == deduplicationKey) ??
        await db.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(x => x.MessageType == messageType &&
            x.DeduplicationKey == deduplicationKey, ct);
    public Task<bool> RecipientExistsAsync(Guid accountId, CancellationToken ct) =>
        db.UserAccounts.AsNoTracking().AnyAsync(x => x.Id == accountId, ct);
    public void Stage(OutboxMessage message) { RequireProducerTransaction(); db.OutboxMessages.Add(message); }
    public void AddAudit(AuditEvent audit) => db.AuditEvents.Add(audit);

    public Task<DurableJobClaim?> ClaimNextAsync(IReadOnlyCollection<string> allowedMessageTypes, DateTimeOffset now, TimeSpan lease, CancellationToken ct)
    {
        var ownedTypes = DurableJobMessageTypes.RequireAllowed(allowedMessageTypes);
        return TransactionAsync<DurableJobClaim?>(async token =>
        {
            // Short claims serialize separately from dispatch. The rowversion also rejects races with a
            // finishing old lease. This lock is transaction-owned and works with READ_COMMITTED_SNAPSHOT.
            await LockAsync("cosmix:attendance-outbox:claim", token);
            var item = await ClaimCandidates(db.OutboxMessages, ownedTypes, now).FirstOrDefaultAsync(token);
            if (item is null) return null;
            var leaseToken = item.Claim(now, now.Add(lease));
            await db.SaveChangesAsync(token);
            return new(item.Id, leaseToken, item.LeaseExpiresAt!.Value, item.Attempts)
            {
                Preparation = new(item.MessageType, item.EncryptedPayloadReference, item.CorrelationId, item.DeduplicationKey)
            };
        }, ct);
    }

    /// <summary>The actual SQL candidate query, exposed for provider translation checks without opening SQL.</summary>
    public static IOrderedQueryable<OutboxMessage> ClaimCandidates(IQueryable<OutboxMessage> source,
        IReadOnlyCollection<string> allowedMessageTypes, DateTimeOffset now)
    {
        var ownedTypes = DurableJobMessageTypes.RequireAllowed(allowedMessageTypes).Select(x => x + "|").ToArray();
        // Binary comparison rejects case aliases. A sentinel also defeats SQL equality's trailing-space
        // padding: neither "known.type " nor another module's type becomes registered ownership.
        return source.Where(x => ownedTypes.Contains(EF.Functions.Collate(x.MessageType + "|", "Latin1_General_100_BIN2")) &&
                ((x.State == DispatchState.Pending && x.AvailableAt <= now) ||
                 (x.State == DispatchState.Processing && x.LeaseExpiresAt <= now)))
            .OrderBy(x => x.AvailableAt).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id);
    }

    public Task<bool> CompleteAsync(DurableJobClaim claim,
        Func<OutboxMessage, CancellationToken, Task> action, CancellationToken ct) =>
        TransactionAsync(async token =>
        {
            var item = await LockedAsync(claim, token);
            if (!Owns(item, claim, clock.GetUtcNow())) return false;
            await action(item!, token);
            var now = clock.GetUtcNow();
            if (!Owns(item, claim, now)) throw new DurableJobFailure("dispatch_lease_expired", true);
            item!.Complete(claim.LeaseToken, now);
            await db.SaveChangesAsync(token);
            return true;
        }, ct);

    public Task<bool> FailAsync(DurableJobClaim claim, string code, DateTimeOffset? retryAt, CancellationToken ct) =>
        TransactionAsync(async token =>
        {
            var item = await LockedAsync(claim, token);
            var now = clock.GetUtcNow();
            if (!Owns(item, claim, now)) return false;
            item!.RecordFailure(claim.LeaseToken, now, code, retryAt.HasValue && retryAt < now ? now : retryAt);
            AddAudit(new AuditEvent("outbox.failed", "attendance-job-worker", "system", "OutboxMessage", item.Id.ToString(),
                now, item.CorrelationId));
            await db.SaveChangesAsync(token);
            return true;
        }, ct);

    private async Task<OutboxMessage?> LockedAsync(DurableJobClaim claim, CancellationToken ct)
    {
        await LockAsync($"cosmix:attendance-outbox:{claim.MessageId:N}", ct);
        // Keep the SQL update lock until the notification and outbox completion commit together.
        return (await db.OutboxMessages.FromSqlInterpolated($"SELECT * FROM [jobs].[OutboxMessages] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {claim.MessageId}")
            .ToListAsync(ct)).SingleOrDefault();
    }

    private static bool Owns(OutboxMessage? item, DurableJobClaim claim, DateTimeOffset now) =>
        item is not null && item.State == DispatchState.Processing && item.LeaseToken == claim.LeaseToken &&
        item.LeaseExpiresAt == claim.LeaseExpiresAt && item.LeaseExpiresAt > now && now >= item.CreatedAt;

    private Task LockAsync(string resource, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"""
        DECLARE @result int;
        EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
        IF @result < 0 THROW 51003, 'Attendance job is temporarily busy.', 1;
        """, ct);

    private async Task<T> TransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Run dispatch in a clean, independently scoped unit of work.");
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var result = await action(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
