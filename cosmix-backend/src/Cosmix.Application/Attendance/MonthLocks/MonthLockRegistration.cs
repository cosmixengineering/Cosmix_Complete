using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Application.Attendance.MonthLocks;

public static class MonthLockRegistration
{
    public static IServiceCollection AddMonthLocksApplication(this IServiceCollection services)
    {
        services.AddScoped<MonthLockService>();
        services.AddScoped<IMonthLockSnapshotReader>(provider => provider.GetRequiredService<MonthLockService>());
        return services;
    }
}
