#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyCaptureRules.packages.lock.json
using Cosmix.Application.Attendance.Punches;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.Corrections;

var checks = 0;
void Expect(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
var valid = new CaptureDecisionInput(PunchAction.CheckIn, false, false, 0, "scheduled", true, "inside", true);
Expect(PunchCaptureRules.Decide(valid).Outcome == PunchAssessmentOutcome.Accepted, "valid check in");
Expect(PunchCaptureRules.Decide(valid with { FaceVerified = false }).Outcome == PunchAssessmentOutcome.Rejected, "face fail closed");
Expect(PunchCaptureRules.Decide(valid with { IsOffline = true }).Outcome == PunchAssessmentOutcome.Unverified, "offline evidence only");
Expect(PunchCaptureRules.Decide(valid with { GeofenceStatus = "outside" }).Outcome == PunchAssessmentOutcome.QueuedForReview, "outside recorded for review");
Expect(PunchCaptureRules.Decide(valid with { GeofenceStatus = "poor_accuracy" }).Outcome == PunchAssessmentOutcome.QueuedForReview, "bad accuracy not accepted");
Expect(PunchCaptureRules.Decide(valid with { ActiveSessions = 1 }).ReasonCode == "SEQUENCE_ALREADY_CHECKED_IN", "duplicate sequence");
Expect(PunchCaptureRules.Decide(valid with { ActiveSessions = 2 }).Outcome == PunchAssessmentOutcome.QueuedForReview, "ambiguous active session");
Expect(PunchCaptureRules.Decide(valid with { PeriodLocked = true }).Outcome == PunchAssessmentOutcome.QueuedForReview, "locked interpretation untouched");
Expect(PunchCaptureRules.Decide(valid with { InsideScheduleWindow = false }).Outcome != PunchAssessmentOutcome.Accepted, "unconfirmed early late policy");
var checkOut = valid with { Action = PunchAction.CheckOut, ActiveSessions = 1, FaceVerified = false, InsideScheduleWindow = false };
Expect(PunchCaptureRules.Decide(checkOut).Outcome == PunchAssessmentOutcome.Accepted, "checkout has no face or ending-time gate");
Expect(PunchCaptureRules.Decide(checkOut with { ActiveSessions = 0 }).ReasonCode == "SEQUENCE_NOT_CHECKED_IN", "orphan checkout");
Expect(PunchCaptureRules.Decide(checkOut with { GeofenceStatus = "not_configured" }).Outcome != PunchAssessmentOutcome.Accepted, "missing geofence");
var overlap = PunchCaptureRules.Decide(valid with { RosterStatus = "overlapping_schedule" });
Expect(overlap.Outcome == PunchAssessmentOutcome.QueuedForReview && overlap.ReasonCode == "OVERLAPPING_SHIFT" &&
    overlap.ExceptionKind == AttendanceExceptionKind.OverlappingShift, "explicit overlapping roster creates its factual exception");
Expect(PunchCaptureRules.Decide(valid with { RosterStatus = "roster_unavailable" }).ExceptionKind is null,
    "unavailable roster never fabricates overlapping shift evidence");
Expect(PunchCaptureRules.Decide(valid with { RosterStatus = "overlapping_schedule", IsOffline = true }).Outcome == PunchAssessmentOutcome.Unverified,
    "overlap does not convert offline evidence into validated attendance");
Console.WriteLine($"Capture rule checks passed: {checks}");
