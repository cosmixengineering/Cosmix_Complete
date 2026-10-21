using Cosmix.Application.Attendance.FaceVerification;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Application.Attendance.Punches;
public static class AttendanceCaptureRegistration
{
    public static IServiceCollection AddAttendanceCaptureApplication(this IServiceCollection services)
    {
        services.AddScoped<AttendanceCaptureService>();
        services.AddScoped<FaceService>();
        services.AddScoped<FaceCaptureService>();
        services.AddScoped<IFaceCaptureEvidenceReader, FaceCaptureEvidenceReader>();
        services.AddScoped<FaceEnrollmentJournal>();
        services.AddScoped<FaceEnrollmentHistoryService>();
        services.AddScoped<FaceRetirementService>();
        services.AddScoped<FaceRetirementProcessor>();
        services.AddScoped<FaceDisposalRunner>();
        return services;
    }
}
