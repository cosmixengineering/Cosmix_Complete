#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.HR.Leaves;

var checks = 0;
var failures = 0;
void Check(bool condition, string name)
{
    checks++;
    if (!condition) { failures++; Console.WriteLine("FAIL: " + name); }
}
void Accept(LeaveBenefitSettings settings, string name)
{
    try { settings.Validate(); Check(true, name); }
    catch (Exception e) { Check(false, name + ": " + e.GetType().Name); }
}
void Reject(LeaveBenefitSettings settings, string name)
{
    try { settings.Validate(); Check(false, name); }
    catch (AuthenticationFailure e) { Check(e.Code == "invalid_leave_benefit_settings" && e.StatusCode == 400, name); }
    catch (Exception e) { Check(false, name + ": " + e.GetType().Name); }
}
void RejectJson(string json, string name, JsonSerializerOptions? options = null)
{
    try { JsonSerializer.Deserialize<LeaveBenefitSettings>(json, options); Check(false, name); }
    catch (JsonException) { Check(true, name); }
    catch (Exception e) { Check(false, name + ": " + e.GetType().Name); }
}

// Synthetic values exercise the contract; none are company policy defaults.
var settings = new LeaveBenefitSettings(LeaveKind.Other, 3, 99.875m, 375, 1, 45, false);
Accept(settings, "Fractional exact KPI and partial-day award minutes are configurable");
Accept(settings with { MinimumServiceMonths = 0, MinimumKpiPercent = 0m, AwardMinutes = 1,
    TargetYearOffset = 1, AwardWindowDaysAfterSourceEnd = 1 }, "Explicit lower limits and next calendar start-year offset are accepted");
Accept(settings with { TargetYearOffset = 2, AwardWindowDaysAfterSourceEnd = 3660 },
    "Explicit two-year offset supports a delayed award");
Accept(settings with { MinimumServiceMonths = 1200, MinimumKpiPercent = 100m, AwardMinutes = 527040,
    TargetYearOffset = 5, AwardWindowDaysAfterSourceEnd = 3660, EncashmentEligible = true },
    "Upper bounds including five-year offset and explicit encashment opt-in are accepted");
foreach (var kind in new[] { LeaveKind.Other, LeaveKind.Sick, LeaveKind.Casual, LeaveKind.Exam,
    LeaveKind.Emergency, LeaveKind.PersonalTravel })
    Accept(settings with { EligibleKind = kind }, "Explicit award kind accepted: " + kind);

Reject(settings with { EligibleKind = LeaveKind.SiteInjuryRest }, "Injury rest cannot become an earned quota award");
Reject(settings with { EligibleKind = (LeaveKind)(-1) }, "Negative leave kind rejected");
Reject(settings with { EligibleKind = (LeaveKind)7 }, "Unknown leave kind rejected");
Reject(settings with { MinimumServiceMonths = -1 }, "Negative service requirement rejected");
Reject(settings with { MinimumServiceMonths = 1201 }, "Excessive service requirement rejected");
Reject(settings with { MinimumKpiPercent = -0.000001m }, "Negative exact KPI threshold rejected");
Reject(settings with { MinimumKpiPercent = 100.000001m }, "Exact KPI above 100 rejected without rounding down");
Reject(settings with { AwardMinutes = 0 }, "Zero award rejected");
Reject(settings with { AwardMinutes = -1 }, "Negative award rejected");
Reject(settings with { AwardMinutes = 527041 }, "Award exceeding annual minute bound rejected");
Reject(settings with { AwardMinutes = int.MaxValue }, "Extreme award rejected without overflow");
Reject(settings with { TargetYearOffset = -1 }, "Earlier target year rejected");
Reject(settings with { TargetYearOffset = 0 }, "Zero calendar start-year offset is unsupported");
Reject(settings with { TargetYearOffset = 6 }, "Target beyond five years rejected");
Reject(settings with { AwardWindowDaysAfterSourceEnd = 0 }, "Zero-length award window rejected");
Reject(settings with { AwardWindowDaysAfterSourceEnd = -1 }, "Negative award window rejected");
Reject(settings with { AwardWindowDaysAfterSourceEnd = 3661 }, "Excessive award window rejected");

