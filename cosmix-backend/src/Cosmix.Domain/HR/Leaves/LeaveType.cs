using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.Leaves;

public sealed class LeaveType
{
    private LeaveType() { }

    public LeaveType(string code, string name, bool requiresEvidence, bool requiresAdvanceApproval, bool reducesRegularBalance, LeaveKind kind = LeaveKind.Other)
    {
        Id = Guid.NewGuid();
        Code = ModelGuard.Code(code, 40);
        Name = ModelGuard.Required(name, 200, nameof(name));
        RequiresEvidence = requiresEvidence;
        RequiresAdvanceApproval = requiresAdvanceApproval;
        ReducesRegularBalance = reducesRegularBalance;
        Kind = Enum.IsDefined(kind) ? kind : throw new ArgumentOutOfRangeException(nameof(kind));
        if ((kind is LeaveKind.Sick or LeaveKind.Exam or LeaveKind.SiteInjuryRest) && !requiresEvidence ||
            kind == LeaveKind.Casual && !requiresAdvanceApproval || kind == LeaveKind.SiteInjuryRest && reducesRegularBalance ||
            (kind is LeaveKind.Sick or LeaveKind.Casual) && !reducesRegularBalance)
            throw new ArgumentException("Leave type contradicts a confirmed SRS requirement.");
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool RequiresEvidence { get; private set; }
    public bool RequiresAdvanceApproval { get; private set; }
    public bool ReducesRegularBalance { get; private set; }
    public LeaveKind Kind { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive { get; private set; } = true;

    public void Deactivate() => IsActive = false;
}
