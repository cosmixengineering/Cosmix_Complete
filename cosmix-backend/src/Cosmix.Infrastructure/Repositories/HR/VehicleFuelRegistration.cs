using Cosmix.Application.HR.VehicleFuel;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Repositories.HR;

public static class VehicleFuelRegistration
{
    public static IServiceCollection AddVehicleFuelInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IVehicleFuelRepository, VehicleFuelRepository>();
        return services;
    }
}
