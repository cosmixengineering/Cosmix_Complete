using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Domain.Attendance.Zones;

namespace Cosmix.Application.Attendance.Zones;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ZoneOfflineObservationRequest(
    [property: JsonRequired] Guid AssignmentId,
    [property: JsonRequired] Guid ConfigurationVersionId,
    [property: JsonRequired] Guid SessionKey,
    [Required, StringLength(64, MinimumLength = 64)] string NoticeSha256,
    [property: JsonRequired] DateTimeOffset DeviceCapturedAtUtc,
    [property: JsonRequired] ZoneEventKind EventKindHint,
    [Required, StringLength(1000)] string WrittenClaim);

public sealed record ZoneOfflineObservationReceipt(Guid EventId, Guid WindowId, string EvidenceStatus,
    DateTimeOffset ReceivedAtUtc, DateTimeOffset DeviceCapturedAtUtc, ZoneEventKind EventKindHint,
    DateTimeOffset WrittenClaimExpiresAtUtc);

public sealed record ZoneOfflineObservationReconciliation(bool Found, string ReceiptState,
    ZoneEventResponseDto? Report, bool CanResubmit = false);

/// <summary>Explicit server-selected approval bound to the original configuration and notice.
/// Defaults never select a deployment delay, grace or retention duration.</summary>
public sealed record ZoneOfflineObservationPolicy(Guid ConfigurationVersionId, DateTimeOffset ApprovedAtUtc,
    string AuthorityEvidenceReference, int MaximumDelaySeconds, int ReportRetentionDays);

public sealed record ZoneOfflineObservationOptions
{
    public const string SectionName = "ZoneOfflineObservations";
    public bool Enabled { get; init; }
    public IReadOnlyList<ZoneOfflineObservationPolicy> Policies { get; init; } = [];
}
