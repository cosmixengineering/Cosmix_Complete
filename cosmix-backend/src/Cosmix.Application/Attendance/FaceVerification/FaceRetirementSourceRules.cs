using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Common.Idempotency;

namespace Cosmix.Application.Attendance.FaceVerification;

/// <summary>Exact terminal-source validation, independent of use expiry and disposal authority.</summary>
public static class FaceRetirementSourceRules
{
    public static FaceRetirementProof Enrollment(FaceRetirementIntent intent, FaceEnrollment? row,
        FaceEnrollment? replacement, bool referenceStillActive)
    {
        FaceRetirementEvidence.RequireIntent(intent);
        if (intent.Capture is not null || row is null) return FaceRetirementProof.Rejected;
        var reference = FaceRetirementEvidence.Deserialize<PrivateFileReference>(Encoding.UTF8.GetBytes(row.EncryptedTemplateReference));
        if (row.EmployeeId != intent.EmployeeId || row.Id != intent.SubjectId || row.IsActive || referenceStillActive ||
            row.RevokedAtUtc != intent.RevokedAtUtc || string.IsNullOrWhiteSpace(row.RevokedBy) ||
            string.IsNullOrWhiteSpace(row.RevocationReason) || row.EncryptionKeyReference != reference.KeyReference || reference != intent.Reference ||
            row.RevokedAtUtc < row.EnrolledAtUtc) return FaceRetirementProof.Rejected;
        if (intent.Reason == FaceStorageRetentionReason.EnrollmentReplaced &&
            (replacement is null || replacement.Id != intent.ReplacementEnrollmentId || replacement.EmployeeId != row.EmployeeId ||
             replacement.ReplacesEnrollmentId != row.Id || replacement.EnrolledAtUtc < row.RevokedAtUtc))
            return FaceRetirementProof.Rejected;
        return FaceRetirementProof.Committed;
    }

