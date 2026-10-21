namespace NOIR.Domain.Entities.Estimation;

/// <summary>
/// A single line item within a Bill of Quantities.
/// Amount is denormalized as Quantity * UnitRate.
/// </summary>
public class BoqItem : TenantEntity<Guid>
{
    private BoqItem() : base() { }

    private BoqItem(Guid id, string? tenantId) : base(id, tenantId) { }

    /// <summary>Parent BOQ ID.</summary>
    public Guid BoqId { get; private set; }

    /// <summary>Section/work package this item belongs to (e.g., "Civil Works").</summary>
    public string Section { get; private set; } = string.Empty;

    /// <summary>Item code within the estimate (e.g., "EXC-001").</summary>
    public string ItemCode { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }

    /// <summary>Unit of measure (e.g., "m3", "ea", "ls").</summary>
    public string Unit { get; private set; } = string.Empty;

    public decimal UnitRate { get; private set; }

    /// <summary>Line amount: Quantity * UnitRate (computed in Create/Update).</summary>
    public decimal Amount { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>
    /// Creates a new BOQ line item with the amount precomputed.
    /// </summary>
    public static BoqItem Create(
        Guid boqId,
        string section,
        string itemCode,
        string description,
        decimal quantity,
        string unit,
        decimal unitRate,
        int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemCode);

        if (quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than zero.");

        if (unitRate < 0)
            throw new InvalidOperationException("Unit rate cannot be negative.");

        return new BoqItem(Guid.NewGuid(), tenantId: null)
        {
            BoqId = boqId,
            Section = section.Trim(),
            ItemCode = itemCode.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Quantity = quantity,
            Unit = unit?.Trim() ?? string.Empty,
            UnitRate = unitRate,
            Amount = quantity * unitRate,
            SortOrder = sortOrder
        };
    }

    /// <summary>
    /// Updates the line item and recomputes the amount.
    /// </summary>
    public void Update(
        string section,
        string itemCode,
        string description,
        decimal quantity,
        string unit,
        decimal unitRate,
        int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemCode);

        if (quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than zero.");

        if (unitRate < 0)
            throw new InvalidOperationException("Unit rate cannot be negative.");

        Section = section.Trim();
        ItemCode = itemCode.Trim();
        Description = description?.Trim() ?? string.Empty;
        Quantity = quantity;
        Unit = unit?.Trim() ?? string.Empty;
        UnitRate = unitRate;
        Amount = quantity * unitRate;
        SortOrder = sortOrder;
    }
}
