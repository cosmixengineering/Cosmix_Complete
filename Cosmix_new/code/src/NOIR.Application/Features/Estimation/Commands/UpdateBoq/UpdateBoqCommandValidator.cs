namespace NOIR.Application.Features.Estimation.Commands.UpdateBoq;

/// <summary>
/// Validator for UpdateBoqCommand.
/// </summary>
public class UpdateBoqCommandValidator : AbstractValidator<UpdateBoqCommand>
{
    public UpdateBoqCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("BOQ ID is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("BOQ title is required.")
            .MaximumLength(200).WithMessage("BOQ title must not exceed 200 characters.");

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
