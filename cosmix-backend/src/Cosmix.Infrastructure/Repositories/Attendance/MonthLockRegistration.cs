using Cosmix.Application.Attendance.MonthLocks;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public static class MonthLockRegistration
{
    public static IServiceCollection AddMonthLocksInfrastructure(this IServiceCollection services)
    { services.AddScoped<IMonthLockRepository, MonthLockRepository>(); return services; }
}
