namespace NOIR.Domain.Entities.Stores;

public enum GrnStatus
{
    Draft = 1,
    Inspected = 2,
    Verified = 3,
    Cancelled = 4
}

/// <summary>
/// Goods Receipt Note (GRN) aggregate root.
/// Tracks physical incoming shipments from suppliers or project returns with acceptance inspection.
/// </summary>
public class GoodsReceiptNote : TenantAggregateRoot<Guid>
{
    private readonly List<GoodsReceiptNoteItem> _items = [];

    private GoodsReceiptNote() : base() { }

    private GoodsReceiptNote(Guid id, string? tenantId) : base(id, tenantId) { }

    public string GrnNumber { get; private set; } = string.Empty;
    public Guid SupplierId { get; private set; }
    public Guid? PurchaseOrderId { get; private set; }
    public string? StoreLocation { get; private set; }
    public DateTimeOffset ReceivedDate { get; private set; } = DateTimeOffset.UtcNow;
    public GrnStatus Status { get; private set; } = GrnStatus.Draft;
    public string? VehicleNumber { get; private set; }
    public string? DeliveryChallanNumber { get; private set; }
    public string? InspectedBy { get; private set; }
    public string? Remarks { get; private set; }

    public IReadOnlyCollection<GoodsReceiptNoteItem> Items => _items.AsReadOnly();

    public static GoodsReceiptNote Create(
        string grnNumber,
        Guid supplierId,
        Guid? purchaseOrderId,
        string? storeLocation,
        string? vehicleNumber,
        string? deliveryChallanNumber,
        string? remarks,
        string? tenantId = null)
    {
        return new GoodsReceiptNote(Guid.NewGuid(), tenantId)
        {
            GrnNumber = grnNumber,
            SupplierId = supplierId,
            PurchaseOrderId = purchaseOrderId,
            StoreLocation = storeLocation,
            VehicleNumber = vehicleNumber,
            DeliveryChallanNumber = deliveryChallanNumber,
            Remarks = remarks,
            Status = GrnStatus.Draft,
            ReceivedDate = DateTimeOffset.UtcNow
        };
    }

    public void AddItem(
        Guid productId,
        string itemDescription,
        decimal receivedQuantity,
        decimal acceptedQuantity,
        decimal rejectedQuantity,
        string unit,
        string? rejectionReason = null)
    {
        var item = new GoodsReceiptNoteItem(
            Id,
            productId,
            itemDescription,
            receivedQuantity,
            acceptedQuantity,
            rejectedQuantity,
            unit,
            rejectionReason,
            TenantId);

        _items.Add(item);
    }

    public void Verify(string inspectedBy)
    {
        InspectedBy = inspectedBy;
        Status = GrnStatus.Verified;
    }

    public void Cancel()
    {
        Status = GrnStatus.Cancelled;
    }
}
