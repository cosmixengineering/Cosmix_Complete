using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Domain.Attendance.Punches;

public enum PunchAssessmentOutcome { Unverified = 1, Accepted = 2, AcceptedWithException = 3, QueuedForReview = 4, Rejected = 5 }

/// <summary>Append-only server interpretation, separate from received evidence. A later interpretation is a new revision.</summary>
public sealed class PunchAssessment : IAppendOnlyEntity
{
    private PunchAssessment() { }
    public PunchAssessment(RawPunchEvent punch, int revision, PunchAssessmentOutcome outcome, string reasonCode,
        Guid? policyVersionId, string calculationVersion, DateTimeOffset assessedAtUtc,
        FaceVerificationAttempt? verification = null, decimal? geofenceDistanceMeters = null,
        bool? locationReliable = null, bool? insideGeofence = null, string? deviceRiskCode = null)
    {
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        if (assessedAtUtc < punch.ReceivedAtUtc) throw new ArgumentException("Assessment cannot predate receipt.");
        if (geofenceDistanceMeters is < 0 or > 99999999.99m) throw new ArgumentOutOfRangeException(nameof(geofenceDistanceMeters));
        if (verification is not null && (punch.Action != PunchAction.CheckIn || verification.EmployeeId != punch.EmployeeId
            || verification.RegisteredDeviceId != punch.RegisteredDeviceId || verification.PunchIdempotencyKey != punch.IdempotencyKey))
            throw new ArgumentException("Verification must belong to this employee, device and Check In request.");
        if (punch.Action == PunchAction.CheckIn && outcome is PunchAssessmentOutcome.Accepted or PunchAssessmentOutcome.AcceptedWithException
            && (verification?.Outcome != FaceVerificationOutcome.Passed || verification.LivenessPassed != true || verification.MatchPassed != true
                || verification.CompletedAtUtc is null || verification.ConsumedByPunchId != punch.Id))
            throw new InvalidOperationException("A successful Check In requires consumed successful verification. Approved alternatives are recorded as corrections.");
        Id = Guid.NewGuid(); RawPunchEventId = punch.Id; EmployeeId = punch.EmployeeId;
        Revision = AttendanceModelGuard.Revision(revision); Outcome = outcome; ReasonCode = ModelGuard.Code(reasonCode, 100);
        PolicyVersionId = AttendanceModelGuard.OptionalId(policyVersionId, nameof(policyVersionId));
        CalculationVersion = ModelGuard.Required(calculationVersion, 100, nameof(calculationVersion));
        AssessedAtUtc = ModelGuard.Utc(assessedAtUtc); FaceVerificationAttemptId = verification?.Id;
        GeofenceDistanceMeters = geofenceDistanceMeters; LocationReliable = locationReliable; InsideGeofence = insideGeofence;
        DeviceRiskCode = ModelGuard.Optional(deviceRiskCode, 100, nameof(deviceRiskCode));
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid RawPunchEventId { get; private set; }
    public int Revision { get; private set; }
    public PunchAssessmentOutcome Outcome { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public Guid? PolicyVersionId { get; private set; }
    public string CalculationVersion { get; private set; } = string.Empty;
    public DateTimeOffset AssessedAtUtc { get; private set; }
    public Guid? FaceVerificationAttemptId { get; private set; }
    public decimal? GeofenceDistanceMeters { get; private set; }
    public bool? LocationReliable { get; private set; }
    public bool? InsideGeofence { get; private set; }
    public string? DeviceRiskCode { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public RawPunchEvent RawPunchEvent { get; private set; } = null!;
    public PolicyVersion? PolicyVersion { get; private set; }
}
