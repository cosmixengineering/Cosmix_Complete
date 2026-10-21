using System.Security.Cryptography;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.FaceVerification;

namespace Cosmix.Application.Attendance.FaceVerification;

public sealed class FaceCaptureService(AttendanceOperation operation, IFaceRepository repository,
    IFaceTemplateStore files, IFaceCaptureProtector protector, FaceCaptureOptions options,
    FaceVerificationPolicy policy, TimeProvider clock)
{
    public async Task<FaceCaptureResponse> UploadVerificationAsync(Guid attemptId,
        UploadVerificationFaceCaptureRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        RequireReady();
        SchedulingOperation.Version(request.ExpectedVersion);
        if (string.IsNullOrWhiteSpace(request.Challenge) || request.Challenge.Length > 128 || request.Challenge.Any(char.IsControl))
            throw AttendanceOperation.Invalid("invalid_face_capture");
        var image = FaceCaptureImage.Decode(request.ImageBase64);
        try
        {
            return await operation.WriteAsync(AttendanceCapturePermissions.FaceSelf, "attendance.face.capture.upload", null,
                key, new { attemptId, request.ExpectedVersion, ChallengeDigest = FaceService.Digest(request.Challenge), ImageDigest = Digest(image) },
                "Employee Check In camera capture", context, async (actor, token) =>
                {
                    RequireReady();
                    var attempt = await repository.AttemptAsync(actor.Identity.EmployeeId, attemptId, token) ?? throw AttendanceOperation.Missing();
                    SchedulingOperation.ExpectVersion(Convert.ToBase64String(attempt.RowVersion), request.ExpectedVersion);
                    var binding = new FaceCaptureBinding(actor.Identity.AccountId, actor.Identity.SessionId,
                        actor.Identity.EmployeeId, actor.DeviceId!.Value, attemptId, FaceService.Digest(request.Challenge));
                    FaceCaptureEvidenceReader.RequireAttempt(attempt, binding, clock.GetUtcNow());
                    return await Store(binding, image, [], actor.Now, attempt.ExpiresAtUtc, token);
                }, ct, self: true, requireDevice: true);
        }
        finally { CryptographicOperations.ZeroMemory(image); }
    }

    public async Task<FaceCaptureResponse> UploadEnrollmentAsync(Guid employeeId,
        UploadEnrollmentFaceCaptureRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        RequireReady();
        if (request.RegisteredDeviceId == Guid.Empty) throw AttendanceOperation.Invalid("invalid_face_capture");
        var image = FaceCaptureImage.Decode(request.ImageBase64);
        try
        {
            return await operation.WriteAsync(AttendanceCapturePermissions.FaceManage, "attendance.face.capture.enrollment", employeeId,
                key, new { request.RegisteredDeviceId, ImageDigest = Digest(image) }, request.Reason, context, async (actor, token) =>
                {
                    RequireReady();
                    await repository.EmployeeDeviceAsync(employeeId, request.RegisteredDeviceId, token);
                    var resources = await repository.ResourcesAsync(employeeId, RosterSchedule.Today(actor.Now), token);
                    var binding = new FaceCaptureBinding(actor.Identity.AccountId, actor.Identity.SessionId,
                        employeeId, request.RegisteredDeviceId, null, null);
                    return await Store(binding, image, resources, actor.Now, null, token);
                }, ct, resources: (actor, token) => repository.ResourcesAsync(employeeId, RosterSchedule.Today(actor.Now), token));
        }
        finally { CryptographicOperations.ZeroMemory(image); }
    }

    private async Task<AttendanceChange<FaceCaptureResponse>> Store(FaceCaptureBinding binding, byte[] image,
        IReadOnlyList<Cosmix.Application.Authentication.Access.AccessResource> resources,
        DateTimeOffset now, DateTimeOffset? challengeExpiry, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var expiry = now.AddSeconds(options.LifetimeSeconds!.Value);
        if (challengeExpiry.HasValue && challengeExpiry < expiry) expiry = challengeExpiry.Value;
        if (expiry <= clock.GetUtcNow()) throw AttendanceOperation.Conflict("face_capture_expired");
        var reference = await files.WriteAsync(image, Purpose(binding.EmployeeId, id), ct);
        var envelope = new FaceCaptureEnvelope(1, id, binding, reference, FaceCaptureImage.MediaType(image), now, expiry, resources);
        return new(id, null, new(protector.Protect(envelope), expiry));
    }
    private void RequireReady()
    {
        if (!options.IsConfigured || !policy.IsConfirmed) throw new AuthenticationFailure("face_capture_not_configured", 503);
        if (!files.IsConfigured) throw new AuthenticationFailure("face_template_storage_unavailable", 503);
    }
    public static string Purpose(Guid employeeId, Guid captureId) => $"attendance:face:template:{employeeId:N}:capture:{captureId:N}";
    private static string Digest(byte[] image) => Convert.ToHexString(SHA256.HashData(image));
}

