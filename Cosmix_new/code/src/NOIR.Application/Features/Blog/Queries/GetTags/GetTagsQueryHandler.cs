
namespace NOIR.Application.Features.Blog.Queries.GetTags;

/// <summary>
/// Wolverine handler for getting a list of blog tags.
/// </summary>
public class GetTagsQueryHandler
{
    private readonly IRepository<PostTag, Guid> _tagRepository;
    private readonly IUserDisplayNameService _userDisplayNameService;

    public GetTagsQueryHandler(IRepository<PostTag, Guid> tagRepository, IUserDisplayNameService userDisplayNameService)
    {
        _tagRepository = tagRepository;
        _userDisplayNameService = userDisplayNameService;
    }

    public async Task<Result<List<PostTagListDto>>> Handle(
        GetTagsQuery query,
        CancellationToken cancellationToken)
    {
        var spec = new TagsSpec(query.Search);
        var tags = await _tagRepository.ListAsync(spec, cancellationToken);

        // Resolve user names
        var userIds = tags
            .SelectMany(x => new[] { x.CreatedBy, x.ModifiedBy })
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct();
        var userNames = await _userDisplayNameService.GetDisplayNamesAsync(userIds, cancellationToken);

        var result = tags.Select(t => new PostTagListDto(
            t.Id,
            t.Name,
            t.Slug,
            t.Description,
            t.Color,
            t.PostCount,
            t.CreatedAt,
            t.ModifiedAt,
            t.CreatedBy != null ? userNames.GetValueOrDefault(t.CreatedBy) : null,
            t.ModifiedBy != null ? userNames.GetValueOrDefault(t.ModifiedBy) : null
        )).ToList();

        return Result.Success(result);
    }
}
