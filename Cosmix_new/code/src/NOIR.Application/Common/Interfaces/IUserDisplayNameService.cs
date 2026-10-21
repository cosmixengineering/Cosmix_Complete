namespace NOIR.Application.Common.Interfaces;

/// <summary>
/// Batch-resolves user IDs to display names for list views.
/// Uses request-scoped caching to prevent duplicate lookups.
/// </summary>
public interface IUserDisplayNameService : IScopedService
{
    /// <summary>
    /// Resolves display names for a batch of user IDs.
    /// Returns a dictionary mapping userId to displayName.
    /// Unknown IDs map to null values.
    /// </summary>
    Task<IReadOnlyDictionary<string, string?>> GetDisplayNamesAsync(
        IEnumerable<string> userIds,
        CancellationToken ct = default);
}
