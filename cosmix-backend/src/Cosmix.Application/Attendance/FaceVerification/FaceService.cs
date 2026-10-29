using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.FaceVerification;

namespace Cosmix.Application.Attendance.FaceVerification;

public sealed class FaceService(AttendanceOperation operation, IFaceRepository repository,
    IFaceVerificationProvider provider, FaceVerificationPolicy policy, IFaceTemplateStore files, TimeProvider clock,
    FaceEnrollmentJournal enrollmentJournal, FaceRetirementService retirement)
{
    public Task<FaceCapabilityResponse> CapabilityAsync(AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(AttendanceCapturePermissions.FaceSelf, "attendance.face.capability", null, context,
            async (actor, token) =>
            {
                var ready = FaceReadiness.Evaluate(policy, provider.Capability, files.IsConfigured,
                    await repository.ActiveAsync(actor.Identity.EmployeeId, token));
                return new FaceCapabilityResponse(ready.SystemAvailable, ready.ReasonCode, ready.EnrollmentActive,
                    EnrollmentCompatible: ready.EnrollmentCompatible, CheckInFaceReady: ready.CheckInFaceReady);
            }, ct, self: true);

    public Task<PagedResult<FaceEnrollmentDeviceResponse>> DevicesAsync(Guid employeeId, int page, int size,
        AuthRequestContext context, CancellationToken ct)
    {
        Page(page, size);
        return operation.ReadAsync(AttendanceCapturePermissions.FaceManage, "attendance.face.enrollment.devices", employeeId, context,
            (_, token) => repository.DevicesAsync(employeeId, page, size, token), ct,
            resources: (a, t) => repository.ResourcesAsync(employeeId, RosterSchedule.Today(a.Now), t));
    }

    public Task<CurrentFaceEnrollmentResponse> CurrentAsync(Guid employeeId, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(AttendanceCapturePermissions.FaceRead, "attendance.face.enrollment.current", employeeId, context,
            async (_, token) =>
            {
                var enrollment = await repository.ActiveAsync(employeeId, token);
                return new CurrentFaceEnrollmentResponse(enrollment is null ? null : Map(enrollment));
            }, ct, resources: (a, t) => repository.ResourcesAsync(employeeId, RosterSchedule.Today(a.Now), t));

    public Task<PagedResult<FaceEnrollmentResponse>> HistoryAsync(Guid employeeId, int page, int size,
        AuthRequestContext context, CancellationToken ct)
    {
        Page(page, size);
        return operation.ReadAsync(AttendanceCapturePermissions.FaceRead, "attendance.face.enrollment.history", employeeId, context,
            (_, token) => repository.EnrollmentsAsync(employeeId, page, size, token), ct,
            resources: (a, t) => repository.ResourcesAsync(employeeId, RosterSchedule.Today(a.Now), t));
    }
    public Task<PagedResult<FaceAttemptResponse>> AttemptsAsync(Guid employeeId, int page, int size,
        AuthRequestContext context, CancellationToken ct)
    {
        Page(page, size);
        return operation.ReadAsync(AttendanceCapturePermissions.FaceRead, "attendance.face.attempt.history", employeeId, context,
            (_, token) => repository.AttemptsAsync(employeeId, page, size, token), ct,
            resources: (a, t) => repository.ResourcesAsync(employeeId, RosterSchedule.Today(a.Now), t));
    }

    public async Task<FaceEnrollmentResponse> EnrollAsync(Guid employeeId, FaceEnrollmentRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        Validate(request);
        RequireText(request.ProviderEvidenceToken, 4096);
        if (request.RegisteredDeviceId == Guid.Empty || request.ExpectedActiveEnrollmentId == Guid.Empty ||
            request.ExpectedActiveEnrollmentId.HasValue != (request.ExpectedActiveVersion is not null)) throw AttendanceOperation.Invalid();
        if (request.ExpectedActiveVersion is not null) SchedulingOperation.Version(request.ExpectedActiveVersion);
        var payload = new { request.RegisteredDeviceId, request.ExpectedActiveEnrollmentId, request.ExpectedActiveVersion,
            EvidenceDigest = Digest(request.ProviderEvidenceToken) };
        var replay = await operation.TryReplayAsync<FaceEnrollmentResponse>(AttendanceCapturePermissions.FaceManage,
            "attendance.face.enrollment", employeeId, key, payload, request.Reason, context, ct,
            resources: (a, t) => repository.ResourcesAsync(employeeId, RosterSchedule.Today(a.Now), t));
        if (replay.Found) return replay.Result!;
        var fingerprint = Digest(JsonSerializer.Serialize(new { employeeId, payload, Reason = request.Reason.Trim() }));
        var execution = await operation.ReadAsync(AttendanceCapturePermissions.FaceManage, "attendance.face.enrollment.preflight", employeeId, context,
            async (actor, token) =>
            {
                var resources = await repository.ResourcesAsync(employeeId, RosterSchedule.Today(actor.Now), token);
                var attempt = await enrollmentJournal.BeginAsync(actor, employeeId, request.RegisteredDeviceId, key, fingerprint,
                    provider.Capability, resources, context, token);
                if (!attempt.Created) return attempt;
                try
                {
                    RequireReady();
                    var active = await repository.ActiveAsync(employeeId, token);
                    if (active?.Id != request.ExpectedActiveEnrollmentId) throw AttendanceOperation.Conflict("face_enrollment_version_conflict");
                    if (active is not null) SchedulingOperation.ExpectVersion(Convert.ToBase64String(active.RowVersion), request.ExpectedActiveVersion);
                    await repository.EmployeeDeviceAsync(employeeId, request.RegisteredDeviceId, token);
                    return attempt;
                }
                catch (AuthenticationFailure error)
                {
                    var terminal = await enrollmentJournal.CompleteFailureInTransactionAsync(attempt.Handle!, PreflightFailure(error), token);
                    return attempt with { Terminal = terminal };
                }
            }, ct, resources: (a, t) => repository.ResourcesAsync(employeeId, RosterSchedule.Today(a.Now), t));
        if (execution.Terminal is not null) return await EnrollmentResult(employeeId, execution.Start, execution.Terminal, context, ct);
        if (!execution.Created || execution.Handle is null) throw AttendanceOperation.Conflict("face_enrollment_reconciliation_pending");

        FaceProviderEnrollmentResult? enrolled = null;
        var providerStage = true;
        try
        {
            // Exactly the new-start owner calls the provider, outside SQL locks. Its immutable receipt ID is the provider operation key.
            enrolled = await provider.EnrollAsync(new(employeeId, request.RegisteredDeviceId, request.ProviderEvidenceToken,
                context.CorrelationId, $"enrollment:{execution.Start.ReceiptId:N}", execution.Start.ActorAccountId,
                Guid.Parse(execution.Start.ActorSessionReference)), ct);
            var invalid = enrolled is null || enrolled.EmployeeId != employeeId || enrolled.DeviceId != request.RegisteredDeviceId ||
                enrolled.Provider != execution.Start.Provider || enrolled.Version != execution.Start.ProviderVersion ||
                enrolled.Success && enrolled.Template is not { Length: > 0 and <= 1048576 };
            if (invalid || !enrolled!.Success)
            {
                var failure = await enrollmentJournal.CompleteFailureAsync(execution.Handle,
                    invalid ? EnrollmentFailure.ProviderInvalidResult : EnrollmentFailure.ProviderRejected, CancellationToken.None);
                return await EnrollmentResult(employeeId, execution.Start, failure, context, ct);
            }
            providerStage = false;
            var outcome = await operation.ReadAsync(AttendanceCapturePermissions.FaceManage, "attendance.face.enrollment.finalize", employeeId,
                context, async (actor, token) =>
                {
                    var existing = await enrollmentJournal.OutcomeAsync(execution.Handle, token);
                    if (existing is not null) return existing;
                    RequireReady();
                    if (provider.Capability.Provider != execution.Start.Provider || provider.Capability.Version != execution.Start.ProviderVersion)
                        throw AttendanceOperation.Conflict("face_enrollment_provider_mismatch");
                    await repository.EmployeeDeviceAsync(employeeId, request.RegisteredDeviceId, token);
                    var prior = await repository.ActiveAsync(employeeId, token);
                    if (prior?.Id != request.ExpectedActiveEnrollmentId) throw AttendanceOperation.Conflict("face_enrollment_version_conflict");
                    var before = prior is null ? null : Map(prior);
                    if (prior is not null)
                    {
                        SchedulingOperation.ExpectVersion(Convert.ToBase64String(prior.RowVersion), request.ExpectedActiveVersion);
                        prior.Revoke(actor.Identity.AccountId.ToString(), request.Reason, actor.Now);
                        // Retire the filtered unique active row before inserting its replacement, within the same outer transaction.
                        await repository.SaveChangesAsync(token);
                    }
                    var reference = await files.WriteAsync(enrolled.Template!, TemplatePurpose(employeeId), token);
                    var value = new FaceEnrollment(employeeId, AttendanceOperation.Reference(reference), reference.KeyReference,
                        enrolled.Provider, enrolled.Version, actor.Identity.AccountId.ToString(), request.RegisteredDeviceId.ToString(),
                        request.Reason, actor.Now, context.CorrelationId, prior?.Id);
                    repository.Add(value);
                    await repository.SaveChangesAsync(token);
                    if (prior is not null)
                        await retirement.StageEnrollmentAsync(actor, prior, value.Id, context, token);
                    return await enrollmentJournal.CompleteSuccessAsync(execution.Handle, Map(value), before, token);
                }, ct, resources: (a, t) => EnrollmentResources(employeeId, execution.Start, a, t));
            return Unwrap(outcome);
        }
        catch (Exception error)
        {
            var failure = error is OperationCanceledException && ct.IsCancellationRequested ? EnrollmentFailure.CallerCancelled :
                providerStage ? error is AuthenticationFailure { StatusCode: 409 } ? EnrollmentFailure.ProviderRejected : EnrollmentFailure.ProviderUnavailable :
                error is AuthenticationFailure { StatusCode: 401 or 403 } ? EnrollmentFailure.FinalizationDenied :
                error is AuthenticationFailure { Code: "private_storage_unavailable" or "face_template_storage_unavailable" or "face_retention_unavailable" } ? EnrollmentFailure.TemplateStorageUnavailable :
                error is AuthenticationFailure { StatusCode: 409 } ? EnrollmentFailure.FinalizationConflict : EnrollmentFailure.InternalFailure;
            // A revoked/aborted caller cannot authorize enrollment, but the process-owned start proof can record this factual failure.
            // If SQL/protected audit storage is itself unavailable, this fails closed; it cannot promise impossible audit durability.
            var terminal = await enrollmentJournal.CompleteFailureAsync(execution.Handle, failure, CancellationToken.None);
            ct.ThrowIfCancellationRequested();
            return await EnrollmentResult(employeeId, execution.Start, terminal, context, ct);
        }
        finally { if (enrolled?.Template is not null) CryptographicOperations.ZeroMemory(enrolled.Template); }
    }

    private async Task<FaceEnrollmentResponse> EnrollmentResult(Guid employeeId, EnrollmentStart start, EnrollmentTerminal terminal,
        AuthRequestContext context, CancellationToken ct)
    {
        var authorized = await operation.ReadAsync(AttendanceCapturePermissions.FaceManage, "attendance.face.enrollment.outcome", employeeId, context,
            (actor, _) => actor.Identity.AccountId == start.ActorAccountId && employeeId == start.EmployeeId
                ? Task.FromResult(terminal) : throw AttendanceOperation.Denied(), ct,
            resources: (actor, token) => EnrollmentResources(employeeId, start, actor, token));
        return Unwrap(authorized);
    }
    private async Task<IReadOnlyList<AccessResource>> EnrollmentResources(Guid employeeId, EnrollmentStart start, AttendanceActor actor, CancellationToken ct) =>
        (await repository.ResourcesAsync(employeeId, RosterSchedule.Today(actor.Now), ct)).Concat(start.Resources).Distinct().ToArray();
    private static FaceEnrollmentResponse Unwrap(EnrollmentTerminal terminal) => terminal.Status == 200 && terminal.Enrollment is not null
        ? terminal.Enrollment : throw new AuthenticationFailure(terminal.Code, terminal.Status);
    private static EnrollmentFailure PreflightFailure(AuthenticationFailure error) => error.Code switch
    {
        "face_policy_not_confirmed" => EnrollmentFailure.PolicyNotConfirmed,
        "face_provider_not_configured" => EnrollmentFailure.ProviderNotConfigured,
        "face_template_storage_unavailable" => EnrollmentFailure.TemplateStorageNotConfigured,
        "face_enrollment_version_conflict" or "scheduling_version_conflict" => EnrollmentFailure.EnrollmentChanged,
        "face_employee_ineligible" => EnrollmentFailure.EmployeeIneligible,
        "face_device_ineligible" => EnrollmentFailure.DeviceIneligible,
        _ => EnrollmentFailure.InternalFailure
    };

    public Task<FaceEnrollmentResponse> RevokeAsync(Guid employeeId, Guid id, RevokeFaceEnrollmentRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        Validate(request); SchedulingOperation.Version(request.ExpectedVersion);
        return operation.WriteAsync(AttendanceCapturePermissions.FaceManage, "attendance.face.revoke", employeeId,
            key, new { id, request.ExpectedVersion }, request.Reason, context, async (actor, token) =>
            {
                if (!policy.IsConfirmed) throw new AuthenticationFailure("face_authority_not_confirmed", 503);
                var value = await repository.EnrollmentAsync(employeeId, id, token) ?? throw AttendanceOperation.Missing();
                SchedulingOperation.ExpectVersion(Convert.ToBase64String(value.RowVersion), request.ExpectedVersion);
                if (!value.IsActive) throw AttendanceOperation.Conflict("face_enrollment_already_revoked");
                var before = Map(value);
                value.Revoke(actor.Identity.AccountId.ToString(), request.Reason, actor.Now);
                await repository.SaveChangesAsync(token);
                await retirement.StageEnrollmentAsync(actor, value, null, context, token);
                return new AttendanceChange<FaceEnrollmentResponse>(id, before, Map(value));
            }, ct, resources: (a, t) => repository.ResourcesAsync(employeeId, RosterSchedule.Today(a.Now), t));
    }

    public Task<FaceChallengeResponse> ChallengeAsync(FaceChallengeRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        Validate(request);
        if (request.PunchIdempotencyKey is null || !Regex.IsMatch(request.PunchIdempotencyKey, "\\A[A-Za-z0-9_.:-]{8,100}\\z"))
            throw AttendanceOperation.Invalid("invalid_punch_idempotency_key");
        return operation.WriteAsync(AttendanceCapturePermissions.FaceSelf, "attendance.face.challenge", null, key,
            request, "Employee Check In face challenge", context, async (actor, token) =>
            {
                RequireReady();
                var employeeId = actor.Identity.EmployeeId;
                var identity = await repository.EmployeeDeviceAsync(employeeId, actor.DeviceId!.Value, token);
                var enrollment = await repository.ActiveAsync(employeeId, token) ?? throw AttendanceOperation.Conflict("face_enrollment_required");
                if (enrollment.Provider != provider.Capability.Provider || enrollment.ProviderVersion != provider.Capability.Version)
                    throw AttendanceOperation.Conflict("face_enrollment_provider_mismatch");
                if (await repository.AttemptCountAsync(employeeId, request.PunchIdempotencyKey, token) >= policy.MaximumAttemptsPerPunch!.Value)
                    throw new AuthenticationFailure("face_retry_limit_reached", 429);
                var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                var attempt = new FaceVerificationAttempt(enrollment, identity.Account, identity.Device, request.PunchIdempotencyKey,
                    Digest(secret), provider.Capability.Provider, provider.Capability.Version, actor.Now,
                    actor.Now.AddSeconds(policy.ChallengeLifetimeSeconds!.Value), context.CorrelationId);
                repository.Add(attempt);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<FaceChallengeResponse>(attempt.Id, null,
                    new(attempt.Id, secret, attempt.ExpiresAtUtc, Convert.ToBase64String(attempt.RowVersion)));
            }, ct, self: true, requireDevice: true);
    }

    public async Task<FaceVerificationResponse> VerifyAsync(Guid id, VerifyFaceRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        Validate(request); SchedulingOperation.Version(request.ExpectedVersion);
        RequireText(request.Challenge, 128); RequireText(request.ProviderEvidenceToken, 4096);
        var payload = new { id, ChallengeDigest = Digest(request.Challenge), EvidenceDigest = Digest(request.ProviderEvidenceToken), request.ExpectedVersion };
        var replay = await operation.TryReplayAsync<FaceVerificationResponse>(AttendanceCapturePermissions.FaceSelf,
            "attendance.face.verify", null, key, payload, "Employee live Check In verification", context, ct, self: true, requireDevice: true);
        if (replay.Found) return await VerificationReadinessAsync(id, request.Challenge, replay.Result!, context, ct);
        var prepared = await operation.ReadAsync(AttendanceCapturePermissions.FaceSelf, "attendance.face.verify.preflight", null, context,
            async (actor, token) =>
            {
                var attempt = await BoundAttempt(actor, id, request.Challenge, token);
                SchedulingOperation.ExpectVersion(Convert.ToBase64String(attempt.RowVersion), request.ExpectedVersion);
                return new VerificationPreparation(attempt.EmployeeId, attempt.RegisteredDeviceKey, attempt.Outcome,
                    attempt.ExpiresAtUtc, actor.Now, attempt.Enrollment.EncryptedTemplateReference, attempt.Enrollment.Provider,
                    attempt.Enrollment.ProviderVersion, actor.Identity.AccountId, actor.Identity.SessionId);
            }, ct, self: true, requireDevice: true);
        FaceProviderVerificationResult? result = null;
        if (prepared.Outcome == FaceVerificationOutcome.Pending && prepared.Now < prepared.ExpiresAt && IsReady &&
            prepared.Provider == provider.Capability.Provider && prepared.ProviderVersion == provider.Capability.Version)
        {
            byte[]? template = null;
            try
            {
                var reference = JsonSerializer.Deserialize<PrivateFileReference>(prepared.TemplateReference)
                    ?? throw new AuthenticationFailure("face_template_unavailable", 503);
                template = await files.ReadAsync(reference, TemplatePurpose(prepared.EmployeeId), ct);
                result = await provider.VerifyAsync(new(id, prepared.EmployeeId, prepared.DeviceId, request.Challenge,
                    request.ProviderEvidenceToken, template, context.CorrelationId, prepared.AccountId, prepared.SessionId), ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { result = null; }
            catch (AuthenticationFailure error) when (error.StatusCode == 503) { result = null; }
            catch (HttpRequestException) { result = null; }
            catch (JsonException) { result = null; }
            finally { if (template is not null) CryptographicOperations.ZeroMemory(template); }
        }
        var response = await operation.WriteAsync(AttendanceCapturePermissions.FaceSelf, "attendance.face.verify", null, key, payload,
            "Employee live Check In verification", context, async (actor, token) =>
            {
                var attempt = await BoundAttempt(actor, id, request.Challenge, token);
                SchedulingOperation.ExpectVersion(Convert.ToBase64String(attempt.RowVersion), request.ExpectedVersion);
                if (attempt.Outcome != FaceVerificationOutcome.Pending) throw AttendanceOperation.Conflict("face_challenge_already_completed");
                var completedAt = clock.GetUtcNow();
                if (completedAt >= attempt.ExpiresAtUtc) attempt.Expire(completedAt);
                else
                {
                    var bound = IsReady && result is not null && result.Provider == attempt.Provider && result.Version == attempt.ProviderVersion &&
                        result.AttemptId == id && result.EmployeeId == actor.Identity.EmployeeId && result.DeviceId == actor.DeviceId &&
                        SecureDigestEquals(result.ChallengeSha256, attempt.ChallengeSha256) && prepared.Provider == attempt.Provider &&
                        prepared.ProviderVersion == attempt.ProviderVersion;
                    attempt.RecordProviderResult(bound && result!.Available, bound && result!.LivenessPassed,
                        bound && result!.MatchPassed, !bound || !result!.Available ? "PROVIDER_UNAVAILABLE" :
                        result.LivenessPassed && result.MatchPassed ? "VERIFIED" : "VERIFICATION_FAILED", completedAt);
                }
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<FaceVerificationResponse>(id, null, new(id, attempt.Outcome.ToString(),
                    attempt.ResultReasonCode!, attempt.ExpiresAtUtc, attempt.Outcome == FaceVerificationOutcome.Passed,
                    Convert.ToBase64String(attempt.RowVersion)));
            }, ct, self: true, requireDevice: true);
        return await VerificationReadinessAsync(id, request.Challenge, response, context, ct);
    }

    private async Task<FaceVerificationResponse> VerificationReadinessAsync(Guid id, string challenge,
        FaceVerificationResponse response, AuthRequestContext context, CancellationToken ct)
    {
        var current = await operation.ReadAsync(AttendanceCapturePermissions.FaceSelf, "attendance.face.verify.readiness", null, context,
            async (actor, token) =>
            {
                var attempt = await repository.AttemptAsync(actor.Identity.EmployeeId, id, token);
                if (attempt is null) return response with { CanSubmitCheckIn = false };
                if (attempt.UserAccountId != actor.Identity.AccountId || attempt.RegisteredDeviceKey != actor.DeviceId ||
                    !SecureDigestEquals(Digest(challenge), attempt.ChallengeSha256)) throw AttendanceOperation.Denied();
                var active = await repository.ActiveAsync(actor.Identity.EmployeeId, token);
                var now = clock.GetUtcNow();
                // Receipts preserve the historical result. Submission eligibility belongs to the
                // current bound proof and enrollment, never to the saved CanSubmitCheckIn flag.
                var ready = response.Outcome == FaceVerificationOutcome.Passed.ToString() &&
                    attempt.Outcome == FaceVerificationOutcome.Passed && attempt.LivenessPassed == true && attempt.MatchPassed == true &&
                    attempt.CompletedAtUtc.HasValue && attempt.CompletedAtUtc <= now && now >= attempt.IssuedAtUtc && now < attempt.ExpiresAtUtc &&
                    !attempt.ConsumedAtUtc.HasValue && !attempt.ConsumedByPunchId.HasValue &&
                    active is not null && active.Id == attempt.FaceEnrollmentId && attempt.Enrollment.IsActive &&
                    active.Provider == attempt.Provider && active.ProviderVersion == attempt.ProviderVersion &&
                    FaceReadiness.Evaluate(policy, provider.Capability, files.IsConfigured, active).CheckInFaceReady;
                return response with { CanSubmitCheckIn = ready };
            }, ct, self: true, requireDevice: true);
        // The final session/grant audit may itself outlast the proof validity window.
        return current.CanSubmitCheckIn && clock.GetUtcNow() >= current.ExpiresAtUtc
            ? current with { CanSubmitCheckIn = false } : current;
    }

    private async Task<FaceVerificationAttempt> BoundAttempt(AttendanceActor actor, Guid id, string challenge, CancellationToken ct)
    {
        var attempt = await repository.AttemptAsync(actor.Identity.EmployeeId, id, ct) ?? throw AttendanceOperation.Missing();
        if (attempt.UserAccountId != actor.Identity.AccountId || attempt.RegisteredDeviceKey != actor.DeviceId ||
            !SecureDigestEquals(Digest(challenge), attempt.ChallengeSha256)) throw AttendanceOperation.Denied();
        if (!attempt.Enrollment.IsActive) throw AttendanceOperation.Conflict("face_enrollment_revoked");
        return attempt;
    }
    private bool IsReady => FaceReadiness.Evaluate(policy, provider.Capability, files.IsConfigured, null).SystemAvailable;
    private void RequireReady()
    {
        var ready = FaceReadiness.Evaluate(policy, provider.Capability, files.IsConfigured, null);
        if (!ready.SystemAvailable) throw new AuthenticationFailure(ready.ReasonCode, 503);
    }
    private static void Validate(object value)
    {
        if (!Validator.TryValidateObject(value, new ValidationContext(value), [], true)) throw AttendanceOperation.Invalid();
    }
    private static void Page(int page, int size)
    { if (page is < 1 or > 1000000 || size is < 1 or > 100) throw AttendanceOperation.Invalid(); }
    private static void RequireText(string? value, int maximum)
    { if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl)) throw AttendanceOperation.Invalid(); }
    public static FaceEnrollmentResponse Map(FaceEnrollment x) => new(x.Id, x.EmployeeId, x.Provider, x.ProviderVersion,
        x.EnrolledAtUtc, x.IsActive, x.RevokedAtUtc, x.ReplacesEnrollmentId, Convert.ToBase64String(x.RowVersion));
    public static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static bool SecureDigestEquals(string? left, string right) => left is { Length: 64 } &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
    private static string TemplatePurpose(Guid employeeId) => $"attendance:face:template:{employeeId:N}";
    private sealed record VerificationPreparation(Guid EmployeeId, Guid DeviceId, FaceVerificationOutcome Outcome,
        DateTimeOffset ExpiresAt, DateTimeOffset Now, string TemplateReference, string Provider, string ProviderVersion,
        Guid AccountId, Guid SessionId);
}
