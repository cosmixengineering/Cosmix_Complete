using Cosmix.Application.HR.Policies.Advanced;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cosmix.Infrastructure.Repositories.HR;

public static class AdvancedPolicyInfrastructureRegistration
{
    public static IServiceCollection AddAdvancedAttendancePoliciesInfrastructure(this IServiceCollection services)
    {
        services.TryAddScoped<AdvancedPolicyRepository>();
        services.TryAddScoped<IAdvancedPolicyPublicationRepository>(sp => sp.GetRequiredService<AdvancedPolicyRepository>());
        services.TryAddScoped<IAdvancedAttendancePolicyRepository>(sp => sp.GetRequiredService<AdvancedPolicyRepository>());
        return services;
    }
}
