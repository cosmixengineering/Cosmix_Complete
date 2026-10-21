using Cosmix.Application.Attendance.SiteReports;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public static class SiteReportInfrastructureRegistration
{
    public static IServiceCollection AddSiteReportsInfrastructure(this IServiceCollection services) =>
        services.AddScoped<ISiteReportRepository, SiteReportRepository>()
            .AddScoped<ISiteReportReviewRepository, SiteReportRepository>();
}
