using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.PayrollAccounting;

public enum PayrollAccountKind { Expense = 1, Liability = 2, Asset = 3, Bank = 4 }
public enum PayrollFinanceApprovalState { Draft = 1, Approved = 2 }
public enum PayrollAccountingPeriodState { Draft = 1, Open = 2, Closed = 3 }

public sealed class PayrollLedgerAccount
{
    private PayrollLedgerAccount() { }
    public PayrollLedgerAccount(string code, string name, PayrollAccountKind kind)
    {
        Id = Guid.NewGuid(); Code = ModelGuard.Code(code, 50); Name = ModelGuard.Required(name, 150, nameof(name));
        Kind = Enum.IsDefined(kind) ? kind : throw new ArgumentOutOfRangeException(nameof(kind)); IsActive = true;
    }
    public Guid Id { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public PayrollAccountKind Kind { get; private set; }
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void SetActive(bool active) => IsActive = active;
}

public sealed class PayrollAccountingPeriod
{
    private PayrollAccountingPeriod() { }
    public PayrollAccountingPeriod(DateOnly start, DateOnly endExclusive, string currency, string maker, DateTimeOffset at)
    {
        if (endExclusive <= start) throw new ArgumentException("Period end must follow start.");
        Id = Guid.NewGuid(); PeriodStart = start; PeriodEndExclusive = endExclusive; Currency = HrModelGuard.Currency(currency);
        CreatedByUserId = ModelGuard.Required(maker, 200, nameof(maker)); CreatedAt = ModelGuard.Utc(at); State = PayrollAccountingPeriodState.Draft;
    }
    public Guid Id { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEndExclusive { get; private set; }
    public string Currency { get; private set; } = "";
    public PayrollAccountingPeriodState State { get; private set; }
    public string CreatedByUserId { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public string? ApprovedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? ClosedByUserId { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Approve(string actor, DateTimeOffset at)
    {
        if (State != PayrollAccountingPeriodState.Draft || actor == CreatedByUserId) throw new InvalidOperationException("Independent draft period approval required.");
        ApprovedByUserId = ModelGuard.Required(actor, 200, nameof(actor)); ApprovedAt = ModelGuard.Utc(at); State = PayrollAccountingPeriodState.Open;
    }
    public void Close(string actor, DateTimeOffset at)
    {
        if (State != PayrollAccountingPeriodState.Open) throw new InvalidOperationException("Only open periods can close.");
        ClosedByUserId = ModelGuard.Required(actor, 200, nameof(actor)); ClosedAt = ModelGuard.Utc(at); State = PayrollAccountingPeriodState.Closed;
    }
}

public sealed class PayrollPostingProfile
{
    private PayrollPostingProfile() { }
    public PayrollPostingProfile(string name, string currency, DateOnly effectiveFrom, DateOnly? effectiveToExclusive,
        string rulesJson, string maker, DateTimeOffset at)
    {
        if (effectiveToExclusive <= effectiveFrom) throw new ArgumentException("Invalid profile dates.");
        Id = Guid.NewGuid(); Name = ModelGuard.Required(name, 150, nameof(name)); Currency = HrModelGuard.Currency(currency);
        EffectiveFrom = effectiveFrom; EffectiveToExclusive = effectiveToExclusive; RulesJson = HrModelGuard.JsonObject(rulesJson, 30000, nameof(rulesJson));
        CreatedByUserId = ModelGuard.Required(maker, 200, nameof(maker)); CreatedAt = ModelGuard.Utc(at); State = PayrollFinanceApprovalState.Draft;
    }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Currency { get; private set; } = "";
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveToExclusive { get; private set; }
    public string RulesJson { get; private set; } = "";
    public PayrollFinanceApprovalState State { get; private set; }
    public string CreatedByUserId { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public string? ApprovedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Approve(string actor, DateTimeOffset at)
    {
        if (State != PayrollFinanceApprovalState.Draft || actor == CreatedByUserId) throw new InvalidOperationException("Independent draft profile approval required.");
        ApprovedByUserId = ModelGuard.Required(actor, 200, nameof(actor)); ApprovedAt = ModelGuard.Utc(at); State = PayrollFinanceApprovalState.Approved;
    }
}
