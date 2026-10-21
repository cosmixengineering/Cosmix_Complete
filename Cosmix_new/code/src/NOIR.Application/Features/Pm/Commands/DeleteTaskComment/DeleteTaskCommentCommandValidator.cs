namespace NOIR.Application.Features.Pm.Commands.DeleteTaskComment;

public sealed class DeleteTaskCommentCommandValidator : AbstractValidator<DeleteTaskCommentCommand>
{
    public DeleteTaskCommentCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("Task ID is required.");

        RuleFor(x => x.CommentId)
            .NotEmpty().WithMessage("Comment ID is required.");
    }
}
