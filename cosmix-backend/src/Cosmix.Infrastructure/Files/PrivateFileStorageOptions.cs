namespace Cosmix.Infrastructure.Files;

public sealed record PrivateFileStorageOptions
{
    public const string SectionName = "PrivateFiles";
    public const int MaximumAllowedSizeBytes = 64 * 1024 * 1024;

    public string Directory { get; init; } = string.Empty;
    public string KeyFile { get; init; } = string.Empty;
    public string KeyReference { get; init; } = string.Empty;
    public int MaxSizeBytes { get; init; } = 10 * 1024 * 1024;

    // At most 16 retained keys, indexed by immutable, non-secret references. Restart after rotation.
    public Dictionary<string, string> DecryptionKeyFiles { get; init; } = new(StringComparer.Ordinal);
}
