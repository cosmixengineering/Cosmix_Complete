using Cosmix.Application.Common.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Application.Attendance.Reports;

public static class AttendanceReportRegistration
{
    public static IServiceCollection AddAttendanceReportsApplication(this IServiceCollection services)
    {
        services.AddScoped<AttendanceReportService>();
        services.AddScoped<IDurableJobHandler, AttendanceReportHandler>();
        return services;
    }
}
