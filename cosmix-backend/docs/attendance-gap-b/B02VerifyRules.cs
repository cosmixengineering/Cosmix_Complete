#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Attendance.Punches.CaptureValidation;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.WorkSessions;

var employee = Guid.NewGuid();
var now = new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
var raw = new RawPunchEvent(employee, PunchAction.CheckOut, Guid.NewGuid().ToString("D"),
    "synthetic-b02-key", new string('A', 64), now, false, latitude: 24m, longitude: 67m, accuracyMeters: 5m);
var prior = new CaptureDecision(PunchAssessmentOutcome.Accepted, "CAPTURE_VALIDATED");
var result = CaptureValidationRules.Evaluate(raw, null, [], null, null, null, prior);
if (result.Decision.Outcome != PunchAssessmentOutcome.QueuedForReview ||
    !result.Codes.Contains("CAPTURE_POLICY_NOT_CONFIGURED") || result.PolicyVersionId is not null)
    throw new Exception("Missing approved policy must retain explicit pending evidence, never clean acceptance.");
var checks = 1;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
void Refuse(Action action, string name)
{
    try { action(); } catch (AuthenticationFailure) { checks++; return; }
    throw new Exception(name);
}
// Every numeric value/code below is synthetic test input, not an approved company rule.
var disabled = new CaptureValidationSettings(new(false, null), new(false, null),
    new(false, null, null, null), new(false, null, null));
CaptureValidationPolicyEntry Policy(CaptureValidationSettings settings)
{
    var entry = new CaptureValidationPolicyEntry(Guid.NewGuid(), 1, null, now.AddDays(-1), now.AddDays(-2),
        "synthetic-admin", "SYNTHETIC-APPROVAL-NOT-COMPANY-POLICY", settings, "Synthetic fixture", "", "synthetic-private-proof");
    return entry with { SourceFingerprint = CaptureValidationPolicyRules.SourceFingerprint(entry) };
}
RawPunchEvent Raw(PunchAction action, DateTimeOffset at, Guid? who = null, bool offline = false,
    decimal? latitude = 24m, decimal? longitude = 67m, decimal? accuracy = 5m) =>
    new(who ?? employee, action, raw.RegisteredDeviceId, Guid.NewGuid().ToString("N"), new string('A', 64), at,
        offline, offline ? now.AddYears(-10) : null, latitude, longitude, accuracy);
RecentCaptureEvidence Accepted(RawPunchEvent punch)
{
    // Check In success cannot be fabricated by a production constructor. Set a synthetic
    // persisted assessment fixture to exercise server interpretation of prior immutable raws.
    var assessment = (PunchAssessment)Activator.CreateInstance(typeof(PunchAssessment), true)!;
    void Set(string name, object value) => typeof(PunchAssessment).GetProperty(name)!.SetValue(assessment, value);
    Set("Id", Guid.NewGuid()); Set("EmployeeId", punch.EmployeeId); Set("RawPunchEventId", punch.Id);
    Set("Revision", 1); Set("Outcome", PunchAssessmentOutcome.Accepted); Set("AssessedAtUtc", punch.ReceivedAtUtc);
    Set("LocationReliable", true); Set("InsideGeofence", true);
    return new(punch, assessment);
}
CaptureValidationAssessment Evaluate(CaptureValidationSettings settings, IReadOnlyList<RecentCaptureEvidence>? recent = null,
    RawPunchEvent? current = null, CaptureDecision? gate = null, WorkSession? active = null,
    RecentCaptureEvidence? opening = null, string? observation = null) =>
    CaptureValidationRules.Evaluate(current ?? raw, Policy(settings), recent ?? [], active, opening, observation, gate ?? prior);

Check(Evaluate(disabled).Decision == prior, "Explicitly approved disabled groups preserve existing valid gates");
var offline = Evaluate(disabled, current: Raw(PunchAction.CheckIn, now, offline: true));
Check(offline.Decision.Outcome == PunchAssessmentOutcome.Unverified, "Offline cannot be accepted even with disabled policy");
Refuse(() => CaptureValidationPolicyRules.Validate(disabled with { Duplicate = new(null, null) }), "Omitted enabled/disabled choice must fail");
Refuse(() => CaptureValidationPolicyRules.Validate(disabled with { Duplicate = new(true, null) }), "Enabled group requires approved numeric limit");
Refuse(() => CaptureValidationPolicyRules.Validate(disabled with { Travel = new(true, 60m, 0m, 10m) }), "Zero speed is invalid");
Refuse(() => CaptureValidationPolicyRules.Validate(disabled with { ActiveDuration = new(false, 10m) }), "Disabled group cannot silently retain applied limits");

var duplicateSettings = disabled with { Duplicate = new(true, 120m) };
var previous = Accepted(Raw(PunchAction.CheckOut, now.AddSeconds(-30)));
var duplicate = Evaluate(duplicateSettings, [previous]);
Check(duplicate.Decision.Outcome == PunchAssessmentOutcome.QueuedForReview && duplicate.Codes.Contains("DUPLICATE_TIME_TOLERANCE") &&
    duplicate.RelatedRawPunchIds.Contains(previous.Raw.Id), "Beyond-key duplicate retains source ID and queues evidence");
var intervening = Accepted(Raw(PunchAction.CheckIn, now.AddSeconds(-15)));
Check(Evaluate(duplicateSettings, [previous, intervening]).Decision == prior,
    "Accepted opposite action permits a legitimate second field/break session inside tolerance");
Check(Evaluate(duplicateSettings, [Accepted(Raw(PunchAction.CheckOut, now.AddSeconds(-121)))]).Decision == prior,
    "Outside approved tolerance is not invented duplicate evidence");
