namespace NOIR.Application.Features.Manufacturing.Commands.CreateProductionOrder;

/// <summary>
/// Validator for CreateProductionOrderCommand.
/// </summary>
public class CreateProductionOrderCommandValidator : AbstractValidator<CreateProductionOrderCommand>
{
    public CreateProductionOrderCommandValidator()
    {
        RuleFor(x => x.OrderNumber)
            .NotEmpty().WithMessage("Order number is required.")
            .MaximumLength(50).WithMessage("Order number must not exceed 50 characters.");

        RuleFor(x => x.BillOfMaterialId)
            .NotEmpty().WithMessage("Bill of material is required.");

        RuleFor(x => x.PlannedQuantity)
            .GreaterThan(0).WithMessage("Planned quantity must be greater than zero.");

        RuleFor(x => x.Notes)
            .MaximumLength(5000).WithMessage("Notes must not exceed 5000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Notes));

        RuleFor(x => x)
            .Must(x => !x.PlannedStartDate.HasValue
                       || !x.PlannedEndDate.HasValue
                       || x.PlannedEndDate.Value >= x.PlannedStartDate.Value)
            .WithMessage("Planned end date cannot be before the planned start date.");
    }
}
