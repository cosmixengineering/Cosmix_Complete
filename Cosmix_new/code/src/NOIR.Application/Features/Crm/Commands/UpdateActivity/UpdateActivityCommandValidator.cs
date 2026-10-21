namespace NOIR.Application.Features.Crm.Commands.UpdateActivity;

public sealed class UpdateActivityCommandValidator : AbstractValidator<UpdateActivityCommand>
{
    public UpdateActivityCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Activity ID is required.");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required.")
            .MaximumLength(200).WithMessage("Subject cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters.")
            .When(x => x.Description is not null);

        RuleFor(x => x.DurationMinutes)
            .GreaterThan(0).WithMessage("Duration must be positive.")
            .When(x => x.DurationMinutes is not null);
    }
}
