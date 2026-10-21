#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Application.Authentication;
using Cosmix.Domain.HR.Policies;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
void Deny(Action action, string code) {
    try { action(); } catch (AuthenticationFailure e) when (e.Code == code) { checks++; return; }
    throw new Exception("Expected " + code);
}
var employee = Guid.NewGuid(); var assignment = Guid.NewGuid(); var site = Guid.NewGuid(); var shift = Guid.NewGuid();
var document = Guid.NewGuid(); var policyId = Guid.NewGuid(); var sourceKey = Guid.NewGuid(); var date = new DateOnly(2026, 10, 13);
DateTimeOffset At(int hour, int minute = 0) => new(2026, 10, 13, hour, minute, 0, TimeSpan.Zero);
AdvancedAttendanceRules Policy(WorkTreatmentKind kind) {
    var family = WorkTreatmentRules.Family(kind);
    var authority = new AdvancedRuleAuthority("EXPLICIT-SYNTHETIC-APPROVAL", AdvancedAttendanceRulesCodec.ReviewPermission(family), true,
        [AdvancedEvidenceKind.AcceptedDuty, AdvancedEvidenceKind.WrittenInstruction, AdvancedEvidenceKind.TripRecord, AdvancedEvidenceKind.VerifiedDocument]);
    return new(1, family, ["SITE"], authority,
        EarlyCredit: family == AdvancedAttendanceFamily.EarlyCredit ? new(3600m, true, true) : null,
        StudyMakeUp: family == AdvancedAttendanceFamily.StudyMakeUp ? new(true, true, StudyKpiTreatment.ExcludeApprovedExcusedInterval, 7200m, 7, true) : null,
        ContinuousDuty: family == AdvancedAttendanceFamily.ContinuousDuty ? new(10m, 4.5m, 12m, true, true, "EXPLICIT-EXAMPLE") : null,
        CompanyTravel: family == AdvancedAttendanceFamily.CompanyTravel ? new(true, true, false, true) : null);
}
WorkTreatmentSource Source(WorkTreatmentKind kind, DateTimeOffset? end = null) {
    var session = new WorkTreatmentSession(Guid.NewGuid(), sourceKey, 1, date, At(8), end ?? At(20), site, null, shift,
        null, assignment, null, null, null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, true, false);
    var roster = new EffectiveRosterResponse(employee, date, "resolved", "UTC", SiteId: site, AssignmentId: assignment,
        ShiftVersionId: shift, StartsAt: At(9), EndsAt: At(18));
    var day = new WorkTreatmentDay(date, new(assignment, Guid.NewGuid(), "SITE", site, null, date, null), roster,
        new("configured", employee, WorkTreatmentRules.Family(kind), date, policyId, [assignment], Policy(kind), new string('A',64)), [session], false);
    var source = new WorkTreatmentSource(employee, kind, date, date, "source_available", "synthetic actual source", [day], [new([new(EmployeeId: employee, SiteId: site)])], "");
    return source with { Fingerprint = WorkTreatmentRules.Fingerprint(source) };
}
WorkTreatmentRequest Proposal(WorkTreatmentSource s, DateTimeOffset start, DateTimeOffset end) => new(s.Kind, date, date,
    start, end, s.Fingerprint, "signed-test-ticket", [new(s.Days[0].Sessions[0].Id, date, start, end)], [], [], null, null, null, null, "explicit factual review");
WorkTreatmentSubmission Instruction(WorkTreatmentSource s, WorkTreatmentKind kind, DateTimeOffset start, DateTimeOffset end) {
    var p = new WorkTreatmentRequest(kind, date, date, start, end, s.Fingerprint, "signed-test-ticket", [], [document], [],
        new(document, "Named employee exact dated instruction", kind == WorkTreatmentKind.StudyInstruction ? TimeSpan.TicksPerHour : null,
            kind == WorkTreatmentKind.StudyInstruction ? date.AddDays(7) : null), null, null, null, "approved instruction");
    return new(1, Guid.NewGuid(), employee, Guid.NewGuid(), Guid.NewGuid(), "instruction-key", At(7), p, s, []);
}
var early = Source(WorkTreatmentKind.EarlyCredit);
var earlyProposal = Proposal(early, At(8,45).AddTicks(7), At(9)) with { InstructionRequestId = Guid.NewGuid() };
Deny(() => WorkTreatmentRules.Evaluate(Guid.NewGuid(), earlyProposal, early, null, [], At(23)), "approved_named_instruction_required");
var earlyInstruction = Instruction(Source(WorkTreatmentKind.EarlyInstruction), WorkTreatmentKind.EarlyInstruction, At(8,30), At(9));
var earlyResult = WorkTreatmentRules.Evaluate(Guid.NewGuid(), earlyProposal, early, earlyInstruction, [], At(23));
Check(earlyResult.ExactTicks == 15 * TimeSpan.TicksPerMinute - 7 && earlyResult.Reservations.Single().Role == WorkCreditRole.EarlyCredit,
    "Explicit early interval retains exact ticks and permitted credit role");
