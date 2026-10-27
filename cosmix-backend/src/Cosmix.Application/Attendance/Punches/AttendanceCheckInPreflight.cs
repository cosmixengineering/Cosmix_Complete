using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Authentication;

namespace Cosmix.Application.Attendance.Punches;

public sealed partial class AttendanceCaptureService
{
    /// <summary>Read-only business preflight. A successful response is not a punch,
    /// proof or authorization token; final submission validates current evidence again.</summary>
    public Task<CheckInPreflightResponse> PreflightAsync(CheckInPreflightRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        if (request is null || request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180 ||
            request.AccuracyMeters is <= 0 or > 99999999.99m ||
            decimal.Round(request.Latitude, 7) != request.Latitude ||
            decimal.Round(request.Longitude, 7) != request.Longitude ||
            decimal.Round(request.AccuracyMeters, 2) != request.AccuracyMeters ||
            request.DeviceCapturedAtUtc == default || request.DeviceCapturedAtUtc.Offset != TimeSpan.Zero)
            throw AttendanceOperation.Invalid("invalid_check_in_location");
        return operation.ReadAsync(AttendanceCapturePermissions.Capture, "attendance.check_in.location.preflight", null,
            context, async (actor, token) =>
            {
                RequireFreshLocation(request, actor.Now);
                var employeeId = actor.Identity.EmployeeId;
                if (locationConsent is null)
                    throw new AuthenticationFailure("location_policy_check_unavailable", 503);
                var notice = await locationConsent.GetActivePolicyAsync(token);
                if (notice is null)
                    throw AttendanceOperation.Invalid("location_notice_not_configured");
                if (!notice.IsActive || notice.PublishedAt is null || notice.PublishedAt > actor.Now)
                    throw new AuthenticationFailure("location_policy_check_unavailable", 503);
                var consent = await locationConsent.GetConsentAsync(employeeId, notice.Version, token);
                if (consent is null || consent.EmployeeId != employeeId || consent.NoticePolicyId != notice.Id ||
                    consent.NoticePolicyVersion != notice.Version || consent.AcceptedAtUtc > actor.Now)
                    throw AttendanceOperation.Conflict("location_consent_required_before_gps_submission");
                var active = await repository.ActiveSessionsAsync(employeeId, token);
                if (active.Count != 0) throw AttendanceOperation.Conflict("check_in_active_session");
                var roster = await SelectRosterAsync(employeeId, active, actor.Now, token);
                if (roster.EmployeeId != employeeId || roster.Status != "scheduled" ||
                    roster.StartsAt is null || roster.EndsAt is null || roster.ShiftVersionId is null ||
                    roster.SiteId.HasValue == roster.BranchId.HasValue ||
                    roster.Explanation?.MembershipRecordId is null || roster.StartsAt > actor.Now || actor.Now >= roster.EndsAt)
                    throw AttendanceOperation.Conflict("check_in_roster_unavailable");
                if (await repository.IsLockedAsync(roster.BusinessDate, token))
                    throw AttendanceOperation.Conflict("attendance_period_locked");
                if (!await CapturePolicyReadyAsync(actor.Now, token))
                    throw new AuthenticationFailure("capture_validation_policy_unavailable", 503);
                GeofenceEvaluation evaluation;
                if (roster.BranchId is Guid branch)
                {
                    var office = await SafeOfficeAsync(employeeId, roster.BusinessDate, roster, null, token);
                    if (office.Source is null || office.Boundary is null ||
                        !await offices.CurrentMembershipMatchesAsync(employeeId, branch,
                            roster.Explanation.MembershipRecordId, RosterSchedule.Today(actor.Now), token))
                        throw AttendanceOperation.Conflict("office_source_unavailable");
                    var boundary = office.Boundary;
                    evaluation = SiteGeofenceEvaluator.EvaluateCircle(boundary.Id, boundary.Latitude, boundary.Longitude,
                        boundary.RadiusMeters, boundary.MaximumAccuracyMeters,
                        request.Latitude, request.Longitude, request.AccuracyMeters);
                }
                else
                {
                    var assignments = await geofences.EmployeeSitesAsync(employeeId, RosterSchedule.Today(actor.Now), token);
                    if (assignments.Count != 1 || assignments[0].SiteId != roster.SiteId ||
                        assignments[0].AssignmentId != roster.Explanation.MembershipRecordId)
                        throw AttendanceOperation.Conflict("site_relationship_changed");
                    var boundary = await geofences.EffectiveAsync(roster.SiteId!.Value, roster.BusinessDate, token);
                    if (boundary is null || boundary.SiteId != roster.SiteId || boundary.EffectiveFrom > roster.BusinessDate)
                        throw AttendanceOperation.Conflict("check_in_boundary_unavailable");
                    evaluation = SiteGeofenceEvaluator.Evaluate(boundary, request.Latitude, request.Longitude, request.AccuracyMeters);
                }
                if (evaluation.Status != "inside")
                    throw AttendanceOperation.Conflict(evaluation.Status == "outside"
                        ? "check_in_location_outside_boundary" : "check_in_location_unreliable");
                var checkedAt = clock.GetUtcNow();
                RequireFreshLocation(request, checkedAt);
                if (roster.StartsAt > checkedAt || checkedAt >= roster.EndsAt)
                    throw AttendanceOperation.Conflict("check_in_roster_unavailable");
                // Recheck persisted mobile/device authority after awaited source/policy reads.
                var session = await authentication.GetSessionAsync(actor.Identity.SessionId, token);
                var device = await authentication.GetDeviceAsync(actor.Identity.AccountId, actor.DeviceId!.Value, token);
                checkedAt = clock.GetUtcNow();
                if (session is null || session.Id != actor.Identity.SessionId || session.UserAccountId != actor.Identity.AccountId ||
                    session.Client != SessionClient.Mobile || session.SecurityVersion != actor.Identity.SecurityVersion ||
                    session.RegisteredDeviceId != actor.DeviceId || !session.IsActiveAt(checkedAt) ||
                    device is null || device.Id != actor.DeviceId || device.UserAccountId != actor.Identity.AccountId ||
                    device.RevokedAt.HasValue || device.VerifiedAt is null || device.VerifiedAt > checkedAt)
                    throw new AuthenticationFailure("invalid_authentication", 401);
                RequireFreshLocation(request, checkedAt);
                return new CheckInPreflightResponse(true, checkedAt);
            }, ct, self: true, requireDevice: true);
    }

    private static void RequireFreshLocation(CheckInPreflightRequest request, DateTimeOffset now)
    {
        var age = now - request.DeviceCapturedAtUtc;
        if (age > TimeSpan.FromSeconds(30) || age < TimeSpan.FromSeconds(-5))
            throw AttendanceOperation.Invalid("check_in_location_stale");
    }
}
