using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;

namespace Cosmix.Application.Attendance.Punches.CaptureValidation;

public sealed class CaptureValidationEvaluator(ICaptureValidationPolicyReader policies,
    ICaptureValidationEvidenceRepository evidence, TimeProvider clock) : ICaptureValidationEvaluator
{
    private readonly Dictionary<Guid, CaptureValidationEvaluation> pending = [];
    private readonly HashSet<Guid> finalizedRaws = [];
    public async Task<CaptureValidationEvaluation> EvaluateAsync(RawPunchEvent raw, RegisteredDevice device,
        WorkSession? activeSession, CaptureDecision existingDecision, CancellationToken ct)
    {
        if (raw is null || device is null || device.Id == Guid.Empty || raw.RegisteredDeviceId != device.Id.ToString("D") ||
            device.RevokedAt.HasValue || device.VerifiedAt is null || device.VerifiedAt > raw.ReceivedAtUtc ||
            device.RegisteredAt > raw.ReceivedAtUtc)
            throw AttendanceOperation.Conflict("capture_validation_device_conflict");
        if (clock.GetUtcNow() < raw.ReceivedAtUtc) throw AttendanceOperation.Conflict("capture_validation_clock_conflict");
        if (finalizedRaws.Contains(raw.Id) || pending.Values.Any(x => x.RawPunchEventId == raw.Id))
            throw AttendanceOperation.Conflict("capture_validation_already_evaluated");
        CaptureValidationPolicyEntry? policy;
        try { policy = await policies.EffectiveAsync(raw.ReceivedAtUtc, ct); }
        catch (AuthenticationFailure error) when (error.StatusCode is 400 or 404 or 409 or 503)
        {
            var pending = CaptureValidationRules.Evaluate(raw, null, [], activeSession, null, null, existingDecision);
            pending = pending with
            {
                Decision = pending.Decision.ReasonCode == "CAPTURE_POLICY_NOT_CONFIGURED"
                    ? pending.Decision with { ReasonCode = "CAPTURE_POLICY_EVIDENCE_UNAVAILABLE" } : pending.Decision,
                Codes = Array.AsReadOnly(pending.Codes.Select(x => x == "CAPTURE_POLICY_NOT_CONFIGURED"
                    ? "CAPTURE_POLICY_EVIDENCE_UNAVAILABLE" : x).ToArray())
            };
            return Bind(raw, pending);
        }
        if (policy is not null) CaptureValidationPolicyRules.ValidateEntry(policy);
        IReadOnlyList<RecentCaptureEvidence> recent = [];
        if (policy is not null)
        {
            var seconds = Math.Max(policy.Settings.Duplicate.Enabled == true ? policy.Settings.Duplicate.ToleranceSeconds!.Value : 0m,
                policy.Settings.Travel.Enabled == true ? policy.Settings.Travel.LookbackSeconds!.Value : 0m);
            if (seconds > 0m)
            {
                var ticks = decimal.Ceiling(seconds * TimeSpan.TicksPerSecond);
                var available = raw.ReceivedAtUtc.UtcTicks - DateTimeOffset.MinValue.UtcTicks;
                var from = raw.ReceivedAtUtc.AddTicks(-(long)Math.Min(ticks, available));
                recent = await evidence.RecentAsync(raw.EmployeeId, from, raw.ReceivedAtUtc, ct);
                if (recent is null || recent.Any(x => x is null || x.Raw is null ||
                    x.Raw.ReceivedAtUtc < from || x.Raw.ReceivedAtUtc > raw.ReceivedAtUtc))
                    throw AttendanceOperation.Conflict("capture_validation_query_scope_conflict");
            }
        }
        RecentCaptureEvidence? opening = null;
        if (policy?.Settings.ActiveDuration.Enabled == true && activeSession?.CheckInPunchId is Guid openingId)
        {
            opening = await evidence.PunchAsync(raw.EmployeeId, openingId, ct);
            if (opening is not null && opening.Raw.Id != openingId)
                throw AttendanceOperation.Conflict("capture_validation_opening_conflict");
        }
        var observation = device.IntegrityAssessedAt is not null && device.IntegrityAssessedAt <= raw.ReceivedAtUtc &&
            device.IntegrityAssessedAt >= device.RegisteredAt ? device.IntegrityAssessmentCode : null;
        var result = CaptureValidationRules.Evaluate(raw, policy, recent, activeSession, opening, observation, existingDecision);
        return Bind(raw, result);
    }
    private CaptureValidationEvaluation Bind(RawPunchEvent raw, CaptureValidationAssessment candidate)
    {
        var evaluatedAt = clock.GetUtcNow();
        if (evaluatedAt < raw.ReceivedAtUtc) throw AttendanceOperation.Conflict("capture_validation_clock_conflict");
        var evaluation = new CaptureValidationEvaluation(Guid.NewGuid(), raw.Id, raw.EmployeeId, raw.Action, raw.IsOffline,
            raw.RegisteredDeviceId, raw.RequestSha256, raw.ReceivedAtUtc, evaluatedAt, candidate);
        pending.Add(evaluation.Id, evaluation);
        return evaluation;
    }
    public Guid RecordFinal(RawPunchEvent raw, CaptureValidationEvaluation evaluation, CaptureDecision finalDecision,
        DateTimeOffset assessedAtUtc)
    {
        if (raw is null || evaluation is null || !pending.TryGetValue(evaluation.Id, out var issued) ||
            !ReferenceEquals(issued, evaluation) || raw.Id != evaluation.RawPunchEventId ||
            raw.EmployeeId != evaluation.EmployeeId || raw.Action != evaluation.Action || raw.IsOffline != evaluation.IsOffline ||
            raw.RegisteredDeviceId != evaluation.RegisteredDeviceId || raw.RequestSha256 != evaluation.RequestSha256 ||
            raw.ReceivedAtUtc != evaluation.ReceivedAtUtc || finalizedRaws.Contains(raw.Id))
            throw AttendanceOperation.Conflict("capture_validation_final_binding_conflict");
        if (assessedAtUtc.Offset != TimeSpan.Zero || assessedAtUtc < evaluation.EvaluatedAtUtc ||
            assessedAtUtc < raw.ReceivedAtUtc || assessedAtUtc > clock.GetUtcNow())
            throw AttendanceOperation.Conflict("capture_validation_final_clock_conflict");
        var candidate = evaluation.Candidate;
        if (finalDecision is null || !Enum.IsDefined(finalDecision.Outcome) ||
            !CaptureValidationPolicyRules.Text(finalDecision.ReasonCode, 100) ||
            raw.IsOffline && finalDecision.Outcome != PunchAssessmentOutcome.Unverified ||
            candidate.Decision.Outcome is not (PunchAssessmentOutcome.Accepted or PunchAssessmentOutcome.AcceptedWithException) &&
                finalDecision.Outcome is PunchAssessmentOutcome.Accepted or PunchAssessmentOutcome.AcceptedWithException ||
            candidate.Decision.Outcome == PunchAssessmentOutcome.Rejected && finalDecision.Outcome != PunchAssessmentOutcome.Rejected ||
            candidate.Decision.Outcome == PunchAssessmentOutcome.Unverified && finalDecision.Outcome != PunchAssessmentOutcome.Unverified)
            throw AttendanceOperation.Conflict("capture_validation_final_outcome_conflict");
        var finalAssessment = candidate with
        {
            Decision = finalDecision,
            Codes = Array.AsReadOnly(candidate.Codes.Append(finalDecision.ReasonCode).Distinct(StringComparer.Ordinal).ToArray())
        };
        var id = Guid.NewGuid();
        // Staged only after the caller's final clock/proof check; the outer transaction commits it.
        evidence.Add(new(id, raw.Id, raw.EmployeeId, raw.Action, raw.IsOffline, raw.RegisteredDeviceId, raw.RequestSha256,
            raw.ReceivedAtUtc, evaluation.EvaluatedAtUtc, assessedAtUtc, candidate.Decision, finalAssessment));
        pending.Remove(evaluation.Id); finalizedRaws.Add(raw.Id);
        return id;
    }
}
