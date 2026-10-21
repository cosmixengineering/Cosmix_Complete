using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Application.Attendance.Waiting;

public static class WaitingRegistration
{
    public static IServiceCollection AddAttendanceWaitingApplication(this IServiceCollection services)
    {
        services.AddScoped<WaitingSources>();
        services.AddScoped<WaitingWorkflowService>();
        services.AddScoped<IWaitingPerformanceEffectReader, WaitingPerformanceEffectReader>();
        return services;
    }
}
