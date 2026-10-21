#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.HR.Leaves;

var checks = 0; var failed = 0;
void Check(bool condition, string message) { checks++; if (!condition) { failed++; Console.WriteLine("FAIL: " + message); } }
var settings = new LeavePolicySettings(11, 3, 4, 2, 420, [Guid.NewGuid()], Guid.NewGuid(), LeaveHolidayTreatment.Exclude, []) {
    SettingsVersion = 3, YearStartMonth = 4, YearStartDay = 1,
    Recovery = new([LeaveKind.Other], [LeavePayTreatment.Unpaid], 420, 120, 30, 60)
};
var document = JsonSerializer.SerializeToNode(settings)!.AsObject();
document["SettingsVersion"] = 4;
document["RecoveryFulfilment"] = JsonSerializer.SerializeToNode(new {
    QualifyingWork = 1, CreditBasis = 1, ReviewAuthority = 1, AllowPartialFulfilment = true,
    MinimumClaimMinutes = 15, MaximumClaimMinutes = 120, MaximumClaimsPerCommitment = 8,
    SubmissionWindowDays = 30, ParentChangeHandling = 1, SourceChangeHandling = 1
});
try { _ = LeavePolicySettings.Parse(document.ToJsonString(), "leave-settings-v4"); Check(true, "Explicit recovery policy is accepted"); }
catch (AuthenticationFailure ex) { Check(false, "Explicit recovery policy is unavailable: " + ex.Code); }
void Rejected(JsonObject candidate, string name, string schema = "leave-settings-v4")
{
    try { _ = LeavePolicySettings.Parse(candidate.ToJsonString(), schema); Check(false, name + " accepted"); }
    catch (AuthenticationFailure ex) { Check(ex.Code == "leave_policy_unavailable" && ex.StatusCode == 409, name + " rejected without invented defaults"); }
}
foreach (var field in document["RecoveryFulfilment"]!.AsObject().Select(x => x.Key).ToArray())
{
    var candidate = document.DeepClone().AsObject(); candidate["RecoveryFulfilment"]!.AsObject().Remove(field);
    Rejected(candidate, "Missing " + field);
}
foreach (var (field, values) in new (string, int[])[] {
    ("QualifyingWork", [-1, 2]), ("CreditBasis", [-1, 2]), ("ReviewAuthority", [-1, 2]),
    ("ParentChangeHandling", [-1, 2]), ("SourceChangeHandling", [-1, 2]),
    ("MinimumClaimMinutes", [0, 1441]), ("MaximumClaimMinutes", [14, 1441]),
    ("MaximumClaimsPerCommitment", [0, 1001]), ("SubmissionWindowDays", [-1, 367])
}) foreach (var value in values)
{
    var candidate = document.DeepClone().AsObject(); candidate["RecoveryFulfilment"]![field] = value;
    Rejected(candidate, field + "=" + value);
}
foreach (var field in new[] { "QualifyingWork", "CreditBasis", "ReviewAuthority", "ParentChangeHandling", "SourceChangeHandling" })
{
    var candidate = document.DeepClone().AsObject(); candidate["RecoveryFulfilment"]![field] = 0;
    var parsed = LeavePolicySettings.Parse(candidate.ToJsonString(), "leave-settings-v4");
    Check(parsed.RecoveryFulfilment is not null, "Pending " + field + " can be retained as explicit configuration");
    try { parsed.RecoveryFulfilment!.RequireConfigured(); Check(false, "Pending " + field + " activated claims"); }
    catch (AuthenticationFailure ex) { Check(ex.Code == "leave_recovery_fulfilment_policy_pending", "Pending " + field + " cannot activate claims"); }
}
var noParent = document.DeepClone().AsObject(); noParent.Remove("Recovery");
Rejected(noParent, "Fulfilment without commitment policy");
var older = document.DeepClone().AsObject(); older["SettingsVersion"] = 3;
Rejected(older, "V4 capability in historical schema", "leave-settings-v3");
Rejected(document, "Mismatched declared schema", "leave-settings-v3");
var unknown = document.DeepClone().AsObject(); unknown["RecoveryFulfilment"]!["UnknownRate"] = 10;
Rejected(unknown, "Unknown recovery setting");
var quoted = document.DeepClone().AsObject(); quoted["RecoveryFulfilment"]!["MaximumClaimMinutes"] = "120";
Rejected(quoted, "Quoted numeric limit");
var duplicate = document.DeepClone().AsObject(); duplicate["RecoveryFulfilment"]!["reviewAuthority"] = 1;
Rejected(duplicate, "Duplicate case-insensitive nested setting");
var exact = LeavePolicySettings.Parse(document.ToJsonString(), "leave-settings-v4").RecoveryFulfilment!;
exact.RequireConfigured();
Check(exact.MinimumClaimMinutes == 15 && exact.MaximumClaimMinutes == 120 && exact.MaximumClaimsPerCommitment == 8 &&
    exact.SubmissionWindowDays == 30 && exact.AllowPartialFulfilment, "Explicit synthetic values retained without rate or amount inference");
Console.WriteLine($"Recovery work policy: {checks - failed}/{checks}");
Environment.ExitCode = failed == 0 ? 0 : 1;
