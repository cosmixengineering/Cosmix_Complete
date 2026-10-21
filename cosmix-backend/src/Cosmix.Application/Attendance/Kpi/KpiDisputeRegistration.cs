using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Application.Attendance.Kpi;

public static class KpiDisputeRegistration
{
    public static IServiceCollection AddAttendanceKpiDisputeApplication(this IServiceCollection services)
    {
        services.AddScoped<KpiDisputeService>();
        services.AddScoped<KpiDisputeAuthority>();
        services.AddScoped<KpiDisputeEvidenceReader>();
        return services;
    }
}
