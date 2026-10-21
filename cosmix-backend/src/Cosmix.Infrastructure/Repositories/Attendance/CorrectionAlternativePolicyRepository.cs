using Cosmix.Application.Attendance.Corrections;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class CorrectionAlternativePolicyRepository(CosmixDbContext db) : ICorrectionAlternativePolicyRepository
{
    public async Task<IReadOnlyList<AuditEvent>> HistoryAsync(CancellationToken ct) => await db.AuditEvents.AsNoTracking()
        .Where(x => x.EventType == CorrectionAlternativePolicyService.ConfigureOperation)
        .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Take(4097).ToArrayAsync(ct);
    public async Task<IReadOnlyList<EmployeeDocument>> DocumentsAsync(Guid employee, IReadOnlyList<Guid> ids, CancellationToken ct) =>
        await db.EmployeeDocuments.AsNoTracking().Where(x => x.EmployeeId == employee && ids.Contains(x.Id)).Take(9).ToArrayAsync(ct);
}
