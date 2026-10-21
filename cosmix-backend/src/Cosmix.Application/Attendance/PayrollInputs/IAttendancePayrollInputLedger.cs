using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.PayrollInputs;

namespace Cosmix.Application.Attendance.PayrollInputs;

public interface IAttendancePayrollInputLedger
{
    Task<bool> IsLockedAsync(DateOnly monthStart, CancellationToken ct);
    Task<AttendancePayrollInputRevision?> LatestAsync(Guid employeeId, DateOnly monthStart, CancellationToken ct);
    Task<AttendancePayrollInputRevision?> FindAsync(Guid employeeId, Guid id, CancellationToken ct);
    Task<PagedResult<AttendancePayrollInputRevision>> HistoryAsync(Guid employeeId, DateOnly monthStart,
        int page, int pageSize, CancellationToken ct);
    void Add(AttendancePayrollInputRevision revision);
}
