using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Zones;

public enum ZoneEventResponseKind { Acknowledged = 1, LocationDisputed = 2, Reviewed = 3 }

/// <summary>Append-only acknowledgement/dispute/review trail instead of modifying original location evidence.</summary>
public sealed class ZoneEventResponse : IAppendOnlyEntity
{
    private ZoneEventResponse() { }
    public ZoneEventResponse(Guid zoneEventId, ZoneEventResponseKind kind, string actor, DateTimeOffset atUtc,
        string? reason = null, string? privateEvidenceReference = null, Guid? approvalRequestId = null)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (kind != ZoneEventResponseKind.Acknowledged && string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Dispute/review needs a reason.");
        Id = Guid.NewGuid(); ZoneEventId = ModelGuard.Id(zoneEventId, nameof(zoneEventId)); Kind = kind;
        Actor = ModelGuard.Required(actor, 200, nameof(actor)); AtUtc = ModelGuard.Utc(atUtc);
        Reason = ModelGuard.Optional(reason, 2000, nameof(reason)); PrivateEvidenceReference = ModelGuard.Optional(privateEvidenceReference, 500, nameof(privateEvidenceReference));
        ApprovalRequestId = AttendanceModelGuard.OptionalId(approvalRequestId, nameof(approvalRequestId));
    }
    public Guid Id { get; private set; }
    public Guid ZoneEventId { get; private set; }
    public ZoneEventResponseKind Kind { get; private set; }
    public string Actor { get; private set; } = string.Empty;
    public DateTimeOffset AtUtc { get; private set; }
    public string? Reason { get; private set; }
    public string? PrivateEvidenceReference { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
    public ZoneEvent Event { get; private set; } = null!;
}
