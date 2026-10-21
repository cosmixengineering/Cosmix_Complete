namespace NOIR.Application.Features.Customers.Commands.BulkDeactivateCustomers;

public sealed class BulkDeactivateCustomersCommandValidator : AbstractValidator<BulkDeactivateCustomersCommand>
{
    public BulkDeactivateCustomersCommandValidator()
    {
        RuleFor(x => x.CustomerIds).NotEmpty().WithMessage("At least one customer ID is required.");
        RuleFor(x => x.CustomerIds.Count).LessThanOrEqualTo(100).WithMessage("Maximum 100 customers per batch.");
        RuleForEach(x => x.CustomerIds).NotEmpty().WithMessage("Customer ID cannot be empty.");
    }
}
