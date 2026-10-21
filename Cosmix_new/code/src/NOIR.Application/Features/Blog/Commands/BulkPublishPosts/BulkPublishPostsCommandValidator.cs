namespace NOIR.Application.Features.Blog.Commands.BulkPublishPosts;

public sealed class BulkPublishPostsCommandValidator : AbstractValidator<BulkPublishPostsCommand>
{
    public BulkPublishPostsCommandValidator()
    {
        RuleFor(x => x.PostIds).NotEmpty().WithMessage("At least one post ID is required.");
        RuleFor(x => x.PostIds.Count).LessThanOrEqualTo(100).WithMessage("Maximum 100 posts per batch.");
        RuleForEach(x => x.PostIds).NotEmpty().WithMessage("Post ID cannot be empty.");
    }
}
