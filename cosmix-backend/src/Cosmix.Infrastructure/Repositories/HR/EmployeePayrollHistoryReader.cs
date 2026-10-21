using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.EmployeeSelfService;
using Cosmix.Domain.HR.PayrollHandoff;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed partial class EmployeeSelfServiceRepository
{
    public async Task<PagedResult<EssPayrollHandoffSummary>> GetPayrollHistoryAsync(Guid employeeId,
        int page, int pageSize, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || page is < 1 or > 4096 || pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(page));
        // Identity restricts both the count and every component before grouping.
        var source = db.PayrollHandoffLines.AsNoTracking().Where(line => line.EmployeeId == employeeId);
        var count = await source.Select(line => line.PayrollHandoffBatchId).Distinct().CountAsync(ct);
        var rows = await source.GroupBy(line => line.PayrollHandoffBatch)
            .Select(group => new
            {
                Batch = group.Key,
                Earnings = group.Where(line => line.Component == PayrollComponentKind.BaseCompensation ||
                    line.Component == PayrollComponentKind.Overtime || line.Component == PayrollComponentKind.Allowance ||
                    line.Component == PayrollComponentKind.FuelClaim).Sum(line => line.Amount),
                Deductions = group.Where(line => line.Component == PayrollComponentKind.AttendanceDeduction ||
                    line.Component == PayrollComponentKind.LeaveAdjustment || line.Component == PayrollComponentKind.LoanRepayment)
                    .Sum(line => line.Amount),
                Net = group.Sum(line => line.Amount),
                Currency = group.Select(line => line.Currency).FirstOrDefault() ?? "PKR"
            }).OrderByDescending(row => row.Batch.PeriodEnd).ThenBy(row => row.Batch.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(rows.Select(row => new EssPayrollHandoffSummary(row.Batch.Id, row.Batch.BatchReference,
            row.Batch.PeriodStart, row.Batch.PeriodEnd, row.Earnings, row.Deductions, row.Net, row.Currency,
            row.Batch.PreparedAt)).ToArray(), count, page, pageSize);
    }
}
