using Cosmix.Application.Attendance.WorkTreatments;
using Microsoft.Extensions.DependencyInjection;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public static class WorkTreatmentRegistration
{
    public static IServiceCollection AddWorkTreatments(this IServiceCollection services) => services
        .AddScoped<IWorkTreatmentRepository, WorkTreatmentRepository>()
        .AddScoped<WorkTreatmentSources>()
        .AddScoped<WorkTreatmentCorrectionProofs>()
        .AddScoped<WorkTreatmentEvidence>()
        .AddScoped<WorkTreatmentService>()
        .AddScoped<IWorkTreatmentCreditReader, WorkTreatmentCreditReader>()
        .AddScoped<IWorkTreatmentDisciplinaryOutcomeReader, WorkTreatmentDisciplinaryOutcomeReader>();
}
