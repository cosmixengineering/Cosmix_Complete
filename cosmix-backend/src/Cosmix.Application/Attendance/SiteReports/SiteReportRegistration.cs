using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Application.Attendance.SiteReports;

public static class SiteReportRegistration
{
    public static IServiceCollection AddSiteReportsApplication(this IServiceCollection services) =>
        services.AddScoped<SiteReportService>()
            .AddScoped<SiteReportReviewService>()
            .AddScoped<SiteReportResponseEvidence>();
}
