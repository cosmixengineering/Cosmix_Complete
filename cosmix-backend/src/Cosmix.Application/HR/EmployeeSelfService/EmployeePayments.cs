using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollAccounting;

namespace Cosmix.Application.HR.EmployeeSelfService;

public sealed partial class EmployeeSelfService
{
    public Task<EmployeePaymentPage> PaymentsAsync(int page, int pageSize, AuthRequestContext context, CancellationToken ct) =>
        ReadFinancialAsync((employeeId, token) =>
            (settlements ?? throw AttendanceOperation.Conflict("payment_history_reader_unavailable"))
                .OwnPageInTransactionAsync(employeeId, page, pageSize, token), context, ct);

    public Task<EmployeePaymentPeriod> PaymentPeriodAsync(Guid batchId, int page, int pageSize, AuthRequestContext context, CancellationToken ct) =>
        ReadFinancialAsync((employeeId, token) =>
            (settlements ?? throw AttendanceOperation.Conflict("payment_history_reader_unavailable"))
                .OwnPeriodInTransactionAsync(batchId, employeeId, page, pageSize, token), context, ct);
}
