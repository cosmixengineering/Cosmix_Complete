using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Application.Attendance.Corrections;

public static class AttendanceCorrectionRegistration
{
    public static IServiceCollection AddAttendanceCorrectionsApplication(this IServiceCollection services)
    {
        services.AddScoped<AttendanceCorrectionService>();
        services.AddScoped<CorrectionReviewService>();
        services.AddScoped<CorrectionAlternativePolicyService>();
        services.AddScoped<AlternativeCorrectionApprovalJournal>();
        services.AddScoped<ICorrectionAlternativeEvidenceReader>(x => x.GetRequiredService<CorrectionAlternativePolicyService>());
        return services;
    }
}
