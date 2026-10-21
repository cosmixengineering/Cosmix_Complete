using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Domain.HR.Policies;

public enum AdvancedAttendanceFamily { Kpi = 1, Waiting = 2, EarlyCredit = 3, StudyMakeUp = 4, ContinuousDuty = 5, CompanyTravel = 6 }
public enum KpiLateBasis { PercentagePoints = 1, PercentOfBaseScore = 2 }
public enum KpiBeyondTierTreatment { ReviewRequired = 1, ExplicitRate = 2, NoExtraKpiEffectAfterIndependentReview = 3 }
public enum KpiShortageTreatment { None = 1, PercentagePointsPerHour = 2, PercentOfCommittedTime = 3 }
public enum KpiCompositionStrategy { Additive = 1, LargestDailyEffect = 2 }
public enum KpiShortageSourceBasis { ScheduledPaidCapacityMinusPayable = 1, RequiredDutyNetOfApprovedLeaveAndConfiguredBreaks = 2 }
public enum KpiMissingCaseSetting { MedicalSickLeave = 1, EmergencyHalfDay = 2 }
public enum KpiMissingAssessmentSetting { AtIndependentReview = 1 }
public enum KpiMissingDurationSetting { DatedRosterWindow = 1, PublishedLeaveStandardDay = 2 }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record KpiMissingEligibilityRules([property: JsonRequired] IReadOnlyList<KpiMissingCaseSetting>? Cases,
    [property: JsonRequired] KpiMissingAssessmentSetting? AssessmentBasis,
    [property: JsonRequired] KpiMissingDurationSetting? DurationBasis,
    [property: JsonRequired] IReadOnlyList<ApprovalState>? AllowedLeaveStates);
public enum AdvancedRounding { ToEven = 1, AwayFromZero = 2 }
public enum WaitingEffect { FactualOnly = 1, PerformanceProposal = 2, FinancialProposal = 3 }
public enum StudyKpiTreatment { OrdinaryApprovedRules = 1, ExcludeApprovedExcusedInterval = 2 }
public enum AdvancedEvidenceKind { AcceptedDuty = 1, ApprovedCorrection = 2, VerifiedDocument = 3, SiteReport = 4, WrittenInstruction = 5, TripRecord = 6 }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdvancedRuleAuthority([property: JsonRequired] string? ApprovalReference,
    [property: JsonRequired] string? ReviewPermission,
    [property: JsonRequired] bool? RequireSupportingEvidence,
    [property: JsonRequired] IReadOnlyList<AdvancedEvidenceKind>? AllowedEvidence);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record KpiRules([property: JsonRequired] decimal? BaseScore,
    [property: JsonRequired] int? YearStartsInMonth, [property: JsonRequired] int? DisplayDecimalPlaces,
    [property: JsonRequired] AdvancedRounding? Rounding, [property: JsonRequired] decimal? MinimumScore,
    [property: JsonRequired] decimal? MaximumScore, [property: JsonRequired] KpiLateBasis? LateBasis,
    [property: JsonRequired] decimal? Late31To60Percent, [property: JsonRequired] decimal? Late61To120Percent,
    [property: JsonRequired] KpiBeyondTierTreatment? Beyond120Treatment, [property: JsonRequired] decimal? Beyond120Percent,
    [property: JsonRequired] KpiShortageTreatment? ShortageTreatment, [property: JsonRequired] decimal? ShortageRate,
    [property: JsonRequired] bool? MissingEvidenceEffectEnabled, [property: JsonRequired] decimal? MissingEvidencePoints)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public KpiCompositionStrategy? Composition { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public KpiShortageSourceBasis? ShortageSourceBasis { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public KpiMissingEligibilityRules? MissingEvidenceEligibility { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WaitingRules([property: JsonRequired] decimal? MaximumAttributedSeconds,
    [property: JsonRequired] bool? ExcludeProductiveAlternateWork, [property: JsonRequired] bool? UnionOverlappingLoss,
    [property: JsonRequired] WaitingEffect? Effect, [property: JsonRequired] decimal? PerformancePointsPerHour,
    [property: JsonRequired] string? FinancialInterpretationReference);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EarlyCreditRules([property: JsonRequired] decimal? MaximumPreStartSeconds,
    [property: JsonRequired] bool? WrittenInstructionRequired, [property: JsonRequired] bool? NamedAssignmentRequired);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudyMakeUpRules([property: JsonRequired] bool? HourlyTreatment,
    [property: JsonRequired] bool? TdaExcluded, [property: JsonRequired] StudyKpiTreatment? KpiTreatment,
    [property: JsonRequired] decimal? MaximumMakeUpSeconds, [property: JsonRequired] int? FulfillmentDeadlineDays,
    [property: JsonRequired] bool? ExclusiveWorkAllocation);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ContinuousDutyRules([property: JsonRequired] decimal? ResetAfterActualDutyHours,
    [property: JsonRequired] decimal? NextDayRequiredHours, [property: JsonRequired] decimal? ExtraHoursBoundary,
    [property: JsonRequired] bool? HomeVisitEndsCycle, [property: JsonRequired] bool? BreaksEndCycle,
    [property: JsonRequired] string? ApprovedExampleReference);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CompanyTravelRules([property: JsonRequired] bool? ApprovedCompanyTripIsNormalWork,
    [property: JsonRequired] bool? PersonalHomeTravelIsLeave,
    [property: JsonRequired] bool? AutomaticOvertimeAllowed, [property: JsonRequired] bool? ExclusiveWorkAllocation);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdvancedAttendanceRules([property: JsonRequired] int? SchemaVersion,
    [property: JsonRequired] AdvancedAttendanceFamily? Family,
    [property: JsonRequired] IReadOnlyList<string>? ApplicableStaffCategoryCodes,
    [property: JsonRequired] AdvancedRuleAuthority? Authority,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] KpiRules? Kpi = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WaitingRules? Waiting = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] EarlyCreditRules? EarlyCredit = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] StudyMakeUpRules? StudyMakeUp = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ContinuousDutyRules? ContinuousDuty = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CompanyTravelRules? CompanyTravel = null);

