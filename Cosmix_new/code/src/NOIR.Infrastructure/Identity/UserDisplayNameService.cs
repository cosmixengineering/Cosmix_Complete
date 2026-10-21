namespace NOIR.Infrastructure.Identity;

/// <summary>
/// Resolves user IDs to display names via batch query.
/// Scoped lifetime = one cache per HTTP request.
/// </summary>
public class UserDisplayNameService : IUserDisplayNameService, IScopedService
{
    private readonly ApplicationDbContext _context;
    private readonly Dictionary<string, string?> _cache = new();

    public UserDisplayNameService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyDictionary<string, string?>> GetDisplayNamesAsync(
        IEnumerable<string> userIds,
        CancellationToken ct = default)
    {
        var ids = userIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        var uncachedIds = ids
            .Where(id => !_cache.ContainsKey(id))
            .ToList();

        if (uncachedIds.Count > 0)
        {
            var users = await _context.Users
                .Where(u => uncachedIds.Contains(u.Id))
                .Select(u => new { u.Id, u.DisplayName, u.FirstName, u.LastName })
                .TagWith("UserDisplayNameService.GetDisplayNamesAsync")
                .AsNoTracking()
                .ToListAsync(ct);

            foreach (var u in users)
            {
                var name = !string.IsNullOrWhiteSpace(u.DisplayName)
                    ? u.DisplayName
                    : $"{u.FirstName} {u.LastName}".Trim();
                _cache[u.Id] = string.IsNullOrWhiteSpace(name) ? null : name;
            }

            // Mark missing IDs as null (deleted user or system action)
            foreach (var id in uncachedIds.Where(id => !_cache.ContainsKey(id)))
            {
                _cache[id] = null;
            }
        }

        return ids.ToDictionary(id => id, id => _cache.GetValueOrDefault(id));
    }
}
