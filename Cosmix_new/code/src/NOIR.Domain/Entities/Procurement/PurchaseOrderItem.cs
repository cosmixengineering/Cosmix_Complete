namespace NOIR.Domain.Entities.Procurement;

/// <summary>
/// Line item for a procurement Purchase Order.
/// </summary>
public class PurchaseOrderItem : TenantEntity<Guid>
{
    private PurchaseOrderItem() : base() { }

    internal PurchaseOrderItem(
        Guid purchaseOrderId,
        Guid productId,
        string itemDescription,
        decimal quantity,
        string unit,
        decimal unitPrice,
        string? tenantId = null)
        : base(Guid.NewGuid(), tenantId)
    {
        PurchaseOrderId = purchaseOrderId;
        ProductId = productId;
        ItemDescription = itemDescription;
        Quantity = quantity;
        Unit = unit;
        UnitPrice = unitPrice;
        TotalPrice = quantity * unitPrice;
        ReceivedQuantity = 0;
    }

    public Guid PurchaseOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ItemDescription { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = "Pcs";
    public decimal UnitPrice { get; private set; }
    public decimal TotalPrice { get; private set; }
    public decimal ReceivedQuantity { get; private set; }

    public void RecordReceived(decimal quantity)
    {
        ReceivedQuantity += quantity;
    }
}