Deny(() => WorkTreatmentRules.Evaluate(Guid.NewGuid(), earlyProposal with { StartUtc = At(8), Intervals = [earlyProposal.Intervals[0] with { StartUtc = At(8) }] }, early, earlyInstruction, [], At(23)),
    "early_credit_outside_written_instruction");
var unverified = early with { Days = [early.Days[0] with { Sessions = [early.Days[0].Sessions[0] with { Verified = false }] }] };
unverified = unverified with { Fingerprint = WorkTreatmentRules.Fingerprint(unverified) };
Deny(() => WorkTreatmentRules.Evaluate(Guid.NewGuid(), earlyProposal with { ExpectedSourceFingerprint = unverified.Fingerprint }, unverified, earlyInstruction, [], At(23)),
    "verified_actual_work_session_required");
var study = Source(WorkTreatmentKind.MakeUp);
var studyInstruction = Instruction(Source(WorkTreatmentKind.StudyInstruction), WorkTreatmentKind.StudyInstruction, At(10), At(11));
var makeUp = Proposal(study, At(18), At(19)) with { InstructionRequestId = studyInstruction.Id };
var makeUpResult = WorkTreatmentRules.Evaluate(Guid.NewGuid(), makeUp, study, studyInstruction, [], At(23));
Check(makeUpResult.ExactTicks == TimeSpan.TicksPerHour && makeUpResult.TdaExcluded &&
    makeUpResult.StudyKpiTreatment == StudyKpiTreatment.ExcludeApprovedExcusedInterval, "Study retains TDA exclusion and exact approved KPI rule without a score");
Deny(() => WorkTreatmentRules.Evaluate(Guid.NewGuid(), makeUp with { StartUtc = At(10), EndUtc = At(11), Intervals = [makeUp.Intervals[0] with { StartUtc = At(10), EndUtc = At(11) }] }, study, studyInstruction, [], At(23)),
    "make_up_outside_obligation_window");
var claim = makeUpResult.Reservations.Single();
foreach (var role in Enum.GetValues<WorkCreditRole>()) {
    var other = claim with { SourceId = Guid.NewGuid(), SessionKey = Guid.NewGuid(), Role = role };
    Deny(() => WorkTreatmentRules.Exclusive([claim], [other]), "work_interval_already_claimed");
}
Deny(() => WorkTreatmentRules.Exclusive([claim, claim with { SourceId = Guid.NewGuid(), SessionKey = Guid.NewGuid() }], []), "work_interval_reused_in_proposal");
WorkTreatmentRules.Exclusive([claim], [claim with { StartUtc = claim.EndUtc, EndUtc = claim.EndUtc.AddTicks(1) }]);
Check(true, "Half-open adjacent ranges remain independent");
WorkTreatmentRules.Exclusive([claim], [claim with { EmployeeId = Guid.NewGuid() }]);
Check(true, "Different employees cannot collide");
var travel = Source(WorkTreatmentKind.CompanyTrip);
var tripResult = WorkTreatmentRules.Evaluate(Guid.NewGuid(), Proposal(travel, At(11), At(12)), travel, null, [], At(23));
Check(tripResult.State == "reviewed_work_proposal" && tripResult.Reservations.Single().Role == WorkCreditRole.NormalWorkTravel,
    "Company trip can produce only a reviewed normal-work proposal");
var home = Source(WorkTreatmentKind.PersonalHomeTravel);
var homeResult = WorkTreatmentRules.Evaluate(Guid.NewGuid(), Proposal(home, At(11), At(12)) with { Intervals = [] }, home, null, [], At(23));
Check(homeResult.State == "leave_or_absence" && homeResult.ExactTicks == 0 && homeResult.Reservations.Count == 0,
    "Personal home travel cannot become work credit");
