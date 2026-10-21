namespace NOIR.Domain.Entities.Fulfillment;

/// <summary>
/// Line item for Delivery Advice / Challan dispatch.
/// </summary>
public class DeliveryChallanItem : TenantEntity<Guid>
{
    private DeliveryChallanItem() : base() { }

    internal DeliveryChallanItem(
        Guid deliveryChallanId,
        Guid productId,
        string itemDescription,
        decimal quantity,
        string unit,
        string? tenantId = null)
        : base(Guid.NewGuid(), tenantId)
    {
        DeliveryChallanId = deliveryChallanId;
        ProductId = productId;
        ItemDescription = itemDescription;
        Quantity = quantity;
        Unit = unit;
    }

    public Guid DeliveryChallanId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ItemDescription { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = "Pcs";
}