const string json = """
{"EligibleKind":0,"MinimumServiceMonths":3,"MinimumKpiPercent":99.875,"AwardMinutes":375,
 "TargetYearOffset":1,"AwardWindowDaysAfterSourceEnd":45,"EncashmentEligible":false}
""";
var parsed = JsonSerializer.Deserialize<LeaveBenefitSettings>(json)!;
Accept(parsed, "Complete policy JSON validates");
Check(parsed == settings, "Policy JSON retains exact configured values including explicit false");
Check(JsonSerializer.Deserialize<LeaveBenefitSettings>(JsonSerializer.Serialize(parsed)) == settings,
    "Retained policy JSON round-trip preserves exact threshold and award configuration");

string[] fields = ["EligibleKind", "MinimumServiceMonths", "MinimumKpiPercent", "AwardMinutes",
    "TargetYearOffset", "AwardWindowDaysAfterSourceEnd", "EncashmentEligible"];
foreach (var field in fields)
{
    var missing = JsonNode.Parse(json)!.AsObject();
    missing.Remove(field);
    RejectJson(missing.ToJsonString(), "Missing explicit field rejected: " + field);
    var nullField = JsonNode.Parse(json)!.AsObject();
    nullField[field] = null;
    RejectJson(nullField.ToJsonString(), "Null field rejected: " + field);
}
foreach (var field in new[] { "MinimumServiceMonths", "MinimumKpiPercent", "AwardMinutes",
    "TargetYearOffset", "AwardWindowDaysAfterSourceEnd" })
{
    var quoted = JsonNode.Parse(json)!.AsObject();
    quoted[field] = quoted[field]!.ToJsonString();
    RejectJson(quoted.ToJsonString(), "Quoted number rejected: " + field,
        new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowReadingFromString });
}
foreach (var extra in new[] { "ReviewerRoleCode", "AllowEncashment", "AwardDays", "UnknownSetting" })
{
    var unknown = JsonNode.Parse(json)!.AsObject();
    unknown[extra] = "Admin";
    RejectJson(unknown.ToJsonString(), "Unknown or superseded field rejected: " + extra);
}
var quotedFlag = JsonNode.Parse(json)!.AsObject();
quotedFlag["EncashmentEligible"] = "false";
RejectJson(quotedFlag.ToJsonString(), "String flag cannot stand in for explicit encashment boolean");
var fractionalMinutes = JsonNode.Parse(json)!.AsObject();
fractionalMinutes["AwardMinutes"] = 1.5m;
RejectJson(fractionalMinutes.ToJsonString(), "Fractional minute value cannot truncate to an integer award");
foreach (var malformed in new[] { "{}", "[]", "{", "true" })
    RejectJson(malformed, "Malformed or incomplete policy rejected: " + malformed);

Check(parsed.ConsumedCorrectionResolution is null && !JsonSerializer.Serialize(parsed).Contains("ConsumedCorrectionResolution"),
    "Legacy policy stays disabled and retains its serialized fingerprint");
var optedIn = settings with { ConsumedCorrectionResolution = new(1, true, "Synthetic approved waiver authority") };
Accept(optedIn, "Explicit version-one waiver policy validates");
Accept(optedIn with { ConsumedCorrectionResolution = new(1, false, "Synthetic waiver disabled") }, "Explicit waiver denial validates");
Check(JsonSerializer.Deserialize<LeaveBenefitSettings>(JsonSerializer.Serialize(optedIn)) == optedIn,
    "Waiver policy retains exact version, decision and authority reference");
foreach (var invalid in new[] {
    new LeaveBenefitResolutionSettings(0, true, "Synthetic"), new(2, true, "Synthetic"),
    new(1, true, ""), new(1, true, "   "), new(1, true, "Synthetic\nreference"), new(1, true, new string('X', 501))
}) {
    try { (settings with { ConsumedCorrectionResolution = invalid }).Validate(); Check(false, "Malformed waiver policy rejected"); }
    catch (AuthenticationFailure e) { Check(e.StatusCode == 400 && e.Code == "invalid_leave_benefit_resolution_settings", "Malformed waiver policy rejected"); }
}
foreach (var field in new[] { "SchemaVersion", "AllowAdminWaiver", "AuthorityReference" }) {
    var missing = JsonSerializer.SerializeToNode(optedIn)!.AsObject();
    missing["ConsumedCorrectionResolution"]!.AsObject().Remove(field);
    RejectJson(missing.ToJsonString(), "Waiver policy requires explicit " + field);
}

Console.WriteLine($"Leave benefit settings: {checks - failures}/{checks} checks passed; pure validation, no workflow or IO.");
return failures == 0 ? 0 : 1;
