namespace NOIR.Domain.Entities.Manufacturing;

/// <summary>
/// Bill of material (BOM): describes the components required to manufacture
/// a finished good. The aggregate root of the manufacturing component list.
/// </summary>
public class BillOfMaterial : TenantAggregateRoot<Guid>
{
    // Identity
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    // Finished good produced by this BOM (plain reference, no navigation)
    public Guid ProductId { get; private set; }

    // Output
    public decimal OutputQuantity { get; private set; } = 1;
    public string Unit { get; private set; } = "pcs";

    // State
    public bool IsActive { get; private set; } = true;
    public string? Description { get; private set; }

    // Components
    private readonly List<BillOfMaterialItem> _items = [];
    public IReadOnlyCollection<BillOfMaterialItem> Items => _items;

    // Private constructor for EF Core
    private BillOfMaterial() : base()
    {
    }

    private BillOfMaterial(Guid id, string? tenantId) : base(id, tenantId)
    {
    }

    /// <summary>
    /// Factory method to create a new bill of material.
    /// </summary>
    public static BillOfMaterial Create(
        string code,
        string name,
        Guid productId,
        decimal outputQuantity,
        string unit,
        string? description,
        string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("BOM code is required.");

        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("BOM name is required.");

        if (productId == Guid.Empty)
            throw new InvalidOperationException("Product is required.");

        if (outputQuantity <= 0)
            throw new InvalidOperationException("Output quantity must be greater than zero.");

        return new BillOfMaterial(Guid.NewGuid(), tenantId)
        {
            Code = code.Trim(),
            Name = name.Trim(),
            ProductId = productId,
            OutputQuantity = outputQuantity,
            Unit = string.IsNullOrWhiteSpace(unit) ? "pcs" : unit.Trim(),
            Description = description
        };
    }

    /// <summary>
    /// Adds a component item to this BOM.
    /// </summary>
    public void AddItem(BillOfMaterialItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_items.Any(i => i.Id == item.Id))
            throw new InvalidOperationException("The component is already part of this BOM.");

        _items.Add(item);
    }

    /// <summary>
    /// Removes a component item from this BOM.
    /// </summary>
    public void RemoveItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("BOM item not found.");

        _items.Remove(item);
    }

    /// <summary>
    /// Updates the BOM header (component list is managed separately).
    /// </summary>
    public void UpdateHeader(
        string name,
        Guid productId,
        decimal outputQuantity,
        string unit,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("BOM name is required.");

        if (productId == Guid.Empty)
            throw new InvalidOperationException("Product is required.");

        if (outputQuantity <= 0)
            throw new InvalidOperationException("Output quantity must be greater than zero.");

        Name = name.Trim();
        ProductId = productId;
        OutputQuantity = outputQuantity;
        Unit = string.IsNullOrWhiteSpace(unit) ? "pcs" : unit.Trim();
        Description = description;
    }

    /// <summary>
    /// Marks the BOM as active (usable for production orders).
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Marks the BOM as inactive (cannot be used for new production orders).
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Ensures the BOM is complete enough to drive a production order.
    /// Used when releasing/creating production orders.
    /// </summary>
    public void ValidateComplete()
    {
        if (_items.Count == 0)
            throw new InvalidOperationException("BOM must have at least one component.");
    }
}
