#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
using System.Text.Json;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.Authentication;
using Cosmix.Domain.HR.Leaves;

var failures = 0; var checks = 0;
void Check(bool value, string name) { checks++; if (!value) { failures++; Console.WriteLine("FAIL: " + name); } }
// JSON keeps this behavioral regression compilable against the frozen pre-feature build.
var usage = JsonSerializer.Deserialize<LeaveBalanceUsage>("""
{"AnnualMinutes":480,"TypeMinutes":480,"Units":[{"UsedMinutes":480,"StandardDayMinutes":480,"Kind":0}],
 "Credits":[{"AwardId":"00000000-0000-0000-0000-000000000001","Minutes":240,"StandardDayMinutes":480,"Kind":0,"Encashable":false}]}
""")!;
Check(!LeaveTreatmentRules.ExceedsDays(usage, LeaveKind.Other, 240, 480, 1, 1),
    "Verified half-day benefit permits half-day after baseline fully used");
Check(LeaveTreatmentRules.ExceedsDays(usage, LeaveKind.Other, 241, 480, 1, 1),
    "One minute beyond earned credit is rejected");
var balance = LeaveTreatmentRules.Balances(usage, LeaveKind.Other, 1, 1);
Check(balance.Annual.RemainingDays == new LeaveExactDays("1", "2"), "Earned quota shown independently of used days");
Check(balance.Annual.UsedDays == new LeaveExactDays("1", "1"), "Benefit does not erase factual leave consumption");
var otherKind = LeaveTreatmentRules.ExceedsDays(usage with { TypeMinutes = 0 }, LeaveKind.Sick, 240, 480, 1, 0);
Check(otherKind, "An Other-kind award cannot expand Sick quota");
Check(LeaveTreatmentRules.ExceedsDays(usage with { Units = [new(480, 480, LeaveKind.Casual)] }, LeaveKind.Casual, 1, 480, 1, 3),
    "Unused Other benefit cannot fund Casual leave through annual ceiling");
var fractional = JsonSerializer.Deserialize<LeaveBalanceUsage>("""
{"AnnualMinutes":0,"TypeMinutes":0,"Units":[],
 "Credits":[{"AwardId":"00000000-0000-0000-0000-000000000001","Minutes":140,"StandardDayMinutes":420,"Kind":0,"Encashable":true}]}
""")!;
Check(!LeaveTreatmentRules.ExceedsDays(fractional, LeaveKind.Other, 160, 480, 0, 0),
    "Retained 140/420 day credit permits exact 160/480 after day-length change");
Check(LeaveTreatmentRules.ExceedsDays(fractional, LeaveKind.Other, 161, 480, 0, 0), "No fractional credit rounding creates extra leave");
var missing = new LeaveBalanceUsage(0, 0, []);
Check(LeaveTreatmentRules.ExceedsDays(missing, LeaveKind.Other, 1, 480, 0, 0), "No credit is inferred without evidence");
var fractionalCredits = fractional.Credits ?? throw new InvalidOperationException("Synthetic earned credit missing");
foreach (var bad in new[] {
    fractional with { Credits = fractionalCredits.Concat(fractionalCredits).ToArray() },
    fractional with { Credits = [fractionalCredits[0] with { Minutes = -1 }] },
    fractional with { Credits = [fractionalCredits[0] with { StandardDayMinutes = 0 }] },
    fractional with { Credits = [fractionalCredits[0] with { AwardId = Guid.Empty }] }
})
{
    try { _ = LeaveTreatmentRules.Balances(bad, LeaveKind.Other, 0, 0); Check(false, "Malformed credit must fail closed"); }
    catch (AuthenticationFailure e) { Check(e.Code == "leave_benefit_credit_unverified", "Malformed credit must fail closed"); }
}
var afterConsumption = fractional with { AnnualMinutes = 160, TypeMinutes = 160, Units = [new(160, 480, LeaveKind.Other)] };
Check(LeaveTreatmentRules.ExceedsDays(afterConsumption, LeaveKind.Other, 1, 480, 0, 0), "Consumed earned quota cannot be spent again");
var offset = fractionalCredits[0] with { ConsumptionOffset = true, Encashable = false };
var corrected = afterConsumption with { Credits = [offset] };
Check(!LeaveTreatmentRules.ExceedsDays(corrected, LeaveKind.Other, 480, 480, 1, 1) &&
    LeaveTreatmentRules.ExceedsDays(corrected, LeaveKind.Other, 481, 480, 1, 1), "Mixed day-unit consumed offset preserves exactly base quota");
foreach (var invalidOffset in new[] {
    corrected with { AnnualMinutes = 159, TypeMinutes = 159, Units = [new(159, 480, LeaveKind.Other)] },
    corrected with { Credits = [offset with { Encashable = true }] }
}) {
    try { _ = LeaveTreatmentRules.Balances(invalidOffset, LeaveKind.Other, 1, 1); Check(false, "Invalid consumed offset"); }
    catch (AuthenticationFailure e) { Check(e.Code is "leave_benefit_credit_unverified" or "leave_benefit_consumption_offset_conflict", "Invalid consumed offset fails closed"); }
}
var legacyCredit = fractionalCredits[0];
Check(JsonSerializer.Serialize(legacyCredit) == JsonSerializer.Serialize(new { legacyCredit.AwardId, legacyCredit.Minutes,
    legacyCredit.StandardDayMinutes, legacyCredit.Kind, legacyCredit.Encashable }), "Legacy credit serialization has no added default marker");
var cashOnly = usage with { Credits = (usage.Credits ?? throw new InvalidOperationException("Synthetic credit missing")).Where(x => x.Encashable).ToArray() };
Check(LeaveTreatmentRules.ExceedsDays(cashOnly, LeaveKind.Other, 1, 480, 1, 1), "Leave-only award cannot create cash capacity");
Console.WriteLine($"Leave benefit quota: {checks - failures}/{checks} checks passed");
return failures == 0 ? 0 : 1;
