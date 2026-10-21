namespace NOIR.Application.Features.Pm.Commands.MoveAllColumnTasks;

public class MoveAllColumnTasksCommandValidator : AbstractValidator<MoveAllColumnTasksCommand>
{
    public MoveAllColumnTasksCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.SourceColumnId).NotEmpty();
        RuleFor(x => x.TargetColumnId).NotEmpty()
            .NotEqual(x => x.SourceColumnId).WithMessage("Target column must differ from source column.");
    }
}
