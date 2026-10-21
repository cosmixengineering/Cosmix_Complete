using NOIR.Application.Features.Media.Specifications;
using NOIR.Domain.Events.Media;

namespace NOIR.Application.Features.Media.EventHandlers;

/// <summary>
/// Handles MediaFileDeletedEvent by cleaning up physical files from storage.
/// Since soft delete is used, the entity is still accessible via IgnoreQueryFilters.
/// </summary>
public class MediaFileDeletedHandler
{
    private readonly IFileStorage _fileStorage;
    private readonly IRepository<MediaFile, Guid> _repository;
    private readonly ILogger<MediaFileDeletedHandler> _logger;

    public MediaFileDeletedHandler(
        IFileStorage fileStorage,
        IRepository<MediaFile, Guid> repository,
        ILogger<MediaFileDeletedHandler> logger)
    {
        _fileStorage = fileStorage;
        _repository = repository;
        _logger = logger;
    }

    public async Task Handle(MediaFileDeletedEvent evt, CancellationToken ct)
    {
        _logger.LogInformation("Cleaning up physical files for deleted media {FileId}: {FileName}",
            evt.FileId, evt.FileName);

        try
        {
            var spec = new MediaFileByIdIncludeDeletedSpec(evt.FileId);
            var mediaFile = await _repository.FirstOrDefaultAsync(spec, ct);

            if (mediaFile is null)
            {
                _logger.LogWarning("Media file {FileId} not found even with IgnoreQueryFilters", evt.FileId);
                return;
            }

            var urlsToDelete = new HashSet<string>();

            if (!string.IsNullOrEmpty(mediaFile.DefaultUrl))
            {
                var storagePath = _fileStorage.GetStoragePath(mediaFile.DefaultUrl);
                if (storagePath is not null)
                    urlsToDelete.Add(storagePath);
            }

            if (!string.IsNullOrEmpty(mediaFile.VariantsJson))
            {
                try
                {
                    var variants = JsonSerializer.Deserialize<List<VariantInfo>>(
                        mediaFile.VariantsJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (variants is not null)
                    {
                        foreach (var variant in variants)
                        {
                            if (!string.IsNullOrEmpty(variant.Url))
                            {
                                var storagePath = _fileStorage.GetStoragePath(variant.Url);
                                if (storagePath is not null)
                                    urlsToDelete.Add(storagePath);
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse variants JSON for media {FileId}", evt.FileId);
                }
            }

            foreach (var path in urlsToDelete)
            {
                try
                {
                    await _fileStorage.DeleteAsync(path, ct);
                    _logger.LogDebug("Deleted physical file: {Path}", path);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete physical file: {Path}", path);
                }
            }

            _logger.LogInformation("Cleaned up {Count} physical files for media {FileId}",
                urlsToDelete.Count, evt.FileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean up physical files for media {FileId}", evt.FileId);
        }
    }

    private sealed record VariantInfo(string? Url);
}
