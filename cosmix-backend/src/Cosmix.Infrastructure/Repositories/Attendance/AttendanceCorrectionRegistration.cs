using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.Calculation;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public static class AttendanceCorrectionRegistration
{
    public static IServiceCollection AddAttendanceCorrectionsInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IAttendanceCorrectionRepository, AttendanceCorrectionRepository>();
        services.AddScoped<ICorrectionReviewRepository, AttendanceCorrectionRepository>();
        services.AddScoped<ICorrectionAlternativePolicyRepository, CorrectionAlternativePolicyRepository>();
        services.AddScoped<IAlternativeCorrectionApprovalRepository, AlternativeCorrectionApprovalRepository>();
        services.AddScoped<IAttendanceAlternativeCorrectionApprovalReader>(provider =>
            (IAttendanceAlternativeCorrectionApprovalReader)provider.GetRequiredService<IAttendanceCalculationRepository>());
        return services;
    }
}
