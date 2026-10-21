using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Application.Attendance.Common;

namespace Cosmix.Application.Attendance.Punches.CaptureValidation;

public static class CaptureValidationRules
{
    public static CaptureValidationAssessment Evaluate(RawPunchEvent raw, CaptureValidationPolicyEntry? policy,
        IReadOnlyList<RecentCaptureEvidence> recent, WorkSession? activeSession,
        RecentCaptureEvidence? opening, string? deviceObservationCode, CaptureDecision existingDecision)
    {
        if (raw is null || raw.EmployeeId == Guid.Empty || !Enum.IsDefined(raw.Action) ||
            !Guid.TryParseExact(raw.RegisteredDeviceId, "D", out var deviceId) || deviceId == Guid.Empty ||
            raw.ReceivedAtUtc.Offset != TimeSpan.Zero || recent is null || existingDecision is null ||
            !Enum.IsDefined(existingDecision.Outcome) ||
            activeSession is not null && (activeSession.EmployeeId != raw.EmployeeId || activeSession.EndsAtUtc.HasValue ||
                activeSession.StartsAtUtc > raw.ReceivedAtUtc || activeSession.CalculatedAtUtc > raw.ReceivedAtUtc))
            throw AttendanceOperation.Conflict("capture_validation_evidence_conflict");
        if (recent.Count > CaptureValidationPolicyRules.MaximumRecentRows + 1 || recent.Any(x => x is null || x.Raw is null) ||
            recent.Select(x => x.Raw.Id).Distinct().Count() != recent.Count)
            throw AttendanceOperation.Conflict("capture_validation_evidence_conflict");
        foreach (var item in recent) ValidateEvidence(raw, item);
        if (opening is not null)
        {
            ValidateEvidence(raw, opening);
            if (activeSession is null || opening.Raw.Id != activeSession.CheckInPunchId ||
                opening.Raw.Action != PunchAction.CheckIn || opening.Raw.ReceivedAtUtc != activeSession.StartsAtUtc)
                throw AttendanceOperation.Conflict("capture_validation_opening_conflict");
        }
        if (policy is not null)
        {
            CaptureValidationPolicyRules.ValidateEntry(policy);
            if (policy.EffectiveFromUtc > raw.ReceivedAtUtc || policy.PublishedAtUtc > raw.ReceivedAtUtc)
                throw AttendanceOperation.Conflict("capture_policy_not_effective");
        }
        var codes = new List<string>(); var related = new HashSet<Guid>();
        CaptureDecision? risk = null;
        void Review(string code, AttendanceExceptionKind? kind = null, Guid? evidenceId = null)
        {
            codes.Add(code); risk ??= new(PunchAssessmentOutcome.QueuedForReview, code, kind);
            if (evidenceId.HasValue) related.Add(evidenceId.Value);
        }
        if (policy is null) Review("CAPTURE_POLICY_NOT_CONFIGURED");
        else if (recent.Count > CaptureValidationPolicyRules.MaximumRecentRows) Review("CAPTURE_RECENT_EVIDENCE_LIMIT");
        else
        {
            var settings = policy.Settings;
            if (settings.Duplicate.Enabled == true)
            {
                var candidates = recent.Where(x => x.Raw.Action == raw.Action &&
                    (decimal)(raw.ReceivedAtUtc - x.Raw.ReceivedAtUtc).Ticks / TimeSpan.TicksPerSecond <= settings.Duplicate.ToleranceSeconds);
                foreach (var candidate in candidates)
                {
                    // An accepted intervening opposite action begins a distinct duty/break cycle.
                    if (recent.Any(x => x.Raw.Action != raw.Action && Accepted(x) &&
                        x.Raw.ReceivedAtUtc > candidate.Raw.ReceivedAtUtc && x.Raw.ReceivedAtUtc < raw.ReceivedAtUtc)) continue;
                    Review("DUPLICATE_TIME_TOLERANCE", AttendanceExceptionKind.SuspectedFalseEvidence, candidate.Raw.Id);
                }
            }
            if (settings.ActiveDuration.Enabled == true && activeSession is not null)
            {
                if (opening is null || !Accepted(opening) || opening.Raw.IsOffline)
                    Review("ACTIVE_DURATION_SOURCE_UNVERIFIED");
                else
                {
                    related.Add(opening.Raw.Id);
                    if ((decimal)(raw.ReceivedAtUtc - opening.Raw.ReceivedAtUtc).Ticks / TimeSpan.TicksPerMinute > settings.ActiveDuration.MaximumMinutes)
                        Review("EXCESSIVE_ACTIVE_DURATION", AttendanceExceptionKind.ExcessiveDuration, opening.Raw.Id);
                }
            }
            if (settings.Travel.Enabled == true)
            {
                var travel = settings.Travel;
                if (!Point(raw, travel.MaximumAccuracyMeters!.Value)) Review("TRAVEL_LOCATION_UNVERIFIED", AttendanceExceptionKind.UnreliableLocation);
                else
                {
                    var point = recent.Where(x => Accepted(x) && !x.Raw.IsOffline && x.Assessment!.LocationReliable == true &&
                        x.Assessment.InsideGeofence == true && Point(x.Raw, travel.MaximumAccuracyMeters.Value) &&
                        (decimal)(raw.ReceivedAtUtc - x.Raw.ReceivedAtUtc).Ticks / TimeSpan.TicksPerSecond <= travel.LookbackSeconds)
                        .OrderByDescending(x => x.Raw.ReceivedAtUtc).ThenBy(x => x.Raw.Id).FirstOrDefault();
                    if (point is null) codes.Add("TRAVEL_NO_PRIOR_ACCEPTED_POINT");
                    else
                    {
                        related.Add(point.Raw.Id);
                        // Accuracy radii reduce the distance before a speed comparison.
                        var distance = Math.Max(0d, Distance(raw, point.Raw) - (double)(raw.AccuracyMeters!.Value + point.Raw.AccuracyMeters!.Value));
                        var elapsedSeconds = (raw.ReceivedAtUtc - point.Raw.ReceivedAtUtc).TotalSeconds;
                        if (distance > (double)travel.MaximumMetersPerSecond!.Value * elapsedSeconds)
                            Review("IMPOSSIBLE_TRAVEL", AttendanceExceptionKind.ImpossibleTravel, point.Raw.Id);
                    }
                }
            }
            if (settings.Device.Enabled == true)
            {
                if (deviceObservationCode is null) Review("DEVICE_OBSERVATION_UNAVAILABLE");
                else
                {
                    var mapping = settings.Device.RiskMappings!.SingleOrDefault(x => x.ObservationCode == deviceObservationCode);
                    if (mapping is null) codes.Add("DEVICE_OBSERVATION_UNMAPPED");
                    else Review(mapping.RiskKind == "mock_location" ? "APPROVED_MOCK_LOCATION_RISK" : "APPROVED_DEVICE_INTEGRITY_RISK",
                        AttendanceExceptionKind.DeviceRisk);
                }
            }
            if (codes.Count == 0) codes.Add(settings.Duplicate.Enabled == false && settings.ActiveDuration.Enabled == false &&
                settings.Travel.Enabled == false && settings.Device.Enabled == false ? "CAPTURE_RULES_EXPLICITLY_DISABLED" : "CAPTURE_POLICY_EVALUATED");
        }
        var decision = existingDecision.Outcome is PunchAssessmentOutcome.Accepted or PunchAssessmentOutcome.AcceptedWithException
            ? risk ?? existingDecision : existingDecision;
        if (raw.IsOffline)
        {
            codes.Add("OFFLINE_REQUIRES_REVIEW");
            decision = existingDecision.Outcome == PunchAssessmentOutcome.Unverified ? existingDecision :
                new(PunchAssessmentOutcome.Unverified, "OFFLINE_REQUIRES_REVIEW");
        }
        return new(decision, Array.AsReadOnly(codes.Distinct(StringComparer.Ordinal).ToArray()), policy?.Id, policy?.Revision,
            policy?.SourceFingerprint, Array.AsReadOnly(related.Order().ToArray()), deviceObservationCode);
    }
    private static bool Accepted(RecentCaptureEvidence item) => item.Assessment?.Outcome is
        PunchAssessmentOutcome.Accepted or PunchAssessmentOutcome.AcceptedWithException;
    internal static void ValidateEvidence(RawPunchEvent current, RecentCaptureEvidence item)
    {
        var raw = item.Raw; var assessment = item.Assessment;
        if (raw is null || raw.Id == Guid.Empty || raw.Id == current.Id || raw.EmployeeId != current.EmployeeId || !Enum.IsDefined(raw.Action) ||
            raw.ReceivedAtUtc > current.ReceivedAtUtc || assessment is not null &&
            (assessment.Id == Guid.Empty || assessment.EmployeeId != current.EmployeeId || assessment.RawPunchEventId != raw.Id ||
                assessment.Revision < 1 || !Enum.IsDefined(assessment.Outcome) || assessment.AssessedAtUtc < raw.ReceivedAtUtc ||
                assessment.AssessedAtUtc > current.ReceivedAtUtc))
            throw AttendanceOperation.Conflict("capture_validation_evidence_conflict");
    }
    private static bool Point(RawPunchEvent raw, decimal maximumAccuracy) => raw.Latitude is >= -90 and <= 90 &&
        raw.Longitude is >= -180 and <= 180 && raw.AccuracyMeters is > 0 && raw.AccuracyMeters <= maximumAccuracy;
    private static double Distance(RawPunchEvent first, RawPunchEvent second)
    {
        const double radians = Math.PI / 180d;
        var latitude = (double)(first.Latitude!.Value - second.Latitude!.Value) * radians;
        var longitude = (double)(first.Longitude!.Value - second.Longitude!.Value) * radians;
        var haversine = Math.Pow(Math.Sin(latitude / 2d), 2d) + Math.Cos((double)first.Latitude.Value * radians) *
            Math.Cos((double)second.Latitude.Value * radians) * Math.Pow(Math.Sin(longitude / 2d), 2d);
        return 2d * 6371008.8d * Math.Asin(Math.Sqrt(Math.Clamp(haversine, 0d, 1d)));
    }
}
