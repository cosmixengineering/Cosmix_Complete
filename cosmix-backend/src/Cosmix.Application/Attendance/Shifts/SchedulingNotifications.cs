using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Notifications;

namespace Cosmix.Application.Attendance.Shifts;

public sealed record SchedulingNotificationRecipient(Guid EmployeeId, Guid AccountId);
public interface ISchedulingNotificationRepository
{
    Task<IReadOnlyList<SchedulingNotificationRecipient>> AccountsAsync(IReadOnlyList<Guid> employeeIds, CancellationToken ct);
}
public interface ISchedulingNotifications
{
    Task StageEmployeesAsync(Guid eventId, Guid siteId, IReadOnlyList<Guid> employeeIds, AuthRequestContext context, CancellationToken ct);
    Task StageIntervalAsync(Guid eventId, Guid siteId, DateOnly from, DateOnly? end, AuthRequestContext context, CancellationToken ct);
}

/// <summary>Called only inside the authorized scheduling transaction using the exact affected employee/date manifest.</summary>
public sealed class SchedulingNotifications(IShiftRepository shifts, ISchedulingNotificationRepository recipients,
    INotificationOutbox notifications) : ISchedulingNotifications
{
    public async Task StageIntervalAsync(Guid eventId, Guid siteId, DateOnly from, DateOnly? end, AuthRequestContext context, CancellationToken ct)
    {
        var memberships = await shifts.GetSiteMembershipImpactAsync(siteId, from, end, ct);
        var affected = memberships.Where(x => x.SiteId == siteId && (!end.HasValue || x.EffectiveFrom < end) &&
            (!x.EffectiveTo.HasValue || x.EffectiveTo > from)).Select(x => x.EmployeeId).Distinct().Order().ToArray();
        await StageEmployeesAsync(eventId, siteId, affected, context, ct);
    }

    public async Task StageEmployeesAsync(Guid eventId, Guid siteId, IReadOnlyList<Guid> employeeIds,
        AuthRequestContext context, CancellationToken ct)
    {
        if (eventId == Guid.Empty || siteId == Guid.Empty || employeeIds.Count > 2000 || employeeIds.Any(x => x == Guid.Empty))
            throw SchedulingOperation.Conflict("scheduling_notification_manifest_invalid");
        if (employeeIds.Count == 0) return;
        var ids = employeeIds.Distinct().Order().ToArray();
        var accounts = await recipients.AccountsAsync(ids, ct);
        if (accounts.Any(x => !ids.Contains(x.EmployeeId) || x.AccountId == Guid.Empty) ||
            accounts.GroupBy(x => x.EmployeeId).Any(x => x.Count() != 1) || accounts.Select(x => x.AccountId).Distinct().Count() != accounts.Count)
            throw SchedulingOperation.Conflict("scheduling_notification_recipient_conflict");
        // Employees without an existing account receive no invented identity/access. Their schedule still applies.
        foreach (var account in accounts.OrderBy(x => x.AccountId))
            // One shared version can affect the same employee at multiple historical sites. Bind
            // to immutable evidence, not one site, so that event deduplicates without a false conflict.
            await notifications.StageAsync(new(eventId, account.AccountId, NotificationTemplates.ShiftChanged, eventId), context, ct);
    }
}
