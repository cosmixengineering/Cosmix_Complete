namespace NOIR.Application.Features.Manufacturing.Commands.CompleteProductionOrder;

/// <summary>
/// Validator for CompleteProductionOrderCommand.
/// </summary>
public class CompleteProductionOrderCommandValidator : AbstractValidator<CompleteProductionOrderCommand>
{
    public CompleteProductionOrderCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Production order ID is required.");
    }
}
