namespace NOIR.Application.Features.Orders.Commands.ManualCreateAndCompleteOrder;

/// <summary>
/// Command to manually create an order and immediately complete it.
/// Used for POS/walk-in scenarios where payment is received on the spot.
/// Reuses the same payload as ManualCreateOrderCommand.
/// </summary>
public sealed record ManualCreateAndCompleteOrderCommand(
    string CustomerEmail,
    string? CustomerName,
    string? CustomerPhone,
    Guid? CustomerId,
    List<ManualOrderItemDto> Items,
    CreateAddressDto? ShippingAddress,
    CreateAddressDto? BillingAddress,
    string? ShippingMethod,
    string? CouponCode,
    string? CustomerNotes,
    string? InternalNotes,
    PaymentMethod? PaymentMethod,
    decimal ShippingAmount = 0,
    decimal DiscountAmount = 0,
    decimal TaxAmount = 0,
    string Currency = "VND") : IAuditableCommand<OrderDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => UserId;
    public string? GetTargetDisplayName() => CustomerEmail;
    public string? GetActionDescription() => $"Manually created and completed order for '{CustomerEmail}'";
}
