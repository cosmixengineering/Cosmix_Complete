namespace NOIR.Application.Features.Media.Specifications;

/// <summary>
/// Specification for paginated media file list with filtering and sorting.
/// </summary>
public sealed class MediaFilesFilteredSpec : Specification<MediaFile>
{
    public MediaFilesFilteredSpec(
        string? search = null,
        string? fileType = null,
        string? folder = null,
        string? orderBy = null,
        bool isDescending = true,
        int skip = 0,
        int take = 24)
    {
        // Search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            Query.Where(m => m.OriginalFileName.Contains(search) || m.Slug.Contains(search));
        }

        // File type filter (e.g., "image" → MimeType starts with "image/")
        if (!string.IsNullOrWhiteSpace(fileType))
        {
            var prefix = fileType + "/";
            Query.Where(m => m.MimeType.StartsWith(prefix));
        }

        // Folder filter
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Query.Where(m => m.Folder == folder);
        }

        // Sorting
        switch (orderBy?.ToLowerInvariant())
        {
            case "name":
                if (isDescending)
                    Query.OrderByDescending(m => m.OriginalFileName);
                else
                    Query.OrderBy(m => m.OriginalFileName);
                break;
            case "size":
                if (isDescending)
                    Query.OrderByDescending(m => m.SizeBytes);
                else
                    Query.OrderBy(m => m.SizeBytes);
                break;
            case "createdby":
            case "creator":
                if (isDescending)
                    Query.OrderByDescending(m => m.CreatedBy);
                else
                    Query.OrderBy(m => m.CreatedBy);
                break;
            case "modifiedby":
            case "editor":
                if (isDescending)
                    Query.OrderByDescending(m => m.ModifiedBy);
                else
                    Query.OrderBy(m => m.ModifiedBy);
                break;
            default: // "createdAt"
                if (isDescending)
                    Query.OrderByDescending(m => m.CreatedAt);
                else
                    Query.OrderBy(m => m.CreatedAt);
                break;
        }

        Query.Skip(skip).Take(take).TagWith("MediaFilesFiltered");
    }
}

/// <summary>
/// Count-only specification for paginated media file queries.
/// </summary>
public sealed class MediaFilesCountSpec : Specification<MediaFile>
{
    public MediaFilesCountSpec(
        string? search = null,
        string? fileType = null,
        string? folder = null)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            Query.Where(m => m.OriginalFileName.Contains(search) || m.Slug.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(fileType))
        {
            var prefix = fileType + "/";
            Query.Where(m => m.MimeType.StartsWith(prefix));
        }

        if (!string.IsNullOrWhiteSpace(folder))
        {
            Query.Where(m => m.Folder == folder);
        }

        Query.TagWith("MediaFilesCount");
    }
}

/// <summary>
/// Specification to get a media file by ID with tracking for mutations.
/// </summary>
public sealed class MediaFileByIdForUpdateSpec : Specification<MediaFile>
{
    public MediaFileByIdForUpdateSpec(Guid id)
    {
        Query.Where(m => m.Id == id)
             .AsTracking()
             .TagWith("MediaFileByIdForUpdate");
    }
}

/// <summary>
/// Specification to get multiple media files by IDs with tracking for bulk operations.
/// </summary>
public sealed class MediaFilesByIdsForUpdateSpec : Specification<MediaFile>
{
    public MediaFilesByIdsForUpdateSpec(List<Guid> ids)
    {
        Query.Where(m => ids.Contains(m.Id))
             .AsTracking()
             .TagWith("MediaFilesByIdsForUpdate");
    }
}

/// <summary>
/// Specification to get a media file by ID, including soft-deleted records.
/// Used by domain event handlers to access file metadata after soft deletion.
/// </summary>
public sealed class MediaFileByIdIncludeDeletedSpec : Specification<MediaFile>
{
    public MediaFileByIdIncludeDeletedSpec(Guid id)
    {
        Query.Where(m => m.Id == id)
             .IgnoreQueryFilters()
             .TagWith("MediaFileByIdIncludeDeleted");
    }
}
