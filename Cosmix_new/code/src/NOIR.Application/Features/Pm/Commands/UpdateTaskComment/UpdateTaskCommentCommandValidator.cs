namespace NOIR.Application.Features.Pm.Commands.UpdateTaskComment;

public sealed class UpdateTaskCommentCommandValidator : AbstractValidator<UpdateTaskCommentCommand>
{
    public UpdateTaskCommentCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("Task ID is required.");

        RuleFor(x => x.CommentId)
            .NotEmpty().WithMessage("Comment ID is required.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Comment content is required.")
            .MaximumLength(10000).WithMessage("Comment content cannot exceed 10000 characters.");
    }
}
