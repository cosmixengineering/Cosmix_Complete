namespace NOIR.Domain.Entities.Manufacturing;

/// <summary>
/// Lifecycle status of a production order.
/// </summary>
public enum ProductionOrderStatus
{
    /// <summary>Order is being prepared and can still be edited.</summary>
    Draft = 0,

    /// <summary>Order has been released to the shop floor.</summary>
    Released = 1,

    /// <summary>Production is actively running.</summary>
    InProgress = 2,

    /// <summary>Production finished and the order was completed.</summary>
    Completed = 3,

    /// <summary>Order was cancelled before completion.</summary>
    Cancelled = 4
}
