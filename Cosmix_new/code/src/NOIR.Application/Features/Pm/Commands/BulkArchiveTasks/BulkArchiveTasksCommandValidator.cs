namespace NOIR.Application.Features.Pm.Commands.BulkArchiveTasks;

public class BulkArchiveTasksCommandValidator : AbstractValidator<BulkArchiveTasksCommand>
{
    public BulkArchiveTasksCommandValidator()
    {
        RuleFor(x => x.TaskIds).NotEmpty().WithMessage("At least one task ID is required.");
        RuleFor(x => x.TaskIds).Must(ids => ids.Count <= 200).WithMessage("Cannot bulk archive more than 200 tasks at once.");
    }
}
