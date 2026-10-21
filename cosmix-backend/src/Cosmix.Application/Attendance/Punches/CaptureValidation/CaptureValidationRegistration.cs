using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Application.Attendance.Punches.CaptureValidation;

public static class CaptureValidationRegistration
{
    public static IServiceCollection AddCaptureValidationApplication(this IServiceCollection services)
    {
        services.AddScoped<ICaptureValidationEvaluator, CaptureValidationEvaluator>();
        services.AddScoped<CaptureValidationPolicyService>();
        services.AddScoped<ICaptureValidationPolicyReader>(x => x.GetRequiredService<CaptureValidationPolicyService>());
        return services;
    }
}
