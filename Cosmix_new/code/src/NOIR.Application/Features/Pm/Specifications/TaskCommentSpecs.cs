namespace NOIR.Application.Features.Pm.Specifications;

/// <summary>
/// Get comment by ID with tracking and Author include.
/// </summary>
public sealed class CommentByIdForUpdateSpec : Specification<TaskComment>
{
    public CommentByIdForUpdateSpec(Guid commentId)
    {
        Query.Where(c => c.Id == commentId)
             .Include(c => c.Author!)
             .AsTracking()
             .TagWith("CommentByIdForUpdate");
    }
}
