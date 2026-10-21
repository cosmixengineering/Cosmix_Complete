using Cosmix.Application.Attendance.Kpi;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public static class KpiRepositoryRegistration
{
    public static IServiceCollection AddAttendanceKpiRepository(this IServiceCollection services)
    {
        services.AddScoped<IKpiRepository, KpiRepository>();
        services.AddScoped<IKpiDisputeRepository, KpiDisputeRepository>();
        return services;
    }
}
