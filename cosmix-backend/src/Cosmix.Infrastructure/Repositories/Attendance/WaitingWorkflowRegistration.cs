using Cosmix.Application.Attendance.Waiting;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public static class WaitingWorkflowRegistration
{
    public static IServiceCollection AddAttendanceWaitingInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IWaitingWorkflowRepository, WaitingWorkflowRepository>();
        return services;
    }
}
