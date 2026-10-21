using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Application.Attendance.Zones;

public static class ZoneRegistration
{
    public static IServiceCollection AddAttendanceZonesApplication(this IServiceCollection services)
    {
        services.AddScoped<ZoneAdministrationService>(); services.AddScoped<ZoneMonitoringService>();
        services.AddScoped<ZoneOfflineObservationService>(); services.AddScoped<ZoneClaimRetentionService>();
        return services;
    }
}
