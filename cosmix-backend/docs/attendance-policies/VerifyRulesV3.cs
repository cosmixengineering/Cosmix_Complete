#:project ../../src/Cosmix.Domain/Cosmix.Domain.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Domain.HR.Policies;

var checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
void Invalid(string json, string code, string message)
{
    var value = AttendancePolicyRulesCodec.ValidateAndCanonicalize(json);
    Check(!value.IsValid && value.Rules is null && value.CanonicalJson is null && value.Errors.Any(x => x.Code == code), message);
}
// Fixed historical wire bytes, not regenerated from the current serializer under test.
const string legacy1 = """
{"schemaVersion":1,"arrival":{"lateAfterShiftStartSeconds":900,"halfDayAfterShiftStartSeconds":3600,"hourlyAfterShiftStartSeconds":9000},"paidTime":{"basis":"scheduled_window_overlap","breakTreatment":"count_all_breaks","earlyExitTreatment":"actual_worked_time","halfDayCreditSeconds":16200,"offDayTreatment":"no_credit","holidayTreatment":"scheduled_day_credit"},"lateDeduction":{"occurrenceThreshold":3,"resetPeriod":"calendar_month","countHalfDayArrival":false,"countHourlyArrival":false,"salaryBasis":"base_salary","salaryDivisor":"actual_calendar_month_days","fixedSalaryDivisorDays":null,"deductionDaysPerThreshold":1,"rounding":"whole_currency_unit_half_up"}}
""";
var legacy2 = legacy1.Replace("\"schemaVersion\":1", "\"schemaVersion\":2")
    .Replace("\"holidayTreatment\":\"scheduled_day_credit\"", "\"holidayTreatment\":\"scheduled_day_credit\",\"partialLeaveHalfDayTreatment\":\"half_day_total_includes_paid_leave\"");
foreach (var legacy in new[] { legacy1, legacy2 })
{
    var read = AttendancePolicyRulesCodec.ValidateAndCanonicalize(legacy);
    Check(read.IsValid && read.CanonicalJson == legacy && AttendancePolicyRulesCodec.Canonicalize(read.Rules!) == legacy,
        "Legacy schema canonical bytes and hash inputs must stay exact");
    Check(read.Rules!.OrdinaryPay is null && read.Rules.LateDeduction!.PolicyChangeCountingStrategy is null &&
        read.Rules.LateDeduction.SalaryEffectiveDateBasis is null, "Legacy financial choices remain missing, never inferred");
    var nullableLegacy = JsonNode.Parse(legacy)!;
    nullableLegacy["ordinaryPay"] = null;
    foreach (var field in new[] { "policyChangeCountingStrategy", "salaryEffectiveDateBasis", "enabled", "deductionKind",
        "fixedAmountPkr", "deductionHoursPerThreshold", "weekStartsOn", "overlapTreatment" }) nullableLegacy["lateDeduction"]![field] = null;
    Check(AttendancePolicyRulesCodec.ValidateAndCanonicalize(nullableLegacy.ToJsonString()).CanonicalJson == legacy,
        "New explicit null members also preserve legacy canonical bytes");
}
var previous = AttendancePolicyRulesCodec.ValidateAndCanonicalize(legacy2).Rules!;
var explicitRules = previous with
{
    SchemaVersion = 3,
    LateDeduction = previous.LateDeduction! with
    {
        PolicyChangeCountingStrategy = AttendanceLatePolicyChangeCountingStrategy.RestartOnLateRuleChange,
        SalaryEffectiveDateBasis = AttendanceLateSalaryEffectiveDateBasis.ThresholdCompletionDate,
        Enabled = true,
        Rounding = AttendanceDeductionRounding.TwoDecimalPlacesHalfUp,
        DeductionKind = AttendanceLateDeductionKind.DayFraction,
        OverlapTreatment = AttendanceLateOverlapTreatment.LargerOfOrdinaryOrLate
    },
    OrdinaryPay = new(AttendanceDeductionSalaryBasis.BaseSalary, AttendanceSalaryDivisor.ActualCalendarMonthDays,
        null, 1m, 0.5m, AttendanceHourlyShortfallCalculation.DailyRateOverScheduledHours,
        AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceOrdinaryPaidDayTreatment.NoDeduction,
        AttendanceOrdinaryPaidDayTreatment.NoDeduction, AttendanceDeductionRounding.TwoDecimalPlacesHalfUp)
};
var canonical = AttendancePolicyRulesCodec.Canonicalize(explicitRules);
Check(AttendancePolicyRulesCodec.SupportedSchemaVersion == 3 && AttendancePolicyRulesCodec.ValidateAndCanonicalize(canonical).CanonicalJson == canonical,
    "Schema3 complete financial form round-trips canonically");
