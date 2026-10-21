using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Web;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Notifications;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Notifications;

public sealed class NotificationRepository(CosmixDbContext db, MobileAuthenticationService mobile, WebAuthenticationService web)
    : INotificationRepository
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

    public async Task<PagedResult<Notification>> ListAsync(string recipientAccountId, NotificationListRequest request, CancellationToken ct)
    {
        var query = db.Notifications.AsNoTracking().Where(x => x.RecipientUserId == recipientAccountId &&
            x.Channel == NotificationChannel.InApp && x.DeliveredAt != null);
        if (request.UnreadOnly) query = query.Where(x => x.ReadAt == null);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        return new(rows, count, request.Page, request.PageSize);
    }

    public Task<Notification?> FindAsync(string recipientAccountId, Guid notificationId, CancellationToken ct) =>
        db.Notifications.SingleOrDefaultAsync(x => x.Id == notificationId && x.RecipientUserId == recipientAccountId &&
            x.Channel == NotificationChannel.InApp && x.DeliveredAt != null, ct);
    public void ExpectVersion(Notification notification, byte[] version) =>
        db.Entry(notification).Property(x => x.RowVersion).OriginalValue = version;
    public void Add(Notification notification) => db.Notifications.Add(notification);
    public void AddAudit(AuditEvent audit) => db.AuditEvents.Add(audit);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
