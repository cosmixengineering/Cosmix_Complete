namespace NOIR.Domain.Entities.Stores;

/// <summary>
/// Line item for a Goods Receipt Note (GRN) inward receipt.
/// </summary>
public class GoodsReceiptNoteItem : TenantEntity<Guid>
{
    private GoodsReceiptNoteItem() : base() { }

    internal GoodsReceiptNoteItem(
        Guid goodsReceiptNoteId,
        Guid productId,
        string itemDescription,
        decimal receivedQuantity,
        decimal acceptedQuantity,
        decimal rejectedQuantity,
        string unit,
        string? rejectionReason,
        string? tenantId = null)
        : base(Guid.NewGuid(), tenantId)
    {
        GoodsReceiptNoteId = goodsReceiptNoteId;
        ProductId = productId;
        ItemDescription = itemDescription;
        ReceivedQuantity = receivedQuantity;
        AcceptedQuantity = acceptedQuantity;
        RejectedQuantity = rejectedQuantity;
        Unit = unit;
        RejectionReason = rejectionReason;
    }

    public Guid GoodsReceiptNoteId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ItemDescription { get; private set; } = string.Empty;
    public decimal ReceivedQuantity { get; private set; }
    public decimal AcceptedQuantity { get; private set; }
    public decimal RejectedQuantity { get; private set; }
    public string Unit { get; private set; } = "Pcs";
    public string? RejectionReason { get; private set; }
}
