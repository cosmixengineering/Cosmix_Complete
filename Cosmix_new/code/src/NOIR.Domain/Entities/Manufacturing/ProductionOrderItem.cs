namespace NOIR.Domain.Entities.Manufacturing;

/// <summary>
/// A material requirement line on a production order.
/// Represents a snapshot of a BOM component, scaled to the planned quantity,
/// with issued (consumed) quantity tracking.
/// </summary>
public class ProductionOrderItem : Entity<Guid>
{
    public Guid ProductionOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal RequiredQuantity { get; private set; }
    public decimal IssuedQuantity { get; private set; } = 0;
    public string Unit { get; private set; } = "pcs";
    public string? Notes { get; private set; }

    // Private constructor for EF Core
    private ProductionOrderItem()
    {
    }

    /// <summary>
    /// Factory method to create a production order material requirement.
    /// </summary>
    public static ProductionOrderItem Create(
        Guid productionOrderId,
        Guid productId,
        decimal requiredQuantity,
        string unit,
        string? notes)
    {
        if (productionOrderId == Guid.Empty)
            throw new InvalidOperationException("Production order is required.");

        if (productId == Guid.Empty)
            throw new InvalidOperationException("Component product is required.");

        if (requiredQuantity < 0)
            throw new InvalidOperationException("Required quantity cannot be negative.");

        return new ProductionOrderItem
        {
            Id = Guid.NewGuid(),
            ProductionOrderId = productionOrderId,
            ProductId = productId,
            RequiredQuantity = requiredQuantity,
            Unit = string.IsNullOrWhiteSpace(unit) ? "pcs" : unit.Trim(),
            Notes = notes
        };
    }

    /// <summary>
    /// Updates the required quantity / unit / notes.
    /// </summary>
    public void Update(decimal requiredQuantity, string unit, string? notes)
    {
        if (requiredQuantity < 0)
            throw new InvalidOperationException("Required quantity cannot be negative.");

        RequiredQuantity = requiredQuantity;
        Unit = string.IsNullOrWhiteSpace(unit) ? "pcs" : unit.Trim();
        Notes = notes;
    }

    /// <summary>
    /// Issues (consumes) material against this requirement.
    /// The issued quantity can never exceed the required quantity.
    /// </summary>
    public void Issue(decimal quantity)
    {
        if (quantity <= 0)
            throw new InvalidOperationException("Issue quantity must be greater than zero.");

        if (IssuedQuantity + quantity > RequiredQuantity)
            throw new InvalidOperationException(
                $"Issued quantity cannot exceed required quantity ({RequiredQuantity}).");

        IssuedQuantity += quantity;
    }
}
