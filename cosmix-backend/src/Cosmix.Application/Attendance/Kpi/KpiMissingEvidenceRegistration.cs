using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cosmix.Application.Attendance.Kpi;

public static class KpiMissingEvidenceRegistration
{
    public static IServiceCollection AddKpiMissingEvidenceApplication(this IServiceCollection services)
    {
        services.TryAddScoped<IKpiMissingEvidenceSourceReader, KpiMissingEvidenceSourceReader>();
        services.TryAddScoped<KpiMissingEvidenceEvidenceReader>();
        services.TryAddScoped<KpiMissingEvidenceAuthority>();
        services.TryAddScoped<KpiMissingEvidenceService>();
        services.TryAddScoped<IReviewedKpiMissingEvidenceReader, KpiMissingEvidenceReader>();
        return services;
    }
}
