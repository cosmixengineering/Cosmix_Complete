using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.PayrollAccounting;

/// <summary>Versioned classification of frozen salary expense; never changes employee pay.</summary>
public sealed class PayrollCostPlan
{
    private PayrollCostPlan() { }
    public PayrollCostPlan(Guid id, Guid batchId, string fingerprint, int revision, Guid? supersedesId,
        string proof, string maker, Guid makerEmployeeId, DateTimeOffset at)
    {
        Id = ModelGuard.Id(id, nameof(id)); BatchId = ModelGuard.Id(batchId, nameof(batchId));
        SourceFingerprint = ModelGuard.Required(fingerprint, 64, nameof(fingerprint));
        Revision = revision > 0 ? revision : throw new ArgumentOutOfRangeException(nameof(revision));
        SupersedesId = supersedesId; ProofReference = ModelGuard.Required(proof, 1000, nameof(proof));
        CreatedBy = ModelGuard.Required(maker, 200, nameof(maker));
        CreatedByEmployeeId = ModelGuard.Id(makerEmployeeId, nameof(makerEmployeeId));
        CreatedAt = ModelGuard.Utc(at); State = "Draft";
    }
    public Guid Id { get; private set; }
    public Guid BatchId { get; private set; }
    public string SourceFingerprint { get; private set; } = "";
    public int Revision { get; private set; }
    public Guid? SupersedesId { get; private set; }
    public string ProofReference { get; private set; } = "";
    public string CreatedBy { get; private set; } = "";
    public Guid CreatedByEmployeeId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string State { get; private set; } = "";
    public string? DecidedBy { get; private set; }
    public Guid? DecidedByEmployeeId { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public string? DecisionProofReference { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void Decide(bool approve, string actor, Guid employeeId, DateTimeOffset at, string proof)
    {
        if (State != "Draft" || actor == CreatedBy || employeeId == CreatedByEmployeeId || at < CreatedAt)
            throw new InvalidOperationException("A different authorized employee must review a draft cost plan.");
        DecidedBy = ModelGuard.Required(actor, 200, nameof(actor));
        DecidedByEmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        DecidedAt = ModelGuard.Utc(at); DecisionProofReference = ModelGuard.Required(proof, 1000, nameof(proof));
        State = approve ? "Approved" : "Rejected";
    }
}
