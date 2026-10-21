namespace NOIR.Application.Features.Pm.Commands.DeleteTaskLabel;

public sealed class DeleteTaskLabelCommandValidator : AbstractValidator<DeleteTaskLabelCommand>
{
    public DeleteTaskLabelCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Project ID is required.");

        RuleFor(x => x.LabelId)
            .NotEmpty().WithMessage("Label ID is required.");
    }
}
