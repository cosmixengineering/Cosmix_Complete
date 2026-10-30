using System.Text;
using System.Text.Json;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.LocationConsent;
using Cosmix.Domain.Attendance.Punches;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cosmix.Tests;

public sealed class AttendanceSqlFactAttribute : FactAttribute
{
    public AttendanceSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COSMIX_ATTENDANCE_SQL_CONNECTION")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COSMIX_ATTENDANCE_SQL_WORKDIR")))
            Skip = "Requires explicit LocalDB master connection and disposable artifact directory.";
    }
}

[Trait("Category", "SqlIntegration")]
public sealed class AttendanceSqlIntegrationTests
{
    [AttendanceSqlFact]
    public async Task Fresh_SQL_consent_geofence_checkin_retry_checkout_history_and_revocation_are_persisted()
    {
        await using var f = new AttendanceSqlFixture();
        await f.InitializeAsync();
        var context = AttendanceSqlFixture.Context;
        Assert.Equal(f.Actor, await f.Run(s => s.Mobile.ValidateSessionAsync(f.Actor.AccountId, f.Actor.SessionId, f.Actor.SecurityVersion, default)));
        var request = f.CheckInRequest;
        var missingConsent = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Run(s =>
            s.Capture.PreflightAsync(new(24m, 67m, 5m, f.Clock.Now), context, default)));
        Assert.Equal("location_consent_required_before_gps_submission", missingConsent.Code);
        await AssertCounts(f, 0, 0, 0);
        var acceptance = await f.Run(s => s.Consent.AcceptAsync(new(1, "SYNTHETIC SQL fixture"), context, default));
        Assert.Equal(acceptance, await f.Run(s => s.Consent.AcceptAsync(new(1), context, default)));
        await using (var db = f.Db())
        {
            var consent = await db.EmployeeLocationConsents.SingleAsync();
            Assert.Equal(f.Actor.EmployeeId, consent.EmployeeId);
            Assert.Equal(f.NoticeId, consent.NoticePolicyId);
        }
        var unavailable = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Run(s =>
            s.Capture.SubmitAsync(PunchAction.CheckIn, request, AttendanceSqlFixture.CheckInKey, context, default), consentAvailable: false));
        Assert.Equal("location_policy_check_unavailable", unavailable.Code);
        await AssertCounts(f, 0, 0, 0);
        var outside = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Run(s =>
            s.Capture.PreflightAsync(new(25m, 67m, 5m, f.Clock.Now), context, default)));
        Assert.Equal("check_in_location_outside_boundary", outside.Code);
        Assert.True((await f.Run(s => s.Capture.PreflightAsync(new(24m, 67m, 5m, f.Clock.Now), context, default))).CanOpenCamera);
        await AssertCounts(f, 0, 0, 0);

        // A caller's fabricated proof cannot use the seeded synthetic upstream verification.
        var rejected = await f.Run(s => s.Capture.SubmitAsync(PunchAction.CheckIn, request with { Proof = "wrong-proof" }, "synthetic-invalid-proof", context, default));
        Assert.Equal("Rejected", rejected.Outcome);
        Assert.Equal("FACE_VERIFICATION_REQUIRED", rejected.ReasonCode);
        await AssertCounts(f, 1, 0, 1);
        await using (var db = f.Db()) Assert.Null((await db.FaceVerificationAttempts.SingleAsync()).ConsumedAtUtc);

        var concurrent = await Task.WhenAll(
            f.Run(s => s.Capture.SubmitAsync(PunchAction.CheckIn, request, AttendanceSqlFixture.CheckInKey, context, default)),
            f.Run(s => s.Capture.SubmitAsync(PunchAction.CheckIn, request, AttendanceSqlFixture.CheckInKey, context, default)));
        var checkIn = concurrent[0];
        Assert.Equal(JsonSerializer.Serialize(checkIn), JsonSerializer.Serialize(concurrent[1]));
        Assert.Equal("Accepted", checkIn.Outcome);
        Assert.Equal(f.Clock.Now, checkIn.ReceivedAtUtc);
        var retry = await f.Run(s => s.Capture.SubmitAsync(PunchAction.CheckIn, request, AttendanceSqlFixture.CheckInKey, context, default));
        Assert.Equal(JsonSerializer.Serialize(checkIn), JsonSerializer.Serialize(retry));
        await AssertCounts(f, 2, 1, 2);
        await using (var db = f.Db())
        {
            Assert.NotNull((await db.FaceVerificationAttempts.SingleAsync()).ConsumedAtUtc);
            Assert.Equal(1, await db.OutboxMessages.CountAsync()); // Rejected-proof exception notice is staged atomically.
        }
        var collision = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Run(s =>
            s.Capture.SubmitAsync(PunchAction.CheckIn, request with { Latitude = 25m }, AttendanceSqlFixture.CheckInKey, context, default)));
        Assert.Equal("idempotency_key_reused", collision.Code);
        await AssertCounts(f, 2, 1, 2);
        Assert.Equal("checked_in", (await f.Run(s => s.Capture.StateAsync(context, default))).State);

        f.Clock.Now = f.Clock.Now.AddHours(8);
        var checkout = new SubmitPunchRequest(false, f.Clock.Now, 24m, 67m, 5m);
        var checkOut = await f.Run(s => s.Capture.SubmitAsync(PunchAction.CheckOut, checkout, "synthetic-sql-checkout", context, default));
        Assert.Equal("Accepted", checkOut.Outcome);
        Assert.Equal(checkIn.SessionKey, checkOut.SessionKey);
        Assert.Equal(JsonSerializer.Serialize(checkOut), JsonSerializer.Serialize(await f.Run(s =>
            s.Capture.SubmitAsync(PunchAction.CheckOut, checkout, "synthetic-sql-checkout", context, default))));
        await AssertCounts(f, 3, 2, 3);
        Assert.Equal("checked_out", (await f.Run(s => s.Capture.StateAsync(context, default))).State);
        var range = new AttendanceHistoryRequest(f.Day, f.Day.AddDays(1), PageSize: 2);
        var history1 = await f.Run(s => s.Capture.HistoryAsync(range, context, default));
        var history2 = await f.Run(s => s.Capture.HistoryAsync(range with { Page = 2 }, context, default));
        Assert.Equal(3, history1.TotalCount); Assert.Equal(2, history1.Items.Count); Assert.Single(history2.Items);
        Assert.Equal(3, history1.Items.Concat(history2.Items).Select(x => x.Id).Distinct().Count());
        Assert.Equal(checkOut.Id, history1.Items[0].Id);
        var sessions = await f.Run(s => s.Capture.SessionsAsync(range, context, default));
        var closed = Assert.Single(sessions.Items);
        Assert.Equal(480m, closed.FactualWorkedMinutes);
        Assert.Equal(checkIn.Id, closed.CheckInPunchId); Assert.Equal(checkOut.Id, closed.CheckOutPunchId);
        await using (var db = f.Db())
        {
            Assert.Equal(3, await db.CaptureValidationFinalEvidences.CountAsync());
            var audits = await db.AuditEvents.Where(x => x.EventType == "attendance.punch.submit").ToArrayAsync();
            Assert.Equal(3, audits.Length);
            Assert.All(audits, a => { Assert.Equal(f.Actor.AccountId.ToString(), a.ActorUserId); Assert.Equal(f.Actor.EmployeeId, a.EmployeeScopeId); });
            foreach (var audit in audits)
            {
                var reference = JsonSerializer.Deserialize<PrivateFileReference>(audit.ProtectedChangesReference!)!;
                var evidence = Encoding.UTF8.GetString(await f.Files.ReadAsync(reference,
                    $"attendance:operation:{f.Actor.AccountId:N}:attendance.punch.submit:audit", default));
                Assert.DoesNotContain(AttendanceSqlFixture.Proof, evidence);
                Assert.DoesNotContain("\"Latitude\"", evidence); Assert.DoesNotContain("\"Longitude\"", evidence);
            }
            var notice = await db.LocationNoticePolicies.SingleAsync(); notice.Deactivate();
            var next = new LocationNoticePolicy(2, "SYNTHETIC revised notice", f.Actor.AccountId.ToString(), f.Clock.Now);
            next.Publish(f.Actor.AccountId.ToString(), f.Clock.Now); db.Add(next); await db.SaveChangesAsync();
        }
        var changedNotice = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Run(s =>
            s.Capture.SubmitAsync(PunchAction.CheckOut, checkout, "synthetic-after-notice", context, default)));
        Assert.Equal("location_consent_required_before_gps_submission", changedNotice.Code);
        await AssertCounts(f, 3, 2, 3);
        await using (var db = f.Db())
        {
            (await db.RegisteredDevices.SingleAsync()).Revoke("SYNTHETIC device revocation", f.Clock.Now);
            await db.SaveChangesAsync();
        }
        Assert.Null(await f.Run(s => s.Mobile.ValidateSessionAsync(f.Actor.AccountId, f.Actor.SessionId, f.Actor.SecurityVersion, default)));
        var revoked = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Run(s => s.Capture.StateAsync(context, default)));
        Assert.Equal(401, revoked.StatusCode);
        await AssertCounts(f, 3, 2, 3);
        await File.WriteAllTextAsync(Path.Combine(f.DirectoryPath, "result.json"), JsonSerializer.Serialize(new {
            Result = "Passed", Scope = "Production services/repositories, fresh SQL migrations, encrypted receipts; synthetic provisioned identity and preverified biometric proof",
            RawPunches = 3, WorkSessionRevisions = 2, IdempotencyReceipts = 3, CameraOrSmsAcceptance = false
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static async Task AssertCounts(AttendanceSqlFixture fixture, int punches, int sessions, int receipts)
    {
        await using var db = fixture.Db();
        Assert.Equal(punches, await db.RawPunchEvents.CountAsync());
        Assert.Equal(punches, await db.PunchAssessments.CountAsync());
        Assert.Equal(sessions, await db.WorkSessions.CountAsync());
        Assert.Equal(receipts, await db.IdempotencyReceipts.CountAsync());
    }
}
