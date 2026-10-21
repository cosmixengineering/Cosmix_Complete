using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.WorkTreatments;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class WorkTreatmentProposalProtector(Func<string, string> protect, Func<string, string> unprotect) : IWorkTreatmentProposalProtector
{
    public string Protect(WorkTreatmentTicket ticket) => protect(JsonSerializer.Serialize(ticket));
    public WorkTreatmentTicket Unprotect(string ticket) {
        try { return JsonSerializer.Deserialize<WorkTreatmentTicket>(unprotect(ticket)) ?? throw AttendanceOperation.Invalid("invalid_work_source_ticket"); }
        catch (System.Security.Cryptography.CryptographicException) { throw AttendanceOperation.Invalid("invalid_work_source_ticket"); }
        catch (JsonException) { throw AttendanceOperation.Invalid("invalid_work_source_ticket"); }
    }
}
