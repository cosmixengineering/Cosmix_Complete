#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyCaptureModels.packages.lock.json
using System.Reflection;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;

// In-memory model checks only. Synthetic hydration represents materialized SQL entities;
// it is not a production account creation or authentication bypass.
var checks = 0;
void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
void Reject(Action action, string name)
{
    checks++;
    try { action(); } catch (Exception e) when (e is ArgumentException or InvalidOperationException) { return; }
    throw new Exception(name);
}
void Set(object entity, string name, object? value) => entity.GetType().GetProperty(name)!.SetValue(entity, value);
var now = new DateTimeOffset(2026, 10, 9, 4, 0, 0, TimeSpan.Zero);
var employeeId = Guid.NewGuid();
var account = (UserAccount)Activator.CreateInstance(typeof(UserAccount), true)!;
Set(account, "Id", Guid.NewGuid()); Set(account, "EmployeeId", employeeId); Set(account, "Status", AccountStatus.Active);
var device = new RegisteredDevice(account.Id, "synthetic-device-hash", "android", "Synthetic", now.AddDays(-1));
device.MarkVerified(now.AddDays(-1));
var enrollment = new FaceEnrollment(employeeId, "private-synthetic-reference", "synthetic-key-reference", "synthetic", "1",
    "synthetic-admin", device.Id.ToString(), "Synthetic enrollment", now.AddHours(-1), "synthetic-correlation");
var secret = new string('C', 64);
FaceVerificationAttempt Attempt(string key = "punch-key-001") => new(enrollment, account, device, key,
    FaceService.Digest(secret), "synthetic", "1", now, now.AddMinutes(2), "synthetic-correlation");
RawPunchEvent Raw(string key = "punch-key-001", PunchAction action = PunchAction.CheckIn, Guid? employee = null,
    string? deviceId = null, DateTimeOffset? at = null) => new(employee ?? employeeId, action, deviceId ?? device.Id.ToString("D"), key,
    new string('A', 64), at ?? now.AddSeconds(20), false, businessDate: new(2026, 10, 9));
var pending = Attempt();
Reject(() => pending.Consume(Raw(), enrollment, account, device, now.AddSeconds(20)), "pending proof cannot consume");
var unavailable = Attempt(); unavailable.RecordProviderResult(false, true, true, "UNAVAILABLE", now.AddSeconds(5));
Check(unavailable.Outcome == FaceVerificationOutcome.Unavailable && unavailable.LivenessPassed == false && unavailable.MatchPassed == false,
    "unavailable provider cannot turn booleans into success");
foreach (var result in new[] { (false, false), (true, false), (false, true) })
{
    var failed = Attempt(); failed.RecordProviderResult(true, result.Item1, result.Item2, "FAILED", now.AddSeconds(5));
    Check(failed.Outcome == FaceVerificationOutcome.Failed, "liveness AND one-to-one match required");
    Reject(() => failed.Consume(Raw(), enrollment, account, device, now.AddSeconds(20)), "failed result denied");
}
var good = Attempt(); good.RecordProviderResult(true, true, true, "PASSED", now.AddSeconds(5));
foreach (var resultField in new[] { "LivenessPassed", "MatchPassed", "CompletedAtUtc" })
{
    var inconsistent = Attempt(); inconsistent.RecordProviderResult(true, true, true, "PASSED", now.AddSeconds(5));
    Set(inconsistent, resultField, null);
    Reject(() => inconsistent.Consume(Raw(), enrollment, account, device, now.AddSeconds(20)),
        "materialized passed status requires confirmed " + resultField + " before consumption");
    Check(inconsistent.ConsumedAtUtc is null, "inconsistent result is not consumed");
}
Reject(() => good.RecordProviderResult(true, true, true, "PASSED", now.AddSeconds(6)), "result immutable once terminal");
Reject(() => good.Consume(Raw(action: PunchAction.CheckOut), enrollment, account, device, now.AddSeconds(20)), "proof Check In only");
Reject(() => good.Consume(Raw(key: "another-key"), enrollment, account, device, now.AddSeconds(20)), "proof bound to punch key");
Reject(() => good.Consume(Raw(employee: Guid.NewGuid()), enrollment, account, device, now.AddSeconds(20)), "proof bound to employee");
Reject(() => good.Consume(Raw(deviceId: Guid.NewGuid().ToString()), enrollment, account, device, now.AddSeconds(20)), "proof bound to device");
Reject(() => good.Consume(Raw(at: now.AddMinutes(2)), enrollment, account, device, now.AddMinutes(2)), "expiry exclusive");
var raw = Raw(); good.Consume(raw, enrollment, account, device, now.AddSeconds(20));
Check(good.ConsumedByPunchId == raw.Id, "successful proof consumption pins raw evidence");
Reject(() => good.Consume(raw, enrollment, account, device, now.AddSeconds(21)), "proof single use");
var assessment = new PunchAssessment(raw, 1, PunchAssessmentOutcome.Accepted, "CAPTURE_VALIDATED", null, "factual-v1", now.AddSeconds(20), good);
Check(assessment.PolicyVersionId is null, "factual assessment needs no fabricated policy");
var corruptAssessment = Attempt(); corruptAssessment.RecordProviderResult(true, true, true, "PASSED", now.AddSeconds(5));
corruptAssessment.Consume(raw, enrollment, account, device, now.AddSeconds(20));
Set(corruptAssessment, "LivenessPassed", false);
Reject(() => new PunchAssessment(raw, 1, PunchAssessmentOutcome.Accepted, "BAD", null, "factual-v1", now.AddSeconds(20), corruptAssessment),
    "a consumed identity-match-only proof cannot create accepted attendance");
