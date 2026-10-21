#:project ../../src/Cosmix.Domain/Cosmix.Domain.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Text.Json;
using Cosmix.Domain.HR.Policies;

var count = 0;
void Check(bool value, string reason) { if (!value) throw new Exception(reason); count++; }
AdvancedAttendanceRules Rules(AdvancedAttendanceFamily family) => new(1, family, ["SITE"],
    new("SYNTHETIC-ONLY-C01", AdvancedAttendanceRulesCodec.ReviewPermission(family), true, [AdvancedEvidenceKind.AcceptedDuty]),
    Kpi: family == AdvancedAttendanceFamily.Kpi ? new(100, 7, 2, AdvancedRounding.ToEven, 0, 100,
        KpiLateBasis.PercentOfBaseScore, .8m, .5m, KpiBeyondTierTreatment.ReviewRequired, null,
        KpiShortageTreatment.None, null, false, null) { Composition = KpiCompositionStrategy.Additive } : null,
    Waiting: family == AdvancedAttendanceFamily.Waiting ? new(3600, true, true, WaitingEffect.FactualOnly, null, null) : null,
    EarlyCredit: family == AdvancedAttendanceFamily.EarlyCredit ? new(7200, true, true) : null,
    StudyMakeUp: family == AdvancedAttendanceFamily.StudyMakeUp ? new(true, true, StudyKpiTreatment.OrdinaryApprovedRules, 3600, 30, true) : null,
    ContinuousDuty: family == AdvancedAttendanceFamily.ContinuousDuty ? new(10, 4.5m, 12, true, false, "SYNTHETIC-EXAMPLE") : null,
    CompanyTravel: family == AdvancedAttendanceFamily.CompanyTravel ? new(true, true, false, true) : null);
foreach (var family in Enum.GetValues<AdvancedAttendanceFamily>()) {
    var valid = AdvancedAttendanceRulesCodec.Validate(Rules(family));
    Check(valid.IsValid, $"Explicit synthetic {family} policy must validate.");
    Check(AdvancedAttendanceRulesCodec.Parse(valid.CanonicalJson).CanonicalJson == valid.CanonicalJson, "Canonical round trip.");
    var missingApproval = Rules(family) with { Authority = Rules(family).Authority! with { ApprovalReference = null } };
    Check(!AdvancedAttendanceRulesCodec.Validate(missingApproval).IsValid, "Never invent management approval.");
    Check(!AdvancedAttendanceRulesCodec.Parse(valid.CanonicalJson![..^1] + ",\"unknown\":1}").IsValid, "Unknown properties rejected.");
    Check(!AdvancedAttendanceRulesCodec.Parse(valid.CanonicalJson[..^1] + ",\"schemaVersion\":1}").IsValid, "Duplicate properties rejected.");
}
var k = Rules(AdvancedAttendanceFamily.Kpi);
Check(!AdvancedAttendanceRulesCodec.Validate(k with { Kpi = k.Kpi! with { BaseScore = null } }).IsValid, "Missing base is not zero/100.");
Check(!AdvancedAttendanceRulesCodec.Validate(k with { Kpi = k.Kpi! with { YearStartsInMonth = null } }).IsValid, "No inferred reset period.");
Check(!AdvancedAttendanceRulesCodec.Validate(k with { Waiting = Rules(AdvancedAttendanceFamily.Waiting).Waiting }).IsValid, "Families cannot smuggle other rules.");
Check(!AdvancedAttendanceRulesCodec.Validate(k with { Authority = k.Authority! with { ReviewPermission = "attendance.policies.configure" } }).IsValid, "Dedicated review permission.");
Check(!AdvancedAttendanceRulesCodec.Validate(k with { ApplicableStaffCategoryCodes = ["SITE", "SITE"] }).IsValid, "Duplicate applicability rejected.");
Check(!AdvancedAttendanceRulesCodec.Validate(k with { Kpi = k.Kpi! with { MissingEvidenceEffectEnabled = true } }).IsValid, "No invented missing-evidence penalty.");
var waiting = Rules(AdvancedAttendanceFamily.Waiting);
Check(!AdvancedAttendanceRulesCodec.Validate(waiting with { Waiting = waiting.Waiting! with { Effect = WaitingEffect.FinancialProposal } }).IsValid, "Financial interpretation is explicit, not posting.");
Check(!AdvancedAttendanceRulesCodec.Validate(waiting with { Waiting = waiting.Waiting! with { ExcludeProductiveAlternateWork = false } }).IsValid, "No productive-time loss attribution.");
var travel = Rules(AdvancedAttendanceFamily.CompanyTravel);
Check(!AdvancedAttendanceRulesCodec.Validate(travel with { CompanyTravel = travel.CompanyTravel! with { AutomaticOvertimeAllowed = true } }).IsValid, "Trip approval does not award OT.");
Check(!AdvancedAttendanceRulesCodec.Parse("{}").IsValid, "Empty schema must remain unavailable.");
Check(AdvancedAttendanceRulesCodec.Validate(k with { ApplicableStaffCategoryCodes = ["SITE", "OUTSIDE"] }).CanonicalJson ==
    AdvancedAttendanceRulesCodec.Validate(k with { ApplicableStaffCategoryCodes = ["OUTSIDE", "SITE"] }).CanonicalJson, "Applicability ordering canonical.");
Console.WriteLine($"C01 typed family behavior: {count} checks passed. Synthetic rules only; no SQL/authenticated/provider acceptance.");
