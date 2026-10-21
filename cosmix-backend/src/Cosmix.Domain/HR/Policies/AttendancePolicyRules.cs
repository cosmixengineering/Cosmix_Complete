using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cosmix.Domain.HR.Policies;

/// <summary>Explicit versioned form input. Validating rules does not publish or approve a policy.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record AttendancePolicyRules(
    [property: JsonRequired] int? SchemaVersion,
    [property: JsonRequired] AttendanceArrivalRules? Arrival,
    [property: JsonRequired] AttendancePaidTimeRules? PaidTime,
    [property: JsonRequired] AttendanceLateDeductionRules? LateDeduction,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AttendanceOrdinaryPayRules? OrdinaryPay = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record AttendanceArrivalRules(
    [property: JsonRequired] int? LateAfterShiftStartSeconds,
    [property: JsonRequired] int? HalfDayAfterShiftStartSeconds,
    [property: JsonRequired] int? HourlyAfterShiftStartSeconds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AttendanceLateThresholdComparison? LateThresholdComparison = null);

[JsonConverter(typeof(AttendancePolicyEnumConverter<AttendanceLateThresholdComparison>))]
public enum AttendanceLateThresholdComparison { StrictlyAfter = 1, AtOrAfter = 2 }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record AttendancePaidTimeRules(
    [property: JsonRequired] AttendancePaidTimeBasis? Basis,
    [property: JsonRequired] AttendanceBreakTreatment? BreakTreatment,
    [property: JsonRequired] AttendanceEarlyExitTreatment? EarlyExitTreatment,
    [property: JsonRequired] int? HalfDayCreditSeconds,
    [property: JsonRequired] AttendanceNonScheduledDayTreatment? OffDayTreatment,
    [property: JsonRequired] AttendanceNonScheduledDayTreatment? HolidayTreatment,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AttendancePartialLeaveHalfDayTreatment? PartialLeaveHalfDayTreatment = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record AttendanceLateDeductionRules(
    [property: JsonRequired] int? OccurrenceThreshold,
    [property: JsonRequired] AttendanceLateResetPeriod? ResetPeriod,
    [property: JsonRequired] bool? CountHalfDayArrival,
    [property: JsonRequired] bool? CountHourlyArrival,
    [property: JsonRequired] AttendanceDeductionSalaryBasis? SalaryBasis,
    [property: JsonRequired] AttendanceSalaryDivisor? SalaryDivisor,
    [property: JsonRequired] int? FixedSalaryDivisorDays,
    [property: JsonRequired] decimal? DeductionDaysPerThreshold,
    [property: JsonRequired] AttendanceDeductionRounding? Rounding,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AttendanceLatePolicyChangeCountingStrategy? PolicyChangeCountingStrategy = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AttendanceLateSalaryEffectiveDateBasis? SalaryEffectiveDateBasis = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Enabled = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AttendanceLateDeductionKind? DeductionKind = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? FixedAmountPkr = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? DeductionHoursPerThreshold = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AttendanceLateWeekStartDay? WeekStartsOn = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AttendanceLateOverlapTreatment? OverlapTreatment = null);

/// <summary>Explicit payroll-input interpretation. These settings do not authorize salary mutation or payment release.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record AttendanceOrdinaryPayRules(
    [property: JsonRequired] AttendanceDeductionSalaryBasis? SalaryBasis,
    [property: JsonRequired] AttendanceSalaryDivisor? SalaryDivisor,
    [property: JsonRequired] int? FixedSalaryDivisorDays,
    [property: JsonRequired] decimal? FullUnpaidDayDeductionDays,
    [property: JsonRequired] decimal? UnpaidHalfDayDeductionDays,
    [property: JsonRequired] AttendanceHourlyShortfallCalculation? HourlyShortfallCalculation,
    [property: JsonRequired] AttendanceOrdinaryPaidDayTreatment? PaidLeaveTreatment,
    [property: JsonRequired] AttendanceOrdinaryPaidDayTreatment? WeeklyOffTreatment,
    [property: JsonRequired] AttendanceOrdinaryPaidDayTreatment? ApplicablePaidHolidayTreatment,
    [property: JsonRequired] AttendanceDeductionRounding? Rounding);

