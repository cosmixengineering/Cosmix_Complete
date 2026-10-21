namespace NOIR.Application.Features.Estimation.Commands.ApproveBoq;

/// <summary>
/// Validator for ApproveBoqCommand.
/// </summary>
public class ApproveBoqCommandValidator : AbstractValidator<ApproveBoqCommand>
{
    public ApproveBoqCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("BOQ ID is required.");
    }
}
