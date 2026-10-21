using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Domain.HR.PayrollHandoff;

/// <summary>Immutable source values: polymorphic SourceType/SourceId are application-validated trace references; they are not an unrestricted query or a database FK.</summary>
public sealed class PayrollHandoffLine : IAppendOnlyEntity
{
    private PayrollHandoffLine() { }

    public PayrollHandoffLine(Guid payrollHandoffBatchId, Guid employeeId, PayrollComponentKind component, string sourceType, Guid sourceId, int sourceVersion, Guid attendancePeriodLockEventId, decimal amount, string currency, Guid? policyVersionId, Guid? approvalRequestId, string calculationPrivateReference, string traceId)
    {
        Id = Guid.NewGuid();
        PayrollHandoffBatchId = ModelGuard.Id(payrollHandoffBatchId, nameof(payrollHandoffBatchId));
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        Component = Enum.IsDefined(component) ? component : throw new ArgumentOutOfRangeException(nameof(component));
        SourceType = ModelGuard.Required(sourceType, 100, nameof(sourceType));
        SourceId = ModelGuard.Id(sourceId, nameof(sourceId));
        SourceVersion = HrModelGuard.Positive(sourceVersion, nameof(sourceVersion));
        AttendancePeriodLockEventId = ModelGuard.Id(attendancePeriodLockEventId, nameof(attendancePeriodLockEventId));
        Amount = HrModelGuard.SignedMoney(amount, nameof(amount));
        Currency = HrModelGuard.Currency(currency);
        PolicyVersionId = policyVersionId.HasValue ? ModelGuard.Id(policyVersionId.Value, nameof(policyVersionId)) : null;
        ApprovalRequestId = approvalRequestId.HasValue ? ModelGuard.Id(approvalRequestId.Value, nameof(approvalRequestId)) : null;
        CalculationPrivateReference = ModelGuard.Required(calculationPrivateReference, 500, nameof(calculationPrivateReference));
        TraceId = ModelGuard.Required(traceId, 100, nameof(traceId));
    }

    public Guid Id { get; private set; }
    public Guid PayrollHandoffBatchId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public PayrollComponentKind Component { get; private set; }
    public string SourceType { get; private set; } = string.Empty;
    public Guid SourceId { get; private set; }
    public int SourceVersion { get; private set; }
    public Guid AttendancePeriodLockEventId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public Guid? PolicyVersionId { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
    public string CalculationPrivateReference { get; private set; } = string.Empty;
    public string TraceId { get; private set; } = string.Empty;
    public PayrollHandoffBatch PayrollHandoffBatch { get; private set; } = null!;
    public Employee Employee { get; private set; } = null!;
    public PolicyVersion? PolicyVersion { get; private set; }
    public ApprovalRequest? ApprovalRequest { get; private set; }
    public AttendancePeriodLockEvent AttendancePeriodLockEvent { get; private set; } = null!;
}