[JsonConverter(typeof(AttendancePolicyEnumConverter<AttendanceHourlyShortfallCalculation>))]
public enum AttendanceHourlyShortfallCalculation { DailyRateOverScheduledHours = 1 }
[JsonConverter(typeof(AttendancePolicyEnumConverter<AttendanceOrdinaryPaidDayTreatment>))]
public enum AttendanceOrdinaryPaidDayTreatment { NoDeduction = 1 }
[JsonConverter(typeof(AttendancePolicyEnumConverter<AttendanceLatePolicyChangeCountingStrategy>))]
public enum AttendanceLatePolicyChangeCountingStrategy { RestartOnLateRuleChange = 1 }
[JsonConverter(typeof(AttendancePolicyEnumConverter<AttendanceLateSalaryEffectiveDateBasis>))]
public enum AttendanceLateSalaryEffectiveDateBasis { ThresholdCompletionDate = 1 }
[JsonConverter(typeof(AttendancePolicyEnumConverter<AttendanceLateDeductionKind>))]
public enum AttendanceLateDeductionKind { DayFraction = 1, FixedAmount = 2, Hours = 3 }
[JsonConverter(typeof(AttendancePolicyEnumConverter<AttendanceLateWeekStartDay>))]
public enum AttendanceLateWeekStartDay { Sunday = 0, Monday = 1, Tuesday = 2, Wednesday = 3, Thursday = 4, Friday = 5, Saturday = 6 }
[JsonConverter(typeof(AttendancePolicyEnumConverter<AttendanceLateOverlapTreatment>))]
public enum AttendanceLateOverlapTreatment { LargerOfOrdinaryOrLate = 1 }

[JsonConverter(typeof(AttendancePolicyEnumConverter<AttendancePaidTimeBasis>))]
public enum AttendancePaidTimeBasis { ActualElapsedDuty = 1, ScheduledWindowOverlap = 2 }
[JsonConverter(typeof(AttendancePolicyEnumConverter<AttendanceBreakTreatment>))]
public enum AttendanceBreakTreatment { CountAllBreaks = 1, DeductScheduledUnpaidBreak = 2 }
[JsonConverter(typeof(AttendancePolicyEnumConverter<AttendanceEarlyExitTreatment>))]
public enum AttendanceEarlyExitTreatment { ActualWorkedTime = 1, HalfDayCredit = 2 }
[JsonConverter(typeof(AttendancePolicyEnumConverter<AttendancePartialLeaveHalfDayTreatment>))]
public enum AttendancePartialLeaveHalfDayTreatment { HalfDayTotalIncludesPaidLeave = 1 }
[JsonConverter(typeof(AttendancePolicyEnumConverter<AttendanceNonScheduledDayTreatment>))]
public enum AttendanceNonScheduledDayTreatment { ActualWorkedTime = 1, NoCredit = 2, ScheduledDayCredit = 3 }
[JsonConverter(typeof(AttendancePolicyEnumConverter<AttendanceLateResetPeriod>))]
public enum AttendanceLateResetPeriod { CalendarMonth = 1, CalendarYear = 2, FiscalJulyJune = 3, Weekly = 4 }
[JsonConverter(typeof(AttendancePolicyEnumConverter<AttendanceDeductionSalaryBasis>))]
public enum AttendanceDeductionSalaryBasis { BaseSalary = 1, GrossSalary = 2 }
[JsonConverter(typeof(AttendancePolicyEnumConverter<AttendanceSalaryDivisor>))]
public enum AttendanceSalaryDivisor { ActualCalendarMonthDays = 1, FixedDays = 2 }
[JsonConverter(typeof(AttendancePolicyEnumConverter<AttendanceDeductionRounding>))]
public enum AttendanceDeductionRounding
{
    WholeCurrencyUnitHalfUp = 1, TwoDecimalPlacesHalfUp = 2,
    WholeCurrencyUnitHalfEven = 3, TwoDecimalPlacesHalfEven = 4
}

public sealed record AttendancePolicyRuleError(string Path, string Code, string Message);
public sealed record AttendancePolicyRulesValidation(
    AttendancePolicyRules? Rules, string? CanonicalJson, IReadOnlyList<AttendancePolicyRuleError> Errors)
{
    public bool IsValid => Rules is not null && CanonicalJson is not null && Errors.Count == 0;
}