Check(Evaluate(duplicateSettings, [Accepted(Raw(PunchAction.CheckIn, now.AddSeconds(-30)))]).Decision == prior,
    "Another action is not a duplicate");
Check(Evaluate(duplicateSettings, [previous], gate: new(PunchAssessmentOutcome.Rejected, "FACE_VERIFICATION_REQUIRED")).Decision.ReasonCode == "FACE_VERIFICATION_REQUIRED",
    "Existing face failure keeps priority over new risk");

var travelSettings = disabled with { Travel = new(true, 600m, 20m, 15m) };
var point = Accepted(Raw(PunchAction.CheckOut, now.AddSeconds(-10), latitude: 25m));
Check(Evaluate(travelSettings, [point]).Codes.Contains("IMPOSSIBLE_TRAVEL"), "Synthetic approved speed catches impossible travel");
Check(Evaluate(disabled, [point]).Decision == prior, "Disabled travel invents no travel rule");
var poor = Accepted(Raw(PunchAction.CheckOut, now.AddSeconds(-10), latitude: 25m, accuracy: 30m));
Check(!Evaluate(travelSettings, [poor]).Codes.Contains("IMPOSSIBLE_TRAVEL"), "Poor quality prior point is not travel proof");
var unaccepted = new RecentCaptureEvidence(point.Raw, null);
Check(!Evaluate(travelSettings, [unaccepted]).Codes.Contains("IMPOSSIBLE_TRAVEL"), "Unaccepted raw cannot prove travel risk");
Check(Evaluate(travelSettings, current: Raw(PunchAction.CheckOut, now, accuracy: 30m)).Decision.Outcome == PunchAssessmentOutcome.QueuedForReview,
    "Enabled travel with unreliable current point remains pending");
var dateLine = Evaluate(travelSettings with { Travel = new(true, 600m, 10000m, 15m) },
    [Accepted(Raw(PunchAction.CheckOut, now.AddSeconds(-60), latitude: 0m, longitude: 179.99m))],
    current: Raw(PunchAction.CheckOut, now, latitude: 0m, longitude: -179.99m));
Check(!dateLine.Codes.Contains("IMPOSSIBLE_TRAVEL"), "Geodesic wraps across the date line");

var openRaw = Raw(PunchAction.CheckIn, now.AddHours(-2));
var session = new WorkSession(Guid.NewGuid(), 1, employee, DateOnly.FromDateTime(now.UtcDateTime),
    openRaw.ReceivedAtUtc, null, Guid.NewGuid(), null, openRaw.ReceivedAtUtc, openRaw);
var durationSettings = disabled with { ActiveDuration = new(true, 60m) };
Check(Evaluate(durationSettings, active: session, opening: Accepted(openRaw)).Codes.Contains("EXCESSIVE_ACTIVE_DURATION"),
    "Duration uses the accepted server opening receipt");
Check(Evaluate(disabled, active: session, opening: Accepted(openRaw)).Decision == prior, "Disabled duration invents no maximum duty");
Check(Evaluate(durationSettings, active: session).Codes.Contains("ACTIVE_DURATION_SOURCE_UNVERIFIED"), "Missing opening cannot prove clean duration");

var deviceSettings = disabled with { Device = new(true, "SYNTHETIC-APPROVED-CODESET", [new("SYNTHETIC_MOCK", "mock_location")]) };
Check(Evaluate(deviceSettings, observation: "SYNTHETIC_MOCK").Codes.Contains("APPROVED_MOCK_LOCATION_RISK"), "Explicit synthetic code set determines risk");
var unknown = Evaluate(deviceSettings, observation: "UNKNOWN_PROVIDER_CODE");
Check(unknown.Decision == prior && unknown.Codes.Contains("DEVICE_OBSERVATION_UNMAPPED"), "Unknown device code is observation, not automatic risk");
Check(Evaluate(deviceSettings).Codes.Contains("DEVICE_OBSERVATION_UNAVAILABLE"), "Enabled device group with missing evidence stays pending");
Check(Evaluate(disabled, observation: "SYNTHETIC_MOCK").Decision == prior, "Disabled device group does not invent interpretation");
Refuse(() => Evaluate(duplicateSettings, [Accepted(Raw(PunchAction.CheckOut, now.AddSeconds(-10), Guid.NewGuid()))]), "Wrong employee evidence fails immediately");
var wrong = Accepted(Raw(PunchAction.CheckOut, now.AddSeconds(-10)));
typeof(PunchAssessment).GetProperty("RawPunchEventId")!.SetValue(wrong.Assessment, Guid.NewGuid());
Refuse(() => Evaluate(duplicateSettings, [wrong]), "Wrong raw assessment reference fails immediately");
var future = Policy(disabled) with { EffectiveFromUtc = now.AddDays(1) };
future = future with { SourceFingerprint = CaptureValidationPolicyRules.SourceFingerprint(future) };
Refuse(() => CaptureValidationRules.Evaluate(raw, future, [], null, null, null, prior), "Future policy cannot govern an earlier receipt");
var modified = Policy(disabled) with { ApprovalReference = "changed-after-publication" };
Refuse(() => CaptureValidationRules.Evaluate(raw, modified, [], null, null, null, prior), "Changed policy source hash fails closed");
Check(duplicate.PolicyVersionId.HasValue && duplicate.PolicyRevision == 1 && duplicate.SourceFingerprint?.Length == 64,
    "Assessment retains exact independent capture-policy provenance");
Check(CaptureValidationPolicyRules.Effective([Policy(disabled)], now.AddDays(-3)) is null, "No policy before publication effective time");
Console.WriteLine($"PASS: {checks} B02 pure rule checks; every active threshold/code is synthetic, no SQL or provider.");
