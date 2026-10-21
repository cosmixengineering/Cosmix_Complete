namespace NOIR.Application.Features.Media.Dtos;

/// <summary>
/// Lightweight DTO for media file list views.
/// </summary>
public sealed record MediaFileListDto(
    Guid Id,
    string ShortId,
    string Slug,
    string OriginalFileName,
    string Folder,
    string DefaultUrl,
    string? ThumbHash,
    string? DominantColor,
    int Width,
    int Height,
    string Format,
    string MimeType,
    long SizeBytes,
    string? AltText,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt,
    string? CreatedByName = null,
    string? ModifiedByName = null);
