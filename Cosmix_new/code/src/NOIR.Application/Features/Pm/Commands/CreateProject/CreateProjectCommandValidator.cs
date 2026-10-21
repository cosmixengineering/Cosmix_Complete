namespace NOIR.Application.Features.Pm.Commands.CreateProject;

public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Project name is required.")
            .MaximumLength(200).WithMessage("Project name cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters.")
            .When(x => x.Description is not null);

        RuleFor(x => x.Currency)
            .MaximumLength(3).WithMessage("Currency code cannot exceed 3 characters.")
            .When(x => x.Currency is not null);

        RuleFor(x => x.Color)
            .MaximumLength(20).WithMessage("Color cannot exceed 20 characters.")
            .When(x => x.Color is not null);

        RuleFor(x => x.Icon)
            .MaximumLength(50).WithMessage("Icon cannot exceed 50 characters.")
            .When(x => x.Icon is not null);

        RuleFor(x => x.Budget)
            .GreaterThanOrEqualTo(0).WithMessage("Budget must be non-negative.")
            .When(x => x.Budget.HasValue);
    }
}
