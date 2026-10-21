using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Attendance.PayrollInputs;

namespace Cosmix.Application.Attendance.PayrollInputs;

public sealed record AttendancePayrollReconciliationSources(IReadOnlyList<AttendancePayrollConfirmedOrdinaryDay> Days,
    IReadOnlyList<AttendanceResourceScope> OriginalScopes, IReadOnlyList<AttendancePayrollInputIssue> Issues);

/// <summary>Internal financial source boundary. Call only inside the caller's existing authorization
/// transaction; checks current session and both source/attestation read scopes without starting one.</summary>
public interface IAttendancePayrollReconciliationSourceReader
{
    Task<AttendancePayrollReconciliationSources> ReadAsync(Guid employeeId, DateOnly targetMonthStart,
        IReadOnlyList<DateOnly> priorDates, CancellationToken ct);
}
