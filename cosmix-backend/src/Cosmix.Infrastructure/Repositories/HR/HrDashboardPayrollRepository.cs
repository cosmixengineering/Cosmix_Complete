using Cosmix.Application.HR.Compensation;
using Cosmix.Application.HR.Dashboard;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

/// <summary>Bounded, read-only sources for the already-authorized active employee population.
/// Salary resolution stays with EmployeeCompensationRepository. No payroll-input calculation,
/// batch preparation, lock, settlement or payment is performed by this reader.</summary>
public sealed class HrDashboardPayrollRepository(CosmixDbContext db) : IHrDashboardPayrollRepository
{
    private const int MaximumBatches = 2048;
    private const int MaximumLines = 65536;

    public async Task<HrDashboardPayrollSources> ReadAsync(IReadOnlyList<Guid> employeeIds,
        DateOnly businessDate, DateOnly monthStart, DateOnly monthEndExclusive, CancellationToken ct)
    {
        if (employeeIds.Count == 0) return new(0, 0, [], []);
        var ids = employeeIds.ToArray();
        var future = await db.CompensationRecords.AsNoTracking()
            .CountAsync(x => ids.Contains(x.EmployeeId) && x.EffectiveFrom > businessDate, ct);
        var pending = await db.ApprovalRequests.AsNoTracking().CountAsync(x =>
            x.RequestType == EmployeeCompensationService.RequestType && x.State == ApprovalState.Pending &&
            ids.Contains(x.SubjectId), ct);
        var batches = await db.PayrollHandoffBatches.AsNoTracking()
            .Where(x => x.PeriodStart == monthStart && x.PeriodEnd == monthEndExclusive &&
                x.ReviewState != "returned" && x.ReviewState != "cancelled" && x.ReviewState != "rejected" &&
                x.Lines.Any(line => ids.Contains(line.EmployeeId)))
            .OrderBy(x => x.Id).Take(MaximumBatches + 1).ToArrayAsync(ct);
        if (batches.Length > MaximumBatches) throw EmployeeOperation.Conflict("hr_dashboard_handoff_source_limit");
        var batchIds = batches.Select(x => x.Id).ToArray();
        var lines = batchIds.Length == 0 ? [] : await db.PayrollHandoffLines.AsNoTracking()
            .Where(x => batchIds.Contains(x.PayrollHandoffBatchId) && ids.Contains(x.EmployeeId))
            .OrderBy(x => x.Id).Take(MaximumLines + 1).ToArrayAsync(ct);
        if (lines.Length > MaximumLines) throw EmployeeOperation.Conflict("hr_dashboard_handoff_source_limit");
        return new(future, pending, batches, lines);
    }
}
