#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); checks++; }
void Failure(Action action, string code) { try { action(); throw new Exception("Expected " + code); }
    catch (AuthenticationFailure failure) { Check(failure.Code == code, code); } }
var employee = Guid.NewGuid(); var leave = Guid.NewGuid(); var date = new DateOnly(2026, 10, 13);
var actor = new AuthenticatedAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
var requester = Guid.NewGuid();
KpiMissingEvidenceRules.Independent(actor, employee, [requester.ToString()]); checks++;
Failure(() => KpiMissingEvidenceRules.Independent(actor with { EmployeeId = employee }, employee, [requester.ToString()]), "access_denied");
Failure(() => KpiMissingEvidenceRules.Independent(actor with { AccountId = requester }, employee, [requester.ToString()]), "access_denied");
var closed = new ApprovalRequest(KpiMissingEvidenceRules.RequestType(date, leave), employee, requester.ToString(), "protected", 1,
    new DateTimeOffset(2026, 10, 13, 1, 0, 0, TimeSpan.Zero));
Failure(() => KpiMissingEvidenceRules.Unique([closed]), "kpi_missing_evidence_review_pending");
closed.Decide(ApprovalState.Approved, actor.AccountId.ToString(), "ConfiguredReviewer", "Verified absence", closed.RequestedAt);
Failure(() => KpiMissingEvidenceRules.Unique([closed]), "kpi_missing_evidence_event_already_reviewed");
var rejected = new ApprovalRequest(KpiMissingEvidenceRules.RequestType(date, leave), employee, requester.ToString(), "protected", 1, closed.RequestedAt);
rejected.Decide(ApprovalState.Rejected, actor.AccountId.ToString(), "ConfiguredReviewer", "Unsupported evidence", rejected.RequestedAt);
KpiMissingEvidenceRules.Unique([rejected]); checks++;
Check(KpiMissingEvidenceRules.Target(date, leave) != KpiMissingEvidenceRules.Target(date.AddDays(1), leave) &&
    KpiMissingEvidenceRules.Target(date, leave) != KpiMissingEvidenceRules.Target(date, Guid.NewGuid()), "uniqueness binds the exact date and leave, not only an employee");
var settings = new KpiMissingEligibilityRules([KpiMissingCaseSetting.EmergencyHalfDay],
    KpiMissingAssessmentSetting.AtIndependentReview, KpiMissingDurationSetting.DatedRosterWindow, [ApprovalState.Pending]);
Check(KpiMissingEvidenceRules.SettingsValid(settings), "explicit case, timing, duration and original approval state accepted");
Check(!KpiMissingEvidenceRules.SettingsValid(settings with { Cases = [] }), "missing case never acquires a default");
Check(!KpiMissingEvidenceRules.SettingsValid(settings with { AllowedLeaveStates = [] }), "missing original-state eligibility never acquires a default");
Check(!KpiMissingEvidenceRules.SettingsValid(settings with { AllowedLeaveStates = [ApprovalState.Cancelled] }), "cancelled leave cannot become missing evidence");
Check(!KpiMissingEvidenceRules.SettingsValid(settings with { AssessmentBasis = (KpiMissingAssessmentSetting)99 }), "unknown company deadline remains unavailable");
Check(!KpiMissingEvidenceRules.SettingsValid(settings with { DurationBasis = null }), "null duration cannot acquire a default");
var approvedMissingPolicy = new KpiRules(100, 1, 2, AdvancedRounding.ToEven, 0, 100, KpiLateBasis.PercentagePoints,
    0.8m, 0.5m, KpiBeyondTierTreatment.ReviewRequired, null, KpiShortageTreatment.None, null, true, 1) {
    Composition = KpiCompositionStrategy.Additive, MissingEvidenceEligibility = settings
};
var rules = new AdvancedAttendanceRules(1, AdvancedAttendanceFamily.Kpi, ["OFFICE"],
    new("Synthetic independently approved test setting", KpiPermissions.Decide, false, []), Kpi: approvedMissingPolicy);
var published = AdvancedAttendanceRulesCodec.Validate(rules);
Check(published.IsValid && published.CanonicalJson is not null, "actual strict Domain publication accepts typed explicit missing eligibility");
var parsed = AdvancedAttendanceRulesCodec.Parse(published.CanonicalJson);
Check(parsed.IsValid && KpiMissingEvidenceRules.Eligibility(parsed.Rules!)?.Cases?.Single() == KpiMissingCaseSetting.EmergencyHalfDay,
    "actual strict parse binds the Domain property directly without a JSON bag");
Check(!AdvancedAttendanceRulesCodec.Validate(rules with { Kpi = approvedMissingPolicy with { MissingEvidenceEligibility = null } }).IsValid,
    "actual publication rejects enabled missing effects without explicit eligibility");
Check(!AdvancedAttendanceRulesCodec.Validate(rules with { Kpi = approvedMissingPolicy with { MissingEvidenceEffectEnabled = false, MissingEvidencePoints = null } }).IsValid,
    "actual publication rejects disabled effects retaining eligibility");
Check(AdvancedAttendanceRulesCodec.Validate(rules with { Kpi = approvedMissingPolicy with {
    MissingEvidenceEffectEnabled = false, MissingEvidencePoints = null, MissingEvidenceEligibility = null } }).IsValid,
    "explicit disabled publication has no inferred eligibility or rate");
Check(KpiMissingEvidenceRules.Duration(240 * TimeSpan.TicksPerMinute, 480 * TimeSpan.TicksPerMinute) == "half_day", "exact half-day classified from explicit source basis");
Check(KpiMissingEvidenceRules.Duration(480 * TimeSpan.TicksPerMinute, 480 * TimeSpan.TicksPerMinute) == "full_day", "exact full day classified");
Check(KpiMissingEvidenceRules.Duration(239 * TimeSpan.TicksPerMinute, 480 * TimeSpan.TicksPerMinute) == "other_interval", "nearly-half interval cannot be rounded into eligibility");
Console.WriteLine($"PASS: {checks} C04 missing-evidence rule/independence/duplicate assertions; no SQL or host.");
