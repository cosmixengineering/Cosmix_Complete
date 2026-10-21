using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.Rosters;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public static class SchedulingRegistration
{
    public static IServiceCollection AddSchedulingInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ISchedulingJournalRepository, SchedulingJournalRepository>();
        services.AddScoped<IShiftRepository, ShiftRepository>();
        services.AddScoped<IRosterRepository, RosterRepository>();
        services.AddScoped<ISiteRosterRepository, SiteRosterRepository>();
        services.AddScoped<IOfficeRosterRepository, OfficeRosterRepository>();
        return services;
    }
}
