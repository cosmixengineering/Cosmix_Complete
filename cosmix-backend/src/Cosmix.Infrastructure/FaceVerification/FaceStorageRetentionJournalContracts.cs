using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.Files;

namespace Cosmix.Infrastructure.FaceVerification;

public sealed record FaceStorageRetentionJournalOptions
{
    public const string SectionName = "FaceStorageRetentionJournal";
    public bool Enabled { get; init; }
    public string IndexDirectory { get; init; } = "";
    public PrivateFileStorageOptions Storage { get; init; } = new();
}

/// <summary>Infrastructure persistence boundary. Reserve an immutable entry before creating its
/// encrypted metadata; an interrupted reservation is never silently removed or skipped.</summary>
public interface IFaceStorageRetentionIndex
{
    bool IsConfigured { get; }
    Task<T> WithObjectLockAsync<T>(string objectId, Func<CancellationToken, Task<T>> action, CancellationToken ct);
    Task<IReadOnlyList<PrivateFileReference>> ReadAsync(string objectId, CancellationToken ct);
    Task AppendAsync(string objectId, int sequence,
        Func<CancellationToken, Task<PrivateFileReference>> writeMetadata, CancellationToken ct);
}
