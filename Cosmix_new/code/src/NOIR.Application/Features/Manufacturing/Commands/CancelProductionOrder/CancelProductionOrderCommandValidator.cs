namespace NOIR.Application.Features.Manufacturing.Commands.CancelProductionOrder;

/// <summary>
/// Validator for CancelProductionOrderCommand.
/// </summary>
public class CancelProductionOrderCommandValidator : AbstractValidator<CancelProductionOrderCommand>
{
    public CancelProductionOrderCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Production order ID is required.");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Reason));
    }
}
