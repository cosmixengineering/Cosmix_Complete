namespace NOIR.Domain.Entities.Procurement;

/// <summary>
/// Procurement Purchase Order aggregate root.
/// Manages procurement lifecycle from requisition to receipt, vendor linkage, and project cost allocations.
/// </summary>
public class PurchaseOrder : TenantAggregateRoot<Guid>
{
    private readonly List<PurchaseOrderItem> _items = [];

    private PurchaseOrder() : base() { }

    private PurchaseOrder(Guid id, string? tenantId) : base(id, tenantId) { }

    public string PoNumber { get; private set; } = string.Empty;
    public Guid SupplierId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public PurchaseOrderStatus Status { get; private set; } = PurchaseOrderStatus.Draft;
    public DateTimeOffset OrderDate { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpectedDeliveryDate { get; private set; }
    public string Currency { get; private set; } = "USD";
    public decimal Subtotal { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string? PaymentTerms { get; private set; }
    public string? ShippingTerms { get; private set; }
    public string? Notes { get; private set; }

    public IReadOnlyCollection<PurchaseOrderItem> Items => _items.AsReadOnly();

    public static PurchaseOrder Create(
        string poNumber,
        Guid supplierId,
        Guid? projectId,
        DateTimeOffset? expectedDeliveryDate,
        string currency,
        string? paymentTerms,
        string? shippingTerms,
        string? notes,
        string? tenantId = null)
    {
        return new PurchaseOrder(Guid.NewGuid(), tenantId)
        {
            PoNumber = poNumber,
            SupplierId = supplierId,
            ProjectId = projectId,
            Status = PurchaseOrderStatus.Draft,
            OrderDate = DateTimeOffset.UtcNow,
            ExpectedDeliveryDate = expectedDeliveryDate,
            Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency,
            PaymentTerms = paymentTerms,
            ShippingTerms = shippingTerms,
            Notes = notes
        };
    }

    public void AddItem(Guid productId, string itemDescription, decimal quantity, string unit, decimal unitPrice)
    {
        var item = new PurchaseOrderItem(Id, productId, itemDescription, quantity, unit, unitPrice, TenantId);
        _items.Add(item);
        RecalculateTotals();
    }

    public void RemoveItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(x => x.Id == itemId);
        if (item is not null)
        {
            _items.Remove(item);
            RecalculateTotals();
        }
    }

    public void Approve()
    {
        Status = PurchaseOrderStatus.Approved;
    }

    public void Issue()
    {
        Status = PurchaseOrderStatus.Issued;
    }

    public void MarkPartiallyReceived()
    {
        Status = PurchaseOrderStatus.PartiallyReceived;
    }

    public void MarkReceived()
    {
        Status = PurchaseOrderStatus.Received;
    }

    public void Cancel()
    {
        Status = PurchaseOrderStatus.Cancelled;
    }

    private void RecalculateTotals()
    {
        Subtotal = _items.Sum(x => x.TotalPrice);
        TotalAmount = Subtotal + TaxAmount;
    }
}
