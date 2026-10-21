namespace NOIR.Domain.Entities.Manufacturing;

/// <summary>
/// A single component line within a bill of material.
/// </summary>
public class BillOfMaterialItem : Entity<Guid>
{
    public Guid BillOfMaterialId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = "pcs";

    /// <summary>Optional scrap/waste percentage applied on top of <see cref="Quantity"/>.</summary>
    public decimal? WastePercent { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>
    /// Computed quantity including waste. Not mapped - ignored in EF configuration.
    /// </summary>
    public decimal EffectiveQuantity =>
        WastePercent is null or 0
            ? Quantity
            : Quantity * (1 + WastePercent.Value / 100);

    // Private constructor for EF Core
    private BillOfMaterialItem()
    {
    }

    /// <summary>
    /// Factory method to create a BOM component item.
    /// </summary>
    public static BillOfMaterialItem Create(
        Guid billOfMaterialId,
        Guid productId,
        decimal quantity,
        string unit,
        decimal? wastePercent,
        string? notes)
    {
        if (billOfMaterialId == Guid.Empty)
            throw new InvalidOperationException("Bill of material is required.");

        if (productId == Guid.Empty)
            throw new InvalidOperationException("Component product is required.");

        if (quantity < 0)
            throw new InvalidOperationException("Quantity cannot be negative.");

        var item = new BillOfMaterialItem
        {
            Id = Guid.NewGuid(),
            BillOfMaterialId = billOfMaterialId,
            ProductId = productId,
            Quantity = quantity,
            Unit = string.IsNullOrWhiteSpace(unit) ? "pcs" : unit.Trim(),
            WastePercent = wastePercent,
            Notes = notes
        };

        return item;
    }

    /// <summary>
    /// Updates the component line values.
    /// </summary>
    public void Update(
        decimal quantity,
        string unit,
        decimal? wastePercent,
        string? notes)
    {
        if (quantity < 0)
            throw new InvalidOperationException("Quantity cannot be negative.");

        Quantity = quantity;
        Unit = string.IsNullOrWhiteSpace(unit) ? "pcs" : unit.Trim();
        WastePercent = wastePercent;
        Notes = notes;
    }
}
