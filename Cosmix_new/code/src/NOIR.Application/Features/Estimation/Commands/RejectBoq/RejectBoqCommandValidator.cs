namespace NOIR.Application.Features.Estimation.Commands.RejectBoq;

/// <summary>
/// Validator for RejectBoqCommand.
/// </summary>
public class RejectBoqCommandValidator : AbstractValidator<RejectBoqCommand>
{
    public RejectBoqCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("BOQ ID is required.");

        RuleFor(x => x.Reason)
            .MaximumLength(1000).WithMessage("Reason must not exceed 1000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Reason));
    }
}
