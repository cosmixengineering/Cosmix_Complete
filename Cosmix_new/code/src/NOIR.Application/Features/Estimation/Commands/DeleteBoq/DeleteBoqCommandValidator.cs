namespace NOIR.Application.Features.Estimation.Commands.DeleteBoq;

/// <summary>
/// Validator for DeleteBoqCommand.
/// </summary>
public class DeleteBoqCommandValidator : AbstractValidator<DeleteBoqCommand>
{
    public DeleteBoqCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("BOQ ID is required.");
    }
}
