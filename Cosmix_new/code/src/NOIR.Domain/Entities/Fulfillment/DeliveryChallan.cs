namespace NOIR.Domain.Entities.Fulfillment;

/// <summary>
/// Delivery Advice / Delivery Challan aggregate root.
/// Manages outbound goods dispatch, logistics vehicle/driver details, customer OTP receipt confirmation, and proof of delivery.
/// </summary>
public class DeliveryChallan : TenantAggregateRoot<Guid>
{
    private readonly List<DeliveryChallanItem> _items = [];

    private DeliveryChallan() : base() { }

    private DeliveryChallan(Guid id, string? tenantId) : base(id, tenantId) { }

    public string ChallanNumber { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public DateTimeOffset DispatchDate { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeliveredDate { get; private set; }
    public DeliveryChallanStatus Status { get; private set; } = DeliveryChallanStatus.Draft;
    public string? SiteAddress { get; private set; }
    public string? TransporterName { get; private set; }
    public string? VehicleNumber { get; private set; }
    public string? DriverName { get; private set; }
    public string? DriverPhone { get; private set; }
    public string? CustomerOtp { get; private set; }
    public string? ProofOfDeliveryUrl { get; private set; }
    public string? Notes { get; private set; }

    public IReadOnlyCollection<DeliveryChallanItem> Items => _items.AsReadOnly();

    public static DeliveryChallan Create(
        string challanNumber,
        Guid customerId,
        Guid? projectId,
        string? siteAddress,
        string? transporterName,
        string? vehicleNumber,
        string? driverName,
        string? driverPhone,
        string? customerOtp,
        string? notes,
        string? tenantId = null)
    {
        return new DeliveryChallan(Guid.NewGuid(), tenantId)
        {
            ChallanNumber = challanNumber,
            CustomerId = customerId,
            ProjectId = projectId,
            SiteAddress = siteAddress,
            TransporterName = transporterName,
            VehicleNumber = vehicleNumber,
            DriverName = driverName,
            DriverPhone = driverPhone,
            CustomerOtp = customerOtp,
            Notes = notes,
            Status = DeliveryChallanStatus.Draft,
            DispatchDate = DateTimeOffset.UtcNow
        };
    }

    public void AddItem(Guid productId, string itemDescription, decimal quantity, string unit)
    {
        var item = new DeliveryChallanItem(Id, productId, itemDescription, quantity, unit, TenantId);
        _items.Add(item);
    }

    public void Dispatch()
    {
        Status = DeliveryChallanStatus.Dispatched;
    }

    public void ConfirmDelivery(string? enteredOtp, string? proofOfDeliveryUrl = null)
    {
        if (!string.IsNullOrEmpty(CustomerOtp) && CustomerOtp != enteredOtp)
        {
            throw new InvalidOperationException("Invalid delivery OTP confirmation code.");
        }

        Status = DeliveryChallanStatus.Delivered;
        DeliveredDate = DateTimeOffset.UtcNow;
        if (!string.IsNullOrEmpty(proofOfDeliveryUrl))
        {
            ProofOfDeliveryUrl = proofOfDeliveryUrl;
        }
    }

    public void Cancel(string? reason)
    {
        Status = DeliveryChallanStatus.Cancelled;
        Notes = string.IsNullOrEmpty(Notes) ? $"Cancelled: {reason}" : $"{Notes} | Cancelled: {reason}";
    }
}
