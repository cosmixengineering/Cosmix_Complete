namespace NOIR.Application.Features.Manufacturing.Commands.IssueMaterial;

/// <summary>
/// Validator for IssueMaterialCommand.
/// </summary>
public class IssueMaterialCommandValidator : AbstractValidator<IssueMaterialCommand>
{
    public IssueMaterialCommandValidator()
    {
        RuleFor(x => x.ProductionOrderId)
            .NotEmpty().WithMessage("Production order ID is required.");

        RuleFor(x => x.ProductionOrderItemId)
            .NotEmpty().WithMessage("Production order item ID is required.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Issue quantity must be greater than zero.");
    }
}
