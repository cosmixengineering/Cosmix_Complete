using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Payslips;

namespace Cosmix.Application.HR.EmployeeSelfService;
public sealed partial class EmployeeSelfService
{
    public Task<PayslipPage> PayslipsAsync(int page, int pageSize, AuthRequestContext context, CancellationToken ct) =>
        ReadFinancialAsync((employeeId, token) =>
            (payslips ?? throw AttendanceOperation.Conflict("payslip_reader_unavailable"))
                .OwnPageInTransactionAsync(employeeId, page, pageSize, token), context, ct);
    public Task<PayslipDocument> PayslipAsync(Guid batchId, string format, AuthRequestContext context, CancellationToken ct) =>
        ReadFinancialAsync((employeeId, token) =>
            (payslips ?? throw AttendanceOperation.Conflict("payslip_reader_unavailable"))
                .DocumentInTransactionAsync(batchId, employeeId, format, token), context, ct);
}
