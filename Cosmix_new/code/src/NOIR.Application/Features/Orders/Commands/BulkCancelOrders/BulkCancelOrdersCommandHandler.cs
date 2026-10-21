namespace NOIR.Application.Features.Orders.Commands.BulkCancelOrders;

/// <summary>
/// Wolverine handler for bulk cancelling orders.
/// </summary>
public class BulkCancelOrdersCommandHandler
{
    private readonly IRepository<Order, Guid> _orderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BulkCancelOrdersCommandHandler(
        IRepository<Order, Guid> orderRepository,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BulkOperationResultDto>> Handle(
        BulkCancelOrdersCommand command,
        CancellationToken cancellationToken)
    {
        var successCount = 0;
        var errors = new List<BulkOperationErrorDto>();

        var spec = new OrdersByIdsForUpdateSpec(command.OrderIds);
        var orders = await _orderRepository.ListAsync(spec, cancellationToken);

        foreach (var orderId in command.OrderIds)
        {
            var order = orders.FirstOrDefault(o => o.Id == orderId);

            if (order is null)
            {
                errors.Add(new BulkOperationErrorDto(orderId, null, "Order not found"));
                continue;
            }

            if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed or OrderStatus.Processing))
            {
                errors.Add(new BulkOperationErrorDto(orderId, order.OrderNumber, $"Order cannot be cancelled in {order.Status} status"));
                continue;
            }

            try
            {
                order.Cancel(command.Reason);
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add(new BulkOperationErrorDto(orderId, order.OrderNumber, ex.Message));
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new BulkOperationResultDto(successCount, errors.Count, errors));
    }
}
