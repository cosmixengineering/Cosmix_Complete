namespace NOIR.Domain.Entities.Stock;

/// <summary>
/// Lifecycle status of a stock indent (purchase requisition).
/// </summary>
public enum IndentStatus
{
    /// <summary>Being edited by the requester.</summary>
    Draft = 0,

    /// <summary>Sent for approval.</summary>
    Submitted = 1,

    /// <summary>Approved by an approver.</summary>
    Approved = 2,

    /// <summary>Rejected by an approver.</summary>
    Rejected = 3,

    /// <summary>Cancelled by the requester.</summary>
    Cancelled = 4
}

/// <summary>
/// Lifecycle status of an inter-location stock transfer.
/// </summary>
public enum TransferStatus
{
    /// <summary>Being edited by the requester.</summary>
    Draft = 0,

    /// <summary>Sent for approval.</summary>
    Submitted = 1,

    /// <summary>Approved by an approver.</summary>
    Approved = 2,

    /// <summary>Rejected by an approver.</summary>
    Rejected = 3,

    /// <summary>Cancelled by the requester.</summary>
    Cancelled = 4,

    /// <summary>Transfer executed and stock moved between locations.</summary>
    Completed = 5
}
