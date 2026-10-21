namespace NOIR.Infrastructure.Services;

/// <summary>
/// FluentStorage implementation of file storage service.
/// Supports local disk, Azure Blob, AWS S3, etc. based on configuration.
/// </summary>
public class FileStorageService : IFileStorage, IScopedService
{
    private readonly IBlobStorage _storage;
    private readonly ILogger<FileStorageService> _logger;
    private readonly string _mediaUrlPrefix;
    private readonly string? _publicBaseUrl;

    public FileStorageService(
        IBlobStorage storage,
        IOptions<StorageSettings> settings,
        ILogger<FileStorageService> logger)
    {
        _storage = storage;
        _logger = logger;
        _mediaUrlPrefix = settings.Value.MediaUrlPrefix.TrimEnd('/');
        _publicBaseUrl = settings.Value.PublicBaseUrl?.TrimEnd('/');

        // Auto-derive public URL for cloud providers when not explicitly configured
        var provider = settings.Value.Provider?.ToLowerInvariant();
        if (string.IsNullOrEmpty(_publicBaseUrl) && provider is "s3" or "azure")
        {
            _publicBaseUrl = DerivePublicBaseUrl(provider, settings.Value);

            if (!string.IsNullOrEmpty(_publicBaseUrl))
            {
                _logger.LogInformation(
                    "Auto-derived {Provider} public URL: {PublicBaseUrl}", provider, _publicBaseUrl);
            }
            else
            {
                _logger.LogWarning(
                    "Cloud storage provider '{Provider}' configured without PublicBaseUrl. " +
                    "Files will be served through the backend proxy, which adds latency. " +
                    "Set Storage:PublicBaseUrl in configuration for direct CDN access.",
                    provider);
            }
        }
    }

    private static string? DerivePublicBaseUrl(string provider, StorageSettings settings)
    {
        if (provider == "s3" && !string.IsNullOrEmpty(settings.S3BucketName))
        {
            var region = settings.S3Region ?? "us-east-1";
            return $"https://{settings.S3BucketName}.s3.{region}.amazonaws.com";
        }

        if (provider == "azure" && !string.IsNullOrEmpty(settings.AzureConnectionString))
        {
            var match = Regex.Match(settings.AzureConnectionString, @"AccountName=([^;]+)");
            if (match.Success && !string.IsNullOrEmpty(settings.AzureContainerName))
            {
                return $"https://{match.Groups[1].Value}.blob.core.windows.net/{settings.AzureContainerName}";
            }
        }

        return null;
    }

    public async Task<string> UploadAsync(string fileName, Stream content, string? folder = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = string.IsNullOrEmpty(folder) ? fileName : $"{folder}/{fileName}";

            // Ensure stream position is at the beginning
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            // Copy to MemoryStream for reliable writing (some storage providers need seekable streams)
            using var memoryStream = new MemoryStream();
            await content.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            await _storage.WriteAsync(path, memoryStream, false, cancellationToken);

            _logger.LogInformation("File uploaded: {Path}, Size: {Size} bytes", path, memoryStream.Length);
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file: {FileName}", fileName);
            throw;
        }
    }

    public async Task<Stream?> DownloadAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await _storage.ExistsAsync(path, cancellationToken))
            {
                _logger.LogWarning("File not found: {Path}", path);
                return null;
            }

            return await _storage.OpenReadAsync(path, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file: {Path}", path);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await _storage.ExistsAsync(path, cancellationToken))
            {
                _logger.LogWarning("File not found for deletion: {Path}", path);
                return false;
            }

            await _storage.DeleteAsync(path, cancellationToken);
            _logger.LogInformation("File deleted: {Path}", path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {Path}", path);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _storage.ExistsAsync(path, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if file exists: {Path}", path);
            return false;
        }
    }

    public async Task<IEnumerable<string>> ListAsync(string? folder = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var blobs = await _storage.ListAsync(new ListOptions { FolderPath = folder }, cancellationToken);
            return blobs.Select(b => b.FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list files in folder: {Folder}", folder ?? "root");
            return [];
        }
    }

    public string MediaUrlPrefix => _mediaUrlPrefix;

    public string? GetPublicUrl(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        // Cloud storage: return absolute URL pointing directly to the provider
        if (!string.IsNullOrEmpty(_publicBaseUrl))
        {
            return $"{_publicBaseUrl}/{path}";
        }

        // Local storage: return relative URL handled by backend media endpoint
        return $"{_mediaUrlPrefix}/{path}";
    }

    public string? GetStoragePath(string publicUrl)
    {
        if (string.IsNullOrEmpty(publicUrl))
        {
            return null;
        }

        // Cloud storage: strip the public base URL prefix
        if (!string.IsNullOrEmpty(_publicBaseUrl))
        {
            var cloudPrefix = $"{_publicBaseUrl}/";
            if (publicUrl.StartsWith(cloudPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return publicUrl[cloudPrefix.Length..];
            }
        }

        // Local storage: strip the media URL prefix
        var prefix = $"{_mediaUrlPrefix}/";
        if (publicUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return publicUrl[prefix.Length..];
        }

        return null;
    }
}
