using Cosmix.Application.Attendance.FaceVerification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cosmix.Infrastructure.FaceVerification;

public static class FaceRetirementRegistration
{
    public static IServiceCollection AddFaceRetirementInfrastructure(this IServiceCollection services)
    {
        services.TryAddScoped<IFaceRetirementRepository, FaceRetirementRepository>();
        services.TryAddSingleton<IFaceEvidenceRetirement>(provider =>
            provider.GetRequiredService<IFaceTemplateStore>() as IFaceEvidenceRetirement
            ?? new UnavailableFaceEvidenceRetirement());
        services.TryAddSingleton<IFaceDisposalPolicy, PendingFaceDisposalPolicy>();
        services.TryAddSingleton<IFacePhysicalDisposal, UnavailableFacePhysicalDisposal>();
        services.TryAddSingleton(new FaceDisposalOptions());
        services.TryAddSingleton(new FaceRetirementWorkerOptions());
        return services;
    }
}
