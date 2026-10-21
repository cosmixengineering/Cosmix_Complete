#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Attendance.Punches.CaptureValidation;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
async Task Refuse(Func<Task> action, string code)
{
    try { await action(); } catch (AuthenticationFailure e) when (e.Code == code) { checks++; return; }
    throw new Exception("Expected " + code);
}
var clock = new Clock(); var employee = Guid.NewGuid();
var device = new RegisteredDevice(Guid.NewGuid(), "synthetic-device", "android", "Synthetic only", clock.Now.AddDays(-1));
device.MarkVerified(clock.Now.AddDays(-1));
RawPunchEvent Raw(DateTimeOffset? at = null, bool offline = false, PunchAction action = PunchAction.CheckOut) => new(employee, action, device.Id.ToString("D"),
    Guid.NewGuid().ToString("N"), new string('A', 64), at ?? clock.Now, offline, offline ? clock.Now.AddYears(-2) : null, 24m, 67m, 5m);
var source = new Sources(); var evaluator = new CaptureValidationEvaluator(source, source, clock);
var prior = new CaptureDecision(PunchAssessmentOutcome.Accepted, "CAPTURE_VALIDATED");
var missingRaw = Raw();
var missing = await evaluator.EvaluateAsync(missingRaw, device, null, prior, default);
Check(missing.Decision.Outcome == PunchAssessmentOutcome.QueuedForReview && source.Evaluations.Count == 0,
    "Async evaluation returns a pending candidate without persisting any final artifact");
Check(source.RecentReads == 0, "No configured rule invents a recent-read window");
var missingId = evaluator.RecordFinal(missingRaw, missing, missing.Decision, clock.Now);
Check(missingId != Guid.Empty && source.Evaluations.Count == 1 && source.Evaluations[0].Id == missingId &&
    source.Evaluations[0].FinalAssessment.Decision == missing.Decision,
    "Mandatory synchronous RecordFinal stages missing-policy pending evidence and returns its ID");
Check(source.Evaluations[0].EmployeeId == employee && source.Evaluations[0].RegisteredDeviceId == device.Id.ToString("D"),
    "Final artifact retains exact server raw employee/device");

// Explicitly disabled, approved synthetic fixture. Not production configuration.
var disabled = new CaptureValidationSettings(new(false, null), new(false, null), new(false, null, null, null), new(false, null, null));
void SetPolicy(CaptureValidationSettings settings)
{
    var policy = new CaptureValidationPolicyEntry(Guid.NewGuid(), 1, null, clock.Now.AddDays(-1), clock.Now.AddDays(-2),
        "synthetic-admin", "SYNTHETIC-APPROVAL", settings, "Synthetic only", "", "synthetic-proof");
    source.Policy = policy with { SourceFingerprint = CaptureValidationPolicyRules.SourceFingerprint(policy) };
}
SetPolicy(disabled);
Check((await evaluator.EvaluateAsync(Raw(), device, null, prior, default)).Decision == prior && source.RecentReads == 0,
    "Actual evaluator applies approved disabled fixture without default evidence rules");
SetPolicy(disabled with { Duplicate = new(true, 45m), Travel = new(true, 120m, 100m, 10m) });
await evaluator.EvaluateAsync(Raw(), device, null, prior, default);
Check(source.From == clock.Now.AddSeconds(-120) && source.To == clock.Now && source.RecentReads == 1,
    "Recent read uses maximum explicitly configured lookback and server receipt time");
source.Recent = [new(Raw(clock.Now.AddSeconds(-200)), null)];
await Refuse(() => evaluator.EvaluateAsync(Raw(), device, null, prior, default), "capture_validation_query_scope_conflict");
source.Recent = [];
var otherDevice = new RegisteredDevice(Guid.NewGuid(), "synthetic-other", "android", "Synthetic", clock.Now.AddDays(-1));
otherDevice.MarkVerified(clock.Now.AddDays(-1));
var writes = source.Evaluations.Count;
await Refuse(() => evaluator.EvaluateAsync(Raw(), otherDevice, null, prior, default), "capture_validation_device_conflict");
Check(source.Evaluations.Count == writes, "Wrong authoritative device is refused before staging evidence");
await Refuse(() => evaluator.EvaluateAsync(Raw(clock.Now.AddSeconds(1)), device, null, prior, default), "capture_validation_clock_conflict");
SetPolicy(disabled);
Check((await evaluator.EvaluateAsync(Raw(offline: true), device, null, prior, default)).Decision.Outcome == PunchAssessmentOutcome.Unverified,
    "Actual evaluator retains offline Unverified independently of approved disabled policy");
