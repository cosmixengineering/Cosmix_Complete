using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Application.HR.VehicleFuel;

public static class VehicleFuelRegistration
{
    public static IServiceCollection AddVehicleFuelApplication(this IServiceCollection services)
    {
        services.AddScoped<VehicleFuelService>();
        return services;
    }
}