var ordinary = explicitRules.OrdinaryPay!;
var node = JsonNode.Parse(canonical)!;
string Changed(string section, string member, JsonNode? value)
{ var changed = node.DeepClone(); changed[section]![member] = value; return changed.ToJsonString(); }
foreach (var section in new[] { "ordinaryPay" })
{
    var missing = node.DeepClone(); missing.AsObject().Remove(section);
    Invalid(missing.ToJsonString(), "required", "Missing schema3 financial section cannot be defaulted");
    missing[section] = null;
    Invalid(missing.ToJsonString(), "required", "Null schema3 financial section cannot be defaulted");
}
foreach (var field in node["ordinaryPay"]!.AsObject().Select(x => x.Key))
{
    var missing = node.DeepClone(); missing["ordinaryPay"]!.AsObject().Remove(field);
    Invalid(missing.ToJsonString(), "json_invalid", "Missing ordinary-pay JSON member rejected: " + field);
    if (field != "fixedSalaryDivisorDays")
        Invalid(Changed("ordinaryPay", field, null), "required", "Null ordinary-pay choice rejected: " + field);
}
foreach (var field in new[] { "policyChangeCountingStrategy", "salaryEffectiveDateBasis", "deductionKind", "overlapTreatment" })
{
    var missing = node.DeepClone(); missing["lateDeduction"]!.AsObject().Remove(field);
    Invalid(missing.ToJsonString(), "required", "Schema3 late interpretation must be explicit: " + field);
    Invalid(Changed("lateDeduction", field, null), "required", "Null late interpretation cannot default: " + field);
    Invalid(Changed("lateDeduction", field, "unsupported"), "json_invalid", "Unknown late interpretation cannot activate: " + field);
    Invalid(Changed("lateDeduction", field, 1), "json_invalid", "Numeric late interpretation cannot bypass named contract: " + field);
}
var absentEnabled = node.DeepClone(); absentEnabled["lateDeduction"]!.AsObject().Remove("enabled");
Invalid(absentEnabled.ToJsonString(), "required", "Late enablement cannot be defaulted from absent input");
Invalid(Changed("lateDeduction", "enabled", null), "required", "Late enablement cannot be inferred from null");
Invalid(Changed("lateDeduction", "enabled", "true"), "json_invalid", "Late enablement is a strict boolean");
Check(AttendancePolicyRulesCodec.Validate(explicitRules with { LateDeduction = explicitRules.LateDeduction! with { Enabled = false } }).IsValid,
    "Explicitly disabled late deduction retains the reviewed configuration");
Invalid(Changed("lateDeduction", "deductionDaysPerThreshold", 1.1m), "out_of_range", "Schema3 late day fraction cannot exceed one full day");
Invalid(Changed("lateDeduction", "deductionDaysPerThreshold", 0m), "out_of_range", "Disable with explicit enablement, not a hidden zero fraction");
Invalid(Changed("lateDeduction", "fixedAmountPkr", 100m), "choice_not_applicable", "Day-fraction mode cannot carry a second fixed charge");
Invalid(Changed("lateDeduction", "deductionHoursPerThreshold", 1m), "choice_not_applicable", "Day-fraction mode cannot carry a second hourly charge");
Invalid(Changed("lateDeduction", "weekStartsOn", "monday"), "choice_not_applicable", "Nonweekly mode cannot silently ignore a week-start choice");
Invalid(Changed("lateDeduction", "resetPeriod", "weekly"), "required", "Weekly counting cannot invent a week-start day");
var fixedLate = explicitRules with { LateDeduction = explicitRules.LateDeduction! with
    { DeductionKind = AttendanceLateDeductionKind.FixedAmount, DeductionDaysPerThreshold = null, FixedAmountPkr = 123.45m } };
Check(AttendancePolicyRulesCodec.Validate(fixedLate).IsValid, "Explicit fixed PKR mode is a distinct supported configuration");
Check(AttendancePolicyRulesCodec.ValidateAndCanonicalize(AttendancePolicyRulesCodec.Canonicalize(fixedLate)).Rules == fixedLate,
    "Fixed PKR mode retains null day multiplier and exact amount");
foreach (var invalidMoney in new[] { 0m, -1m, 1.001m, 10000000000000000m })
    Check(!AttendancePolicyRulesCodec.Validate(fixedLate with { LateDeduction = fixedLate.LateDeduction! with { FixedAmountPkr = invalidMoney } }).IsValid,
        "Fixed amount must fit positive existing decimal(18,2) money contract");
