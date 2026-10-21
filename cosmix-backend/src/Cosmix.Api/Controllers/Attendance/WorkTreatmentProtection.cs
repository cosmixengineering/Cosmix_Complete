using Cosmix.Api.Attendance;
using Cosmix.Application.Attendance.WorkTreatments;

namespace Cosmix.Api.Controllers.Attendance;

public static class WorkTreatmentProtection
{
    public static IServiceCollection AddWorkTreatmentSourceProtection(this IServiceCollection services) =>
        services.AddSingleton<IWorkTreatmentProposalProtector, WorkTreatmentTicketProtector>();
}
