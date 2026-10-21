namespace NOIR.Application.Features.Manufacturing.Commands.StartProductionOrder;

/// <summary>
/// Validator for StartProductionOrderCommand.
/// </summary>
public class StartProductionOrderCommandValidator : AbstractValidator<StartProductionOrderCommand>
{
    public StartProductionOrderCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Production order ID is required.");
    }
}
