namespace NOIR.Domain.Entities.Procurement;

public enum PurchaseOrderStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Issued = 4,
    PartiallyReceived = 5,
    Received = 6,
    Cancelled = 7
}
