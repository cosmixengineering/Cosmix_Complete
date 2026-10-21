using Cosmix.Application.Attendance.Reminders;
using Cosmix.Application.Common.BackgroundJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public static class MissingCheckoutReminderRegistration
{
    public static IServiceCollection AddMissingCheckoutReminders(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new MissingCheckoutReminderOptions();
        configuration.GetSection("MissingCheckoutReminders").Bind(options);
        if (!options.IsValid()) throw new InvalidOperationException("Invalid MissingCheckoutReminders worker options.");
        services.AddSingleton(options);
        services.AddScoped<IMissingCheckoutReminderRepository, MissingCheckoutReminderRepository>();
        services.AddScoped<MissingCheckoutReminderProcessor>();
        // An opt-out must leave queued reminders unclaimed, not burn attempts while disabled.
        if (options.Enabled) services.AddScoped<IDurableJobHandler, MissingCheckoutReminderHandler>();
        return services;
    }
}
