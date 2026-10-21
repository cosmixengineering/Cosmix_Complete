namespace NOIR.Application.Features.Media.Queries.GetMediaFileById;

/// <summary>
/// Query to get a media file by its ID with full metadata.
/// </summary>
public sealed record GetMediaFileByIdQuery(Guid Id);
