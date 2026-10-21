namespace NOIR.Domain.Entities.Manufacturing;

/// <summary>
/// Production order: an instruction to manufacture a quantity of a finished good
/// from a bill of material, with progress and material issue tracking.
/// </summary>
public class ProductionOrder : TenantAggregateRoot<Guid>
{
    // Identity
    public string OrderNumber { get; private set; } = string.Empty;

    // Source
    public Guid BillOfMaterialId { get; private set; }
    public Guid ProductId { get; private set; }

    // Quantities
    public decimal PlannedQuantity { get; private set; }
    public decimal ProducedQuantity { get; private set; } = 0;
    public decimal ScrapQuantity { get; private set; } = 0;

    // Lifecycle
    public ProductionOrderStatus Status { get; private set; } = ProductionOrderStatus.Draft;
    public DateOnly? PlannedStartDate { get; private set; }
    public DateOnly? PlannedEndDate { get; private set; }
    public string? Notes { get; private set; }

    // People
    public Guid? CreatedByUserId { get; private set; }
    public Guid? CompletedByUserId { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    // Material requirements snapshot (copied from the BOM at creation time)
    private readonly List<ProductionOrderItem> _items = [];
    public IReadOnlyCollection<ProductionOrderItem> Items => _items;

    // Private constructor for EF Core
    private ProductionOrder() : base()
    {
    }

    private ProductionOrder(Guid id, string? tenantId) : base(id, tenantId)
    {
    }

    /// <summary>
    /// Factory method to create a new production order in Draft status.
    /// </summary>
    public static ProductionOrder Create(
        string orderNumber,
        Guid billOfMaterialId,
        Guid productId,
        decimal plannedQuantity,
        DateOnly? plannedStartDate,
        DateOnly? plannedEndDate,
        string? notes,
        Guid? createdByUserId,
        string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
            throw new InvalidOperationException("Order number is required.");

        if (billOfMaterialId == Guid.Empty)
            throw new InvalidOperationException("Bill of material is required.");

        if (productId == Guid.Empty)
            throw new InvalidOperationException("Product is required.");

        if (plannedQuantity <= 0)
            throw new InvalidOperationException("Planned quantity must be greater than zero.");

        if (plannedStartDate.HasValue && plannedEndDate.HasValue && plannedEndDate.Value < plannedStartDate.Value)
            throw new InvalidOperationException("Planned end date cannot be before the planned start date.");

        return new ProductionOrder(Guid.NewGuid(), tenantId)
        {
            OrderNumber = orderNumber.Trim(),
            BillOfMaterialId = billOfMaterialId,
            ProductId = productId,
            PlannedQuantity = plannedQuantity,
            PlannedStartDate = plannedStartDate,
            PlannedEndDate = plannedEndDate,
            Notes = notes,
            CreatedByUserId = createdByUserId
        };
    }

    /// <summary>
    /// Adds a material requirement line to the order snapshot.
    /// </summary>
    public void AddItem(ProductionOrderItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_items.Any(i => i.Id == item.Id))
            throw new InvalidOperationException("The item is already part of this production order.");

        _items.Add(item);
    }

    /// <summary>
    /// Removes a material requirement line from the order snapshot.
    /// </summary>
    public void RemoveItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Production order item not found.");

        _items.Remove(item);
    }

    /// <summary>
    /// Updates the order header. Only allowed while the order is a Draft.
    /// </summary>
    public void UpdateHeader(
        decimal plannedQuantity,
        DateOnly? plannedStartDate,
        DateOnly? plannedEndDate,
        string? notes)
    {
        if (Status != ProductionOrderStatus.Draft)
            throw new InvalidOperationException("Only draft production orders can be updated.");

        if (plannedQuantity <= 0)
            throw new InvalidOperationException("Planned quantity must be greater than zero.");

        if (plannedStartDate.HasValue && plannedEndDate.HasValue && plannedEndDate.Value < plannedStartDate.Value)
            throw new InvalidOperationException("Planned end date cannot be before the planned start date.");

        PlannedQuantity = plannedQuantity;
        PlannedStartDate = plannedStartDate;
        PlannedEndDate = plannedEndDate;
        Notes = notes;
    }

    /// <summary>
    /// Releases the order for production (Draft → Released).
    /// </summary>
    public void Release()
    {
        if (Status != ProductionOrderStatus.Draft)
            throw new InvalidOperationException(
                $"Only draft production orders can be released. Current status: {Status}.");

        Status = ProductionOrderStatus.Released;
    }

    /// <summary>
    /// Starts production (Released → InProgress).
    /// </summary>
    public void Start()
    {
        if (Status != ProductionOrderStatus.Released)
            throw new InvalidOperationException(
                $"Only released production orders can be started. Current status: {Status}.");

        Status = ProductionOrderStatus.InProgress;
    }

    /// <summary>
    /// Reports production progress. Quantities are increments added to the running totals.
    /// Only allowed while the order is InProgress. Completion is always explicit.
    /// </summary>
    public void ReportProgress(decimal producedQuantity, decimal scrapQuantity)
    {
        if (Status != ProductionOrderStatus.InProgress)
            throw new InvalidOperationException(
                $"Progress can only be reported on in-progress production orders. Current status: {Status}.");

        if (producedQuantity < 0 || scrapQuantity < 0)
            throw new InvalidOperationException("Progress quantities cannot be negative.");

        if (producedQuantity == 0 && scrapQuantity == 0)
            throw new InvalidOperationException("At least one progress quantity must be greater than zero.");

        ProducedQuantity += producedQuantity;
        ScrapQuantity += scrapQuantity;
    }

    /// <summary>
    /// Completes the order (InProgress → Completed). Requires produced quantity.
    /// </summary>
    public void Complete(Guid? completedByUserId)
    {
        if (Status != ProductionOrderStatus.InProgress)
            throw new InvalidOperationException(
                $"Only in-progress production orders can be completed. Current status: {Status}.");

        if (ProducedQuantity <= 0)
            throw new InvalidOperationException(
                "Cannot complete a production order without produced quantity.");

        Status = ProductionOrderStatus.Completed;
        CompletedByUserId = completedByUserId;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Cancels the order (Draft/Released/InProgress → Cancelled).
    /// </summary>
    public void Cancel(string? reason = null)
    {
        if (Status is ProductionOrderStatus.Completed or ProductionOrderStatus.Cancelled)
            throw new InvalidOperationException(
                $"Production orders in {Status} status cannot be cancelled.");

        if (!string.IsNullOrWhiteSpace(reason))
        {
            Notes = string.IsNullOrWhiteSpace(Notes)
                ? $"Cancelled: {reason.Trim()}"
                : $"{Notes}{Environment.NewLine}Cancelled: {reason.Trim()}";
        }

        Status = ProductionOrderStatus.Cancelled;
    }
}
