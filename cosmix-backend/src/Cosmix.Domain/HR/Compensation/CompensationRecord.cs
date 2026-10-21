using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.HR.Compensation;

/// <summary>An effective-dated restricted compensation version. Approved revisions append a new version, preserving old payroll evidence.</summary>
public sealed class CompensationRecord : IAppendOnlyEntity, IEmployeeProfileRecord
{
    private CompensationRecord() { }

    public CompensationRecord(Guid employeeId, int revision, Guid? supersedesCompensationRecordId, decimal baseAmount, decimal grossAmount, string currency, WageBasis wageBasis, PaymentMethod paymentMethod, DateOnly effectiveFrom, DateOnly? effectiveTo, Guid? approvalRequestId, string recordedByUserId, DateTimeOffset recordedAt)
    {
        ModelGuard.EffectiveDates(effectiveFrom, effectiveTo);
        if (grossAmount < baseAmount) throw new ArgumentException("Gross compensation cannot be below base compensation.");
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        Revision = HrModelGuard.Positive(revision, nameof(revision));
        SupersedesCompensationRecordId = supersedesCompensationRecordId.HasValue ? ModelGuard.Id(supersedesCompensationRecordId.Value, nameof(supersedesCompensationRecordId)) : null;
        BaseAmount = HrModelGuard.Money(baseAmount, nameof(baseAmount));
        GrossAmount = HrModelGuard.Money(grossAmount, nameof(grossAmount));
        Currency = HrModelGuard.Currency(currency);
        WageBasis = Enum.IsDefined(wageBasis) ? wageBasis : throw new ArgumentOutOfRangeException(nameof(wageBasis));
        PaymentMethod = Enum.IsDefined(paymentMethod) ? paymentMethod : throw new ArgumentOutOfRangeException(nameof(paymentMethod));
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        ApprovalRequestId = approvalRequestId.HasValue ? ModelGuard.Id(approvalRequestId.Value, nameof(approvalRequestId)) : null;
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public int Revision { get; private set; }
    public Guid? SupersedesCompensationRecordId { get; private set; }
    public decimal BaseAmount { get; private set; }
    public decimal GrossAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public WageBasis WageBasis { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public ApprovalRequest? ApprovalRequest { get; private set; }
    public CompensationRecord? SupersedesCompensationRecord { get; private set; }
}
