using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;

namespace Cosmix.Application.Attendance.Punches.CaptureValidation;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DuplicateCaptureRule([property: JsonRequired] bool? Enabled, decimal? ToleranceSeconds);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ActiveDurationCaptureRule([property: JsonRequired] bool? Enabled, decimal? MaximumMinutes);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TravelCaptureRule([property: JsonRequired] bool? Enabled, decimal? LookbackSeconds,
    decimal? MaximumMetersPerSecond, decimal? MaximumAccuracyMeters);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeviceRiskMapping(string ObservationCode, string RiskKind);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeviceCaptureRule([property: JsonRequired] bool? Enabled, string? ApprovedCodeSetReference,
    IReadOnlyList<DeviceRiskMapping>? RiskMappings);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CaptureValidationSettings(DuplicateCaptureRule Duplicate, ActiveDurationCaptureRule ActiveDuration,
    TravelCaptureRule Travel, DeviceCaptureRule Device);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishCaptureValidationPolicyRequest(DateTimeOffset EffectiveFromUtc,
    [property: JsonRequired] int ExpectedRevision, string ExpectedTimelineFingerprint,
    [Required, StringLength(200)] string ApprovalReference, CaptureValidationSettings Settings,
    [Required, StringLength(1000)] string Reason);

public sealed record CaptureValidationPolicyEntry(Guid Id, int Revision, Guid? SupersedesVersionId,
    DateTimeOffset EffectiveFromUtc, DateTimeOffset PublishedAtUtc, string PublishedBy, string ApprovalReference,
    CaptureValidationSettings Settings, string Reason, string SourceFingerprint, string ProtectedEvidenceReference);
public sealed record CaptureValidationPolicyResponse(Guid Id, int Revision, Guid? SupersedesVersionId,
    DateTimeOffset EffectiveFromUtc, DateTimeOffset PublishedAtUtc, string PublishedBy, string ApprovalReference,
    CaptureValidationSettings Settings, string Reason, string SourceFingerprint);
public sealed record CaptureValidationPolicyPreview(DateTimeOffset AsOfUtc, int LatestRevision,
    string TimelineFingerprint, string State, CaptureValidationPolicyResponse? Effective);
public sealed record CaptureValidationPolicyProof(int SchemaVersion, CaptureValidationPolicyEntry Entry);
public sealed record RecentCaptureEvidence(RawPunchEvent Raw, PunchAssessment? Assessment);
public sealed record CaptureValidationAssessment(CaptureDecision Decision, IReadOnlyList<string> Codes,
    Guid? PolicyVersionId, int? PolicyRevision, string? SourceFingerprint, IReadOnlyList<Guid> RelatedRawPunchIds,
    string? DeviceObservationCode);
public sealed record CaptureValidationEvaluation(Guid Id, Guid RawPunchEventId, Guid EmployeeId, PunchAction Action, bool IsOffline,
    string RegisteredDeviceId, string RequestSha256, DateTimeOffset ReceivedAtUtc, DateTimeOffset EvaluatedAtUtc,
    CaptureValidationAssessment Candidate)
{
    public CaptureDecision Decision => Candidate.Decision;
    public IReadOnlyList<string> Codes => Candidate.Codes;
    public Guid? PolicyVersionId => Candidate.PolicyVersionId;
    public int? PolicyRevision => Candidate.PolicyRevision;
    public string? SourceFingerprint => Candidate.SourceFingerprint;
    public IReadOnlyList<Guid> RelatedRawPunchIds => Candidate.RelatedRawPunchIds;
    public string? DeviceObservationCode => Candidate.DeviceObservationCode;
}
public sealed record CaptureValidationEvidence(Guid Id, Guid RawPunchEventId, Guid EmployeeId,
    PunchAction Action, bool IsOffline, string RegisteredDeviceId, string RequestSha256, DateTimeOffset ReceivedAtUtc,
    DateTimeOffset EvaluatedAtUtc, DateTimeOffset AssessedAtUtc, CaptureDecision CandidateDecision,
    CaptureValidationAssessment FinalAssessment);

public interface ICaptureValidationPolicyRepository
{
    Task<IReadOnlyList<CaptureValidationPolicyEntry>> TimelineAsync(CancellationToken ct);
    void Add(CaptureValidationPolicyEntry entry);
}
public interface ICaptureValidationEvidenceRepository
{
    Task<IReadOnlyList<RecentCaptureEvidence>> RecentAsync(Guid employeeId, DateTimeOffset fromInclusiveUtc,
        DateTimeOffset throughInclusiveUtc, CancellationToken ct);
    Task<RecentCaptureEvidence?> PunchAsync(Guid employeeId, Guid punchId, CancellationToken ct);
    Task<CaptureValidationEvidence?> FinalAsync(Guid employeeId, Guid punchId, CancellationToken ct);
    void Add(CaptureValidationEvidence evidence);
}
public interface ICaptureValidationPolicyReader
{
    Task<CaptureValidationPolicyEntry?> EffectiveAsync(DateTimeOffset receivedAtUtc, CancellationToken ct);
}
public interface ICaptureValidationEvaluator
{
    Task<CaptureValidationEvaluation> EvaluateAsync(RawPunchEvent raw, RegisteredDevice device,
        WorkSession? activeSession, CaptureDecision existingDecision, CancellationToken ct);
    Guid RecordFinal(RawPunchEvent raw, CaptureValidationEvaluation evaluation, CaptureDecision finalDecision,
        DateTimeOffset assessedAtUtc);
}