    public static bool Claim(FaceRetirementIntent intent, IdempotencyReceipt claim, FaceCaptureEnvelope stored)
    {
        FaceRetirementEvidence.RequireIntent(intent);
        var e = intent.Capture!; var b = e.Binding; var self = b.AttemptId.HasValue;
        var permission = self ? "attendance.face.self" : "attendance.face.manage";
        // Same shape and property names as the existing AttendanceOperation capture claim digest.
        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            permission, employeeId = (Guid?)b.EmployeeId, DeviceId = self ? (Guid?)b.DeviceId : null,
            payload = new { operationKey = intent.OperationKey, e.CaptureId, TokenDigest = intent.TokenSha256, b.SessionId },
            reason = "Server claim of bound biometric evidence"
        })));
        return claim.ActorUserId == b.AccountId.ToString() && claim.Operation == "attendance.face.capture.claim" &&
            claim.Key == $"capture:{e.CaptureId:N}" && claim.RequestSha256 == digest && claim.ResultCode == "attendance-operation-v1" &&
            claim.CompletedAt is not null && claim.CreatedAt >= e.IssuedAtUtc && claim.CompletedAt >= claim.CreatedAt &&
            claim.ProtectedResultReference is not null && FaceRetirementEvidence.Same(e, stored);
    }

    public static FaceRetirementProof Verification(FaceRetirementIntent intent, IdempotencyReceipt claim,
        FaceVerificationAttempt? attempt)
    {
        var e = intent.Capture!; var b = e.Binding;
        if (attempt is null || attempt.Id != b.AttemptId || attempt.EmployeeId != intent.EmployeeId ||
            attempt.UserAccountId != b.AccountId || attempt.RegisteredDeviceKey != b.DeviceId ||
            attempt.ChallengeSha256 != b.ChallengeSha256) return FaceRetirementProof.Rejected;
        if (attempt.Outcome == FaceVerificationOutcome.Pending && attempt.CompletedAtUtc is null) return FaceRetirementProof.Pending;
        if (!Enum.IsDefined(attempt.Outcome) || attempt.Outcome == FaceVerificationOutcome.Pending ||
            attempt.CompletedAtUtc is null || attempt.CompletedAtUtc < claim.CompletedAt ||
            attempt.CompletedAtUtc < e.IssuedAtUtc || attempt.ResultReasonCode is null ||
            attempt.Outcome != FaceVerificationOutcome.Expired && attempt.CompletedAtUtc >= attempt.ExpiresAtUtc ||
            attempt.Outcome == FaceVerificationOutcome.Expired && attempt.CompletedAtUtc < attempt.ExpiresAtUtc)
            return FaceRetirementProof.Rejected;
        return FaceRetirementProof.Committed;
    }

    public static FaceRetirementProof EnrollmentCapture(FaceRetirementIntent intent, IdempotencyReceipt claim,
        IdempotencyReceipt startReceipt, EnrollmentStart start, IdempotencyReceipt? terminalReceipt, EnrollmentTerminal? terminal)
    {
        var b = intent.Capture!.Binding;
        if (intent.OperationKey != $"enrollment:{start.ReceiptId:N}" || start.EmployeeId != intent.EmployeeId ||
            start.RequestedDeviceId != b.DeviceId || start.ActorAccountId != b.AccountId || start.ActorSessionReference != b.SessionId.ToString() ||
            startReceipt.Id != start.ReceiptId || startReceipt.ActorUserId != b.AccountId.ToString() ||
            startReceipt.Operation != FaceEnrollmentJournal.StartOperation || startReceipt.ResultCode != "face-enrollment-start-v1" ||
            startReceipt.RequestSha256 != start.RequestSha256 || startReceipt.CreatedAt != start.StartedAtUtc ||
            startReceipt.CompletedAt is null || !FaceReadiness.ValidIdentity(start.Provider) || !FaceReadiness.ValidIdentity(start.ProviderVersion))
            return FaceRetirementProof.Rejected;
        if (terminalReceipt is null && terminal is null) return FaceRetirementProof.Pending;
        if (terminalReceipt is null || terminal is null || terminalReceipt.Operation != FaceEnrollmentJournal.TerminalOperation ||
            terminalReceipt.ActorUserId != startReceipt.ActorUserId || terminalReceipt.Key != start.ReceiptId.ToString("N") ||
            terminalReceipt.RequestSha256 != start.RequestSha256 || terminalReceipt.ResultCode != "face-enrollment-terminal-v1" ||
            terminalReceipt.CompletedAt is null || terminalReceipt.CreatedAt != terminal.CompletedAtUtc ||
            terminal.StartReceiptId != start.ReceiptId || terminal.EmployeeId != intent.EmployeeId || terminal.RequestedDeviceId != b.DeviceId ||
            terminal.Provider != start.Provider || terminal.ProviderVersion != start.ProviderVersion ||
            terminal.CompletedAtUtc < claim.CompletedAt || terminal.CompletedAtUtc < start.StartedAtUtc ||
            terminal.CompletedAtUtc < intent.Capture.IssuedAtUtc) return FaceRetirementProof.Rejected;
        var known = terminal.Status == 200
            ? terminal.Stage == "succeeded" && terminal.Code == "face_enrollment_verified" &&
              terminal.Enrollment is { IsActive: true } enrollment && enrollment.Id != Guid.Empty &&
              enrollment.EmployeeId == start.EmployeeId && enrollment.Provider == start.Provider && enrollment.ProviderVersion == start.ProviderVersion
            : terminal.Enrollment is null && Enum.GetValues<EnrollmentFailure>().Select(FaceEnrollmentJournal.Describe)
                .Any(x => x.Stage == terminal.Stage && x.Code == terminal.Code && x.Status == terminal.Status);
        return known ? FaceRetirementProof.Committed : FaceRetirementProof.Rejected;
    }
}
