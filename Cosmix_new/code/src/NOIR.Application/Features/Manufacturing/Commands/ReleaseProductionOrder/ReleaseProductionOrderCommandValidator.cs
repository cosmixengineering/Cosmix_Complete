namespace NOIR.Application.Features.Manufacturing.Commands.ReleaseProductionOrder;

/// <summary>
/// Validator for ReleaseProductionOrderCommand.
/// </summary>
public class ReleaseProductionOrderCommandValidator : AbstractValidator<ReleaseProductionOrderCommand>
{
    public ReleaseProductionOrderCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Production order ID is required.");
    }
}
