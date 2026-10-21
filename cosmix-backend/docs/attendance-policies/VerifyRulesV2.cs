#:project ../../src/Cosmix.Domain/Cosmix.Domain.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Text.Json.Nodes;
using Cosmix.Domain.HR.Policies;

var baseline = new AttendancePolicyRules(1, new(900, 3600, 9000),
    new(AttendancePaidTimeBasis.ScheduledWindowOverlap, AttendanceBreakTreatment.CountAllBreaks,
        AttendanceEarlyExitTreatment.HalfDayCredit, 16200, AttendanceNonScheduledDayTreatment.NoCredit, AttendanceNonScheduledDayTreatment.NoCredit),
    new(3, AttendanceLateResetPeriod.CalendarMonth, false, false, AttendanceDeductionSalaryBasis.BaseSalary,
        AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1, AttendanceDeductionRounding.WholeCurrencyUnitHalfUp));
var legacy = AttendancePolicyRulesCodec.Canonicalize(baseline);
var json = JsonNode.Parse(legacy)!;
json["schemaVersion"] = 2;
json["paidTime"]!["partialLeaveHalfDayTreatment"] = "half_day_total_includes_paid_leave";
var accepted = AttendancePolicyRulesCodec.ValidateAndCanonicalize(json.ToJsonString());
if (!accepted.IsValid) throw new Exception("Explicit v2 partial-leave/half-day interpretation must be supported");
if (!accepted.CanonicalJson!.Contains("half_day_total_includes_paid_leave")) throw new Exception("Composition choice must affect retained canonical rules");
json["paidTime"]!.AsObject().Remove("partialLeaveHalfDayTreatment");
if (!AttendancePolicyRulesCodec.ValidateAndCanonicalize(json.ToJsonString()).Errors.Any(x => x.Path.EndsWith("partialLeaveHalfDayTreatment") && x.Code == "required"))
    throw new Exception("Schema2 must require explicit composition choice");
json["paidTime"]!["partialLeaveHalfDayTreatment"] = "guess";
if (AttendancePolicyRulesCodec.ValidateAndCanonicalize(json.ToJsonString()).IsValid) throw new Exception("Unknown composition cannot be published");
json["paidTime"]!["partialLeaveHalfDayTreatment"] = "half_day_total_includes_paid_leave";
json["schemaVersion"] = 1;
if (AttendancePolicyRulesCodec.ValidateAndCanonicalize(json.ToJsonString()).IsValid) throw new Exception("A new interpretation cannot masquerade as legacy schema1");
json["schemaVersion"] = 99;
if (!AttendancePolicyRulesCodec.ValidateAndCanonicalize(json.ToJsonString()).Errors.Any(x => x.Code == "schema_unsupported"))
    throw new Exception("Unknown schema rejected");
if (AttendancePolicyRulesCodec.ValidateAndCanonicalize(legacy).CanonicalJson != legacy || legacy.Contains("partialLeaveHalfDayTreatment"))
    throw new Exception("Legacy canonical rules and fingerprints must not change");
Console.WriteLine("PASS: 7 versioned policy composition/compatibility checks; no SQL.");