Check(!AttendancePolicyRulesCodec.Validate(fixedLate with { LateDeduction = fixedLate.LateDeduction! with { FixedAmountPkr = null } }).IsValid,
    "Fixed amount cannot be omitted in fixed-amount mode");
var hoursLate = explicitRules with { LateDeduction = explicitRules.LateDeduction! with
    { DeductionKind = AttendanceLateDeductionKind.Hours, DeductionDaysPerThreshold = null, DeductionHoursPerThreshold = 0.5m } };
Check(AttendancePolicyRulesCodec.Validate(hoursLate).IsValid, "Explicit half-hour mode is supported");
foreach (var invalidHours in new[] { 0m, -1m, 24.1m })
    Check(!AttendancePolicyRulesCodec.Validate(hoursLate with { LateDeduction = hoursLate.LateDeduction! with { DeductionHoursPerThreshold = invalidHours } }).IsValid,
        "Configured late hours must be positive and within one-day technical bound");
Check(!AttendancePolicyRulesCodec.Validate(hoursLate with { LateDeduction = hoursLate.LateDeduction! with { DeductionHoursPerThreshold = null } }).IsValid,
    "Hourly mode requires explicit configured hours");
foreach (var weekStart in Enum.GetValues<AttendanceLateWeekStartDay>())
    Check(AttendancePolicyRulesCodec.Validate(explicitRules with { LateDeduction = explicitRules.LateDeduction! with
        { ResetPeriod = AttendanceLateResetPeriod.Weekly, WeekStartsOn = weekStart } }).IsValid,
        "Every explicit week start is supported without a hidden default");
Check(!AttendancePolicyRulesCodec.Validate(explicitRules with { LateDeduction = explicitRules.LateDeduction! with
    { ResetPeriod = AttendanceLateResetPeriod.Weekly, WeekStartsOn = (AttendanceLateWeekStartDay)7 } }).IsValid,
    "Undefined typed week start fails closed");
foreach (var field in new[] { "hourlyShortfallCalculation", "paidLeaveTreatment", "weeklyOffTreatment", "applicablePaidHolidayTreatment" })
{
    Invalid(Changed("ordinaryPay", field, 1), "json_invalid", "Numeric ordinary enum is rejected: " + field);
    Invalid(Changed("ordinaryPay", field, "unknown"), "json_invalid", "Unknown ordinary enum is rejected: " + field);
}
Invalid(Changed("ordinaryPay", "salaryBasis", "gross_salary"), "ordinary_salary_basis_unsupported", "Ordinary inputs cannot infer unconfirmed gross-salary basis");
Invalid(Changed("ordinaryPay", "salaryDivisor", "fixed_days"), "fixed_divisor_required", "Fixed ordinary divisor requires explicit positive days");
Invalid(Changed("ordinaryPay", "fixedSalaryDivisorDays", 30), "fixed_divisor_not_applicable", "Actual-month divisor cannot silently ignore fixed days");
Invalid(Changed("ordinaryPay", "fullUnpaidDayDeductionDays", "1"), "json_invalid", "Ordinary fractions are strict JSON numbers");
Invalid(Changed("ordinaryPay", "fullUnpaidDayDeductionDays", -0.1m), "out_of_range", "Negative ordinary fractions cannot create credits");
Invalid(Changed("ordinaryPay", "fullUnpaidDayDeductionDays", 1.1m), "out_of_range", "Full-day ordinary fraction is bounded");
Invalid(Changed("ordinaryPay", "unpaidHalfDayDeductionDays", 1.1m), "out_of_range", "Half-day ordinary fraction is bounded");
Invalid(Changed("ordinaryPay", "fullUnpaidDayDeductionDays", 0.4m), "unpaid_fraction_order", "Half-day fraction cannot exceed full-day fraction");
Check(AttendancePolicyRulesCodec.Validate(explicitRules with { OrdinaryPay = ordinary with { FullUnpaidDayDeductionDays = 0m, UnpaidHalfDayDeductionDays = 0m } }).IsValid,
    "Explicit zero ordinary fractions are supported configurable choices");
Check(AttendancePolicyRulesCodec.Validate(explicitRules with { OrdinaryPay = ordinary with { SalaryDivisor = AttendanceSalaryDivisor.FixedDays, FixedSalaryDivisorDays = 26 } }).IsValid,
    "Explicit fixed-day ordinary divisor is supported");
