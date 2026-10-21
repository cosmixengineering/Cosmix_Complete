namespace NOIR.Application.Features.Pm.Commands.ReorderTask;

public sealed class ReorderTaskCommandValidator : AbstractValidator<ReorderTaskCommand>
{
    public ReorderTaskCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("Task ID is required.");

        RuleFor(x => x.NewSortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Sort order cannot be negative.");
    }
}
