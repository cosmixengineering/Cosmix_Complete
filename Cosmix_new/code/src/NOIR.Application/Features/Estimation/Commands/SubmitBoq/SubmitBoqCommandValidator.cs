namespace NOIR.Application.Features.Estimation.Commands.SubmitBoq;

/// <summary>
/// Validator for SubmitBoqCommand.
/// </summary>
public class SubmitBoqCommandValidator : AbstractValidator<SubmitBoqCommand>
{
    public SubmitBoqCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("BOQ ID is required.");
    }
}
