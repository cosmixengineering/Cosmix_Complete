using Cosmix.Application.Authentication.Access;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cosmix.Infrastructure.Authentication.Access;

public static class AccessManagementExtensions
{
    public static IServiceCollection AddAccessManagement(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IAccountAccessReader, AccountAccessReader>();
        services.AddScoped<IAccessManagementRepository, AccessManagementRepository>();
        services.AddScoped<IAccessManagementService, AccessManagementService>();
        return services;
    }
}
