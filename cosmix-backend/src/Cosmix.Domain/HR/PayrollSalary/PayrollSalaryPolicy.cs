using System.Text.Json;
using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Domain.HR.PayrollSalary;

public sealed class PayrollSalaryPolicy : IAppendOnlyEntity
{
    private PayrollSalaryPolicy() { }
    public PayrollSalaryPolicy(Guid id, int revision, DateOnly effectiveFrom, PayrollSalaryPolicyRules rules,
        Guid? supersedesId, Guid approvalRequestId, string recordedBy, DateTimeOffset recordedAt)
    {
        rules.Validate();
        if (revision < 1 || effectiveFrom.Day != 1) throw new ArgumentException("Salary policy starts at a month boundary.");
        Id = ModelGuard.Id(id, nameof(id)); Revision = revision; EffectiveFrom = effectiveFrom;
        RulesJson = JsonSerializer.Serialize(rules); SupersedesId = supersedesId;
        ApprovalRequestId = ModelGuard.Id(approvalRequestId, nameof(approvalRequestId));
        RecordedBy = ModelGuard.Required(recordedBy, 200, nameof(recordedBy)); RecordedAt = ModelGuard.Utc(recordedAt);
    }
    public Guid Id { get; private set; }
    public int Revision { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public string RulesJson { get; private set; } = "";
    public Guid? SupersedesId { get; private set; }
    public Guid ApprovalRequestId { get; private set; }
    public string RecordedBy { get; private set; } = "";
    public DateTimeOffset RecordedAt { get; private set; }
    public ApprovalRequest ApprovalRequest { get; private set; } = null!;
}
