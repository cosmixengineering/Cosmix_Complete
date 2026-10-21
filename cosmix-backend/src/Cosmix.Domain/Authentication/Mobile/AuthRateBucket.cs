using Cosmix.Domain.Common;

namespace Cosmix.Domain.Authentication.Mobile;

/// <summary>Persist each consumption with row-version concurrency; limits and window duration come from approved policy.</summary>
public sealed class AuthRateBucket
{
    private AuthRateBucket() { }

    public AuthRateBucket(string key, DateTimeOffset windowStartsAt, DateTimeOffset windowEndsAt)
    {
        if (windowEndsAt <= windowStartsAt) throw new ArgumentException("Rate window end must follow its start.");
        Key = ModelGuard.Required(key, 150, nameof(key));
        WindowStartsAt = ModelGuard.Utc(windowStartsAt); WindowEndsAt = ModelGuard.Utc(windowEndsAt);
    }

    public string Key { get; private set; } = string.Empty;
    public DateTimeOffset WindowStartsAt { get; private set; }
    public DateTimeOffset WindowEndsAt { get; private set; }
    public int Count { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool TryConsume(int limit, DateTimeOffset at)
    {
        if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));
        if (at < WindowStartsAt) return false;
        if (at >= WindowEndsAt)
        {
            var startsAt = ModelGuard.Utc(at);
            var endsAt = startsAt.Add(WindowEndsAt - WindowStartsAt);
            WindowStartsAt = startsAt; WindowEndsAt = endsAt; Count = 0;
        }
        if (Count >= limit) return false;
        Count = checked(Count + 1);
        return true;
    }
}
