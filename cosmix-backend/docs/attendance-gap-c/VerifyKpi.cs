#:project ../../src/Cosmix.Domain/Cosmix.Domain.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.Attendance.Kpi;
using Cosmix.Domain.Common.Approvals;

var checks = 0;
void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
var from = new DateOnly(2025, 7, 1);
var rules = new AdvancedAttendanceRules(1, AdvancedAttendanceFamily.Kpi, ["SITE"],
    new("SYNTHETIC-KPI-C04", "attendance.kpi.decide", true, [AdvancedEvidenceKind.AcceptedDuty]),
    Kpi: new(100, 7, 2, AdvancedRounding.ToEven, 0, 100, KpiLateBasis.PercentOfBaseScore,
        .8m, .5m, KpiBeyondTierTreatment.ReviewRequired, null, KpiShortageTreatment.None, null, false, null) { Composition = KpiCompositionStrategy.Additive });
KpiInput Input(int late = 0) => new(rules, from, from.AddYears(1),
    Enumerable.Range(0, 365).Select(i => new KpiDayInput(from.AddDays(i), Guid.NewGuid(), new string('A', 64),
        i < late, null, null, null, null)).ToArray(), []);
foreach (var (count, score) in new[] { (0, 100m), (30, 100m), (31, 99.2m), (60, 76m), (61, 75.5m), (120, 46m) }) {
    var result = AttendanceKpiCalculation.Calculate(Input(count));
    Check(result.State == "ready_for_review" && result.ExactScore == score && result.LateOccurrences == count,
        $"Exact configured tier at {count}.");
    Check(!result.RequiresDisciplinaryReview, "No premature disciplinary flag.");
}
var beyond = AttendanceKpiCalculation.Calculate(Input(121));
Check(beyond.State == "review_required" && beyond.RequiresDisciplinaryReview && beyond.ExactScore == 46m,
    "Beyond120 requires review, not invented rate/termination.");
var explicitRate = Input(121) with { Rules = rules with { Kpi = rules.Kpi! with { Beyond120Treatment = KpiBeyondTierTreatment.ExplicitRate, Beyond120Percent = 2m } } };
Check(AttendanceKpiCalculation.Calculate(explicitRate).ExactScore == 44m, "Explicit beyond-tier rule retained.");
Check(AttendanceKpiCalculation.Calculate(Input(121) with { Rules = rules with { Kpi = rules.Kpi! with {
    Beyond120Treatment = KpiBeyondTierTreatment.NoExtraKpiEffectAfterIndependentReview } } }) is { State: "ready_for_review", ExactScore: 46m, RequiresDisciplinaryReview: true },
    "No extra score loss is an explicit company choice that still needs disciplinary review, not a missing-value fallback.");
var base80 = Input(31) with { Rules = rules with { Kpi = rules.Kpi! with { BaseScore = 80m } } };
Check(AttendanceKpiCalculation.Calculate(base80).ExactScore == 79.36m, "Percent base is not always100.");
Check(AttendanceKpiCalculation.Calculate(base80 with { Rules = base80.Rules with { Kpi = base80.Rules.Kpi! with { LateBasis = KpiLateBasis.PercentagePoints } } }).ExactScore == 79.2m,
    "Percentage points remains a distinct explicit basis.");
var raw = Input();
var rounded = raw with { Days = raw.Days.Select((d, i) => i == 0 ? d with { ApprovedWaitingPoints = .005m } : d).ToArray() };
var precise = AttendanceKpiCalculation.Calculate(rounded);
Check(precise.ExactScore == 99.995m && precise.DisplayScore == 100m, "Rounded100 must not erase exact benefit boundary.");
Check(AttendanceKpiCalculation.Calculate(raw with { Days = [] }).ExactScore is null, "Missing sources are not100.");
Check(AttendanceKpiCalculation.Calculate(raw with { Days = raw.Days.Skip(1).ToArray() }).State == "unavailable", "Full verified coverage.");
Check(AttendanceKpiCalculation.Calculate(raw with { Days = raw.Days.Select((d, i) => i == 1 ? d with { Date = raw.Days[0].Date } : d).ToArray() }).State == "unavailable", "Duplicate date.");
Check(AttendanceKpiCalculation.Calculate(raw with { Days = raw.Days.Select((d, i) => i == 1 ? d with { SummaryId = raw.Days[0].SummaryId } : d).ToArray() }).State == "unavailable", "Duplicate saved identity.");
Check(AttendanceKpiCalculation.Calculate(raw with { SourceIssues = ["stale_revision"] }).ExactScore is null, "Stale evidence neverzero.");
var shortage = raw with { Rules = rules with { Kpi = rules.Kpi! with { ShortageTreatment = KpiShortageTreatment.PercentagePointsPerHour, ShortageRate = 1m,
    ShortageSourceBasis = KpiShortageSourceBasis.ScheduledPaidCapacityMinusPayable } } };
