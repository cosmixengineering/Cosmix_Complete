using Cosmix.Application.Authentication.Bootstrap;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Application.Authentication.Web;
using Cosmix.Domain.Authentication;
using Cosmix.Infrastructure.Authentication.Web;
using Cosmix.Infrastructure.Authentication.Providers;
using Cosmix.Infrastructure.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Authentication.Bootstrap;

public static class BootstrapRegistration
{
    /// <summary>Register only in the local command's isolated service provider, never in the HTTP host.</summary>
    public static IServiceCollection AddOwnerBootstrap(this IServiceCollection services,
        IConfiguration configuration, BootstrapStorageOptions storage)
    {
        var connection = configuration.GetConnectionString("CosmixDatabase");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Configure ConnectionStrings:CosmixDatabase privately.");
        services.AddDbContext<CosmixDbContext>(options => options.UseSqlServer(connection, sql => sql.CommandTimeout(30)));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new BootstrapPolicy(configuration.GetValue<int>("WebAuthentication:MinimumPasswordLength"),
            configuration["MobileAuthentication:BusinessTimeZoneId"] ?? string.Empty));
        services.AddSingleton(storage);
        services.AddSingleton<IBootstrapSnapshotStore, PrivateBootstrapSnapshotStore>();
        services.AddSingleton<PasswordHasher<UserAccount>>();
        services.AddSingleton<IWebPasswordHasher, MicrosoftWebPasswordVerifier>();
        services.AddSingleton<IPhoneNumberNormalizer, LibPhoneNumberNormalizer>();
        services.AddScoped<IBootstrapRepository, BootstrapRepository>();
        services.AddScoped<OwnerBootstrapService>();
        return services;
    }
}
