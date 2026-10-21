namespace NOIR.Domain.Entities.Estimation;

/// <summary>
/// Immutable snapshot of a BOQ total at approval time.
/// Captures the previous and new totals for audit history.
/// </summary>
public class BoqRevision : Entity<Guid>
{
    private BoqRevision() : base() { }

    private BoqRevision(Guid id) : base(id) { }

    /// <summary>Parent BOQ ID.</summary>
    public Guid BoqId { get; private set; }

    /// <summary>Sequential revision number (1-based).</summary>
    public int RevisionNumber { get; private set; }

    /// <summary>Optional reason/context for the revision.</summary>
    public string? Reason { get; private set; }

    /// <summary>Total before this revision (0 on first revision).</summary>
    public decimal PreviousTotal { get; private set; }

    /// <summary>Total at the time of this revision.</summary>
    public decimal NewTotal { get; private set; }

    /// <summary>User who created the revision.</summary>
    public string? CreatedByUserId { get; private set; }

    /// <summary>
    /// Creates a revision snapshot. CreatedAt is stamped by the base Entity constructor.
    /// </summary>
    public static BoqRevision Create(
        Guid boqId,
        int revisionNumber,
        decimal previousTotal,
        decimal newTotal,
        string? reason,
        string? createdByUserId)
    {
        return new BoqRevision(Guid.NewGuid())
        {
            BoqId = boqId,
            RevisionNumber = revisionNumber,
            PreviousTotal = previousTotal,
            NewTotal = newTotal,
            Reason = reason?.Trim(),
            CreatedByUserId = createdByUserId
        };
    }
}
