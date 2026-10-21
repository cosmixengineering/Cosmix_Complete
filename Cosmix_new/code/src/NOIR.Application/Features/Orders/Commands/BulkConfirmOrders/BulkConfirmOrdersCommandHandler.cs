namespace NOIR.Application.Features.Orders.Commands.BulkConfirmOrders;

/// <summary>
/// Wolverine handler for bulk confirming orders.
/// </summary>
public class BulkConfirmOrdersCommandHandler
{
    private readonly IRepository<Order, Guid> _orderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BulkConfirmOrdersCommandHandler(
        IRepository<Order, Guid> orderRepository,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BulkOperationResultDto>> Handle(
        BulkConfirmOrdersCommand command,
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

            if (order.Status != OrderStatus.Pending)
            {
                errors.Add(new BulkOperationErrorDto(orderId, order.OrderNumber, $"Order is not in Pending status (current: {order.Status})"));
                continue;
            }

            try
            {
                order.Confirm();
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
