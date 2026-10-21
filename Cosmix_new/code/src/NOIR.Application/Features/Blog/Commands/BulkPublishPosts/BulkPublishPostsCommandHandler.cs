namespace NOIR.Application.Features.Blog.Commands.BulkPublishPosts;

/// <summary>
/// Wolverine handler for bulk publishing blog posts.
/// </summary>
public class BulkPublishPostsCommandHandler
{
    private readonly IRepository<Post, Guid> _postRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BulkPublishPostsCommandHandler(
        IRepository<Post, Guid> postRepository,
        IUnitOfWork unitOfWork)
    {
        _postRepository = postRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BulkOperationResultDto>> Handle(
        BulkPublishPostsCommand command,
        CancellationToken cancellationToken)
    {
        var successCount = 0;
        var errors = new List<BulkOperationErrorDto>();

        var spec = new PostsByIdsForUpdateSpec(command.PostIds);
        var posts = await _postRepository.ListAsync(spec, cancellationToken);

        foreach (var postId in command.PostIds)
        {
            var post = posts.FirstOrDefault(p => p.Id == postId);

            if (post is null)
            {
                errors.Add(new BulkOperationErrorDto(postId, null, "Post not found"));
                continue;
            }

            if (post.Status != PostStatus.Draft)
            {
                errors.Add(new BulkOperationErrorDto(postId, post.Title, $"Post is not in Draft status (current: {post.Status})"));
                continue;
            }

            try
            {
                post.Publish();
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add(new BulkOperationErrorDto(postId, post.Title, ex.Message));
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new BulkOperationResultDto(successCount, errors.Count, errors));
    }
}
