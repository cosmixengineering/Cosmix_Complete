using Cosmix.Application.Attendance.Punches.CaptureValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public static class CaptureValidationRegistration
{
    public static IServiceCollection AddCaptureValidationInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ICaptureValidationPolicyRepository, CaptureValidationPolicyRepository>();
        services.AddScoped<ICaptureValidationEvidenceRepository, CaptureValidationEvidenceRepository>();
        return services;
    }
}