public sealed record AdvancedRulesValidation(AdvancedAttendanceRules? Rules, string? CanonicalJson,
    IReadOnlyList<AttendancePolicyRuleError> Errors)
{
    public bool IsValid => Rules is not null && CanonicalJson is not null && Errors.Count == 0;
}

/// <summary>Independent family schema; ordinary attendance schema1-3 and late counting are unchanged.</summary>
public static class AdvancedAttendanceRulesCodec
{
    public const string Schema = "attendance.advanced.v1";
    public static string Code(AdvancedAttendanceFamily family) => Enum.IsDefined(family)
        ? "ATTADV_" + family.ToString().ToUpperInvariant() : throw new ArgumentOutOfRangeException(nameof(family));
    public static string ReviewPermission(AdvancedAttendanceFamily family) => family switch {
        AdvancedAttendanceFamily.Kpi => "attendance.kpi.decide",
        AdvancedAttendanceFamily.Waiting => "attendance.waiting.decide",
        _ when Enum.IsDefined(family) => "attendance.work-treatments.decide",
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };
    private static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16
    };
    public static AdvancedRulesValidation Parse(string? json)
    {
        if (json is null || Encoding.UTF8.GetByteCount(json) > 32000) return Invalid("$", "invalid_policy_json");
        try {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object || Duplicate(document.RootElement))
                return Invalid("$", "invalid_policy_json");
            return Validate(JsonSerializer.Deserialize<AdvancedAttendanceRules>(json, Options));
        } catch (JsonException) { return Invalid("$", "invalid_policy_json"); }
    }
    public static AdvancedRulesValidation Validate(AdvancedAttendanceRules? rules)
    {
        if (rules is null) return Invalid("$", "rules_required");
        var errors = new List<AttendancePolicyRuleError>();
        void Error(string path, string code) => errors.Add(new(path, code, "Supply an explicit approved, applicable value."));
        void Choice<T>(T? value, string path) where T : struct, Enum { if (!value.HasValue || !Enum.IsDefined(value.Value)) Error(path, "required_choice"); }
        void Positive(decimal? value, decimal maximum, string path) { if (value is null || value <= 0 || value > maximum) Error(path, "positive_value_required"); }
        void Required(bool? value, string path) { if (value is null) Error(path, "required"); }
        if (rules.SchemaVersion != 1) Error("$.schemaVersion", "unsupported_schema");
        Choice(rules.Family, "$.family");
        if (rules.ApplicableStaffCategoryCodes is not { Count: > 0 and <= 32 } codes ||
            codes.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 40 || x != x.Trim().ToUpperInvariant() ||
                x.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.'))) ||
            codes.Distinct(StringComparer.Ordinal).Count() != codes.Count) Error("$.applicableStaffCategoryCodes", "explicit_categories_required");
        if (rules.Authority is not { } authority) Error("$.authority", "authority_required");
        else {
            if (string.IsNullOrWhiteSpace(authority.ApprovalReference) || authority.ApprovalReference.Length > 500 || authority.ApprovalReference.Any(char.IsControl))
                Error("$.authority.approvalReference", "approval_reference_required");
            if (rules.Family.HasValue && Enum.IsDefined(rules.Family.Value) && authority.ReviewPermission != ReviewPermission(rules.Family.Value))
                Error("$.authority.reviewPermission", "dedicated_permission_required");
            Required(authority.RequireSupportingEvidence, "$.authority.requireSupportingEvidence");
            if (authority.AllowedEvidence is not { Count: <= 6 } evidence || evidence.Any(x => !Enum.IsDefined(x)) ||
                evidence.Distinct().Count() != evidence.Count || authority.RequireSupportingEvidence == true && evidence.Count == 0)
                Error("$.authority.allowedEvidence", "explicit_evidence_required");
        }
        var selected = new (AdvancedAttendanceFamily Family, object? Value)[] {
            (AdvancedAttendanceFamily.Kpi, rules.Kpi), (AdvancedAttendanceFamily.Waiting, rules.Waiting),
            (AdvancedAttendanceFamily.EarlyCredit, rules.EarlyCredit), (AdvancedAttendanceFamily.StudyMakeUp, rules.StudyMakeUp),
            (AdvancedAttendanceFamily.ContinuousDuty, rules.ContinuousDuty), (AdvancedAttendanceFamily.CompanyTravel, rules.CompanyTravel)
        };
        if (selected.Any(x => (x.Family == rules.Family) != (x.Value is not null))) Error("$", "exact_family_required");
        if (rules.Kpi is { } k) {
            if (k.BaseScore is null or < 0 or > 100 || k.MinimumScore is null or < 0 or > 100 || k.MaximumScore is null or < 0 or > 100 ||
                k.MinimumScore > k.BaseScore || k.BaseScore > k.MaximumScore) Error("$.kpi", "invalid_score_bounds");
            if (k.YearStartsInMonth is null or < 1 or > 12) Error("$.kpi.yearStartsInMonth", "explicit_year_required");
            if (k.DisplayDecimalPlaces is null or < 0 or > 6) Error("$.kpi.displayDecimalPlaces", "explicit_precision_required");
            Choice(k.Rounding, "$.kpi.rounding"); Choice(k.LateBasis, "$.kpi.lateBasis");
            if (k.Late31To60Percent != 0.8m || k.Late61To120Percent != 0.5m) Error("$.kpi", "srs_tier_values_required");
            Choice(k.Beyond120Treatment, "$.kpi.beyond120Treatment");
            if (k.Beyond120Treatment == KpiBeyondTierTreatment.ExplicitRate) Positive(k.Beyond120Percent, 100, "$.kpi.beyond120Percent");
            else if (k.Beyond120Percent is not null) Error("$.kpi.beyond120Percent", "not_applicable");
            Choice(k.ShortageTreatment, "$.kpi.shortageTreatment");
            Choice(k.Composition, "$.kpi.composition");
            if (k.ShortageTreatment == KpiShortageTreatment.None) {
                if (k.ShortageRate is not null || k.ShortageSourceBasis is not null) Error("$.kpi.shortageRate", "not_applicable");
            } else {
                Positive(k.ShortageRate, 100, "$.kpi.shortageRate");
                Choice(k.ShortageSourceBasis, "$.kpi.shortageSourceBasis");
            }
            Required(k.MissingEvidenceEffectEnabled, "$.kpi.missingEvidenceEffectEnabled");
            if (k.MissingEvidenceEffectEnabled == true) {
                Positive(k.MissingEvidencePoints, 100, "$.kpi.missingEvidencePoints");
                if (k.MissingEvidenceEligibility is not { } eligibility) Error("$.kpi.missingEvidenceEligibility", "explicit_eligibility_required");
                else {
                    if (eligibility.Cases is not { Count: > 0 and <= 2 } cases || cases.Any(x => !Enum.IsDefined(x)) || cases.Distinct().Count() != cases.Count)
                        Error("$.kpi.missingEvidenceEligibility.cases", "explicit_cases_required");
                    Choice(eligibility.AssessmentBasis, "$.kpi.missingEvidenceEligibility.assessmentBasis");
                    Choice(eligibility.DurationBasis, "$.kpi.missingEvidenceEligibility.durationBasis");
                    if (eligibility.AllowedLeaveStates is not { Count: > 0 and <= 4 } states ||
                        states.Any(x => x is not (ApprovalState.Pending or ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected)) ||
                        states.Distinct().Count() != states.Count) Error("$.kpi.missingEvidenceEligibility.allowedLeaveStates", "explicit_states_required");
                }
            } else if (k.MissingEvidencePoints is not null || k.MissingEvidenceEligibility is not null)
                Error("$.kpi.missingEvidencePoints", "not_applicable");
        }
        if (rules.Waiting is { } w) {
            Positive(w.MaximumAttributedSeconds, 366m * 86400m, "$.waiting.maximumAttributedSeconds");
            if (w.ExcludeProductiveAlternateWork != true || w.UnionOverlappingLoss != true) Error("$.waiting", "exclusive_causation_required");
            Choice(w.Effect, "$.waiting.effect");
            if (w.Effect == WaitingEffect.PerformanceProposal) Positive(w.PerformancePointsPerHour, 100, "$.waiting.performancePointsPerHour");
            else if (w.PerformancePointsPerHour is not null) Error("$.waiting.performancePointsPerHour", "not_applicable");
            if (w.Effect == WaitingEffect.FinancialProposal ? string.IsNullOrWhiteSpace(w.FinancialInterpretationReference) || w.FinancialInterpretationReference.Length > 500 : w.FinancialInterpretationReference is not null)
                Error("$.waiting.financialInterpretationReference", "explicit_financial_contract_required");
        }
        if (rules.EarlyCredit is { } e) {
            Positive(e.MaximumPreStartSeconds, 86400, "$.earlyCredit.maximumPreStartSeconds");
            if (e.WrittenInstructionRequired != true || e.NamedAssignmentRequired != true) Error("$.earlyCredit", "explicit_instruction_required");
        }
        if (rules.StudyMakeUp is { } s) {
            Required(s.HourlyTreatment, "$.studyMakeUp.hourlyTreatment");
            if (s.TdaExcluded != true || s.ExclusiveWorkAllocation != true) Error("$.studyMakeUp", "exclusive_study_treatment_required");
            Choice(s.KpiTreatment, "$.studyMakeUp.kpiTreatment"); Positive(s.MaximumMakeUpSeconds, 366m * 86400m, "$.studyMakeUp.maximumMakeUpSeconds");
            if (s.FulfillmentDeadlineDays is null or < 1 or > 366) Error("$.studyMakeUp.fulfillmentDeadlineDays", "explicit_deadline_required");
        }
        if (rules.ContinuousDuty is { } c) {
            Positive(c.ResetAfterActualDutyHours, 744, "$.continuousDuty.resetAfterActualDutyHours");
            Positive(c.NextDayRequiredHours, 24, "$.continuousDuty.nextDayRequiredHours");
            Positive(c.ExtraHoursBoundary, 744, "$.continuousDuty.extraHoursBoundary");
            Required(c.HomeVisitEndsCycle, "$.continuousDuty.homeVisitEndsCycle"); Required(c.BreaksEndCycle, "$.continuousDuty.breaksEndCycle");
            if (string.IsNullOrWhiteSpace(c.ApprovedExampleReference) || c.ApprovedExampleReference.Length > 500) Error("$.continuousDuty.approvedExampleReference", "approved_examples_required");
        }
        if (rules.CompanyTravel is { } t && (t.ApprovedCompanyTripIsNormalWork != true || t.PersonalHomeTravelIsLeave != true ||
            t.AutomaticOvertimeAllowed != false || t.ExclusiveWorkAllocation != true)) Error("$.companyTravel", "approved_travel_boundary_required");
        if (errors.Count != 0) return new(null, null, errors);
        var canonical = JsonSerializer.Serialize(rules with { ApplicableStaffCategoryCodes = rules.ApplicableStaffCategoryCodes!.Order(StringComparer.Ordinal).ToArray(),
            Authority = rules.Authority! with { AllowedEvidence = rules.Authority!.AllowedEvidence!.Order().ToArray() } }, Options);
        return Encoding.UTF8.GetByteCount(canonical) <= 32000 ? new(rules, canonical, []) : Invalid("$", "invalid_policy_json");
    }
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static AdvancedRulesValidation Invalid(string path, string code) => new(null, null, [new(path, code, "Supply complete approved typed rules.")]);
    private static bool Duplicate(JsonElement element) {
        if (element.ValueKind == JsonValueKind.Object) {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in element.EnumerateObject()) if (!names.Add(p.Name) || Duplicate(p.Value)) return true;
        } else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) if (Duplicate(item)) return true;
        return false;
    }
}
