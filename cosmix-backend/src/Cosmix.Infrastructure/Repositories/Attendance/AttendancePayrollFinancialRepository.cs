using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class AttendancePayrollFinancialRepository(CosmixDbContext db) : IAttendancePayrollFinancialRepository
{
    public async Task<IReadOnlyList<AttendancePayrollFinancialHistoryRow>> HistoryAsync(Guid employeeId,
        DateOnly sourceMonth, CancellationToken ct) => await (
        from adjustment in db.Set<AttendancePayrollFinancialAdjustment>().AsNoTracking()
        where adjustment.EmployeeId == employeeId && adjustment.SourceMonth == sourceMonth
        join resolution in db.Set<AttendancePayrollFinancialResolution>().AsNoTracking()
            on adjustment.Id equals resolution.AdjustmentId into terminal
        from resolution in terminal.DefaultIfEmpty()
        orderby adjustment.Revision
        select new AttendancePayrollFinancialHistoryRow(adjustment, resolution))
        .Take(AttendancePayrollFinancialRules.MaximumAdjustments + 1).ToArrayAsync(ct);

    public async Task<IReadOnlyList<AttendancePayrollFinancialHistoryRow>> TargetAsync(DateOnly targetMonth,
        CancellationToken ct) => await (
        from adjustment in db.Set<AttendancePayrollFinancialAdjustment>().AsNoTracking()
        where adjustment.TargetMonth == targetMonth
        join resolution in db.Set<AttendancePayrollFinancialResolution>().AsNoTracking()
            on adjustment.Id equals resolution.AdjustmentId into terminal
        from resolution in terminal.DefaultIfEmpty()
        orderby adjustment.EmployeeId, adjustment.SourceMonth, adjustment.Revision
        select new AttendancePayrollFinancialHistoryRow(adjustment, resolution))
        .Take(4097).ToArrayAsync(ct);

    public async Task<bool> ReceiptExistsAsync(string receiptKey, CancellationToken ct) =>
        await db.Set<AttendancePayrollFinancialResolution>().AnyAsync(x => x.ExternalReceiptKey == receiptKey, ct) ||
        await db.Set<AttendancePayrollAttestation>().AnyAsync(x => x.ExternalReceiptKey == receiptKey && x.Step >= 2, ct);

    public void Add(AttendancePayrollFinancialAdjustment adjustment) => db.Set<AttendancePayrollFinancialAdjustment>().Add(adjustment);
    public void Add(AttendancePayrollFinancialResolution resolution) => db.Set<AttendancePayrollFinancialResolution>().Add(resolution);
}
