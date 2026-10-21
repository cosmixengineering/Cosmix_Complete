namespace NOIR.Application.Features.Pm.Commands.CreateTaskLabel;

public sealed class CreateTaskLabelCommandValidator : AbstractValidator<CreateTaskLabelCommand>
{
    public CreateTaskLabelCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Project ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Label name is required.")
            .MaximumLength(50).WithMessage("Label name cannot exceed 50 characters.");

        RuleFor(x => x.Color)
            .NotEmpty().WithMessage("Label color is required.")
            .MaximumLength(20).WithMessage("Label color cannot exceed 20 characters.");
    }
}
