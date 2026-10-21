using NOIR.Application.Features.Media.Dtos;
using NOIR.Application.Specifications.MediaFiles;

namespace NOIR.Application.Features.Media.Queries.GetMediaFileById;

/// <summary>
/// Wolverine handler for getting a media file by ID.
/// </summary>
public class GetMediaFileByIdQueryHandler
{
    private readonly IRepository<MediaFile, Guid> _repository;

    public GetMediaFileByIdQueryHandler(IRepository<MediaFile, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<Result<MediaFileDto>> Handle(
        GetMediaFileByIdQuery query,
        CancellationToken cancellationToken)
    {
        var spec = new MediaFileByIdSpec(query.Id);
        var mediaFile = await _repository.FirstOrDefaultAsync(spec, cancellationToken);

        if (mediaFile is null)
        {
            return Result.Failure<MediaFileDto>(
                Error.NotFound($"Media file with ID '{query.Id}' not found.", "NOIR-MEDIA-001"));
        }

        var variants = JsonSerializer.Deserialize<List<MediaVariantDto>>(
            mediaFile.VariantsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        var srcsets = JsonSerializer.Deserialize<Dictionary<string, string>>(
            mediaFile.SrcsetsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        var dto = new MediaFileDto
        {
            Id = mediaFile.Id,
            ShortId = mediaFile.ShortId,
            Slug = mediaFile.Slug,
            OriginalFileName = mediaFile.OriginalFileName,
            Folder = mediaFile.Folder,
            DefaultUrl = mediaFile.DefaultUrl,
            ThumbHash = mediaFile.ThumbHash,
            DominantColor = mediaFile.DominantColor,
            Width = mediaFile.Width,
            Height = mediaFile.Height,
            AspectRatio = mediaFile.AspectRatio,
            Format = mediaFile.Format,
            MimeType = mediaFile.MimeType,
            SizeBytes = mediaFile.SizeBytes,
            HasTransparency = mediaFile.HasTransparency,
            AltText = mediaFile.AltText,
            Variants = variants,
            Srcsets = srcsets,
            CreatedAt = mediaFile.CreatedAt
        };

        return Result.Success(dto);
    }
}
