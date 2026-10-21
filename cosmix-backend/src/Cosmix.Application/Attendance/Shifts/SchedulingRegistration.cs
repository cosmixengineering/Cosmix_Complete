using Microsoft.Extensions.DependencyInjection;
using Cosmix.Application.Attendance.Rosters;

namespace Cosmix.Application.Attendance.Shifts;

public static class SchedulingRegistration
{
    public static IServiceCollection AddSchedulingApplication(this IServiceCollection services)
    {
        services.AddScoped<SchedulingOperation>();
        services.AddScoped<ShiftVersionPublication>();
        services.AddScoped<SiteRosterImpactValidator>();
        services.AddScoped<ShiftService>();
        services.AddScoped<SiteRosterResolver>();
        services.AddScoped<OfficeRosterResolver>();
        services.AddScoped<OfficeRosterImpactValidator>();
        services.AddScoped<OfficeRosterService>();
        services.AddScoped<IIndividualRosterResolver, EmployeeRosterResolver>();
        services.AddScoped<IndividualRosterService>();
        services.AddScoped<SiteRosterService>();
        return services;
    }
}
