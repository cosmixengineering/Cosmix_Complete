using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.LocationConsent;

public sealed class LocationNoticePolicy
{
    private LocationNoticePolicy() { }

    public LocationNoticePolicy(int version, string noticeText, string createdByUserId, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        Version = version > 0 ? version : throw new ArgumentOutOfRangeException(nameof(version), "Version must be positive.");
        NoticeText = ModelGuard.Required(noticeText, 4000, nameof(noticeText));
        CreatedByUserId = ModelGuard.Required(createdByUserId, 200, nameof(createdByUserId));
        CreatedAt = ModelGuard.Utc(createdAt);
        IsActive = false;
    }

    public Guid Id { get; private set; }
    public int Version { get; private set; }
    public string NoticeText { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public string? PublishedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedByUserId { get; private set; } = string.Empty;

    public void Publish(string actorUserId, DateTimeOffset publishedAt)
    {
        PublishedByUserId = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        PublishedAt = ModelGuard.Utc(publishedAt);
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
