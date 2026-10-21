namespace NOIR.Application.Features.Pm.Commands.RemoveLabelFromTask;

public sealed class RemoveLabelFromTaskCommandValidator : AbstractValidator<RemoveLabelFromTaskCommand>
{
    public RemoveLabelFromTaskCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("Task ID is required.");

        RuleFor(x => x.LabelId)
            .NotEmpty().WithMessage("Label ID is required.");
    }
}
