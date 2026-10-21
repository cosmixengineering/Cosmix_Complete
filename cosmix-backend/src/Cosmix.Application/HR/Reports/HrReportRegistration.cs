using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Application.HR.Reports;

public static class HrReportRegistration
{
    public static IServiceCollection AddHrReportsApplication(this IServiceCollection services)
    {
        services.AddScoped<HrReportService>();
        services.AddScoped<HrReportAuthorization>();
        services.AddScoped<HrReportExportService>();
        services.AddScoped<Cosmix.Application.Common.BackgroundJobs.IDurableJobHandler, HrReportExportHandler>();
        return services;
    }
}
