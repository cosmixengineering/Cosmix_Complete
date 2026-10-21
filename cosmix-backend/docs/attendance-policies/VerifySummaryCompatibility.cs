#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Text.Json;
using Cosmix.Application.Attendance.DailySummaries;

var old = new AttendancePolicyResult("policy_integration_unavailable", null, null, null, null, null, null, ["Pending configuration"]);
var legacyShape = new { old.Status, old.PolicyVersionId, old.CalculationVersion, old.PayableMinutes,
    old.LateMinutes, old.OvertimeMinutes, old.DayStatus, old.Explanations };
var legacy = JsonSerializer.Serialize(legacyShape);
if (JsonSerializer.Serialize(old) != legacy) throw new Exception("Legacy policy evidence shape changed");
var property = typeof(AttendancePolicyResult).GetProperty("InputProvenance")
    ?? throw new Exception("Configured calculation needs source provenance in the saved fingerprint");
var fingerprint = new string('A', 64);
property.SetValue(old, fingerprint);
var updated = JsonSerializer.Serialize(old);
if (updated == legacy || !updated.Contains(fingerprint)) throw new Exception("New source provenance must affect evidence serialization");
if (JsonSerializer.Deserialize<AttendancePolicyResult>(legacy) is not { } restored || property.GetValue(restored) is not null)
    throw new Exception("Legacy evidence must remain readable without provenance");
if (JsonSerializer.Serialize(restored) != legacy) throw new Exception("Legacy evidence must retain exactly the same fingerprint input");
Console.WriteLine("PASS: 4 summary policy provenance/legacy compatibility checks; no SQL or private data.");
