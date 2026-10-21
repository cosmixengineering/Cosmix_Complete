namespace NOIR.Application.Features.Orders.Commands.ManualCreateAndCompleteOrder;

/// <summary>
/// Wolverine handler for creating and immediately completing an order.
/// Dispatches creation via IMessageBus, then completes the order.
/// </summary>
public class ManualCreateAndCompleteOrderCommandHandler
{
    private readonly IMessageBus _messageBus;
    private readonly IRepository<Order, Guid> _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public ManualCreateAndCompleteOrderCommandHandler(
        IMessageBus messageBus,
        IRepository<Order, Guid> orderRepository,
        IUnitOfWork unitOfWork,
        IEntityUpdateHubContext entityUpdateHub)
    {
        _messageBus = messageBus;
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _entityUpdateHub = entityUpdateHub;
    }

    public async Task<Result<OrderDto>> Handle(
        ManualCreateAndCompleteOrderCommand command,
        CancellationToken cancellationToken)
    {
        // Create the order with Paid status (which confirms it)
        var createCommand = new ManualCreateOrder.ManualCreateOrderCommand(
            command.CustomerEmail,
            command.CustomerName,
            command.CustomerPhone,
            command.CustomerId,
            command.Items,
            command.ShippingAddress,
            command.BillingAddress,
            command.ShippingMethod,
            command.CouponCode,
            command.CustomerNotes,
            command.InternalNotes,
            command.PaymentMethod,
            PaymentStatus.Paid,
            command.ShippingAmount,
            command.DiscountAmount,
            command.TaxAmount,
            command.Currency)
        { UserId = command.UserId };

        var createResult = await _messageBus.InvokeAsync<Result<OrderDto>>(createCommand, cancellationToken);
        if (!createResult.IsSuccess)
        {
            return createResult;
        }

        // Load the order and complete it
        var orderId = createResult.Value.Id;
        var spec = new OrderByIdForUpdateSpec(orderId);
        var order = await _orderRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderDto>(
                Error.Failure(ErrorCodes.System.UnknownError, "Failed to retrieve created order for completion."));
        }

        order.ManualComplete();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "Order",
            entityId: order.Id,
            operation: EntityOperation.Updated,
            tenantId: order.TenantId!,
            ct: cancellationToken);

        return Result.Success(OrderMapper.ToDto(order));
    }
}
