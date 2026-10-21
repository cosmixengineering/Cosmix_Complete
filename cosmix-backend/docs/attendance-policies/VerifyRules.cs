#:project ../../src/Cosmix.Domain/Cosmix.Domain.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyRules.packages.lock.json
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Domain.HR.Policies;

var checks = 0;
void Expect(bool condition, string message)
{
    checks++;
    if (!condition) throw new InvalidOperationException(message);
}

// Synthetic examples exercise explicit choices; none of these numbers are approved company policy.
const string validJson = """
{
  "schemaVersion": 1,
  "arrival": {"lateAfterShiftStartSeconds": 900, "halfDayAfterShiftStartSeconds": 3600, "hourlyAfterShiftStartSeconds": 9000},
  "paidTime": {"basis": "scheduled_window_overlap", "breakTreatment": "count_all_breaks", "earlyExitTreatment": "actual_worked_time", "halfDayCreditSeconds": 16200, "offDayTreatment": "no_credit", "holidayTreatment": "scheduled_day_credit"},
  "lateDeduction": {"occurrenceThreshold": 3, "resetPeriod": "calendar_month", "countHalfDayArrival": false, "countHourlyArrival": false, "salaryBasis": "base_salary", "salaryDivisor": "actual_calendar_month_days", "fixedSalaryDivisorDays": null, "deductionDaysPerThreshold": 1, "rounding": "whole_currency_unit_half_up"}
}
""";
var result = AttendancePolicyRulesCodec.ValidateAndCanonicalize(validJson);
Expect(result.IsValid, "All explicit supported input is accepted without introducing policy defaults.");
Expect(result.Rules!.PaidTime!.HalfDayCreditSeconds == 16200, "Explicit half-day duration survives round trip.");
Expect(result.Rules.LateDeduction!.CountHalfDayArrival == false, "False is a deliberate choice, not missing input.");
Expect(result.CanonicalJson is not null && !result.CanonicalJson.Contains('\n'), "Canonical form is compact.");
Expect(AttendancePolicyRulesCodec.ValidateAndCanonicalize(result.CanonicalJson).CanonicalJson == result.CanonicalJson,
    "Canonicalization is idempotent.");
Expect(AttendancePolicyRulesCodec.Validate(result.Rules).CanonicalJson == result.CanonicalJson,
    "Typed and stored JSON paths produce the same canonical representation.");
var ordered = JsonNode.Parse(validJson)!.AsObject();
var reverse = new JsonObject(ordered.Reverse().Select(p => new KeyValuePair<string, JsonNode?>(p.Key, p.Value?.DeepClone())));
Expect(AttendancePolicyRulesCodec.ValidateAndCanonicalize(reverse.ToJsonString()).CanonicalJson == result.CanonicalJson,
    "Input property ordering cannot change the canonical rules digest.");
Expect(AttendancePolicyRulesCodec.ValidateAndCanonicalize(validJson.Replace("\"deductionDaysPerThreshold\": 1", "\"deductionDaysPerThreshold\": 1.000")).CanonicalJson == result.CanonicalJson,
    "Equivalent decimal representations canonicalize identically.");

void Rejected(string? input, string expectedCode, string description)
{
    var rejected = AttendancePolicyRulesCodec.ValidateAndCanonicalize(input);
    Expect(!rejected.IsValid && rejected.Rules is null && rejected.CanonicalJson is null &&
        rejected.Errors.Any(e => e.Code == expectedCode), description);
}
string Change(string section, string property, JsonNode? value)
{
    var node = JsonNode.Parse(validJson)!;
    node[section]![property] = value;
    return node.ToJsonString();
}
string Remove(string section, string property)
{
    var node = JsonNode.Parse(validJson)!;
    node[section]!.AsObject().Remove(property);
    return node.ToJsonString();
}

