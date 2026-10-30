using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Infrastructure.FaceVerification;
using Cosmix.Tests.Preflight;
using Cosmix.Domain.Attendance.LocationConsent;
using System.Text.Json;
using Xunit;

namespace Cosmix.Tests;

// Controlled application-service integration. Repository, identity and face proof are synthetic;
// this suite does not claim SQL persistence, camera/liveness, mobile login or device acceptance.
public sealed class MobileAttendanceWorkflowAcceptanceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Preflight_checkin_exact_retry_checkout_and_history_use_production_service(bool office)
    {
        var f = new PreflightFixture();
        if (office) f.UseOffice();
        var initial = await f.Service.StateAsync(f.Context, default);
        Assert.True(initial.CheckInEnabled);
        Assert.False(initial.CheckOutEnabled);
        var preflight = await f.Service.PreflightAsync(new(24m, 67m, 5m, f.Clock.Now), f.Context, default);
        Assert.True(preflight.CanOpenCamera);
        Assert.Empty(f.Raw);
        Assert.Empty(f.Sessions);

        var checkIn = await f.In();
        Assert.Equal("Accepted", checkIn.Outcome);
        Assert.Equal(f.Clock.Now, checkIn.ReceivedAtUtc);
        Assert.Equal(f.Actor.EmployeeId, Assert.Single(f.Raw).EmployeeId);
        Assert.NotNull(f.Attempt.ConsumedAtUtc);
        Assert.Single(f.Sessions);
        Assert.Equal(JsonSerializer.Serialize(checkIn), JsonSerializer.Serialize(await f.In()));
        Assert.Single(f.Raw);
        Assert.Single(f.Sessions);
        var active = await f.Service.StateAsync(f.Context, default);
        Assert.Equal("checked_in", active.State);
        Assert.False(active.CheckInEnabled);
        Assert.True(active.CheckOutEnabled);

        f.Clock.Now = f.Clock.Now.AddHours(8);
        var checkoutRequest = new SubmitPunchRequest(false, f.Clock.Now, 24m, 67m, 5m);
        var checkOut = await f.Service.SubmitAsync(PunchAction.CheckOut, checkoutRequest, "synthetic-checkout", f.Context, default);
        Assert.Equal("Accepted", checkOut.Outcome);
        Assert.Equal(checkIn.SessionKey, checkOut.SessionKey);
        Assert.Equal(JsonSerializer.Serialize(checkOut), JsonSerializer.Serialize(await f.Service.SubmitAsync(PunchAction.CheckOut, checkoutRequest, "synthetic-checkout", f.Context, default)));
        Assert.Equal(2, f.Raw.Count);
        Assert.Equal(2, f.Sessions.Count);
        Assert.Equal("checked_out", (await f.Service.StateAsync(f.Context, default)).State);
        var range = new AttendanceHistoryRequest(f.Roster.BusinessDate, f.Roster.BusinessDate.AddDays(1));
        var history = await f.Service.HistoryAsync(range, f.Context, default);
        Assert.Equal(2, history.TotalCount);
        Assert.Equal(new[] { checkIn.Id, checkOut.Id }, history.Items.OrderBy(x => x.ReceivedAtUtc).Select(x => x.Id));
        Assert.All(history.Items, x => Assert.Equal("Accepted", x.Outcome));
        var sessions = await f.Service.SessionsAsync(range, f.Context, default);
        var closed = Assert.Single(sessions.Items);
        Assert.Equal(480m, closed.FactualWorkedMinutes);
        Assert.Equal(checkIn.Id, closed.CheckInPunchId);
        Assert.Equal(checkOut.Id, closed.CheckOutPunchId);
    }

    [Theory]
    [InlineData(PunchAction.CheckIn)]
    [InlineData(PunchAction.CheckOut)]
    public async Task Direct_GPS_submission_fails_closed_when_consent_repository_is_unavailable(PunchAction action)
    {
        var f = new PreflightFixture(consentAvailable: false);
        var request = action == PunchAction.CheckIn ? f.Request : f.Request with { FaceAttemptId = null, Proof = null };
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Service.SubmitAsync(action, request, f.Key, f.Context, default));
        Assert.Equal("location_policy_check_unavailable", error.Code);
        Assert.Equal(503, error.StatusCode);
        Assert.Empty(f.Raw);
        Assert.Empty(f.Assessments);
        Assert.Empty(f.Sessions);
        Assert.Null(f.Attempt.ConsumedAtUtc);
    }

    [Fact]
    public async Task A_successful_preflight_does_not_authorize_outside_radius_submission()
    {
        var f = new PreflightFixture();
        Assert.True((await f.Service.PreflightAsync(new(24m, 67m, 5m, f.Clock.Now), f.Context, default)).CanOpenCamera);
        f.Request = f.Request with { Latitude = 25m };
        var result = await f.In();
        Assert.NotEqual("Accepted", result.Outcome);
        Assert.Empty(f.Sessions);
        Assert.Null(f.Attempt.ConsumedAtUtc);
        Assert.Single(f.Raw);
    }

    public static IEnumerable<object[]> InvalidConsentCases() =>
        from entryPoint in new[] { "preflight", "checkin", "checkout" }
        from defect in new[] { "employee", "notice-id", "notice-version", "future-consent", "future-publication", "unpublished" }
        select new object[] { entryPoint, defect };

    [Theory, MemberData(nameof(InvalidConsentCases))]
    public async Task Location_consent_must_belong_to_current_employee_and_published_notice(string entryPoint, string defect)
    {
        var f = new PreflightFixture();
        var notice = f.NoticePolicy!;
        f.ConsentOverride = () => new EmployeeLocationConsent(
            defect == "employee" ? Guid.NewGuid() : f.Actor.EmployeeId,
            defect == "notice-id" ? Guid.NewGuid() : notice.Id,
            defect == "notice-version" ? notice.Version + 1 : notice.Version,
            defect == "future-consent" ? f.Clock.Now.AddMinutes(1) : f.Clock.Now.AddMinutes(-1));
        if (defect == "future-publication") notice.Publish("synthetic", f.Clock.Now.AddMinutes(1));
        if (defect == "unpublished") PreflightFixture.Set(notice, nameof(notice.PublishedAt), null);
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(async () =>
        {
            if (entryPoint == "preflight")
                await f.Service.PreflightAsync(new(24m, 67m, 5m, f.Clock.Now), f.Context, default);
            else
                await f.Service.SubmitAsync(entryPoint == "checkin" ? PunchAction.CheckIn : PunchAction.CheckOut,
                    entryPoint == "checkin" ? f.Request : f.Request with { FaceAttemptId = null, Proof = null }, f.Key, f.Context, default);
        });
        Assert.Equal(defect is "future-publication" or "unpublished" ? "location_policy_check_unavailable" : "location_consent_required_before_gps_submission", error.Code);
        Assert.Empty(f.Raw);
        Assert.Empty(f.Sessions);
        Assert.Null(f.Attempt.ConsumedAtUtc);
    }

    [Fact]
    public async Task Check_out_rejects_face_evidence_before_any_business_write()
    {
        var f = new PreflightFixture();
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Service.SubmitAsync(PunchAction.CheckOut, f.Request, f.Key, f.Context, default));
        Assert.Equal("invalid_punch_evidence", error.Code);
        Assert.Empty(f.Raw);
        Assert.Empty(f.Sessions);
    }

    [Fact]
    public async Task Client_clock_cannot_replace_server_attendance_timestamp()
    {
        var f = new PreflightFixture();
        var serverTime = f.Clock.Now;
        f.Request = f.Request with { DeviceCapturedAtUtc = serverTime.AddSeconds(-10) };
        var result = await f.In();
        Assert.Equal("Accepted", result.Outcome);
        Assert.Equal(serverTime, result.ReceivedAtUtc);
        Assert.Equal(serverTime.AddSeconds(-10), Assert.Single(f.Raw).DeviceCapturedAtUtc);
        Assert.Equal(serverTime, Assert.Single(f.Sessions).StartsAtUtc);
    }

    [Fact]
    public void Face_provider_timeout_budget_is_bounded()
    {
        var options = new CompreFaceOptions
        {
            BaseUri = new Uri("https://10.0.0.1/"), DetectionApiKey = "test-key-1", VerificationApiKey = "test-key-2",
            DeploymentVersion = "synthetic", MinimumDetectionProbability = 0.9, TimeoutSeconds = 30
        };
        CompreFaceClient.ValidateOptions(options);
        CompreFaceClient.ValidateOptions(options with { TimeoutSeconds = 60 });
        Assert.Throws<AuthenticationFailure>(() => CompreFaceClient.ValidateOptions(options with { TimeoutSeconds = 61 }));
    }
}
