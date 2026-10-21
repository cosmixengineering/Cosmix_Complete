using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Notifications;

namespace Cosmix.Application.Common.Notifications;

public sealed class NotificationService(ICurrentAuthentication current, IAccessManagementRepository transactions,
    INotificationRepository notifications, TimeProvider clock)
{
    public Task<PagedResult<NotificationResponse>> ListAsync(NotificationListRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        Validate(request);
        if (request.Page is < 1 or > 10000 || request.PageSize is < 1 or > 100) throw Invalid();
        return AuthorizedAsync(async (actor, token) =>
        {
            var result = await notifications.ListAsync(actor.AccountId.ToString(), request, token);
            Audit(actor, "notification.list", "collection", context);
            await notifications.SaveChangesAsync(token);
            return new PagedResult<NotificationResponse>(result.Items.Select(Map).ToArray(),
                result.TotalCount, result.Page, result.PageSize);
        }, ct);
    }

    public Task<NotificationResponse> GetAsync(Guid id, AuthRequestContext context, CancellationToken ct)
    {
        if (id == Guid.Empty) throw Invalid();
        return AuthorizedAsync(async (actor, token) =>
        {
            var item = await notifications.FindAsync(actor.AccountId.ToString(), id, token) ?? throw Missing();
            Audit(actor, "notification.read", id.ToString(), context);
            await notifications.SaveChangesAsync(token);
            return Map(item);
        }, ct);
    }

    public Task<NotificationReadResponse> MarkReadAsync(Guid id, MarkNotificationReadRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        if (id == Guid.Empty) throw Invalid();
        Validate(request);
        var version = Version(request.ExpectedVersion);
        return AuthorizedAsync(async (actor, token) =>
        {
            var item = await notifications.FindAsync(actor.AccountId.ToString(), id, token) ?? throw Missing();
            // Read is monotonic. A repeat returns its original ReadAt without another write or audit,
            // even when the caller has the pre-read version. Unread stale writes still conflict.
            if (item.ReadAt.HasValue) return new NotificationReadResponse(Map(item), false);
            if (!item.RowVersion.SequenceEqual(version)) throw new AuthenticationFailure("notification_version_conflict", 409);
            notifications.ExpectVersion(item, version);
            item.MarkRead(actor.AccountId.ToString(), clock.GetUtcNow());
            Audit(actor, "notification.mark-read", id.ToString(), context);
            await notifications.SaveChangesAsync(token);
            return new NotificationReadResponse(Map(item), true);
        }, ct);
    }

    private Task<T> AuthorizedAsync<T>(Func<AuthenticatedAccount, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var actor = current.Require();
        if (actor.Client == SessionClient.Web && !actor.MfaSatisfied) throw new AuthenticationFailure("access_denied", 403);
        return transactions.InTransactionAsync(async token =>
        {
            if (!await notifications.IsCurrentSessionAsync(actor, token))
                throw new AuthenticationFailure("invalid_authentication", 401);
            return await action(actor, token);
        }, ct);
    }

    private void Audit(AuthenticatedAccount actor, string operation, string target, AuthRequestContext context) =>
        notifications.AddAudit(new AuditEvent(operation, actor.AccountId.ToString(), "recipient", "Notification", target,
            clock.GetUtcNow(), context.CorrelationId, employeeScopeId: actor.EmployeeId, ipAddress: context.IpAddress,
            deviceReference: actor.SessionId.ToString()));

    private static NotificationResponse Map(Notification item)
    {
        // Legacy/unknown templates remain generic; neither their code nor protected data is used as display text.
        var known = NotificationTemplates.TryGet(item.TemplateCode, out var text);
        return new(item.Id, known ? item.TemplateCode : "notification.notice",
            known ? text.Title : "Notification available", known ? text.Body : "Open the application to review your updates.",
            item.CreatedAt, item.ReadAt, Convert.ToBase64String(item.RowVersion));
    }

    private static void Validate(object? value)
    {
        if (value is null || !Validator.TryValidateObject(value, new ValidationContext(value), [], true)) throw Invalid();
    }
    private static byte[] Version(string value)
    {
        if (value is null || value.Length != 12) throw Invalid();
        try
        {
            var result = Convert.FromBase64String(value);
            return result.Length == 8 && Convert.ToBase64String(result) == value ? result : throw Invalid();
        }
        catch (FormatException) { throw Invalid(); }
    }
    private static AuthenticationFailure Invalid() => new("invalid_notification_request", 400);
    private static AuthenticationFailure Missing() => new("notification_not_found", 404);
}
