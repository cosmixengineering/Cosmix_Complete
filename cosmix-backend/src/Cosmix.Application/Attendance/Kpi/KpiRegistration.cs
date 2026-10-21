using Microsoft.Extensions.DependencyInjection;
using Cosmix.Application.Attendance.WorkTreatments;

namespace Cosmix.Application.Attendance.Kpi;

public static class KpiRegistration
{
    public static IServiceCollection AddAttendanceKpiApplication(this IServiceCollection services)
    {
        services.AddScoped<AttendanceKpiService>();
        services.AddScoped<KpiDisputeService>();
        services.AddScoped<KpiDisputeEvidenceReader>();
        services.AddScoped<KpiDisputeAuthority>();
        services.AddScoped<IKpiSourceReader, KpiSourceReader>();
        services.AddScoped<IFinalizedKpiReader, FinalizedKpiReader>();
        services.AddScoped<IWorkTreatmentAnnualLateReader>(provider => new KpiAnnualLateReader(
            provider.GetRequiredService<IKpiRepository>(),
            (employee, asOf, token) => provider.GetRequiredService<IKpiSourceReader>().ReadAsync(employee, asOf, token),
            provider.GetRequiredService<Cosmix.Application.Attendance.Common.AttendanceOperation>()));
        return services;
    }
}
