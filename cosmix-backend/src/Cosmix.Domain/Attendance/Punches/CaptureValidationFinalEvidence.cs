using System.Text.Json;
using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Punches;

public sealed class CaptureValidationFinalEvidence : IAppendOnlyEntity
{
    private CaptureValidationFinalEvidence() { }
    public CaptureValidationFinalEvidence(Guid id, Guid rawPunchEventId, Guid employeeId, PunchAction action, bool isOffline,
        string registeredDeviceId, string requestSha256, DateTimeOffset receivedAtUtc, DateTimeOffset evaluatedAtUtc,
        DateTimeOffset assessedAtUtc, PunchAssessmentOutcome candidateOutcome, string candidateReasonCode,
        PunchAssessmentOutcome finalOutcome, string finalReasonCode, Guid? policyVersionId, int? policyRevision,
        string? sourceFingerprint, string codesJson, string relatedRawPunchIdsJson, string? deviceObservationCode)
    {
        if (!Enum.IsDefined(action) || !Enum.IsDefined(candidateOutcome) || !Enum.IsDefined(finalOutcome) ||
            evaluatedAtUtc < receivedAtUtc || assessedAtUtc < evaluatedAtUtc ||
            isOffline && (candidateOutcome != PunchAssessmentOutcome.Unverified || finalOutcome != PunchAssessmentOutcome.Unverified))
            throw new ArgumentException("Final capture evidence requires valid decisions and receipt/evaluation/final chronology.");
        var hasPolicy = policyVersionId.HasValue;
        if (hasPolicy != policyRevision.HasValue || hasPolicy != (sourceFingerprint is not null) || policyRevision is < 1 ||
            finalOutcome is PunchAssessmentOutcome.Accepted or PunchAssessmentOutcome.AcceptedWithException && !hasPolicy ||
            candidateOutcome is not (PunchAssessmentOutcome.Accepted or PunchAssessmentOutcome.AcceptedWithException) &&
                finalOutcome is PunchAssessmentOutcome.Accepted or PunchAssessmentOutcome.AcceptedWithException)
            throw new ArgumentException("Final acceptance requires an approved capture-policy source and an eligible candidate.");
        using var codes = JsonDocument.Parse(codesJson);
        using var related = JsonDocument.Parse(relatedRawPunchIdsJson);
        if (codes.RootElement.ValueKind != JsonValueKind.Array || codes.RootElement.GetArrayLength() == 0 ||
            codes.RootElement.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(x.GetString()) || x.GetString()!.Length > 100 || x.GetString()!.Any(char.IsControl)) ||
            related.RootElement.ValueKind != JsonValueKind.Array || related.RootElement.EnumerateArray().Any(x =>
                x.ValueKind != JsonValueKind.String || !Guid.TryParse(x.GetString(), out var relatedId) ||
                relatedId == Guid.Empty || relatedId == rawPunchEventId))
            throw new ArgumentException("Final evidence codes and related raw identities must be valid structured arrays.");
        Id = ModelGuard.Id(id, nameof(id)); RawPunchEventId = ModelGuard.Id(rawPunchEventId, nameof(rawPunchEventId));
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId)); Action = action; IsOffline = isOffline;
        RegisteredDeviceId = ModelGuard.Required(registeredDeviceId, 200, nameof(registeredDeviceId));
        if (!Guid.TryParseExact(RegisteredDeviceId, "D", out var device) || device == Guid.Empty)
            throw new ArgumentException("The server registered device identity is required.");
        RequestSha256 = CaptureValidationModelGuard.Hash(requestSha256);
        ReceivedAtUtc = ModelGuard.Utc(receivedAtUtc); EvaluatedAtUtc = ModelGuard.Utc(evaluatedAtUtc); AssessedAtUtc = ModelGuard.Utc(assessedAtUtc);
        CandidateOutcome = candidateOutcome; CandidateReasonCode = ModelGuard.Code(candidateReasonCode, 100);
        FinalOutcome = finalOutcome; FinalReasonCode = ModelGuard.Code(finalReasonCode, 100);
        if (CandidateReasonCode != candidateReasonCode || FinalReasonCode != finalReasonCode)
            throw new ArgumentException("Evidence must retain the exact canonical codes used by the actual raw assessment.");
        PolicyVersionId = AttendanceModelGuard.OptionalId(policyVersionId, nameof(policyVersionId)); PolicyRevision = policyRevision;
        SourceFingerprint = sourceFingerprint is null ? null : CaptureValidationModelGuard.Hash(sourceFingerprint);
        CodesJson = ModelGuard.Required(codesJson, 8192, nameof(codesJson));
        RelatedRawPunchIdsJson = ModelGuard.Required(relatedRawPunchIdsJson, 32768, nameof(relatedRawPunchIdsJson));
        DeviceObservationCode = ModelGuard.Optional(deviceObservationCode, 100, nameof(deviceObservationCode));
    }
    public Guid Id { get; private set; }
    public Guid RawPunchEventId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public PunchAction Action { get; private set; }
    public bool IsOffline { get; private set; }
    public string RegisteredDeviceId { get; private set; } = "";
    public string RequestSha256 { get; private set; } = "";
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public DateTimeOffset EvaluatedAtUtc { get; private set; }
    public DateTimeOffset AssessedAtUtc { get; private set; }
    public PunchAssessmentOutcome CandidateOutcome { get; private set; }
    public string CandidateReasonCode { get; private set; } = "";
    public PunchAssessmentOutcome FinalOutcome { get; private set; }
    public string FinalReasonCode { get; private set; } = "";
    public Guid? PolicyVersionId { get; private set; }
    public int? PolicyRevision { get; private set; }
    public string? SourceFingerprint { get; private set; }
    public string CodesJson { get; private set; } = "";
    public string RelatedRawPunchIdsJson { get; private set; } = "";
    public string? DeviceObservationCode { get; private set; }
}
