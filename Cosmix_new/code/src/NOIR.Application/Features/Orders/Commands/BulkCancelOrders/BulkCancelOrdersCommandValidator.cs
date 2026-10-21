namespace NOIR.Application.Features.Orders.Commands.BulkCancelOrders;

public sealed class BulkCancelOrdersCommandValidator : AbstractValidator<BulkCancelOrdersCommand>
{
    public BulkCancelOrdersCommandValidator()
    {
        RuleFor(x => x.OrderIds).NotEmpty().WithMessage("At least one order ID is required.");
        RuleFor(x => x.OrderIds.Count).LessThanOrEqualTo(100).WithMessage("Maximum 100 orders per batch.");
        RuleForEach(x => x.OrderIds).NotEmpty().WithMessage("Order ID cannot be empty.");
    }
}
