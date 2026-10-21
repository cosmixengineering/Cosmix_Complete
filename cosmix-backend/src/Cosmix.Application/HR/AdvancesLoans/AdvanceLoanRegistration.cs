using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Application.HR.AdvancesLoans;

public static class AdvanceLoanApplicationRegistration
{
    public static IServiceCollection AddAdvancesLoansApplication(this IServiceCollection services)
    {
        services.AddScoped<AdvanceLoanService>();
        return services;
    }
}