SetPolicy(disabled with { Device = new(true, "SYNTHETIC-CODESET", [new("SYNTHETIC_RISK", "device_integrity")]) });
device.RecordSeen(clock.Now, "SYNTHETIC_RISK");
var mapped = await evaluator.EvaluateAsync(Raw(), device, null, prior, default);
Check(mapped.Codes.Contains("APPROVED_DEVICE_INTEGRITY_RISK") && mapped.DeviceObservationCode == device.IntegrityAssessmentCode,
    "Server registered-device observation, never client verification result, feeds approved mapping");
device.RecordSeen(clock.Now, "UNKNOWN_CODE");
Check((await evaluator.EvaluateAsync(Raw(), device, null, prior, default)).Decision == prior, "Unknown device observation alone is not a risk");
source.PolicyFailure = new AuthenticationFailure("private_storage_unavailable", 503);
var unavailable = await evaluator.EvaluateAsync(Raw(), device, null, prior, default);
Check(unavailable.Decision.Outcome == PunchAssessmentOutcome.QueuedForReview && unavailable.Codes.Contains("CAPTURE_POLICY_EVIDENCE_UNAVAILABLE") &&
    unavailable.PolicyVersionId is null, "Unavailable protected policy retains explicit pending evidence rather than clean acceptance");
var blocked = await evaluator.EvaluateAsync(Raw(), device, null, new(PunchAssessmentOutcome.Rejected, "FACE_VERIFICATION_REQUIRED"), default);
Check(blocked.Decision.ReasonCode == "FACE_VERIFICATION_REQUIRED" && blocked.Codes.Contains("CAPTURE_POLICY_EVIDENCE_UNAVAILABLE"),
    "Unavailable policy proof retains existing gate priority and additional evidence code");
source.PolicyFailure = null;
SetPolicy(disabled);
// An initial Accepted Check In gate is synthetic. No passed biometric proof is manufactured.
var candidateRaw = Raw(action: PunchAction.CheckIn);
var beforeEvaluation = source.Evaluations.Count;
source.OnPolicyRead = () => clock.Now = clock.Now.AddMinutes(3);
var candidate = await evaluator.EvaluateAsync(candidateRaw, device, null, prior, default);
source.OnPolicyRead = null;
Check(candidate.Decision.Outcome == PunchAssessmentOutcome.Accepted && source.Evaluations.Count == beforeEvaluation &&
    candidate.EvaluatedAtUtc == clock.Now && candidate.RawPunchEventId == candidateRaw.Id,
    "Awaited policy IO may outlast face expiry; Accepted remains an unpersisted candidate bound to the raw");
var expired = new CaptureDecision(PunchAssessmentOutcome.Rejected, "FACE_PROOF_EXPIRED");
await Refuse(() => Task.FromResult(evaluator.RecordFinal(candidateRaw, candidate, expired, candidateRaw.ReceivedAtUtc)),
    "capture_validation_final_clock_conflict");
await Refuse(() => Task.FromResult(evaluator.RecordFinal(Raw(), candidate, expired, clock.Now)), "capture_validation_final_binding_conflict");
await Refuse(() => Task.FromResult(evaluator.RecordFinal(candidateRaw, candidate with { EmployeeId = Guid.NewGuid() }, expired, clock.Now)),
    "capture_validation_final_binding_conflict");
var finalId = evaluator.RecordFinal(candidateRaw, candidate, expired, clock.Now);
var recorded = source.Evaluations.Single(x => x.Id == finalId);
var actualRawAssessment = new PunchAssessment(candidateRaw, 1, expired.Outcome, expired.ReasonCode, null,
    "synthetic-b02-finalization", clock.Now);
Check(recorded.CandidateDecision == prior && recorded.FinalAssessment.Decision.Outcome == actualRawAssessment.Outcome &&
    recorded.FinalAssessment.Decision.ReasonCode == actualRawAssessment.ReasonCode &&
    recorded.RawPunchEventId == actualRawAssessment.RawPunchEventId && recorded.AssessedAtUtc == actualRawAssessment.AssessedAtUtc &&
    recorded.FinalAssessment.Codes.Contains("FACE_PROOF_EXPIRED"),
    "Final persisted outcome/code/time bind to the actual raw assessment; Accepted candidate is clearly separate");