var continuous = Source(WorkTreatmentKind.ContinuousDuty, At(18));
var continuousProposal = Proposal(continuous, At(8), At(18));
var boundary = WorkTreatmentRules.Evaluate(Guid.NewGuid(), continuousProposal, continuous, null, [], At(23));
Check(boundary.Cycles.Single().ResetReached && boundary.Cycles.Single().NextDayRequiredTicks == 45 * TimeSpan.TicksPerHour / 10,
    "Explicit 10/4.5 configuration retains exact boundary proposal");
var shortSource = Source(WorkTreatmentKind.ContinuousDuty, At(17,59));
var shortResult = WorkTreatmentRules.Evaluate(Guid.NewGuid(), Proposal(shortSource, At(8), At(17,59)), shortSource, null, [], At(23));
Check(!shortResult.Cycles.Single().ResetReached, "One-minute-before reset remains below boundary");
var overnight = Source(WorkTreatmentKind.ContinuousDuty, At(5).AddDays(1));
var extraBoundary = WorkTreatmentRules.Evaluate(Guid.NewGuid(), Proposal(overnight, At(8), At(5).AddDays(1)), overnight, null, [], At(6).AddDays(1));
Check(extraBoundary.Cycles.Single().ExtraBoundaryReached && extraBoundary.Cycles.Single().OutsideRosterTicks == 12 * TimeSpan.TicksPerHour &&
    overnight.Days.Single().Sessions.Single().BusinessDate == date, "Configured 12-extra-hour overnight boundary retains the source business date");
var extraShort = Source(WorkTreatmentKind.ContinuousDuty, At(4,59).AddDays(1));
var extraShortResult = WorkTreatmentRules.Evaluate(Guid.NewGuid(), Proposal(extraShort, At(8), At(4,59).AddDays(1)), extraShort, null, [], At(6).AddDays(1));
Check(!extraShortResult.Cycles.Single().ExtraBoundaryReached, "One-minute-before extra boundary stays below the configured limit");
var breakProposal = continuousProposal with { DocumentIds = [document], CycleEvents = [new(WorkCycleEventKind.Break, At(12), At(13), document)] };
var breakResult = WorkTreatmentRules.Evaluate(Guid.NewGuid(), breakProposal, continuous, null, [], At(23));
Check(breakResult.ExactTicks == 9 * TimeSpan.TicksPerHour && breakResult.Cycles.Count == 2 && breakResult.Cycles.All(x => !x.ResetReached),
    "Verified break excludes duration and honors configured cycle termination");
var noBreakReset = continuous with { Days = [continuous.Days[0] with { Policy = continuous.Days[0].Policy with {
    Rules = Policy(WorkTreatmentKind.ContinuousDuty) with { ContinuousDuty = new(10,4.5m,12,true,false,"EXPLICIT-OTHER-EXAMPLE") } } }] };
noBreakReset = noBreakReset with { Fingerprint = WorkTreatmentRules.Fingerprint(noBreakReset) };
var preserved = WorkTreatmentRules.Evaluate(Guid.NewGuid(), breakProposal with { ExpectedSourceFingerprint = noBreakReset.Fingerprint }, noBreakReset, null, [], At(23));
Check(preserved.Cycles.Count == 1 && preserved.ExactTicks == 9 * TimeSpan.TicksPerHour, "Configured non-ending breaks preserve a cycle without adding gap time");
var missingPolicy = early with { Days = [early.Days[0] with { Policy = early.Days[0].Policy with { Rules = null, State = "missing" } }] };
missingPolicy = missingPolicy with { Fingerprint = WorkTreatmentRules.Fingerprint(missingPolicy) };
Deny(() => WorkTreatmentRules.Policy(missingPolicy), "work_treatment_policy_unavailable");
Deny(() => WorkTreatmentRules.Hours(0.000000000001m), "policy_time_precision_unrepresentable");
Check(WorkTreatmentRules.Fingerprint(early) != WorkTreatmentRules.Fingerprint(early with { Days = [early.Days[0] with { Locked = true }] }),
    "Source fingerprint binds real lock state");
Console.WriteLine($"PASS: {checks} C03/C06 pure factual/precision/exclusivity/configured-example checks. No SQL/provider/host or payable integration.");
