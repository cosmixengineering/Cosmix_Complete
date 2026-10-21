using System.Text.RegularExpressions;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Zones;

namespace Cosmix.Application.Attendance.Zones;

/// <summary>Append-only historical employee reports, never live location or verified entry/exit.</summary>
public sealed class ZoneOfflineObservationService(AttendanceOperation operation, IZoneRepository repository,
    IRosterRepository rosters, IAttendanceCaptureRepository capture, ZoneOfflineObservationOptions options,
    IZoneClaimStore claims, ISchedulingJournalRepository journal, IAccountAccessReader access,
    IAuthenticationRepository authentication, TimeProvider clock, IZoneClaimRetentionStatus retention)
{
    public const string KeyPattern = @"\A[A-Za-z0-9_.:-]{8,100}\z";

    public Task<ZoneOfflineObservationReceipt> SubmitAsync(Guid windowId, ZoneOfflineObservationRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (key is null || key.Length is < 8 or > 100 || !Regex.IsMatch(key, KeyPattern, RegexOptions.CultureInvariant))
            throw AttendanceOperation.Invalid("invalid_idempotency_key");
        ValidateRequest(windowId, request, clock.GetUtcNow());
        var requestFingerprint = ZoneProjection.Sha(new { windowId, request });
        return operation.WriteAsync(ZonePermissions.Self, "zones.offline-observation", null, key,
            new { windowId, requestFingerprint }, "Employee submitted an unverified offline zone report", context,
            async (actor, token) =>
            {
                ValidateRequest(windowId, request, actor.Now);
                if (await repository.HasEventKeyAsync(actor.Identity.EmployeeId, key, token))
                    throw AttendanceOperation.Conflict("zone_observation_already_received");
                var window = await repository.WindowAsync(actor.Identity.EmployeeId, windowId, token);
                if (window is null || window.Id != windowId || window.EmployeeId != actor.Identity.EmployeeId ||
                    window.RegisteredDeviceId != actor.DeviceId || window.WorkSessionKey != request.SessionKey ||
                    window.ZoneAssignmentId != request.AssignmentId || window.ConfigurationVersionId != request.ConfigurationVersionId ||
                    window.AcceptedCheckInId is null || window.RetentionPolicyVersionId.HasValue ||
                    window.NoticeShownAtUtc == DateTimeOffset.MinValue || window.NoticeShownAtUtc > window.StartsAtUtc)
                    throw AttendanceOperation.Conflict("zone_offline_window_unavailable");
                var at = request.DeviceCapturedAtUtc;
                if (at < window.StartsAtUtc || at >= window.EndsAtUtc || at < window.NoticeShownAtUtc)
                    throw Invalid();

                var identity = await capture.IdentityAsync(actor.Identity.AccountId, actor.DeviceId!.Value, token);
                if (identity.Account.Id != actor.Identity.AccountId || identity.Account.EmployeeId != actor.Identity.EmployeeId ||
                    identity.Device.Id != actor.DeviceId || identity.Device.UserAccountId != identity.Account.Id ||
                    identity.Device.RevokedAt.HasValue || identity.Device.VerifiedAt is null || identity.Device.VerifiedAt > actor.Now)
                    throw new AuthenticationFailure("invalid_authentication", 401);
                if (at < identity.Device.RegisteredAt) throw AttendanceOperation.Invalid("zone_capture_precedes_registered_device");
                if (at < identity.Device.VerifiedAt) throw AttendanceOperation.Invalid("zone_capture_precedes_verified_device");

                var duty = await repository.HistoricalDutyAsync(actor.Identity.EmployeeId, request.SessionKey, at, token);
                if (duty is null || duty.Session.EmployeeId != actor.Identity.EmployeeId || duty.Session.SessionKey != request.SessionKey ||
                    duty.Session.SiteId is null || duty.Session.CheckInPunchId is null || duty.CheckInId == Guid.Empty ||
                    duty.CheckInId != duty.Session.CheckInPunchId || duty.Session.StartsAtUtc > at || duty.Session.EndsAtUtc <= at)
                    throw AttendanceOperation.Conflict("zone_historical_accepted_duty_required");
                if (duty.CheckInId != window.AcceptedCheckInId)
                    throw AttendanceOperation.Conflict("zone_offline_window_unavailable");
                var site = duty.Session.SiteId.Value;
                var date = ZoneProjection.Date(at);
                if (await rosters.EffectiveSiteAsync(actor.Identity.EmployeeId, date, token) != site)
                    throw AttendanceOperation.Conflict("zone_offline_site_assignment_unavailable");
                await rosters.ValidateEligibilityAsync(actor.Identity.EmployeeId, date, date.AddDays(1), site, at, token);

                var configurations = await repository.ConfigurationsAsync(site, token);
                var configuration = configurations.Where(x => x.SiteId == site && x.EffectiveFromUtc <= at)
                    .OrderByDescending(x => x.EffectiveFromUtc).ThenByDescending(x => x.VersionNumber).FirstOrDefault();
                if (configurations.Count > 2048 || configuration is null || configuration.Id != window.ConfigurationVersionId ||
                    configuration.EffectiveToUtc <= at || configuration.ApprovedAtUtc > window.NoticeShownAtUtc ||
                    string.IsNullOrWhiteSpace(configuration.AuthorityEvidenceReference) ||
                    duty.Session.StartsAtUtc.AddSeconds(configuration.MaximumMonitoringSeconds) <= at)
                    throw AttendanceOperation.Conflict("zone_offline_configuration_unavailable");
                if (!string.Equals(request.NoticeSha256, ZoneProjection.NoticeSha(configuration.VisibleNotice), StringComparison.Ordinal))
                    throw AttendanceOperation.Conflict("zone_offline_notice_mismatch");

                var matches = await repository.ApplicableAssignmentsAsync(actor.Identity.EmployeeId, site, at, token);
                var pinned = matches.Where(x => x.Assignment.Id == window.ZoneAssignmentId).ToArray();
                if (matches.Count > 2048 || pinned.Length != 1)
                    throw AttendanceOperation.Conflict("zone_offline_assignment_unavailable");
                var assignment = pinned[0]; var version = assignment.Assignment.ZoneVersion;
                var group = matches.Where(x => x.Assignment.ZoneVersion.OperationalZoneId == version.OperationalZoneId).ToArray();
                var code = version.ZoneType.Code;
                if (assignment.Assignment.IsEmployeeOverride || assignment.Assignment.EmployeeId.HasValue && assignment.Assignment.EmployeeId != actor.Identity.EmployeeId ||
                    assignment.Assignment.EffectiveFromUtc > at || ZoneProjection.End(assignment) <= at ||
                    assignment.Assignment.ApprovedAtUtc > window.NoticeShownAtUtc || version.ApprovedAtUtc > window.NoticeShownAtUtc ||
                    version.EffectiveFromUtc > at || version.EffectiveToUtc <= at || version.Zone.SiteId != site ||
                    !version.Zone.IsActive || !version.ZoneType.IsActive || code is not ("OPERATIONAL" or "RED") ||
                    version.EntryRule != (code == "RED" ? ZoneEntryRule.Prohibited : ZoneEntryRule.Permitted) ||
                    string.IsNullOrWhiteSpace(version.DirectAuthorityEvidenceReference) || string.IsNullOrWhiteSpace(assignment.Assignment.DirectAuthorityEvidenceReference) ||
                    group.Any(x => x.Assignment.IsEmployeeOverride) || group.Select(x => x.Assignment.ZoneVersionId).Distinct().Count() != 1)
                    throw AttendanceOperation.Conflict("zone_offline_assignment_unavailable");

                var policy = RequirePolicy(configuration.Id, window.NoticeShownAtUtc, at, actor.Now);
                if (!claims.IsConfigured) throw new AuthenticationFailure("zone_claim_storage_unavailable", 503);
                var prepareNow = await RequireCurrentAsync(actor, token);
                if (!retention.IsAvailable(prepareNow)) throw new AuthenticationFailure("zone_claim_retention_unavailable", 503);
                if (RequirePolicy(configuration.Id, window.NoticeShownAtUtc, at, prepareNow) != policy) throw Unconfigured();
                var eventId = Guid.NewGuid();
                var expiry = at.AddDays(policy.ReportRetentionDays);
                var reference = await claims.StoreAsync(new(eventId, actor.Identity.EmployeeId, actor.Identity.AccountId,
                    actor.DeviceId.Value, window.Id, assignment.Assignment.Id, configuration.Id, request.SessionKey,
                    duty.CheckInId, duty.Session.Revision, request.NoticeSha256, window.NoticeShownAtUtc, actor.Now,
                    at, expiry, request.EventKindHint, request.WrittenClaim, requestFingerprint, policy), token);
                // Private I/O may outlast session, policy delay or retention. Recheck before staging SQL.
                var finalNow = await RequireCurrentAsync(actor, token);
                if (!retention.IsAvailable(finalNow)) throw new AuthenticationFailure("zone_claim_retention_unavailable", 503);
                if (RequirePolicy(configuration.Id, window.NoticeShownAtUtc, at, finalNow) != policy)
                    throw Unconfigured();
                var row = ZoneEvent.EmployeeReport(eventId, window, assignment.Assignment, request.EventKindHint,
                    identity.Account, identity.Device, key, actor.Now, at, reference, expiry);
                repository.Add(row);
                return new AttendanceChange<ZoneOfflineObservationReceipt>(row.Id, null,
                    new(row.Id, window.Id, "unverified_employee_report", row.ReceivedAtUtc, at, request.EventKindHint, expiry));
            }, ct, self: true, requireDevice: true, beforeReplay: async (actor, token) => { await RequireCurrentAsync(actor, token); });
    }

    public Task<ZoneOfflineObservationReconciliation> ReconcileAsync(Guid windowId, string key, AuthRequestContext context, CancellationToken ct)
    {
        if (windowId == Guid.Empty || key is null || key.Length is < 8 or > 100 || !Regex.IsMatch(key, KeyPattern, RegexOptions.CultureInvariant))
            throw AttendanceOperation.Invalid("invalid_idempotency_key");
        return operation.ReadAsync(ZonePermissions.Self, "zones.offline-observation-reconciliation", null, context,
            async (actor, token) =>
            {
                var row = await repository.OwnOfflineEventAsync(actor.Identity.EmployeeId, actor.Identity.AccountId, windowId, key, token);
                await RequireCurrentAsync(actor, token);
                if (row is null) return new ZoneOfflineObservationReconciliation(false, "no_matching_retained_report", null);
                if (row.Event.EmployeeId != actor.Identity.EmployeeId || row.Event.UserAccountId != actor.Identity.AccountId ||
                    row.Event.DutyMonitoringWindowId != windowId || row.Event.IdempotencyKey != key ||
                    row.Event.Source != ZoneEventSource.EmployeeReport || !row.Event.IsOffline)
                    throw AttendanceOperation.Denied();
                return new ZoneOfflineObservationReconciliation(true, "current_retained_report_not_original_receipt", ZoneProjection.Event(row));
            }, ct, self: true, requireDevice: true);
    }

    private async Task<DateTimeOffset> RequireCurrentAsync(AttendanceActor actor, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (now < actor.Now || !await journal.IsCurrentSessionAsync(actor.Identity, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);
        var session = await authentication.GetSessionAsync(actor.Identity.SessionId, ct);
        var device = await authentication.GetDeviceAsync(actor.Identity.AccountId, actor.DeviceId!.Value, ct);
        if (session is null || session.UserAccountId != actor.Identity.AccountId || session.Client != SessionClient.Mobile ||
            session.RegisteredDeviceId != actor.DeviceId || session.SecurityVersion != actor.Identity.SecurityVersion || !session.IsActiveAt(now) ||
            device is null || device.Id != actor.DeviceId || device.UserAccountId != actor.Identity.AccountId ||
            device.RevokedAt.HasValue || device.VerifiedAt is null || device.VerifiedAt > now)
            throw new AuthenticationFailure("invalid_authentication", 401);
        var grants = await access.GetGrantsAsync(actor.Identity.AccountId, now, ct);
        var last = clock.GetUtcNow();
        if (last < now || !session.IsActiveAt(last) || device.VerifiedAt > last)
            throw new AuthenticationFailure("invalid_authentication", 401);
        if (!AccessPermissionEvaluator.Allows(actor.Identity, grants.Where(x => x.IsEffectiveAt(last)),
                ZonePermissions.Self, new(EmployeeId: actor.Identity.EmployeeId)))
            throw AttendanceOperation.Denied();
        return last;
    }

    private ZoneOfflineObservationPolicy RequirePolicy(Guid configurationId, DateTimeOffset noticeAt, DateTimeOffset capturedAt, DateTimeOffset now)
    {
        if (!options.Enabled || options.Policies is null || options.Policies.Count > 2048 || options.Policies.Any(x => x is null))
            throw Unconfigured();
        var policies = options.Policies.Where(x => x.ConfigurationVersionId == configurationId).Take(2).ToArray();
        if (policies.Length != 1) throw Unconfigured();
        var policy = policies[0];
        if (policy.ApprovedAtUtc.Offset != TimeSpan.Zero || policy.ApprovedAtUtc == DateTimeOffset.MinValue || policy.ApprovedAtUtc > noticeAt ||
            string.IsNullOrWhiteSpace(policy.AuthorityEvidenceReference) || policy.AuthorityEvidenceReference.Length > 500 ||
            policy.AuthorityEvidenceReference.Any(char.IsControl) || policy.ReportRetentionDays is < 1 or > 3650 ||
            policy.MaximumDelaySeconds < 1 || policy.MaximumDelaySeconds > (long)policy.ReportRetentionDays * 86400)
            throw Unconfigured();
        if (now - capturedAt >= TimeSpan.FromDays(policy.ReportRetentionDays))
            throw AttendanceOperation.Conflict("zone_offline_retention_elapsed");
        if (now - capturedAt > TimeSpan.FromSeconds(policy.MaximumDelaySeconds))
            throw AttendanceOperation.Conflict("zone_offline_delay_exceeded");
        return policy;
    }

    private static void ValidateRequest(Guid windowId, ZoneOfflineObservationRequest? request, DateTimeOffset now)
    {
        if (windowId == Guid.Empty || request is null || request.AssignmentId == Guid.Empty || request.ConfigurationVersionId == Guid.Empty ||
            request.SessionKey == Guid.Empty || !Enum.IsDefined(request.EventKindHint) || request.DeviceCapturedAtUtc.Offset != TimeSpan.Zero ||
            request.DeviceCapturedAtUtc == DateTimeOffset.MinValue || request.DeviceCapturedAtUtc > now ||
            request.NoticeSha256 is null || !Regex.IsMatch(request.NoticeSha256, @"\A[A-F0-9]{64}\z", RegexOptions.CultureInvariant) ||
            string.IsNullOrWhiteSpace(request.WrittenClaim) || request.WrittenClaim.Length > 1000 ||
            request.WrittenClaim.Any(x => char.IsControl(x) && x is not ('\r' or '\n' or '\t')))
            throw Invalid();
    }
    private static AuthenticationFailure Invalid() => AttendanceOperation.Invalid("invalid_zone_offline_observation");
    private static AuthenticationFailure Unconfigured() => new("zone_offline_policy_unavailable", 503);
}
