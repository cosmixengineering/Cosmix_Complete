namespace NOIR.Application.Features.Pm.Commands.CreateColumn;

public sealed class CreateColumnCommandValidator : AbstractValidator<CreateColumnCommand>
{
    public CreateColumnCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Project ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Column name is required.")
            .MaximumLength(100).WithMessage("Column name cannot exceed 100 characters.");

        RuleFor(x => x.Color)
            .MaximumLength(20).WithMessage("Color cannot exceed 20 characters.")
            .When(x => x.Color is not null);

        RuleFor(x => x.WipLimit)
            .GreaterThan(0).WithMessage("WIP limit must be positive.")
            .When(x => x.WipLimit.HasValue);
    }
}
