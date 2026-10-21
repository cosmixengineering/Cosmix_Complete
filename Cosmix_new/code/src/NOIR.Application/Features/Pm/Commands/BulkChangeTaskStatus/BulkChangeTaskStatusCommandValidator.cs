namespace NOIR.Application.Features.Pm.Commands.BulkChangeTaskStatus;

public class BulkChangeTaskStatusCommandValidator : AbstractValidator<BulkChangeTaskStatusCommand>
{
    public BulkChangeTaskStatusCommandValidator()
    {
        RuleFor(x => x.TaskIds).NotEmpty().WithMessage("At least one task ID is required.");
        RuleFor(x => x.TaskIds).Must(ids => ids.Count <= 200).WithMessage("Cannot bulk change status for more than 200 tasks at once.");
        RuleFor(x => x.Status).IsInEnum().WithMessage("Invalid task status.");
    }
}
