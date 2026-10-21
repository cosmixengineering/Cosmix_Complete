using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cosmix.Application.HR.Allowances;

public static class AllowanceRegistration
{
    public static IServiceCollection AddAllowancesApplication(this IServiceCollection services)
    {
        services.AddScoped<AllowanceService>();
        services.AddScoped<AllowancePolicyService>();
        services.TryAddScoped<IAllowancePolicyResolver, AllowancePolicyResolver>();
        return services;
    }
}