public sealed class FaceCaptureEvidenceReader(AttendanceOperation operation, IFaceRepository repository,
    IFaceTemplateStore files, IFaceCaptureProtector protector, FaceCaptureOptions options,
    FaceVerificationPolicy policy, TimeProvider clock, FaceRetirementService retirement) : IFaceCaptureEvidenceReader
{
    public async Task<byte[]> ReadAsync(string token, FaceCaptureBinding binding, string operationKey, CancellationToken ct)
    {
        if (!options.IsConfigured || !policy.IsConfirmed || !files.IsConfigured)
            throw new AuthenticationFailure("face_capture_not_configured", 503);
        if (string.IsNullOrWhiteSpace(operationKey) || operationKey.Length > 100 || operationKey.Any(char.IsControl))
            throw AttendanceOperation.Invalid("invalid_face_capture");
        var envelope = protector.Unprotect(token, binding);
        var self = binding.AttemptId.HasValue;
        var permission = self ? AttendanceCapturePermissions.FaceSelf : AttendanceCapturePermissions.FaceManage;
        var context = new AuthRequestContext("", $"face-capture:{envelope.CaptureId:N}");
        await operation.ReadAsync(permission, "attendance.face.capture.preflight", self ? null : binding.EmployeeId,
            context, async (actor, cancellation) =>
            {
                await RequireBinding(actor, binding, cancellation);
                return true;
            }, ct, self: self, requireDevice: self,
            resources: self ? null : async (actor, cancellation) => (await repository.ResourcesAsync(binding.EmployeeId,
                RosterSchedule.Today(actor.Now), cancellation)).Concat(envelope.OriginalResources).Distinct().ToArray());
        var claim = await operation.WriteAsync(self ? AttendanceCapturePermissions.FaceSelf : AttendanceCapturePermissions.FaceManage,
            "attendance.face.capture.claim", self ? null : binding.EmployeeId, $"capture:{envelope.CaptureId:N}",
            new { operationKey, envelope.CaptureId, TokenDigest = FaceService.Digest(token), binding.SessionId },
            "Server claim of bound biometric evidence", context, async (actor, cancellation) =>
            {
                await RequireBinding(actor, binding, cancellation);
                await retirement.StageCaptureAsync(actor, envelope, operationKey, FaceService.Digest(token), context, cancellation);
                return new AttendanceChange<FaceCaptureEnvelope>(envelope.CaptureId, null, envelope);
            }, ct, self: self, requireDevice: self,
            resources: self ? null : async (actor, cancellation) => (await repository.ResourcesAsync(binding.EmployeeId,
                RosterSchedule.Today(actor.Now), cancellation)).Concat(envelope.OriginalResources).Distinct().ToArray());
        // A retained claim is usable only under the same live binding, not just receipt authority.
        if (claim.Binding != binding || claim.CaptureId != envelope.CaptureId || claim.ExpiresAtUtc <= clock.GetUtcNow())
            throw AttendanceOperation.Conflict("face_capture_expired");
        byte[]? image = null;
        try
        {
            // A claim receipt is not authority to release bytes. Revalidate and read under one
            // authorization transaction so a revocation between preflight and replay cannot bypass it.
            return await operation.ReadAsync(permission, "attendance.face.capture.read", self ? null : binding.EmployeeId,
                context, async (actor, cancellation) =>
                {
                    if (!options.IsConfigured || !policy.IsConfirmed || !files.IsConfigured)
                        throw new AuthenticationFailure("face_capture_not_configured", 503);
                    await RequireBinding(actor, binding, cancellation);
                    if (claim.ExpiresAtUtc <= clock.GetUtcNow()) throw AttendanceOperation.Conflict("face_capture_expired");
                    // Replay also stages legacy claims, inside the same reauthorized read boundary.
                    await retirement.StageCaptureAsync(actor, claim, operationKey, FaceService.Digest(token), context, cancellation);
                    image = await files.ReadAsync(claim.Image, FaceCaptureService.Purpose(binding.EmployeeId, claim.CaptureId), cancellation);
                    if (image.Length != claim.Image.SizeBytes || FaceCaptureImage.MediaType(image) != claim.MediaType ||
                        !FaceService.SecureDigestEquals(Convert.ToHexString(SHA256.HashData(image)), claim.Image.Sha256))
                        throw new AuthenticationFailure("face_capture_unavailable", 503);
                    return image;
                }, ct, self: self, requireDevice: self,
                resources: self ? null : async (actor, cancellation) => (await repository.ResourcesAsync(binding.EmployeeId,
                    RosterSchedule.Today(actor.Now), cancellation)).Concat(envelope.OriginalResources).Distinct().ToArray());
        }
        catch
        {
            if (image is not null) CryptographicOperations.ZeroMemory(image);
            throw;
        }
    }
    private async Task RequireBinding(AttendanceActor actor, FaceCaptureBinding binding, CancellationToken ct)
    {
        if (actor.Identity.AccountId != binding.AccountId || actor.Identity.SessionId != binding.SessionId ||
            binding.AttemptId.HasValue && (actor.DeviceId != binding.DeviceId || actor.Identity.EmployeeId != binding.EmployeeId))
            throw AttendanceOperation.Denied();
        await repository.EmployeeDeviceAsync(binding.EmployeeId, binding.DeviceId, ct);
        if (binding.AttemptId.HasValue)
        {
            var attempt = await repository.AttemptAsync(binding.EmployeeId, binding.AttemptId.Value, ct) ?? throw AttendanceOperation.Missing();
            RequireAttempt(attempt, binding, clock.GetUtcNow());
        }
    }
    public static void RequireAttempt(FaceVerificationAttempt attempt, FaceCaptureBinding binding, DateTimeOffset now)
    {
        if (attempt.Id != binding.AttemptId || attempt.UserAccountId != binding.AccountId ||
            attempt.EmployeeId != binding.EmployeeId || attempt.RegisteredDeviceKey != binding.DeviceId ||
            !FaceService.SecureDigestEquals(binding.ChallengeSha256, attempt.ChallengeSha256)) throw AttendanceOperation.Denied();
        if (!attempt.Enrollment.IsActive) throw AttendanceOperation.Conflict("face_enrollment_revoked");
        if (attempt.Outcome != FaceVerificationOutcome.Pending || attempt.ConsumedAtUtc.HasValue || now >= attempt.ExpiresAtUtc)
            throw AttendanceOperation.Conflict("face_challenge_already_completed");
    }
}

public static class FaceCaptureImage
{
    public static byte[] Decode(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64) || base64.Length > 699052 || base64.Any(char.IsWhiteSpace))
            throw AttendanceOperation.Invalid("invalid_face_capture_image");
        byte[] image;
        try { image = Convert.FromBase64String(base64); }
        catch (FormatException) { throw AttendanceOperation.Invalid("invalid_face_capture_image"); }
        try { MediaType(image); return image; }
        catch { CryptographicOperations.ZeroMemory(image); throw; }
    }
    public static string MediaType(byte[] image)
    {
        if (image.Length is < 8 or > 524288) throw AttendanceOperation.Invalid("invalid_face_capture_image");
        if (image.AsSpan(0, 8).SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 })) return "image/png";
        if (image[0] == 255 && image[1] == 216 && image[2] == 255) return "image/jpeg";
        throw AttendanceOperation.Invalid("invalid_face_capture_image");
    }
}
