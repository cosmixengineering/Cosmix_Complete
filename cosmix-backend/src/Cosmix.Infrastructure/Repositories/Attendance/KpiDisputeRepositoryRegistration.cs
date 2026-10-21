using Cosmix.Application.Attendance.Kpi;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public static class KpiDisputeRepositoryRegistration
{
    public static IServiceCollection AddAttendanceKpiDisputeRepository(this IServiceCollection services)
    {
        services.AddScoped<IKpiDisputeRepository, KpiDisputeRepository>();
        return services;
    }
}
