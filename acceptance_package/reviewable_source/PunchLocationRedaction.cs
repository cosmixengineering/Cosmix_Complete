using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.LocationConsent;

public sealed class PunchLocationRedaction
{
    private PunchLocationRedaction() { }

    public PunchLocationRedaction(Guid punchEventId, DateTimeOffset redactedAtUtc, string actorUserId,
        int retentionPolicyDays, string? reason = null)
    {
        Id = Guid.NewGuid();
        PunchEventId = ModelGuard.Id(punchEventId, nameof(punchEventId));
        RedactedAtUtc = ModelGuard.Utc(redactedAtUtc);
        ActorUserId = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        RetentionPolicyDays = retentionPolicyDays > 0 ? retentionPolicyDays : throw new ArgumentOutOfRangeException(nameof(retentionPolicyDays));
        Reason = ModelGuard.Optional(reason, 200, nameof(reason)) ?? "Automated retention cleanup";
    }

    public Guid Id { get; private set; }
    public Guid PunchEventId { get; private set; }
    public RawPunchEvent PunchEvent { get; private set; } = null!;
    public DateTimeOffset RedactedAtUtc { get; private set; }
    public string ActorUserId { get; private set; } = string.Empty;
    public int RetentionPolicyDays { get; private set; }
    public string Reason { get; private set; } = string.Empty;
}