Reject(() => new PunchAssessment(Raw(), 1, PunchAssessmentOutcome.Accepted, "BAD", null, "factual-v1", now.AddSeconds(20)), "accepted check-in requires consumed proof");
var open = new WorkSession(Guid.NewGuid(), 1, employeeId, new(2026, 10, 9), raw.ReceivedAtUtc, null,
    Guid.NewGuid(), null, now.AddSeconds(20), raw);
Check(open.PolicyVersionId is null && open.EndsAtUtc is null, "factual open session independent of policy");
var outRaw = Raw("checkout-key", PunchAction.CheckOut, at: now.AddHours(13));
var closed = new WorkSession(open.SessionKey, 2, employeeId, open.AttendanceDate, open.StartsAtUtc, outRaw.ReceivedAtUtc,
    open.ShiftVersionId, null, now.AddHours(13), raw, outRaw);
Check(closed.CheckOutPunchId == outRaw.Id && closed.AttendanceDate == open.AttendanceDate && open.EndsAtUtc is null,
    "closure is immutable new revision preserving shift-start date");
Reject(() => new WorkSession(open.SessionKey, 3, employeeId, open.AttendanceDate, open.StartsAtUtc.AddMinutes(1), outRaw.ReceivedAtUtc,
    open.ShiftVersionId, null, now.AddHours(13), raw, outRaw), "manual time change requires correction");
var expires = Attempt();
Reject(() => expires.Expire(now.AddSeconds(119)), "cannot expire live pending proof");
expires.Expire(now.AddMinutes(2));
Check(expires.Outcome == FaceVerificationOutcome.Expired && expires.CompletedAtUtc == now.AddMinutes(2), "late provider completion can be audited as expired");
Reject(() => expires.RecordProviderResult(true, true, true, "LATE", now.AddMinutes(2)), "expired attempt cannot revive");
Reject(() => expires.Consume(Raw(), enrollment, account, device, now.AddMinutes(2)), "expired attempt cannot consume");
var unverifiedDevice = new RegisteredDevice(account.Id, "other-synthetic", "ios", "Synthetic", now.AddDays(-1));
Reject(() => new FaceVerificationAttempt(enrollment, account, unverifiedDevice, "punch-key-002", new string('A', 64),
    "synthetic", "1", now, now.AddMinutes(2), "synthetic"), "unverified device cannot create challenge");
var revokeAttempt = Attempt(); revokeAttempt.RecordProviderResult(true, true, true, "PASSED", now.AddSeconds(5));
enrollment.Revoke("synthetic-admin", "Synthetic revocation", now.AddSeconds(10));
Reject(() => revokeAttempt.Consume(Raw(), enrollment, account, device, now.AddSeconds(20)), "revocation invalidates unused passed proof");
Check(!new FaceVerificationPolicy().IsConfirmed, "no automatic biometric authority or retention");
Check(FaceService.SecureDigestEquals(FaceService.Digest(secret), FaceService.Digest(secret)), "valid digest compare");
Check(!FaceService.SecureDigestEquals(FaceService.Digest("wrong"), FaceService.Digest(secret)), "wrong secret denied");
Console.WriteLine($"Capture model checks passed: {checks}");
