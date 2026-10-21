using System.Data;
using System.Security.Cryptography;
using System.Text;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Notifications;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cosmix.Api.BackgroundJobs;

public sealed class DocumentExpiryReminderWorker(
    IServiceScopeFactory scopes,
    ILogger<DocumentExpiryReminderWorker> logger,
    TimeProvider clock) : BackgroundService
{
    private const int BatchSize = 100;
    private enum SourceKind { Document, Certification, Insurance }
    private sealed class Candidate
    {
        public Guid Id { get; init; }
        public Guid AccountId { get; init; }
        public DateOnly ExpiresOn { get; init; }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            await CheckExpiriesAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CheckExpiriesAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task CheckExpiriesAsync(CancellationToken stoppingToken)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        foreach (var kind in Enum.GetValues<SourceKind>())
        {
            try
            {
                Guid? after = null;
                long staged = 0;
                while (true)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    // A fresh unit of work bounds both tracked rows and rollback to one page.
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<CosmixDbContext>();
                    var outbox = scope.ServiceProvider.GetRequiredService<INotificationOutbox>();
                    var batch = await StageBatchAsync(db, outbox, kind, today, after, stoppingToken);
                    staged += batch.Staged;
                    if (batch.Count < BatchSize) break;
                    after = batch.LastId;
                }
                if (staged > 0)
                    logger.LogInformation("Document expiry reminders queued {Count} {SourceKind} notifications.", staged, kind);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception)
            {
                // Failed pages restart from the beginning next sweep; committed outbox identities
                // make replay safe. Keep other source kinds progressing and private details out of logs.
                logger.LogWarning("Document expiry reminder sweep failed for {SourceKind}; committed pages will be deduplicated on retry.", kind);
            }
        }
    }

    private static IQueryable<Candidate> Candidates(CosmixDbContext db, SourceKind kind, DateOnly today, Guid? after)
    {
        var threshold = today.AddDays(30);
        IQueryable<Candidate> query = kind switch
        {
            SourceKind.Document =>
                from doc in db.EmployeeDocuments.AsNoTracking()
                join account in db.UserAccounts.AsNoTracking() on doc.EmployeeId equals account.EmployeeId
                where doc.LifecycleStatus == EmployeeDocumentLifecycleStatus.Current &&
                    doc.ExpiresOn.HasValue && doc.ExpiresOn.Value >= today && doc.ExpiresOn.Value <= threshold &&
                    account.Status != AccountStatus.Disabled
                select new Candidate { Id = doc.Id, AccountId = account.Id, ExpiresOn = doc.ExpiresOn!.Value },
            SourceKind.Certification =>
                from cert in db.EmployeeCertifications.AsNoTracking()
                join account in db.UserAccounts.AsNoTracking() on cert.EmployeeId equals account.EmployeeId
                where cert.Status == EmployeeCertificationStatus.Active &&
                    cert.EffectiveOn <= today && !db.EmployeeCertifications.Any(next => next.EmployeeId == cert.EmployeeId &&
                        next.SupersedesEmployeeCertificationId == cert.Id && next.EffectiveOn <= today) &&
                    cert.ExpiresOn.HasValue && cert.ExpiresOn.Value >= today && cert.ExpiresOn.Value <= threshold &&
                    account.Status != AccountStatus.Disabled
                select new Candidate { Id = cert.Id, AccountId = account.Id, ExpiresOn = cert.ExpiresOn!.Value },
            SourceKind.Insurance =>
                from ins in db.InsurancePolicies.AsNoTracking()
                join account in db.UserAccounts.AsNoTracking() on ins.EmployeeId equals (Guid?)account.EmployeeId
                where ins.EmployeeId.HasValue && ins.ExpiryDate >= today && ins.ExpiryDate <= threshold &&
                    account.Status != AccountStatus.Disabled
                select new Candidate { Id = ins.Id, AccountId = account.Id, ExpiresOn = ins.ExpiryDate },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        // Account.EmployeeId is unique; a source ID therefore identifies exactly one joined row.
        return query.Where(x => after == null || x.Id > after.Value).OrderBy(x => x.Id).Take(BatchSize);
    }

    private static async Task<(int Count, Guid? LastId, int Staged)> StageBatchAsync(CosmixDbContext db,
        INotificationOutbox outbox, SourceKind kind, DateOnly today, Guid? after, CancellationToken ct)
    {
        // The transaction-owned lock serializes producers across processes. Read committed avoids
        // holding legacy inbox range locks while the existing dispatcher owns an outbox row.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource=N'cosmix:document-expiry-reminders',
                @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51003, 'Document expiry reminders are temporarily busy.', 1;
            """, ct);
        var candidates = await Candidates(db, kind, today, after).ToArrayAsync(ct);
        var staged = 0;
        foreach (var candidate in candidates)
        {
            var (prefix, template) = kind switch
            {
                SourceKind.Document => ("doc", NotificationTemplates.DocumentExpiring),
                SourceKind.Certification => ("cert", NotificationTemplates.CertificationExpiring),
                SourceKind.Insurance => ("ins", NotificationTemplates.InsuranceExpiring),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            var correlation = FormattableString.Invariant($"expiry:{prefix}:{candidate.Id:N}:{candidate.ExpiresOn:yyyyMMdd}");
            var recipient = candidate.AccountId.ToString();
            if (await db.Notifications.AsNoTracking().AnyAsync(n => n.RecipientUserId == recipient &&
                    n.CorrelationId == correlation && n.Channel == NotificationChannel.InApp && n.DeliveredAt != null, ct))
                continue;

            // Same source/type/expiry survives restarts; the existing outbox key also binds recipient
            // and template. A renewed expiry is a different event, even on the same document ID.
            var eventId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(correlation)).AsSpan(0, 16));
            var intent = new NotificationIntent(eventId, candidate.AccountId, template, candidate.Id);
            if (await outbox.StageAsync(intent, new AuthRequestContext("system", correlation), ct)) staged++;
        }
        if (staged > 0) await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (candidates.Length, candidates.LastOrDefault()?.Id, staged);
    }
}
