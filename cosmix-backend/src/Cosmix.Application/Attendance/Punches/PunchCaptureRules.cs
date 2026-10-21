using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.Punches;

namespace Cosmix.Application.Attendance.Punches;

public sealed record CaptureDecisionInput(PunchAction Action, bool IsOffline, bool PeriodLocked,
    int ActiveSessions, string RosterStatus, bool InsideScheduleWindow, string GeofenceStatus, bool FaceVerified);
public sealed record CaptureDecision(PunchAssessmentOutcome Outcome, string ReasonCode,
    AttendanceExceptionKind? ExceptionKind = null);

/// <summary>Factual capture gates only. No invented pay, travel-speed, duplicate tolerance or continuous-duty rules.</summary>
public static class PunchCaptureRules
{
    public static CaptureDecision Decide(CaptureDecisionInput input)
    {
        if (input.IsOffline) return new(PunchAssessmentOutcome.Unverified, "OFFLINE_REQUIRES_REVIEW");
        if (input.PeriodLocked) return Review("PERIOD_LOCKED");
        if (input.ActiveSessions > 1) return Review("ACTIVE_SESSION_CONFLICT", AttendanceExceptionKind.SequenceError);
        if (input.Action == PunchAction.CheckIn && input.ActiveSessions != 0)
            return Review("SEQUENCE_ALREADY_CHECKED_IN", AttendanceExceptionKind.SequenceError);
        if (input.Action == PunchAction.CheckOut && input.ActiveSessions != 1)
            return Review("SEQUENCE_NOT_CHECKED_IN", AttendanceExceptionKind.SequenceError);
        if (input.RosterStatus == "overlapping_schedule") return Review("OVERLAPPING_SHIFT", AttendanceExceptionKind.OverlappingShift);
        if (input.RosterStatus != "scheduled") return Review("ROSTER_UNAVAILABLE");
        if (input.GeofenceStatus == "outside") return Review("OUTSIDE_GEOFENCE", AttendanceExceptionKind.OutsideGeofence);
        if (input.GeofenceStatus != "inside") return Review("LOCATION_UNVERIFIED", AttendanceExceptionKind.UnreliableLocation);
        if (input.Action == PunchAction.CheckIn && !input.FaceVerified)
            return new(PunchAssessmentOutcome.Rejected, "FACE_VERIFICATION_REQUIRED", AttendanceExceptionKind.FaceVerification);
        if (input.Action == PunchAction.CheckIn && !input.InsideScheduleWindow)
            return Review("OUTSIDE_SCHEDULE_WINDOW");
        return new(PunchAssessmentOutcome.Accepted, "CAPTURE_VALIDATED");
    }

    private static CaptureDecision Review(string code, AttendanceExceptionKind? exception = null) =>
        new(PunchAssessmentOutcome.QueuedForReview, code, exception);
}
