using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Infrastructure.FaceVerification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Cosmix.Infrastructure.Files;

namespace Cosmix.Infrastructure.Repositories.Attendance;
public static class AttendanceCaptureRegistration
{
    public static IServiceCollection AddAttendanceCaptureInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IAttendanceCaptureRepository, AttendanceCaptureRepository>();
        services.AddScoped<IFaceRepository, FaceRepository>();
        services.AddScoped<IFaceEnrollmentJournalRepository, FaceEnrollmentJournalRepository>();
        services.AddScoped<IFaceEnrollmentHistoryRepository, FaceEnrollmentHistoryRepository>();
        services.TryAddSingleton(provider => provider.GetRequiredService<IConfiguration>()
            .GetSection(FaceVerificationPolicy.SectionName).Get<FaceVerificationPolicy>() ?? new());
        services.TryAddSingleton(provider => provider.GetRequiredService<IConfiguration>()
            .GetSection(FaceCaptureOptions.SectionName).Get<FaceCaptureOptions>() ?? new());
        services.TryAddScoped<IFaceVerificationProvider>(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var options = configuration.GetSection(SelfHostedFaceOptions.SectionName).Get<SelfHostedFaceOptions>() ?? new();
            if (!options.Enabled)
                return new UnconfiguredFaceVerificationProvider();
            return new SelfHostedFaceVerificationProvider(options,
                provider.GetRequiredService<CompreFaceClient>(), provider.GetRequiredService<SilentFaceLivenessService>(),
                provider.GetRequiredService<IFaceCaptureEvidenceReader>());
        });
        services.AddHttpClient("Cosmix.CompreFace")
            .ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(provider => CompreFaceClient.CreateTransport(
                provider.GetRequiredService<IConfiguration>().GetSection(CompreFaceOptions.SectionName)
                    .Get<CompreFaceOptions>() ?? new()))
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
            .RemoveAllLoggers();
        services.TryAddSingleton(provider => new CompreFaceClient(provider.GetRequiredService<IConfiguration>()
            .GetSection(CompreFaceOptions.SectionName).Get<CompreFaceOptions>() ?? new(),
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("Cosmix.CompreFace")));
        services.TryAddSingleton(provider => new SilentFaceLivenessService(provider.GetRequiredService<IConfiguration>()
            .GetSection(SilentFaceOptions.SectionName).Get<SilentFaceOptions>() ?? new()));
        services.TryAddSingleton<IFaceTemplateStore>(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var biometric = configuration.GetSection(FaceTemplateStorageOptions.SectionName).Get<FaceTemplateStorageOptions>() ?? new();
            var ordinary = provider.GetRequiredService<PrivateFileStorageOptions>();
            var vault = new EncryptedFaceTemplateStore(biometric, ordinary);
            // Capture uploads can remain enabled independently of provider selection.
            var options = configuration.GetSection(FaceStorageRetentionOptions.SectionName).Get<FaceStorageRetentionOptions>() ?? new();
            var journalOptions = configuration.GetSection(FaceStorageRetentionJournalOptions.SectionName).Get<FaceStorageRetentionJournalOptions>() ?? new();
            var journal = new EncryptedFaceStorageRetentionJournal(journalOptions, biometric, ordinary);
            return new RetentionControlledFaceTemplateStore(vault, journal, options, provider.GetRequiredService<TimeProvider>());
        });
        return services.AddFaceRetirementInfrastructure();
    }
}
