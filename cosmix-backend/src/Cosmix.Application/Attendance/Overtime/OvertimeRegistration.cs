using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cosmix.Application.Attendance.Overtime;

public static class OvertimeRegistration
{
    public static IServiceCollection AddAttendanceOvertimeApplication(this IServiceCollection services)
    {
        services.AddScoped<OvertimeService>(); services.AddScoped<OvertimeSources>();
        services.AddScoped<Cosmix.Application.Attendance.MonthLocks.IMonthLockOvertimeReader>(
            provider => provider.GetRequiredService<OvertimeService>());
        services.AddScoped<OvertimeEligibilityService>();
        services.TryAddScoped<IOvertimeEligibilityReader>(provider => provider.GetRequiredService<OvertimeEligibilityService>());
        services.TryAddScoped<IOvertimeDurationPolicy, ConfiguredOvertimeDurationPolicy>();
        return services;
    }
}
