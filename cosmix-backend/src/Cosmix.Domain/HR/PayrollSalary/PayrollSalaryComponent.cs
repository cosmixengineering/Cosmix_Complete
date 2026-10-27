using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.HR.PayrollSalary;

public sealed class PayrollSalaryComponent : IAppendOnlyEntity
{
    private PayrollSalaryComponent() { }
    public PayrollSalaryComponent(Guid id, Guid employeeId, string code, PayrollSalaryComponentKind kind, decimal amount,
        string currency, DateOnly effectiveFrom, DateOnly? effectiveToExclusive, bool prorateWithBase, int revision,
        Guid? supersedesId, Guid? sourceImportId, Guid approvalRequestId, string recordedBy, DateTimeOffset recordedAt)
    {
        if (!Enum.IsDefined(kind) || effectiveToExclusive <= effectiveFrom || revision < 1 ||
            kind is PayrollSalaryComponentKind.OneTimeEarning or PayrollSalaryComponentKind.OneTimeDeduction &&
                (prorateWithBase || effectiveToExclusive.HasValue)) throw new ArgumentException("Invalid component terms.");
        Id = ModelGuard.Id(id, nameof(id)); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        Code = ModelGuard.Code(code, 40); Kind = kind; Amount = HrModelGuard.Money(amount, nameof(amount));
        Currency = HrModelGuard.Currency(currency); EffectiveFrom = effectiveFrom; EffectiveToExclusive = effectiveToExclusive;
        ProrateWithBase = prorateWithBase; Revision = revision; SupersedesId = supersedesId; SourceImportId = sourceImportId;
        ApprovalRequestId = ModelGuard.Id(approvalRequestId, nameof(approvalRequestId));
        RecordedBy = ModelGuard.Required(recordedBy, 200, nameof(recordedBy)); RecordedAt = ModelGuard.Utc(recordedAt);
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string Code { get; private set; } = "";
    public PayrollSalaryComponentKind Kind { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "";
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveToExclusive { get; private set; }
    public bool ProrateWithBase { get; private set; }
    public int Revision { get; private set; }
    public Guid? SupersedesId { get; private set; }
    public Guid? SourceImportId { get; private set; }
    public Guid ApprovalRequestId { get; private set; }
    public string RecordedBy { get; private set; } = "";
    public DateTimeOffset RecordedAt { get; private set; }
    public ApprovalRequest ApprovalRequest { get; private set; } = null!;
    public Employee Employee { get; private set; } = null!;
}
