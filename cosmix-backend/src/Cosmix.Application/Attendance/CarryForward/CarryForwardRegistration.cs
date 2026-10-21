using Cosmix.Application.Attendance.MonthLocks;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Application.Attendance.CarryForward;

public static class CarryForwardRegistration
{
    public static IServiceCollection AddAttendanceCarryForwardApplication(this IServiceCollection services)
    {
        services.AddScoped<CarryForwardService>(); services.AddScoped<CarryForwardEvidence>();
        services.AddScoped<ICarryForwardDutyOverlap, CarryForwardDutyOverlap>();
        services.AddScoped<IMonthLockCarryForwardReader, CarryForwardLockReader>();
        return services;
    }
}
