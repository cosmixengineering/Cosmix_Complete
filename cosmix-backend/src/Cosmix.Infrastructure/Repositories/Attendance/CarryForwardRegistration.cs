using Cosmix.Application.Attendance.CarryForward;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public static class CarryForwardRegistration
{
    public static IServiceCollection AddAttendanceCarryForwardInfrastructure(this IServiceCollection services)
    { services.AddScoped<ICarryForwardRepository, CarryForwardRepository>(); return services; }
}
