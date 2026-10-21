using Cosmix.Application.Authentication.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cosmix.Infrastructure.Authentication.Providers;

public static class AuthenticationProviderExtensions
{
    /// <summary>
    /// Optional helpers only. The composition root calls this explicitly; no workflow or startup validation.
    /// Readiness is exposed through IsConfigured. Configuration changes/rotation require an application restart.
    /// Do not add retry, body logging, custom telemetry enrichment or redirect handlers to this named client.
    /// </summary>
    public static IServiceCollection AddAuthenticationProviders(this IServiceCollection services, IConfiguration configuration)
    {
        // Parse without throwing so malformed disabled configuration cannot break unrelated endpoints.
        var enabled = bool.TryParse(configuration["MobileAuthentication:Enabled"], out var value) && value;
        services.Configure<OtpCryptoOptions>(options =>
        {
            var section = configuration.GetSection(OtpCryptoOptions.SectionName);
            options.Enabled = enabled;
            if (!enabled) return;
            options.KeyVersion = section[nameof(options.KeyVersion)] ?? string.Empty;
            options.VerifierKeyBase64 = section[nameof(options.VerifierKeyBase64)] ?? string.Empty;
            options.EncryptionKeyBase64 = section[nameof(options.EncryptionKeyBase64)] ?? string.Empty;
            options.MaxDeliveryLifetimeSeconds = int.TryParse(section[nameof(options.MaxDeliveryLifetimeSeconds)], out var seconds) ? seconds : 0;
        });
        services.Configure<SmsGatewayOptions>(options =>
        {
            var section = configuration.GetSection(SmsGatewayOptions.SectionName);
            options.Enabled = enabled;
            if (!enabled) return;
            options.Provider = section[nameof(options.Provider)] ?? string.Empty;
            options.Endpoint = section[nameof(options.Endpoint)] ?? string.Empty;
            options.BearerToken = section[nameof(options.BearerToken)] ?? string.Empty;
            options.TimeoutSeconds = int.TryParse(section[nameof(options.TimeoutSeconds)], out var seconds) ? seconds : 0;
        });
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPhoneNumberNormalizer, LibPhoneNumberNormalizer>();
        services.TryAddSingleton<IOtpProtector, OtpProtector>();
        services.TryAddTransient<IOtpSmsSender, GenericHttpSmsSender>();
        services.AddHttpClient(GenericHttpSmsSender.HttpClientName, client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.MaxResponseContentBufferSize = 4096;
            })
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                MaxResponseHeadersLength = 16,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                MaxResponseDrainSize = 0
            });
        return services;
    }
}
