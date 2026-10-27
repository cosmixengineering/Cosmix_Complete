using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class AttendancePayrollActivationRepository(CosmixDbContext db) : IAttendancePayrollActivationRepository
{
    public async Task<IReadOnlyList<AttendancePayrollActivationRow>> HistoryAsync(Guid sourceRevisionId, CancellationToken ct)
    {
        var requests = await db.Set<ApprovalRequest>()
            .Where(x => x.RequestType == AttendancePayrollActivationService.RequestType && x.SubjectId == sourceRevisionId)
            .OrderBy(x => x.RequestedAt).ThenBy(x => x.Id).Take(33).ToArrayAsync(ct);
        var ids = requests.Select(x => x.Id).ToArray();
        var decisions = await db.Set<ApprovalDecision>().AsNoTracking().Where(x => ids.Contains(x.ApprovalRequestId))
            .Take(67).ToArrayAsync(ct);
        return requests.Select(x => new AttendancePayrollActivationRow(x, decisions.Where(d => d.ApprovalRequestId == x.Id).ToArray())).ToArray();
    }
    public void Add(ApprovalRequest request) { RequireTransaction(); db.Set<ApprovalRequest>().Add(request); }
    public void Add(ApprovalDecision decision) { RequireTransaction(); db.Set<ApprovalDecision>().Add(decision); }
    public void ExpectVersion(ApprovalRequest request, byte[] version)
    {
        RequireTransaction();
        db.Entry(request).Property(x => x.RowVersion).OriginalValue = version;
    }
    public async Task SaveAsync(CancellationToken ct) { RequireTransaction(); await db.SaveChangesAsync(ct); }
    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Payroll activation requires the shared authorization transaction.");
    }
}
