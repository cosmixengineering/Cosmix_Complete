namespace NOIR.Application.Features.Manufacturing.Commands.UpdateProductionOrder;

/// <summary>
/// Validator for UpdateProductionOrderCommand.
/// </summary>
public class UpdateProductionOrderCommandValidator : AbstractValidator<UpdateProductionOrderCommand>
{
    public UpdateProductionOrderCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Production order ID is required.");

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
