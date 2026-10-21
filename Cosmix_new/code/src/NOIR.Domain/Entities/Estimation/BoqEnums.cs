namespace NOIR.Domain.Entities.Estimation;

/// <summary>
/// Lifecycle status of a Bill of Quantities.
/// </summary>
public enum BoqStatus
{
    /// <summary>Initial state. Editable.</summary>
    Draft = 0,

    /// <summary>Submitted for approval. Locked for edits.</summary>
    UnderReview = 1,

    /// <summary>Approved by a reviewer.</summary>
    Approved = 2,

    /// <summary>Rejected by a reviewer. Can be revised and resubmitted.</summary>
    Rejected = 3
}