Rejected(null, "rules_required", "Null JSON is not implicit default rules.");
Rejected(" ", "rules_required", "Whitespace JSON is not implicit default rules.");
Rejected("{", "json_invalid", "Malformed JSON is rejected without leaking parser details.");
Rejected("[]", "json_object_required", "An array is not a rules object.");
Rejected("null", "json_object_required", "JSON null is not a rules object.");
Rejected(validJson + "{}", "json_invalid", "A second JSON document is not silently ignored.");
Rejected(validJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99"), "schema_unsupported", "Unknown schema never falls back to version one.");
Rejected(validJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": null"), "required", "Omitted schema cannot activate current version.");
Rejected(validJson.Replace("\"schemaVersion\": 1,", ""), "json_invalid", "Missing required JSON member is rejected.");
Rejected(validJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 2"), "json_duplicate_property", "Repeated root properties cannot win by parser order.");
Rejected(validJson.Replace("\"salaryBasis\": \"base_salary\"", "\"salaryBasis\": \"base_salary\", \"salaryBasis\": \"gross_salary\""), "json_duplicate_property", "Repeated nested properties are rejected.");
Rejected(validJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schema\\u0056ersion\": 2"), "json_duplicate_property", "Escaped duplicate names are compared after decoding.");
Rejected(validJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"SchemaVersion\": 2"), "json_invalid", "Alternative member casing cannot override or duplicate a field.");
Rejected(validJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"overrideSalary\": true"), "json_invalid", "Unknown root properties are rejected.");
Rejected(Change("arrival", "unknown", 1), "json_invalid", "Unknown nested properties are rejected.");
Rejected(Change("paidTime", "basis", "ACTUAL_ELAPSED_DUTY"), "json_invalid", "Enum values require exact stable spelling.");
Rejected(Change("paidTime", "basis", 1), "json_invalid", "Numeric enum values cannot bypass the named contract.");
Rejected(Change("paidTime", "basis", "unknown"), "json_invalid", "Unknown enum values are rejected.");
Rejected(Change("arrival", "lateAfterShiftStartSeconds", "900"), "json_invalid", "Numeric strings cannot bypass numeric input validation.");
Rejected(Change("arrival", "lateAfterShiftStartSeconds", 0.5m), "json_invalid", "Thresholds must be whole seconds.");
Rejected(Change("arrival", "lateAfterShiftStartSeconds", -1), "out_of_range", "Negative arrival threshold is rejected.");
Rejected(Change("arrival", "hourlyAfterShiftStartSeconds", 86401), "out_of_range", "Threshold cannot exceed the supported one-day schema bound.");
Rejected(Change("arrival", "halfDayAfterShiftStartSeconds", 900), "arrival_band_order", "Equal arrival thresholds create no implicit boundary precedence.");
Rejected(Change("arrival", "hourlyAfterShiftStartSeconds", 3500), "arrival_band_order", "Reversed hourly and half-day thresholds are rejected.");
Rejected(Change("paidTime", "halfDayCreditSeconds", 0), "out_of_range", "Zero half-day credit is not an implicit absence rule.");
Rejected(Change("paidTime", "halfDayCreditSeconds", 86401), "out_of_range", "Half-day credit cannot exceed the supported daily bound.");
Rejected(Change("lateDeduction", "occurrenceThreshold", 0), "out_of_range", "Zero late occurrence threshold cannot divide by zero.");
Rejected(Change("lateDeduction", "occurrenceThreshold", 367), "out_of_range", "Occurrence threshold is bounded to the supported reset periods.");
Rejected(Change("lateDeduction", "countHourlyArrival", null), "required", "A missing participation decision is not false.");
Rejected(Change("lateDeduction", "deductionDaysPerThreshold", 0), "out_of_range", "Zero deduction does not silently disable a salary rule.");
Rejected(Change("lateDeduction", "deductionDaysPerThreshold", 367), "out_of_range", "Unbounded deduction multiplier is rejected.");
Rejected(Change("lateDeduction", "deductionDaysPerThreshold", -0.5m), "out_of_range", "Negative deduction multiplier cannot create salary credit.");
Rejected(Change("lateDeduction", "fixedSalaryDivisorDays", 30), "fixed_divisor_not_applicable", "Calendar divisor cannot silently retain an unused fixed divisor.");
Rejected(Change("lateDeduction", "salaryDivisor", "fixed_days"), "fixed_divisor_required", "Fixed divisor must have explicit days.");

