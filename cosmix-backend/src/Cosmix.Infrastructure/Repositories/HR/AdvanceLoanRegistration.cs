using Cosmix.Application.HR.AdvancesLoans;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Repositories.HR;

public static class AdvanceLoanInfrastructureRegistration
{
    public static IServiceCollection AddAdvancesLoansInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IAdvanceLoanRepository, AdvanceLoanRepository>();
        return services;
    }
}