Check(AttendanceKpiCalculation.Calculate(shortage).State == "unavailable", "No floor-minute shortage fallback.");
shortage = shortage with { Days = shortage.Days.Select((d, i) => d with { CommittedTicks = TimeSpan.TicksPerHour, ShortageTicks = i == 0 ? 1 : 0 }).ToArray() };
Check(AttendanceKpiCalculation.Calculate(shortage).ExactScore < 100m, "One tick shortage survives to exact score.");
var missing = raw with { Rules = rules with { Kpi = rules.Kpi! with { MissingEvidenceEffectEnabled = true, MissingEvidencePoints = 1,
    MissingEvidenceEligibility = new([KpiMissingCaseSetting.EmergencyHalfDay], KpiMissingAssessmentSetting.AtIndependentReview,
        KpiMissingDurationSetting.DatedRosterWindow, [ApprovalState.Approved]) } } };
Check(AttendanceKpiCalculation.Calculate(missing).State == "unavailable", "Missing evidence counts require independent review.");
missing = missing with { Days = missing.Days.Select((d, i) => d with { ApprovedMissingEvidenceOccurrences = i == 0 ? 1 : 0 }).ToArray() };
Check(AttendanceKpiCalculation.Calculate(missing).ExactScore == 99m, "Configured reviewed missing-evidence event.");
var reorder = Input(61);
Check(AttendanceKpiCalculation.Calculate(reorder).ExactScore == AttendanceKpiCalculation.Calculate(reorder with { Days = reorder.Days.Reverse().ToArray() }).ExactScore,
    "Counting chronological despite SQL input order.");
Check(AttendanceKpiCalculation.Year(new(2026, 6, 30), 7) == (new DateOnly(2025, 7, 1), new DateOnly(2026, 7, 1)), "Fiscal reset anchor.");
var leap = new DateOnly(2023, 7, 1);
Check(AttendanceKpiCalculation.Calculate(raw with { From = leap, ToExclusive = leap.AddYears(1), Days = Enumerable.Range(0, 366).Select(i => raw.Days[0] with { Date = leap.AddDays(i), SummaryId = Guid.NewGuid() }).ToArray() }).State == "ready_for_review", "Actual leap-year days.");
Check(AttendanceKpiCalculation.Calculate(raw with { Rules = rules with { Kpi = rules.Kpi! with { BaseScore = null } } }).ExactScore is null, "No unconfigured base.");
Check(AttendanceKpiCalculation.Calculate(raw).Fingerprint != AttendanceKpiCalculation.Calculate(rounded).Fingerprint, "Exact effects participate in immutable fingerprint.");
Check(AttendanceKpiCalculation.Calculate(raw with { ToExclusive = from.AddDays(7), Days = raw.Days.Take(7).ToArray() }).State == "ready_for_review",
    "Current year-to-date previews must use complete actual covered days without inventing future data.");
var composed = Input(31);
composed = composed with { Days = composed.Days.Select((d, i) => i == 30 ? d with { ApprovedWaitingPoints = 1 } : d).ToArray() };
Check(AttendanceKpiCalculation.Calculate(composed).ExactScore == 98.2m, "Explicit additive composition.");
Check(AttendanceKpiCalculation.Calculate(composed with { Rules = rules with { Kpi = rules.Kpi! with { Composition = KpiCompositionStrategy.LargestDailyEffect } } }).ExactScore == 99m,
    "Explicit largest daily effect retains candidate evidence.");
Check(AttendanceKpiCalculation.Calculate(raw with { Rules = rules with { Kpi = rules.Kpi! with { Composition = null } } }).ExactScore is null,
    "No invented component composition.");
Check(AttendanceKpiCalculation.Calculate(raw with { Days = raw.Days.Select((d, i) => i == 0 ? d with { ApprovedWaitingPoints = 200 } : d).ToArray() }) is
    { State: "ready_for_review", ExactScore: 0m }, "Legitimate configured effect above100 composes then clamps; no invented per-day cap.");
Console.WriteLine($"C04 KPI calculation: {checks} checks passed; synthetic source data, no SQL/HR benefit or salary activation.");
