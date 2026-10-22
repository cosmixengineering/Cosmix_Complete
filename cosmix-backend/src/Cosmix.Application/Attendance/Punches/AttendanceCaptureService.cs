using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Attendance.LocationConsent;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Attendance.Punches.CaptureValidation;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Sites;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;

namespace Cosmix.Application.Attendance.Punches;

public sealed class AttendanceCaptureService(AttendanceOperation operation, IAttendanceCaptureRepository repository,
    IIndividualRosterResolver rosters, ISiteGeofenceRepository geofences,
    IFaceVerificationProvider face, FaceVerificationPolicy facePolicy, TimeProvider clock,
    IAccountAccessReader access, IFaceTemplateStore templates, INotificationOutbox notifications,
    IOfficeAttendanceSourceReader offices, ICaptureValidationEvaluator captureValidation,
    ICaptureValidationPolicyReader capturePolicies, ICaptureValidationEvidenceRepository validationEvidence,
    IAuthenticationRepository authentication, ISchedulingJournalRepository journal,
    ILocationConsentRepository? locationConsent = null)
{
    public Task<PunchResponse> SubmitAsync(PunchAction action, SubmitPunchRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        Validate(request);
        if (!Enum.IsDefined(action) || request.FaceAttemptId == Guid.Empty || request.Proof?.Length > 128 ||
            request.Latitude.HasValue != request.Longitude.HasValue || request.Latitude is < -90 or > 90 ||
            request.Longitude is < -180 or > 180 || request.AccuracyMeters is <= 0 or > 99999999.99m ||
            request.Latitude.HasValue && decimal.Round(request.Latitude.Value, 7) != request.Latitude.Value ||
            request.Longitude.HasValue && decimal.Round(request.Longitude.Value, 7) != request.Longitude.Value ||
            request.AccuracyMeters.HasValue && decimal.Round(request.AccuracyMeters.Value, 2) != request.AccuracyMeters.Value ||
            request.AccuracyMeters.HasValue && !request.Latitude.HasValue ||
            request.IsOffline && !request.DeviceCapturedAtUtc.HasValue ||
            action == PunchAction.CheckOut && (request.FaceAttemptId.HasValue || request.Proof is not null))
            throw AttendanceOperation.Invalid("invalid_punch_evidence");
        // The secret itself is never copied to a punch or audit snapshot.
        var payload = new { Action = action, request.IsOffline, request.DeviceCapturedAtUtc, request.Latitude,
            request.Longitude, request.AccuracyMeters, request.FaceAttemptId, ProofDigest = request.Proof is null ? null : FaceService.Digest(request.Proof) };
        return operation.WriteAsync(AttendanceCapturePermissions.Capture, "attendance.punch.submit", null, key, payload,
            "Employee submitted mobile attendance evidence", context, async (actor, token) =>
            {
                var employeeId = actor.Identity.EmployeeId;
                if (request.Latitude.HasValue && request.Longitude.HasValue && locationConsent is not null)
                {
                    var activePolicy = await locationConsent.GetActivePolicyAsync(token);
                    if (activePolicy is not null && activePolicy.IsActive)
                    {
                        var consent = await locationConsent.GetConsentAsync(employeeId, activePolicy.Version, token);
                        if (consent is null)
                        {
                            throw AttendanceOperation.Invalid("location_consent_required_before_gps_submission");
                        }
                    }
                }
                var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { actor.DeviceId, payload })));
                // This unique evidence key outlives the short-lived protected HTTP receipt.
                var duplicate = await repository.FindPunchAsync(employeeId, key, token);
                if (duplicate is not null)
                {
                    if (duplicate.RequestSha256 != fingerprint) throw AttendanceOperation.Conflict("punch_key_reused");
                    throw AttendanceOperation.Conflict("punch_already_recorded_read_history");
                }
                var active = await repository.ActiveSessionsAsync(employeeId, token);
                var selected = await SelectRosterAsync(employeeId, active, actor.Now, token);
                var businessDate = selected.BusinessDate;
                var original = active.Count == 1 && active[0].CheckInPunchId is Guid openingId
                    ? await repository.PunchAsync(employeeId, openingId, token) : null;
                SiteGeofenceVersion? boundary = null;
                var office = await SafeOfficeAsync(employeeId, businessDate, selected,
                    action == PunchAction.CheckOut ? original?.OfficeGeofenceVersionId : null, token);
                var officeBoundary = office.Boundary;
                if (selected.SiteId is Guid siteId)
                    boundary = action == PunchAction.CheckOut && original?.GeofenceVersionId is Guid pinned
                        ? await geofences.VersionAsync(siteId, pinned, token)
                        : await geofences.EffectiveAsync(siteId, businessDate, token);
                var evaluation = request.Latitude.HasValue && request.Longitude.HasValue && request.AccuracyMeters.HasValue
                    ? selected.BranchId.HasValue
                        ? officeBoundary is null ? new GeofenceEvaluation("not_configured", null, null)
                            : SiteGeofenceEvaluator.EvaluateCircle(officeBoundary.Id, officeBoundary.Latitude, officeBoundary.Longitude,
                                officeBoundary.RadiusMeters, officeBoundary.MaximumAccuracyMeters,
                                request.Latitude.Value, request.Longitude.Value, request.AccuracyMeters.Value)
                        : SiteGeofenceEvaluator.Evaluate(boundary, request.Latitude.Value, request.Longitude.Value, request.AccuracyMeters.Value)
                    : new GeofenceEvaluation("missing_location", null, boundary?.Id ?? officeBoundary?.Id);
                bool siteMatches;
                if (selected.BranchId is Guid officeBranch)
                    siteMatches = await offices.CurrentMembershipMatchesAsync(employeeId, officeBranch,
                        selected.Explanation?.MembershipRecordId, RosterSchedule.Today(actor.Now), token);
                else
                {
                    var receiptAssignments = await geofences.EmployeeSitesAsync(employeeId, RosterSchedule.Today(actor.Now), token);
                    siteMatches = receiptAssignments.Count == 1 && receiptAssignments[0].SiteId == selected.SiteId;
                }
                var raw = new RawPunchEvent(employeeId, action, actor.DeviceId!.Value.ToString("D"), key, fingerprint, actor.Now,
                    request.IsOffline, request.DeviceCapturedAtUtc, request.Latitude, request.Longitude, request.AccuracyMeters,
                    selected.SiteId, boundary?.Id, selected.Explanation?.MembershipRecordId, selected.ShiftVersionId,
                    request.FaceAttemptId?.ToString("D"), context.CorrelationId, businessDate: businessDate,
                    siteRosterAssignmentId: selected.BranchId.HasValue ? null : selected.Explanation?.SiteAssignmentId,
                    temporaryShiftChangeId: selected.BranchId.HasValue ? null : selected.Explanation?.TemporaryChangeId,
                    holidayCalendarId: selected.Holiday?.CalendarId, branchId: selected.BranchId,
                    officeRosterAssignmentId: selected.BranchId.HasValue ? selected.Explanation?.OfficeRosterAssignmentId : null,
                    officeGeofenceVersionId: office.Source?.OfficeGeofenceVersionId);
                var identity = await repository.IdentityAsync(actor.Identity.AccountId, actor.DeviceId.Value, token);
                FaceVerificationAttempt? verification = null;
                if (action == PunchAction.CheckIn && request.FaceAttemptId is Guid attemptId)
                    verification = await repository.AttemptAsync(employeeId, attemptId, token);
                var locked = await repository.IsLockedAsync(businessDate, token);
                var facePermission = action != PunchAction.CheckIn || AccessPermissionEvaluator.Allows(actor.Identity,
                    await access.GetGrantsAsync(actor.Identity.AccountId, clock.GetUtcNow(), token), AttendanceCapturePermissions.FaceSelf,
                    new AccessResource(EmployeeId: employeeId));
                // SQL/roster/private work may outlast a proof. Never backdate consumption to request start.
                var assessedAt = clock.GetUtcNow();
                var faceValid = facePermission && IsValidProof(verification, request.Proof, raw, identity, assessedAt);
                var decision = PunchCaptureRules.Decide(new(action, request.IsOffline, locked, active.Count,
                    siteMatches ? selected.Status : "site_relationship_changed",
                    selected.StartsAt <= actor.Now && actor.Now < selected.EndsAt, evaluation.Status, faceValid));
                // A claimed clock is evidence, never attendance time. Contradictory chronology
                // needs human review; retain the event without consuming proof or changing duty.
                var chronologyIssue = CaptureChronologyIssue(raw, identity.Device);
                if (chronologyIssue is not null && decision.Outcome is PunchAssessmentOutcome.Accepted or PunchAssessmentOutcome.Unverified)
                    decision = new(request.IsOffline ? PunchAssessmentOutcome.Unverified : PunchAssessmentOutcome.QueuedForReview,
                        chronologyIssue, AttendanceExceptionKind.SequenceError);
                // Accepted closures retain the opening shift/context and must not manufacture a session from unknown legacy evidence.
                if (decision.Outcome == PunchAssessmentOutcome.Accepted && action == PunchAction.CheckOut &&
                    (original is null || original.ShiftVersionId != active[0].ShiftVersionId || actor.Now <= active[0].StartsAtUtc ||
                     original.SiteId != active[0].SiteId || original.BranchId != active[0].BranchId ||
                     original.BusinessDate != active[0].AttendanceDate ||
                     !original.EmployeeAssignmentId.HasValue || original.EmployeeAssignmentId != selected.Explanation?.MembershipRecordId ||
                     (selected.BranchId.HasValue
                         ? !MatchesOffice(original, active[0], office.Source)
                         : !original.SiteRosterAssignmentId.HasValue || original.SiteRosterAssignmentId != selected.Explanation?.SiteAssignmentId) ||
                     original.TemporaryShiftChangeId != selected.Explanation?.TemporaryChangeId ||
                     original.HolidayCalendarId != selected.Holiday?.CalendarId || original.GeofenceVersionId != boundary?.Id))
                    decision = new(PunchAssessmentOutcome.QueuedForReview, "SESSION_EVIDENCE_CONFLICT", AttendanceExceptionKind.SequenceError);
                if (selected.BranchId.HasValue && office.Source is null && decision.Outcome == PunchAssessmentOutcome.Accepted)
                    decision = new(PunchAssessmentOutcome.QueuedForReview, "OFFICE_SOURCE_UNAVAILABLE", AttendanceExceptionKind.UnreliableLocation);
                var validation = await captureValidation.EvaluateAsync(raw, identity.Device,
                    active.Count == 1 ? active[0] : null, decision, token);
                decision = validation.Decision;
                if (!await journal.IsCurrentSessionAsync(actor.Identity, token))
                    throw new AuthenticationFailure("invalid_authentication", 401);
                var finalSession = await authentication.GetSessionAsync(actor.Identity.SessionId, token);
                var finalGrants = await access.GetGrantsAsync(actor.Identity.AccountId, clock.GetUtcNow(), token);
                // Policy/history I/O may also outlast the proof. Final evidence must reflect this last gate.
                assessedAt = clock.GetUtcNow();
                if (finalSession is null || finalSession.Id != actor.Identity.SessionId ||
                    finalSession.UserAccountId != actor.Identity.AccountId || finalSession.Client != SessionClient.Mobile ||
                    finalSession.SecurityVersion != actor.Identity.SecurityVersion || finalSession.RegisteredDeviceId != actor.DeviceId ||
                    !finalSession.IsActiveAt(assessedAt) || identity.Device.RevokedAt.HasValue || identity.Device.VerifiedAt is null ||
                    identity.Device.VerifiedAt > assessedAt)
                    throw new AuthenticationFailure("invalid_authentication", 401);
                var effectiveGrants = finalGrants.Where(x => x.IsEffectiveAt(assessedAt)).ToArray();
                if (!AccessPermissionEvaluator.Allows(actor.Identity, effectiveGrants, AttendanceCapturePermissions.Capture,
                    new AccessResource(EmployeeId: employeeId))) throw AttendanceOperation.Denied();
                facePermission = AccessPermissionEvaluator.Allows(actor.Identity, effectiveGrants,
                    AttendanceCapturePermissions.FaceSelf, new AccessResource(EmployeeId: employeeId));
                if (decision.Outcome == PunchAssessmentOutcome.Accepted && action == PunchAction.CheckIn &&
                    (!facePermission || !IsValidProof(verification, request.Proof, raw, identity, assessedAt)))
                    decision = new(PunchAssessmentOutcome.Rejected, "FACE_VERIFICATION_REQUIRED", AttendanceExceptionKind.FaceVerification);
                repository.Add(raw);
                var validationId = captureValidation.RecordFinal(raw, validation, decision, assessedAt);
                if (decision.Outcome == PunchAssessmentOutcome.Accepted && action == PunchAction.CheckIn)
                    verification!.Consume(raw, verification.Enrollment, identity.Account, identity.Device, assessedAt);
                // Invalid/wrong-device attempts are not attached to another event's assessment.
                var boundVerification = verification is not null && verification.RegisteredDeviceId == raw.RegisteredDeviceId &&
                    verification.PunchIdempotencyKey == key ? verification : null;
                var assessment = new PunchAssessment(raw, 1, decision.Outcome, decision.ReasonCode, null,
                    "capture-factual-v3", assessedAt, boundVerification,
                    evaluation.DistanceMeters.HasValue ? decimal.Round((decimal)evaluation.DistanceMeters.Value, 2) : null,
                    evaluation.Status is "inside" or "outside" ? true : evaluation.Status == "poor_accuracy" ? false : null,
                    evaluation.Status == "inside" ? true : evaluation.Status == "outside" ? false : null, identity.Device.IntegrityAssessmentCode);
                repository.Add(assessment);
                WorkSession? session = null;
                if (decision.Outcome == PunchAssessmentOutcome.Accepted)
                {
                    session = action == PunchAction.CheckIn
                        ? new WorkSession(Guid.NewGuid(), 1, employeeId, businessDate, actor.Now, null,
                            selected.ShiftVersionId!.Value, null, assessedAt, raw, siteId: selected.SiteId,
                            branchId: office.Source?.BranchId, officeRosterAssignmentId: office.Source?.OfficeRosterAssignmentId,
                            employeeAssignmentId: office.Source?.EmployeeAssignmentId,
                            officeGeofenceVersionId: office.Source?.OfficeGeofenceVersionId,
                            holidayCalendarId: office.Source?.HolidayCalendarId)
                        : new WorkSession(active[0].SessionKey, checked(active[0].Revision + 1), employeeId, active[0].AttendanceDate,
                            active[0].StartsAtUtc, actor.Now, active[0].ShiftVersionId, active[0].PolicyVersionId, assessedAt,
                            original!, raw, siteId: active[0].SiteId, branchId: active[0].BranchId,
                            officeRosterAssignmentId: active[0].OfficeRosterAssignmentId,
                            employeeAssignmentId: active[0].EmployeeAssignmentId,
                            officeGeofenceVersionId: active[0].OfficeGeofenceVersionId,
                            holidayCalendarId: active[0].HolidayCalendarId);
                    repository.Add(session);
                }
                if (decision.ExceptionKind is AttendanceExceptionKind kind)
                {
                    var exception = new AttendanceException(employeeId, businessDate, kind, decision.ReasonCode, assessedAt, rawPunchId: raw.Id);
                    repository.Add(exception);
                    await notifications.StageAsync(new(exception.Id, actor.Identity.AccountId, NotificationTemplates.AttendanceException, raw.Id), context, token);
                }
                if (action == PunchAction.CheckIn && active.Count == 1 && selected.EndsAt < actor.Now)
                {
                    var exception = new AttendanceException(employeeId, active[0].AttendanceDate, AttendanceExceptionKind.MissingCheckOut,
                        "A new Check In was attempted after the active duty's scheduled end without a confirmed Check Out.", actor.Now,
                        rawPunchId: raw.Id, workSessionId: active[0].Id);
                    repository.Add(exception);
                    await notifications.StageAsync(new(exception.Id, actor.Identity.AccountId, NotificationTemplates.MissingPunch, raw.Id), context, token);
                }
                // Retain device integrity code on the assessment. Its provider-specific interpretation
                // is not an approved rule here; a nonempty code alone does not mean a risk or pay deduction.
                var validationResponse = new PunchValidationResponse(validationId, validation.PolicyVersionId,
                    validation.PolicyRevision, validation.SourceFingerprint,
                    validation.Codes.Append(decision.ReasonCode).Distinct(StringComparer.Ordinal).ToArray(), validation.RelatedRawPunchIds);
                return new AttendanceChange<PunchResponse>(raw.Id, null, Map(raw, assessment, session?.SessionKey, validationResponse));
            }, ct, self: true, requireDevice: true);
    }

    public Task<AttendanceStateResponse> StateAsync(AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(AttendanceCapturePermissions.SelfRead, "attendance.state.self", null, context,
            async (actor, token) =>
            {
                var active = await repository.ActiveSessionsAsync(actor.Identity.EmployeeId, token);
                var enrolled = await repository.ActiveEnrollmentAsync(actor.Identity.EmployeeId, token);
                var readiness = FaceReadiness.Evaluate(facePolicy, face.Capability, templates.IsConfigured, enrolled);
                var ready = readiness.CheckInFaceReady;
                var roster = await SelectRosterAsync(actor.Identity.EmployeeId, active, actor.Now, token);
                var locked = await repository.IsLockedAsync(roster.BusinessDate, token);
                var office = await SafeOfficeAsync(actor.Identity.EmployeeId, roster.BusinessDate, roster,
                    active.Count == 1 ? active[0].OfficeGeofenceVersionId : null, token);
                var sourceReady = !roster.BranchId.HasValue || office.Source is not null &&
                    await offices.CurrentMembershipMatchesAsync(actor.Identity.EmployeeId, roster.BranchId.Value,
                        roster.Explanation?.MembershipRecordId, RosterSchedule.Today(actor.Now), token);
                var validationReady = await CapturePolicyReadyAsync(actor.Now, token);
                var mobile = actor.Identity.Client == SessionClient.Mobile;
                var grants = await access.GetGrantsAsync(actor.Identity.AccountId, clock.GetUtcNow(), token);
                var resource = new AccessResource(EmployeeId: actor.Identity.EmployeeId);
                var canCapture = AccessPermissionEvaluator.Allows(actor.Identity, grants, AttendanceCapturePermissions.Capture, resource);
                var canVerify = AccessPermissionEvaluator.Allows(actor.Identity, grants, AttendanceCapturePermissions.FaceSelf, resource);
                var faceStatus = readiness.ReasonCode;
                var insideSchedule = roster.Status == "scheduled" && roster.StartsAt <= actor.Now && actor.Now < roster.EndsAt;
                var next = !mobile || !canCapture ? "mobile_attendance_access_required" : active.Count > 1 || locked ? "request_correction" :
                    !validationReady || !sourceReady ? "contact_attendance_support" :
                    active.Count == 1 ? "check_out" : !insideSchedule ? "view_shift" :
                    ready && canVerify ? "start_face_challenge" : "contact_attendance_support";
                return new AttendanceStateResponse(actor.Identity.EmployeeId, actor.Now, RosterSchedule.TimeZoneId,
                    active.Count > 1 ? "conflict" : active.Count == 1 ? "checked_in" : "checked_out", next,
                    mobile && canCapture && canVerify && active.Count == 0 && ready && !locked && insideSchedule && sourceReady && validationReady,
                    mobile && canCapture && active.Count == 1 && !locked && sourceReady && validationReady,
                    faceStatus, active.Count == 1 ? Map(active[0]) : null,
                    ["Location and current site or office are validated again on submission.",
                        validationReady ? "Configured capture validation is available." : "Approved capture validation policy is unavailable; contact attendance support.",
                        "Pay, continuous-duty reset and review authority remain separately configured."]);
            }, ct, self: true);

    public Task<PagedResult<PunchResponse>> HistoryAsync(AttendanceHistoryRequest request, AuthRequestContext context, CancellationToken ct)
    {
        ValidateHistory(request);
        return operation.ReadAsync(AttendanceCapturePermissions.SelfRead, "attendance.punches.self", null, context,
            async (actor, token) =>
            {
                var result = await repository.PunchesAsync(actor.Identity.EmployeeId, request, token);
                var rows = new List<PunchResponse>();
                foreach (var raw in result.Items)
                {
                    var final = await validationEvidence.FinalAsync(actor.Identity.EmployeeId, raw.Id, token);
                    if (final is not null && (final.EmployeeId != raw.EmployeeId || final.RawPunchEventId != raw.Id))
                        throw AttendanceOperation.Conflict("capture_validation_history_scope_conflict");
                    var evidence = final is null ? null : new PunchValidationResponse(final.Id,
                        final.FinalAssessment.PolicyVersionId, final.FinalAssessment.PolicyRevision,
                        final.FinalAssessment.SourceFingerprint, final.FinalAssessment.Codes, final.FinalAssessment.RelatedRawPunchIds);
                    rows.Add(Map(raw, await repository.LatestAssessmentAsync(raw.Id, token), null, evidence));
                }
                return new PagedResult<PunchResponse>(rows, result.TotalCount, result.Page, result.PageSize);
            }, ct, self: true);
    }
    public Task<PagedResult<WorkSessionResponse>> SessionsAsync(AttendanceHistoryRequest request, AuthRequestContext context, CancellationToken ct)
    {
        ValidateHistory(request);
        return operation.ReadAsync(AttendanceCapturePermissions.SelfRead, "attendance.sessions.self", null, context,
            async (actor, token) =>
            {
                var result = await repository.SessionsAsync(actor.Identity.EmployeeId, request, token);
                return new PagedResult<WorkSessionResponse>(result.Items.Select(Map).ToArray(), result.TotalCount, result.Page, result.PageSize);
            }, ct, self: true);
    }

    private async Task<EffectiveRosterResponse> SelectRosterAsync(Guid employeeId, IReadOnlyList<WorkSession> active,
        DateTimeOffset now, CancellationToken ct)
    {
        var today = RosterSchedule.Today(now);
        if (active.Count == 1)
        {
            var roster = await SafeRosterAsync(employeeId, active[0].AttendanceDate, ct);
            // Accepted session identity is authoritative even if later configuration is unavailable.
            return roster.ShiftVersionId == active[0].ShiftVersionId && roster.SiteId == active[0].SiteId &&
                roster.BranchId == active[0].BranchId
                ? roster : roster with { Status = "session_roster_changed" };
        }
        var current = await SafeRosterAsync(employeeId, today, ct);
        if (today <= DateOnly.MinValue.AddDays(1)) return current;
        var previous = await SafeRosterAsync(employeeId, today.AddDays(-1), ct);
        var previousCovers = previous.Status == "scheduled" && previous.StartsAt <= now && now < previous.EndsAt;
        var currentCovers = current.Status == "scheduled" && current.StartsAt <= now && now < current.EndsAt;
        if (previousCovers && currentCovers) return current with { Status = "overlapping_schedule" };
        return previousCovers ? previous : current;
    }
    private async Task<EffectiveRosterResponse> SafeRosterAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        try { return await rosters.ResolveAsync(employeeId, date, ct); }
        catch (AuthenticationFailure error) when (error.StatusCode is 404 or 409)
        { return new(employeeId, date, "roster_unavailable", RosterSchedule.TimeZoneId); }
    }
    private async Task<OfficeAttendanceContext> SafeOfficeAsync(Guid employeeId, DateOnly date,
        EffectiveRosterResponse roster, Guid? pinnedBoundary, CancellationToken ct)
    {
        try { return await offices.ReadAsync(employeeId, date, roster, pinnedBoundary, ct); }
        catch (AuthenticationFailure error) when (error.StatusCode is 404 or 409)
        { return new("office_source_unavailable", null, null); }
    }
    private async Task<bool> CapturePolicyReadyAsync(DateTimeOffset atUtc, CancellationToken ct)
    {
        try
        {
            var policy = await capturePolicies.EffectiveAsync(atUtc, ct);
            if (policy is null) return false;
            CaptureValidationPolicyRules.ValidateEntry(policy);
            return true;
        }
        catch (AuthenticationFailure error) when (error.StatusCode is 400 or 404 or 409 or 503)
        { return false; }
    }
    private static bool MatchesOffice(RawPunchEvent opening, WorkSession session, OfficeAttendanceSource? source) =>
        source is not null && opening.BranchId == source.BranchId && session.BranchId == source.BranchId &&
        opening.OfficeRosterAssignmentId == source.OfficeRosterAssignmentId && session.OfficeRosterAssignmentId == source.OfficeRosterAssignmentId &&
        opening.EmployeeAssignmentId == source.EmployeeAssignmentId && session.EmployeeAssignmentId == source.EmployeeAssignmentId &&
        opening.OfficeGeofenceVersionId == source.OfficeGeofenceVersionId && session.OfficeGeofenceVersionId == source.OfficeGeofenceVersionId &&
        opening.HolidayCalendarId == source.HolidayCalendarId && session.HolidayCalendarId == source.HolidayCalendarId;
    private bool IsValidProof(FaceVerificationAttempt? attempt, string? proof, RawPunchEvent raw,
        CaptureIdentity identity, DateTimeOffset now) => facePolicy.IsConfirmed && face.Capability.Available && templates.IsConfigured &&
        attempt is not null && proof is not null && attempt.Provider == face.Capability.Provider && attempt.ProviderVersion == face.Capability.Version &&
        attempt.Outcome == FaceVerificationOutcome.Passed && attempt.LivenessPassed == true && attempt.MatchPassed == true &&
        !attempt.ConsumedAtUtc.HasValue && attempt.CompletedAtUtc <= now && now < attempt.ExpiresAtUtc &&
        attempt.UserAccountId == identity.Account.Id && attempt.EmployeeId == raw.EmployeeId && attempt.RegisteredDeviceKey == identity.Device.Id &&
        attempt.Enrollment is not null && attempt.FaceEnrollmentId == attempt.Enrollment.Id &&
        attempt.Enrollment.IsActive && attempt.Enrollment.EmployeeId == raw.EmployeeId &&
        attempt.Enrollment.Provider == attempt.Provider && attempt.Enrollment.ProviderVersion == attempt.ProviderVersion &&
        attempt.PunchIdempotencyKey == raw.IdempotencyKey &&
        now >= attempt.IssuedAtUtc && FaceService.SecureDigestEquals(FaceService.Digest(proof), attempt.ChallengeSha256);
    private static string? CaptureChronologyIssue(RawPunchEvent punch, RegisteredDevice device)
    {
        if (punch.DeviceCapturedAtUtc is not DateTimeOffset captured) return null;
        if (captured > punch.ReceivedAtUtc) return "CAPTURE_AFTER_RECEIPT";
        if (captured < device.RegisteredAt) return "CAPTURE_PRECEDES_DEVICE_REGISTRATION";
        if (device.VerifiedAt is DateTimeOffset verified && captured < verified)
            return "CAPTURE_PRECEDES_DEVICE_VERIFICATION";
        return null;
    }
    private static void Validate(object value)
    { if (!Validator.TryValidateObject(value, new ValidationContext(value), [], true)) throw AttendanceOperation.Invalid(); }
    private static void ValidateHistory(AttendanceHistoryRequest request)
    {
        Validate(request);
        if (request.Page is < 1 or > 1000000 || request.PageSize is < 1 or > 100 ||
            request.From == default || request.To <= request.From || request.To.DayNumber - request.From.DayNumber > 366)
            throw AttendanceOperation.Invalid("attendance_history_range_invalid");
    }
    private static PunchResponse Map(RawPunchEvent raw, PunchAssessment? assessment, Guid? sessionKey, PunchValidationResponse? validation = null) =>
        new(raw.Id, raw.Action.ToString(), raw.ReceivedAtUtc, raw.BusinessDate ?? RosterSchedule.Today(raw.ReceivedAtUtc),
            assessment?.Outcome.ToString() ?? "Unverified", assessment?.ReasonCode ?? "ASSESSMENT_NOT_AVAILABLE",
            raw.SiteId, raw.ShiftVersionId, sessionKey, raw.IsOffline, assessment?.Id,
            BusinessDateSource: raw.BusinessDate.HasValue ? "server_roster" : "legacy_received_date",
            BranchId: raw.BranchId, OfficeRosterAssignmentId: raw.OfficeRosterAssignmentId,
            OfficeGeofenceVersionId: raw.OfficeGeofenceVersionId, Validation: validation);
    public static WorkSessionResponse Map(WorkSession x) => new(x.Id, x.SessionKey, x.Revision, x.AttendanceDate,
        x.StartsAtUtc, x.EndsAtUtc, x.ShiftVersionId, x.SiteId, x.CheckInPunchId, x.CheckOutPunchId,
        x.EndsAtUtc.HasValue ? (decimal)(x.EndsAtUtc.Value - x.StartsAtUtc).Ticks / TimeSpan.TicksPerMinute : null,
        x.BranchId, x.OfficeRosterAssignmentId, x.BranchId.HasValue ? x.EmployeeAssignmentId : null,
        x.OfficeGeofenceVersionId, x.BranchId.HasValue ? x.HolidayCalendarId : null);
}
