using Cosmix.Application.Attendance.Zones;
using Cosmix.Infrastructure.FaceVerification;
using Cosmix.Infrastructure.Files;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Zones;

public static class ZoneInfrastructureRegistration
{
    public static IServiceCollection AddAttendanceZonesInfrastructure(this IServiceCollection services,IConfiguration configuration)
    {
        services.AddSingleton(configuration.GetSection(ZoneRuntimeOptions.SectionName).Get<ZoneRuntimeOptions>()??new());
        services.AddSingleton(configuration.GetSection(ZoneOfflineObservationOptions.SectionName).Get<ZoneOfflineObservationOptions>()??new());
        services.AddSingleton(configuration.GetSection(ZoneClaimStorageOptions.SectionName).Get<ZoneClaimStorageOptions>()??new());
        services.AddSingleton<IZoneClaimStore>(provider=>new EncryptedZoneClaimStore(
            provider.GetRequiredService<ZoneClaimStorageOptions>(),provider.GetRequiredService<PrivateFileStorageOptions>(),
            configuration.GetSection(FaceTemplateStorageOptions.SectionName).Get<FaceTemplateStorageOptions>()??new(),
            configuration.GetSection(ZoneLocationStorageOptions.SectionName).Get<ZoneLocationStorageOptions>()??new(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ZoneClaimRetentionHealth>();
        services.AddSingleton<IZoneClaimRetentionStatus>(provider=>provider.GetRequiredService<ZoneClaimRetentionHealth>());
        services.AddHostedService<ZoneClaimRetentionWorker>();
        services.AddSingleton<IZoneLocationStore>(provider=>new EncryptedZoneLocationStore(
            configuration.GetSection(ZoneLocationStorageOptions.SectionName).Get<ZoneLocationStorageOptions>()??new(),
            provider.GetRequiredService<PrivateFileStorageOptions>(),configuration.GetSection(FaceTemplateStorageOptions.SectionName).Get<FaceTemplateStorageOptions>()??new(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ZoneRetentionHealth>();
        services.AddSingleton<IZoneRetentionStatus>(provider=>provider.GetRequiredService<ZoneRetentionHealth>());
        services.AddScoped<IZoneRepository,ZoneRepository>(); services.AddScoped<ZoneRetentionProcessor>();
        return services;
    }
}
