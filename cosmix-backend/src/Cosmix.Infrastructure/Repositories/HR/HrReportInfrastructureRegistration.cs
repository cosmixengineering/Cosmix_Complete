using Cosmix.Application.HR.Reports;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Repositories.HR;

public static class HrReportInfrastructureRegistration
{
    public static IServiceCollection AddHrReportsInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IHrReportRepository, HrReportRepository>();
        services.AddScoped<IHrReportExportRepository, HrReportExportRepository>();
        return services;
    }
}
