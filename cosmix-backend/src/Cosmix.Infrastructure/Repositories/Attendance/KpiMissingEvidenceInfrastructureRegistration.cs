using Cosmix.Application.Attendance.Kpi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public static class KpiMissingEvidenceInfrastructureRegistration
{
    public static IServiceCollection AddKpiMissingEvidenceInfrastructure(this IServiceCollection services)
    { services.TryAddScoped<IKpiMissingEvidenceRepository, KpiMissingEvidenceRepository>(); return services; }
}