public static class AttendancePolicyRulesCodec
{
    public const int SupportedSchemaVersion = 3;
    public const int MaximumJsonLength = 32000;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16
    };

    /// <summary>Validate an explicitly supplied form. This has no persistence, approval, or payroll effects.</summary>
    public static AttendancePolicyRulesValidation Validate(AttendancePolicyRules? rules)
    {
        if (rules is null) return Invalid("$", "rules_required", "Explicit attendance rules are required.");
        var errors = new List<AttendancePolicyRuleError>();
        if (rules.SchemaVersion is null) Required("$.schemaVersion");
        else if (rules.SchemaVersion is not (1 or 2 or SupportedSchemaVersion))
            errors.Add(new("$.schemaVersion", "schema_unsupported", "The rules schema is not supported."));

        if (rules.Arrival is not { } arrival) Required("$.arrival");
        else
        {
            Range(arrival.LateAfterShiftStartSeconds, 0, 86400, "$.arrival.lateAfterShiftStartSeconds");
            Range(arrival.HalfDayAfterShiftStartSeconds, 0, 86400, "$.arrival.halfDayAfterShiftStartSeconds");
            Range(arrival.HourlyAfterShiftStartSeconds, 0, 86400, "$.arrival.hourlyAfterShiftStartSeconds");
            if (arrival.LateThresholdComparison is { } comparison)
            {
                if (rules.SchemaVersion != 3)
                    errors.Add(new("$.arrival.lateThresholdComparison", "schema_option_mismatch", "Explicit late boundary comparison requires rules schema 3."));
                else if (!Enum.IsDefined(comparison))
                    errors.Add(new("$.arrival.lateThresholdComparison", "enum_unsupported", "The selected comparison is not supported."));
            }
            if (arrival.LateAfterShiftStartSeconds is { } late && arrival.HalfDayAfterShiftStartSeconds is { } half &&
                arrival.HourlyAfterShiftStartSeconds is { } hourly && (late >= half || half >= hourly))
                errors.Add(new("$.arrival", "arrival_band_order", "Arrival thresholds must be strictly ordered: late, half-day, then hourly."));
        }

        if (rules.PaidTime is not { } paid) Required("$.paidTime");
        else
        {
            Choice(paid.Basis, "$.paidTime.basis");
            Choice(paid.BreakTreatment, "$.paidTime.breakTreatment");
            Choice(paid.EarlyExitTreatment, "$.paidTime.earlyExitTreatment");
            Range(paid.HalfDayCreditSeconds, 1, 86400, "$.paidTime.halfDayCreditSeconds");
            Choice(paid.OffDayTreatment, "$.paidTime.offDayTreatment");
            Choice(paid.HolidayTreatment, "$.paidTime.holidayTreatment");
            if (rules.SchemaVersion is 2 or 3)
                Choice(paid.PartialLeaveHalfDayTreatment, "$.paidTime.partialLeaveHalfDayTreatment");
            else if (paid.PartialLeaveHalfDayTreatment is not null)
                errors.Add(new("$.paidTime.partialLeaveHalfDayTreatment", "schema_option_mismatch", "This explicit interpretation requires rules schema 2 or later."));
        }

        if (rules.LateDeduction is not { } deduction) Required("$.lateDeduction");
        else
        {
            Range(deduction.OccurrenceThreshold, 1, 366, "$.lateDeduction.occurrenceThreshold");
            Choice(deduction.ResetPeriod, "$.lateDeduction.resetPeriod");
            if (deduction.CountHalfDayArrival is null) Required("$.lateDeduction.countHalfDayArrival");
            if (deduction.CountHourlyArrival is null) Required("$.lateDeduction.countHourlyArrival");
            Choice(deduction.SalaryBasis, "$.lateDeduction.salaryBasis");
            Choice(deduction.SalaryDivisor, "$.lateDeduction.salaryDivisor");
            Choice(deduction.Rounding, "$.lateDeduction.rounding");
            if (deduction.SalaryDivisor == AttendanceSalaryDivisor.FixedDays)
            {
                if (deduction.FixedSalaryDivisorDays is null)
                    errors.Add(new("$.lateDeduction.fixedSalaryDivisorDays", "fixed_divisor_required", "Choose an explicit fixed salary divisor."));
                else Range(deduction.FixedSalaryDivisorDays, 1, 366, "$.lateDeduction.fixedSalaryDivisorDays");
            }
            else if (deduction.SalaryDivisor == AttendanceSalaryDivisor.ActualCalendarMonthDays && deduction.FixedSalaryDivisorDays is not null)
                errors.Add(new("$.lateDeduction.fixedSalaryDivisorDays", "fixed_divisor_not_applicable", "Use null for an actual-calendar-month divisor."));
            if (rules.SchemaVersion == 3)
            {
                DeductionPrecision(deduction.Rounding, "$.lateDeduction.rounding");
                Choice(deduction.PolicyChangeCountingStrategy, "$.lateDeduction.policyChangeCountingStrategy");
                Choice(deduction.SalaryEffectiveDateBasis, "$.lateDeduction.salaryEffectiveDateBasis");
                if (deduction.Enabled is null) Required("$.lateDeduction.enabled");
                Choice(deduction.DeductionKind, "$.lateDeduction.deductionKind");
                Choice(deduction.OverlapTreatment, "$.lateDeduction.overlapTreatment");
                if (deduction.DeductionKind == AttendanceLateDeductionKind.DayFraction)
                    PositiveDecimal(deduction.DeductionDaysPerThreshold, 1m, "$.lateDeduction.deductionDaysPerThreshold");
                else if (deduction.DeductionDaysPerThreshold is not null)
                    NotApplicable("$.lateDeduction.deductionDaysPerThreshold");
                if (deduction.DeductionKind == AttendanceLateDeductionKind.FixedAmount)
                {
                    if (deduction.FixedAmountPkr is not { } amount) Required("$.lateDeduction.fixedAmountPkr");
                    else
                    {
                        try
                        {
                            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
                            HrModelGuard.Money(amount, nameof(amount));
                        }
                        catch (ArgumentOutOfRangeException)
                        { errors.Add(new("$.lateDeduction.fixedAmountPkr", "out_of_range", "Use a positive PKR amount that fits decimal(18,2).")); }
                    }
                }
                else if (deduction.FixedAmountPkr is not null) NotApplicable("$.lateDeduction.fixedAmountPkr");
                if (deduction.DeductionKind == AttendanceLateDeductionKind.Hours)
                    PositiveDecimal(deduction.DeductionHoursPerThreshold, 24m, "$.lateDeduction.deductionHoursPerThreshold");
                else if (deduction.DeductionHoursPerThreshold is not null) NotApplicable("$.lateDeduction.deductionHoursPerThreshold");
                if (deduction.ResetPeriod == AttendanceLateResetPeriod.Weekly)
                    Choice(deduction.WeekStartsOn, "$.lateDeduction.weekStartsOn");
                else if (deduction.WeekStartsOn is not null) NotApplicable("$.lateDeduction.weekStartsOn");
            }
            else
            {
                PositiveDecimal(deduction.DeductionDaysPerThreshold, 366m, "$.lateDeduction.deductionDaysPerThreshold");
                if (deduction.PolicyChangeCountingStrategy is not null || deduction.SalaryEffectiveDateBasis is not null ||
                    deduction.Enabled is not null || deduction.DeductionKind is not null || deduction.FixedAmountPkr is not null ||
                    deduction.DeductionHoursPerThreshold is not null || deduction.WeekStartsOn is not null || deduction.OverlapTreatment is not null ||
                    deduction.ResetPeriod == AttendanceLateResetPeriod.Weekly)
                    errors.Add(new("$.lateDeduction", "schema_option_mismatch", "Explicit late financial interpretation requires rules schema 3."));
            }
        }

        if (rules.SchemaVersion == 3)
        {
            if (rules.OrdinaryPay is not { } ordinary) Required("$.ordinaryPay");
            else
            {
                Choice(ordinary.SalaryBasis, "$.ordinaryPay.salaryBasis");
                if (ordinary.SalaryBasis is not null && ordinary.SalaryBasis != AttendanceDeductionSalaryBasis.BaseSalary)
                    errors.Add(new("$.ordinaryPay.salaryBasis", "ordinary_salary_basis_unsupported", "Ordinary payroll inputs use the confirmed basic monthly salary basis."));
                Choice(ordinary.SalaryDivisor, "$.ordinaryPay.salaryDivisor");
                if (ordinary.SalaryDivisor == AttendanceSalaryDivisor.FixedDays)
                {
                    if (ordinary.FixedSalaryDivisorDays is null)
                        errors.Add(new("$.ordinaryPay.fixedSalaryDivisorDays", "fixed_divisor_required", "Choose an explicit fixed salary divisor."));
                    else Range(ordinary.FixedSalaryDivisorDays, 1, 366, "$.ordinaryPay.fixedSalaryDivisorDays");
                }
                else if (ordinary.SalaryDivisor == AttendanceSalaryDivisor.ActualCalendarMonthDays && ordinary.FixedSalaryDivisorDays is not null)
                    errors.Add(new("$.ordinaryPay.fixedSalaryDivisorDays", "fixed_divisor_not_applicable", "Use null for an actual-calendar-month divisor."));
                Fraction(ordinary.FullUnpaidDayDeductionDays, "$.ordinaryPay.fullUnpaidDayDeductionDays");
                Fraction(ordinary.UnpaidHalfDayDeductionDays, "$.ordinaryPay.unpaidHalfDayDeductionDays");
                if (ordinary.UnpaidHalfDayDeductionDays > ordinary.FullUnpaidDayDeductionDays)
                    errors.Add(new("$.ordinaryPay.unpaidHalfDayDeductionDays", "unpaid_fraction_order", "The half-day fraction cannot exceed the full-day fraction."));
                Choice(ordinary.HourlyShortfallCalculation, "$.ordinaryPay.hourlyShortfallCalculation");
                Choice(ordinary.PaidLeaveTreatment, "$.ordinaryPay.paidLeaveTreatment");
                Choice(ordinary.WeeklyOffTreatment, "$.ordinaryPay.weeklyOffTreatment");
                Choice(ordinary.ApplicablePaidHolidayTreatment, "$.ordinaryPay.applicablePaidHolidayTreatment");
                Choice(ordinary.Rounding, "$.ordinaryPay.rounding");
                DeductionPrecision(ordinary.Rounding, "$.ordinaryPay.rounding");
            }
        }
        else if (rules.OrdinaryPay is not null)
            errors.Add(new("$.ordinaryPay", "schema_option_mismatch", "Ordinary payroll-input settings require rules schema 3."));

        if (errors.Count > 0) return new(null, null, errors.AsReadOnly());
        // Decimal trailing zeroes are not a semantic change and must not change a rules fingerprint.
        var normalized = rules with
        {
            LateDeduction = rules.LateDeduction! with
            {
                DeductionDaysPerThreshold = NormalizeNullable(rules.LateDeduction!.DeductionDaysPerThreshold),
                FixedAmountPkr = NormalizeNullable(rules.LateDeduction.FixedAmountPkr),
                DeductionHoursPerThreshold = NormalizeNullable(rules.LateDeduction.DeductionHoursPerThreshold)
            },
            OrdinaryPay = rules.OrdinaryPay is not { } ordinaryRules ? null : ordinaryRules with
            {
                FullUnpaidDayDeductionDays = Normalize(ordinaryRules.FullUnpaidDayDeductionDays!.Value),
                UnpaidHalfDayDeductionDays = Normalize(ordinaryRules.UnpaidHalfDayDeductionDays!.Value)
            }
        };
        return new(normalized, JsonSerializer.Serialize(normalized, Options), Array.Empty<AttendancePolicyRuleError>());

        void Required(string path) => errors.Add(new(path, "required", "Choose an explicit value; no default is assumed."));
        void Range(int? value, int minimum, int maximum, string path)
        {
            if (value is null) Required(path);
            else if (value < minimum || value > maximum)
                errors.Add(new(path, "out_of_range", $"The supported range is {minimum} through {maximum}."));
        }
        void Choice<T>(T? value, string path) where T : struct, Enum
        {
            if (value is null) Required(path);
            else if (!Enum.IsDefined(value.Value)) errors.Add(new(path, "enum_unsupported", "The selected option is not supported."));
        }
        void DeductionPrecision(AttendanceDeductionRounding? value, string path)
        {
            if (value is AttendanceDeductionRounding.WholeCurrencyUnitHalfUp or AttendanceDeductionRounding.WholeCurrencyUnitHalfEven)
                errors.Add(new(path, "deduction_precision_required", "Choose explicit two-decimal rounding for schema 3 attendance deductions."));
        }
        void Fraction(decimal? value, string path)
        {
            if (value is null) Required(path);
            else if (value is < 0 or > 1)
                errors.Add(new(path, "out_of_range", "The supported deduction-day fraction is zero through one."));
        }
        void PositiveDecimal(decimal? value, decimal maximum, string path)
        {
            if (value is null) Required(path);
            else if (value <= 0 || value > maximum)
                errors.Add(new(path, "out_of_range", $"The supported value is greater than zero through {maximum.ToString(CultureInfo.InvariantCulture)}."));
        }
        void NotApplicable(string path) => errors.Add(new(path, "choice_not_applicable", "Use null or omit this value when its corresponding option is not selected."));
    }

    private static decimal? NormalizeNullable(decimal? value) => value.HasValue ? Normalize(value.Value) : null;
    private static decimal Normalize(decimal value) => decimal.Parse(value.ToString("G29", CultureInfo.InvariantCulture),
        NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>Read stored/untrusted JSON with duplicate, unknown-member, enum, size and schema validation.</summary>
    public static AttendancePolicyRulesValidation ValidateAndCanonicalize(string? json)
    {
        if (json?.Length > MaximumJsonLength)
            return Invalid("$", "rules_too_large", "The attendance rules exceed the supported size.");
        if (string.IsNullOrWhiteSpace(json)) return Invalid("$", "rules_required", "Explicit attendance rules are required.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return Invalid("$", "json_object_required", "Attendance rules must be a JSON object.");
            if (DuplicateProperty(document.RootElement, "$", out var duplicate))
                return Invalid(duplicate!, "json_duplicate_property", "Repeated JSON member names are not allowed.");
            var rules = JsonSerializer.Deserialize<AttendancePolicyRules>(document.RootElement, Options);
            if (rules?.SchemaVersion is 1 or 2 &&
                document.RootElement.TryGetProperty("arrival", out var arrival) && arrival.ValueKind == JsonValueKind.Object &&
                arrival.TryGetProperty("lateThresholdComparison", out _))
                return Invalid("$.arrival.lateThresholdComparison", "schema_option_mismatch",
                    "Explicit late boundary comparison requires rules schema 3.");
            return Validate(rules);
        }
        catch (JsonException)
        {
            // Parser details can include supplied JSON. Do not expose them in an API validation response.
            return Invalid("$", "json_invalid", "Use the exact attendance rules schema, required members and named options.");
        }
    }

    public static string Canonicalize(AttendancePolicyRules rules)
    {
        var result = Validate(rules);
        return result.IsValid ? result.CanonicalJson! : throw new ArgumentException("Attendance policy rules are invalid.", nameof(rules));
    }

    private static AttendancePolicyRulesValidation Invalid(string path, string code, string message) =>
        new(null, null, Array.AsReadOnly(new[] { new AttendancePolicyRuleError(path, code, message) }));

    private static bool DuplicateProperty(JsonElement element, string path, out string? duplicate)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    duplicate = path;
                    return true;
                }
                if (DuplicateProperty(property.Value, path + "." + property.Name, out duplicate)) return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                if (DuplicateProperty(child, path + "[]", out duplicate)) return true;
        }
        duplicate = null;
        return false;
    }
}

/// <summary>Schema enums accept only exact snake_case names, including on typed HTTP inputs.</summary>
public sealed class AttendancePolicyEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    private static readonly IReadOnlyDictionary<string, T> Values = Enum.GetValues<T>()
        .ToDictionary(value => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()), value => value, StringComparer.Ordinal);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } text && Values.TryGetValue(text, out var value)) return value;
        throw new JsonException("Use an exact supported attendance policy option name.");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (!Enum.IsDefined(value)) throw new JsonException("The attendance policy option is unsupported.");
        writer.WriteStringValue(JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()));
    }
}
