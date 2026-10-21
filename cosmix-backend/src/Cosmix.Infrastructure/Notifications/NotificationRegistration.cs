using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Application.Common.Notifications;
using Cosmix.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Notifications;

public static class NotificationRegistration
{
    public static IServiceCollection AddAttendanceNotificationsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("AttendanceJobs").Get<AttendanceJobOptions>() ?? new();
        if (!options.IsValid()) throw new InvalidOperationException("AttendanceJobs settings are invalid.");
        services.AddSingleton(options);
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IDurableJobRepository, DurableJobRepository>();
        return services;
    }
}
