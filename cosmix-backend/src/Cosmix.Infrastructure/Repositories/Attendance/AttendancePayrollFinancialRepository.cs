using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class AttendancePayrollFinancialRepository(CosmixDbContext db) : IAttendancePayrollFinancialRepository
{
    private IQueryable<AttendancePayrollFinancialHistoryRow> Rows() =>
        from adjustment in db.Set<AttendancePayrollFinancialAdjustment>().AsNoTracking()
        join resolution in db.Set<AttendancePayrollFinancialResolution>().AsNoTracking()
            on adjustment.Id equals resolution.AdjustmentId into terminal
        from resolution in terminal.DefaultIfEmpty()
        select new AttendancePayrollFinancialHistoryRow(adjustment, resolution);

    public async Task<IReadOnlyList<AttendancePayrollFinancialHistoryRow>> HistoryAsync(Guid employeeId,
        DateOnly sourceMonth, CancellationToken ct) => await Rows()
        .Where(x => x.Adjustment.EmployeeId == employeeId && x.Adjustment.SourceMonth == sourceMonth)
        .OrderBy(x => x.Adjustment.Revision).Take(AttendancePayrollFinancialRules.MaximumAdjustments + 1).ToArrayAsync(ct);

    public async Task<IReadOnlyList<AttendancePayrollFinancialHistoryRow>> TargetAsync(DateOnly targetMonth,
        CancellationToken ct) => await Rows().Where(x => x.Adjustment.TargetMonth == targetMonth)
        .OrderBy(x => x.Adjustment.EmployeeId).ThenBy(x => x.Adjustment.SourceMonth)
        .ThenBy(x => x.Adjustment.Revision).Take(4097).ToArrayAsync(ct);

    public async Task<bool> ReceiptExistsAsync(string receiptKey, CancellationToken ct) =>
        await db.Set<AttendancePayrollFinancialResolution>().AnyAsync(x => x.ExternalReceiptKey == receiptKey, ct) ||
        await db.Set<AttendancePayrollAttestation>().AnyAsync(x => x.ExternalReceiptKey == receiptKey && x.Step >= 2, ct);

    public void Add(AttendancePayrollFinancialAdjustment adjustment) => db.Set<AttendancePayrollFinancialAdjustment>().Add(adjustment);
    public void Add(AttendancePayrollFinancialResolution resolution) => db.Set<AttendancePayrollFinancialResolution>().Add(resolution);
}
