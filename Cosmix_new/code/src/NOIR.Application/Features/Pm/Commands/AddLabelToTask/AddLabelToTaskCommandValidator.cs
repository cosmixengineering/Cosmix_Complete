namespace NOIR.Application.Features.Pm.Commands.AddLabelToTask;

public sealed class AddLabelToTaskCommandValidator : AbstractValidator<AddLabelToTaskCommand>
{
    public AddLabelToTaskCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("Task ID is required.");

        RuleFor(x => x.LabelId)
            .NotEmpty().WithMessage("Label ID is required.");
    }
}
