using Cosmix.Application.HR.Allowances;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Repositories.HR;

public static class AllowanceRegistration
{
    public static IServiceCollection AddAllowancesInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<AllowanceRepository>();
        services.AddScoped<IAllowanceRepository>(provider => provider.GetRequiredService<AllowanceRepository>());
        services.AddScoped<IAllowancePolicyRepository>(provider => provider.GetRequiredService<AllowanceRepository>());
        services.AddScoped<IAllowanceSettlementStore>(provider => provider.GetRequiredService<AllowanceRepository>());
        return services;
    }
}
