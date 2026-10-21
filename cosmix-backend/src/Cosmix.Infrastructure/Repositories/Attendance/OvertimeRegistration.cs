using Cosmix.Application.Attendance.Overtime;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public static class OvertimeRegistration
{
    public static IServiceCollection AddAttendanceOvertimeInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IOvertimeRepository, OvertimeRepository>();
        services.AddScoped<IOvertimeEligibilityRepository, OvertimeEligibilityRepository>();
        return services;
    }
}
