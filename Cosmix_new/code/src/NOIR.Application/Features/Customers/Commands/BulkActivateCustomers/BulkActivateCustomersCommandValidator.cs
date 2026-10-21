namespace NOIR.Application.Features.Customers.Commands.BulkActivateCustomers;

public sealed class BulkActivateCustomersCommandValidator : AbstractValidator<BulkActivateCustomersCommand>
{
    public BulkActivateCustomersCommandValidator()
    {
        RuleFor(x => x.CustomerIds).NotEmpty().WithMessage("At least one customer ID is required.");
        RuleFor(x => x.CustomerIds.Count).LessThanOrEqualTo(100).WithMessage("Maximum 100 customers per batch.");
        RuleForEach(x => x.CustomerIds).NotEmpty().WithMessage("Customer ID cannot be empty.");
    }
}
