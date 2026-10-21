using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Notifications;

namespace Cosmix.Application.Common.Notifications;

public sealed record NotificationListRequest([Range(1, 10000)] int Page = 1,
    [Range(1, 100)] int PageSize = 25, bool UnreadOnly = false);
public sealed record MarkNotificationReadRequest([Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion);
public sealed record NotificationResponse(Guid Id, string TemplateCode, string Title, string Body,
    DateTimeOffset CreatedAt, DateTimeOffset? ReadAt, string Version);
public sealed record NotificationReadResponse(NotificationResponse Notification, bool Changed);

/// <summary>No arbitrary display text, employee data or protected reference is accepted from producers.</summary>
public sealed record NotificationIntent(Guid EventId, Guid RecipientAccountId, string TemplateCode, Guid? ResourceId = null);

public interface INotificationOutbox
{
    /// <summary>
    /// Stage only, in the producer's existing business transaction and scoped unit of work. This never saves or commits.
    /// EventId identifies immutable business evidence. A retry with the same event, template and recipient deduplicates;
    /// changing ResourceId under that identity conflicts. Recipient authority must be resolved by the producer.
    /// </summary>
    Task<bool> StageAsync(NotificationIntent intent, AuthRequestContext context, CancellationToken ct);
}

public interface INotificationRepository
{
    Task<bool> IsCurrentSessionAsync(AuthenticatedAccount actor, CancellationToken ct);
    Task<PagedResult<Notification>> ListAsync(string recipientAccountId, NotificationListRequest request, CancellationToken ct);
    Task<Notification?> FindAsync(string recipientAccountId, Guid notificationId, CancellationToken ct);
    void ExpectVersion(Notification notification, byte[] version);
    void Add(Notification notification);
    void AddAudit(AuditEvent audit);
    Task SaveChangesAsync(CancellationToken ct);
}
