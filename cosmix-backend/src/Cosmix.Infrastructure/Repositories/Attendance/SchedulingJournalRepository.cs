using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Web;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class SchedulingJournalRepository(CosmixDbContext db,
    MobileAuthenticationService mobile, WebAuthenticationService web) : ISchedulingJournalRepository
{
    public async Task<bool> IsCurrentSessionAsync(AuthenticatedAccount actor, CancellationToken ct)
    {
        var verified = actor.Client switch
        {
            SessionClient.Web => await web.ValidateSessionAsync(actor.AccountId, actor.SessionId, actor.SecurityVersion, ct),
            SessionClient.Mobile => await mobile.ValidateSessionAsync(actor.AccountId, actor.SessionId, actor.SecurityVersion, ct),
            _ => null
        };
        return verified == actor;
    }

    public Task<IdempotencyReceipt?> FindReceiptAsync(string actor, string operation, string key, CancellationToken ct) =>
        db.IdempotencyReceipts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.ActorUserId == actor && x.Operation == operation && x.Key == key, ct);

    public void AddAudit(AuditEvent audit) => db.AuditEvents.Add(audit);
    public void AddReceipt(IdempotencyReceipt receipt) => db.IdempotencyReceipts.Add(receipt);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
