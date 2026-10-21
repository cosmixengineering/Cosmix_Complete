namespace NOIR.Application.Features.Estimation.Commands.CreateBoq;

/// <summary>
/// Validator for CreateBoqCommand.
/// </summary>
public class CreateBoqCommandValidator : AbstractValidator<CreateBoqCommand>
{
    public CreateBoqCommandValidator()
    {
        RuleFor(x => x.BoqNumber)
            .NotEmpty().WithMessage("BOQ number is required.")
            .MaximumLength(50).WithMessage("BOQ number must not exceed 50 characters.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("BOQ title is required.")
            .MaximumLength(200).WithMessage("BOQ title must not exceed 200 characters.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .MaximumLength(3).WithMessage("Currency must not exceed 3 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes must not exceed 1000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Notes));

        RuleFor(x => x.MarkupPercent)
            .InclusiveBetween(0, 1000).WithMessage("Markup percent must be between 0 and 1000.");
    }
}