foreach (var section in new[] { "arrival", "paidTime", "lateDeduction" })
{
    var node = JsonNode.Parse(validJson)!;
    node[section] = null;
    Rejected(node.ToJsonString(), "required", $"Null {section} section is unavailable, not defaults.");
    node.AsObject().Remove(section);
    Rejected(node.ToJsonString(), "json_invalid", $"Missing {section} section is rejected.");
}
foreach (var pair in new[]
{
    ("arrival", "lateAfterShiftStartSeconds"), ("arrival", "halfDayAfterShiftStartSeconds"), ("arrival", "hourlyAfterShiftStartSeconds"),
    ("paidTime", "basis"), ("paidTime", "breakTreatment"), ("paidTime", "earlyExitTreatment"), ("paidTime", "halfDayCreditSeconds"),
    ("paidTime", "offDayTreatment"), ("paidTime", "holidayTreatment"), ("lateDeduction", "occurrenceThreshold"),
    ("lateDeduction", "resetPeriod"), ("lateDeduction", "countHalfDayArrival"), ("lateDeduction", "salaryBasis"),
    ("lateDeduction", "salaryDivisor"), ("lateDeduction", "deductionDaysPerThreshold"), ("lateDeduction", "rounding")
})
{
    Rejected(Change(pair.Item1, pair.Item2, null), "required", $"Missing value {pair.Item1}.{pair.Item2} cannot become a default.");
    Rejected(Remove(pair.Item1, pair.Item2), "json_invalid", $"Missing member {pair.Item1}.{pair.Item2} is rejected.");
}

var fixedDays = JsonNode.Parse(validJson)!;
fixedDays["lateDeduction"]!["salaryDivisor"] = "fixed_days";
fixedDays["lateDeduction"]!["fixedSalaryDivisorDays"] = 26;
Expect(AttendancePolicyRulesCodec.ValidateAndCanonicalize(fixedDays.ToJsonString()).IsValid, "Explicit fixed divisor can be validated without becoming a default.");
fixedDays["lateDeduction"]!["fixedSalaryDivisorDays"] = 0;
Rejected(fixedDays.ToJsonString(), "out_of_range", "Explicit zero divisor is rejected.");
fixedDays["lateDeduction"]!["fixedSalaryDivisorDays"] = 367;
Rejected(fixedDays.ToJsonString(), "out_of_range", "Unsupported fixed divisor is rejected.");
var exactBoundary = result.Rules! with { Arrival = new(0, 1, 86400) };
Expect(AttendancePolicyRulesCodec.Validate(exactBoundary).IsValid, "Zero grace and exact maximum boundary are deliberate supported inputs.");
var invalidEnum = result.Rules with { PaidTime = result.Rules.PaidTime! with { Basis = (AttendancePaidTimeBasis)999 } };
Expect(AttendancePolicyRulesCodec.Validate(invalidEnum).Errors.Any(e => e.Code == "enum_unsupported"), "Typed callers cannot inject undefined enum values.");
Expect(!AttendancePolicyRulesCodec.Validate(null).IsValid, "Typed null input remains unavailable.");
try { AttendancePolicyRulesCodec.Canonicalize(invalidEnum); throw new InvalidOperationException("Invalid rules were serialized."); }
catch (ArgumentException) { checks++; }

Rejected(new string(' ', 32001) + validJson, "rules_too_large", "Input cannot exceed the existing policy storage envelope.");
Rejected(validJson.Replace("\"schemaVersion\": 1,", "\"schemaVersion\": 1, // comment\n"), "json_invalid", "Comments are not stored rules.");
Rejected(validJson.Replace("\"schemaVersion\": 1,", "\"schemaVersion\": 1,,"), "json_invalid", "Extra separators are rejected.");
Console.WriteLine($"Attendance policy rule checks passed: {checks}");
