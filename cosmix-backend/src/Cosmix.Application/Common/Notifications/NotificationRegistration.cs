using Cosmix.Application.Common.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Application.Common.Notifications;

public static class NotificationRegistration
{
    public static IServiceCollection AddAttendanceNotificationsApplication(this IServiceCollection services)
    {
        services.AddScoped<NotificationService>();
        services.AddScoped<INotificationOutbox, NotificationOutbox>();
        services.AddScoped<IDurableJobHandler, InAppNotificationHandler>();
        services.AddScoped<DurableJobProcessor>();
        return services;
    }
}