foreach (var divisor in new[] { 0, 367 })
    Check(!AttendancePolicyRulesCodec.Validate(explicitRules with { OrdinaryPay = ordinary with { SalaryDivisor = AttendanceSalaryDivisor.FixedDays, FixedSalaryDivisorDays = divisor } }).IsValid,
        "Invalid ordinary divisor rejected");
foreach (var rounding in Enum.GetValues<AttendanceDeductionRounding>())
{
    var permitted = rounding is AttendanceDeductionRounding.TwoDecimalPlacesHalfUp or AttendanceDeductionRounding.TwoDecimalPlacesHalfEven;
    foreach (var section in new[] { "ordinaryPay", "lateDeduction" })
    {
        var value = section == "ordinaryPay" ? explicitRules with { OrdinaryPay = ordinary with { Rounding = rounding } } :
            explicitRules with { LateDeduction = explicitRules.LateDeduction! with { Rounding = rounding } };
        var validated = AttendancePolicyRulesCodec.Validate(value);
        Check(validated.IsValid == permitted && (permitted || validated.Errors.Any(e =>
            e.Code == "deduction_precision_required" && e.Path == $"$.{section}.rounding")),
            "Schema3 deductions require explicit two-decimal precision: " + section + " " + rounding);
    }
    foreach (var legacySchema in new[] { 1, 2 })
    {
        var legacy = AttendancePolicyRulesCodec.ValidateAndCanonicalize(legacySchema == 1 ? legacy1 : legacy2).Rules!;
        var historical = legacy with { LateDeduction = legacy.LateDeduction! with { Rounding = rounding } };
        Check(AttendancePolicyRulesCodec.ValidateAndCanonicalize(AttendancePolicyRulesCodec.Canonicalize(historical)).Rules == historical,
            "Legacy rounding remains readable without conversion: " + legacySchema + " " + rounding);
    }
}
Check(AttendancePolicyRulesCodec.Validate(explicitRules with { LateDeduction = explicitRules.LateDeduction! with
    { Enabled = false, Rounding = AttendanceDeductionRounding.WholeCurrencyUnitHalfUp } }).Errors.Any(e => e.Code == "deduction_precision_required"),
    "Disabled schema3 late settings still retain reviewed two-decimal precision");
Check(AttendancePolicyRulesCodec.Canonicalize(explicitRules with { OrdinaryPay = ordinary with { FullUnpaidDayDeductionDays = 1.000m, UnpaidHalfDayDeductionDays = 0.5000m } }) == canonical,
    "Equivalent financial decimals preserve exact configuration identity");
foreach (var legacySchema in new[] { 1, 2 })
{
    var legacy = AttendancePolicyRulesCodec.ValidateAndCanonicalize(legacySchema == 1 ? legacy1 : legacy2).Rules!;
    Check(AttendancePolicyRulesCodec.Validate(legacy with { OrdinaryPay = ordinary }).Errors.Any(x => x.Code == "schema_option_mismatch"),
        "New ordinary interpretation cannot masquerade as old schema");
    Check(AttendancePolicyRulesCodec.Validate(legacy with { LateDeduction = legacy.LateDeduction! with { PolicyChangeCountingStrategy = AttendanceLatePolicyChangeCountingStrategy.RestartOnLateRuleChange } }).Errors.Any(x => x.Code == "schema_option_mismatch"),
        "New late-count interpretation cannot masquerade as old schema");
    Check(AttendancePolicyRulesCodec.Validate(legacy with { LateDeduction = legacy.LateDeduction! with { ResetPeriod = AttendanceLateResetPeriod.Weekly } }).Errors.Any(x => x.Code == "schema_option_mismatch"),
        "Legacy schema cannot acquire a new weekly interpretation");
}
Invalid(canonical.Replace("\"fullUnpaidDayDeductionDays\":1", "\"fullUnpaidDayDeductionDays\":1,\"fullUnpaidDayDeductionDays\":0"),
    "json_duplicate_property", "Repeated financial value cannot win by parser order");
Invalid(Changed("ordinaryPay", "releasePayment", true), "json_invalid", "Policy settings cannot carry an invented payment-release authority");
var normalized = AttendancePolicyRulesCodec.Validate(explicitRules with { OrdinaryPay = ordinary with { FullUnpaidDayDeductionDays = 0.8m } }).Rules!;
Check(JsonSerializer.Serialize(normalized.LateDeduction) == JsonSerializer.Serialize(explicitRules.LateDeduction),
    "Ordinary changes do not alter the late-rule signature input");
Console.WriteLine($"PASS: {checks} schema3 financial-policy/legacy-byte checks; Domain only, no database or salary writes.");