await Refuse(() => Task.FromResult(evaluator.RecordFinal(candidateRaw, candidate, expired, clock.Now)), "capture_validation_final_binding_conflict");
Check(source.Evaluations.Count == beforeEvaluation + 1, "RecordFinal cannot stage a second final artifact for the candidate");
source.Policy = null;
var pendingRaw = Raw(); var pendingCandidate = await evaluator.EvaluateAsync(pendingRaw, device, null, prior, default);
await Refuse(() => Task.FromResult(evaluator.RecordFinal(pendingRaw, pendingCandidate, prior, clock.Now)), "capture_validation_final_outcome_conflict");
var offlineRaw = Raw(offline: true); var offlineCandidate = await evaluator.EvaluateAsync(offlineRaw, device, null, prior, default);
var offlineId = evaluator.RecordFinal(offlineRaw, offlineCandidate, offlineCandidate.Decision, clock.Now);
Check(source.Evaluations.Single(x => x.Id == offlineId).FinalAssessment.Decision.Outcome == PunchAssessmentOutcome.Unverified,
    "Missing-policy offline candidate can explicitly finalize Unverified without face acceptance");
await Refuse(() => Task.FromResult(evaluator.RecordFinal(offlineRaw, offlineCandidate, prior, clock.Now)), "capture_validation_final_binding_conflict");
SetPolicy(disabled with { Duplicate = new(true, 45m) });
source.Recent = Enumerable.Range(0, CaptureValidationPolicyRules.MaximumRecentRows + 1).Select(_ =>
    new RecentCaptureEvidence(Raw(clock.Now.AddSeconds(-1)), null)).ToArray();
var overflowRaw = Raw(); var beforeOverflow = source.Evaluations.Count;
var overflow = await evaluator.EvaluateAsync(overflowRaw, device, null, prior, default);
Check(overflow.Decision.Outcome == PunchAssessmentOutcome.QueuedForReview && overflow.Codes.Contains("CAPTURE_RECENT_EVIDENCE_LIMIT") &&
    source.Evaluations.Count == beforeOverflow, "Bounded read overflow cannot be truncated into a clean candidate or prematurely written");
evaluator.RecordFinal(overflowRaw, overflow, overflow.Decision, clock.Now);
Check(source.Evaluations.Count == beforeOverflow + 1, "Explicit finalization can retain pending bounded-read overflow evidence");
source.Recent = [];
Console.WriteLine($"PASS: {checks} B02 actual evaluator checks; synthetic repositories, zero SQL/provider/host access.");

sealed class Clock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
sealed class Sources : ICaptureValidationPolicyReader, ICaptureValidationEvidenceRepository
{
    public CaptureValidationPolicyEntry? Policy;
    public AuthenticationFailure? PolicyFailure;
    public Action? OnPolicyRead;
    public IReadOnlyList<RecentCaptureEvidence> Recent = [];
    public List<CaptureValidationEvidence> Evaluations = [];
    public int RecentReads;
    public DateTimeOffset From, To;
    public async Task<CaptureValidationPolicyEntry?> EffectiveAsync(DateTimeOffset receivedAtUtc, CancellationToken ct)
    {
        await Task.Yield();
        OnPolicyRead?.Invoke();
        if (PolicyFailure is not null) throw PolicyFailure;
        return Policy;
    }
    public Task<IReadOnlyList<RecentCaptureEvidence>> RecentAsync(Guid employeeId, DateTimeOffset fromInclusiveUtc, DateTimeOffset throughInclusiveUtc, CancellationToken ct)
    { RecentReads++; From = fromInclusiveUtc; To = throughInclusiveUtc; return Task.FromResult(Recent); }
    public Task<RecentCaptureEvidence?> PunchAsync(Guid employeeId, Guid punchId, CancellationToken ct) => Task.FromResult<RecentCaptureEvidence?>(null);
    public Task<CaptureValidationEvidence?> FinalAsync(Guid employeeId, Guid punchId, CancellationToken ct) =>
        Task.FromResult(Evaluations.SingleOrDefault(x => x.EmployeeId == employeeId && x.RawPunchEventId == punchId));
    public void Add(CaptureValidationEvidence evidence) => Evaluations.Add(evidence);
}
