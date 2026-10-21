namespace NOIR.Application.Features.Customers.Commands.BulkImportCustomers;

/// <summary>
/// Validates bulk import customers command.
/// </summary>
public class BulkImportCustomersCommandValidator : AbstractValidator<BulkImportCustomersCommand>
{
    public BulkImportCustomersCommandValidator()
    {
        RuleFor(x => x.Customers).NotEmpty();
        RuleFor(x => x.Customers.Count).LessThanOrEqualTo(1000);
    }
}
