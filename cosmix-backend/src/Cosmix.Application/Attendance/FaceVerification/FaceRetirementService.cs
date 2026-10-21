using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Authentication;
using System.Text.Json;

namespace Cosmix.Application.Attendance.FaceVerification;

public sealed class FaceRetirementService(IFaceRetirementRepository repository, IPrivateFileStore evidence, TimeProvider clock)
{
    public const string IntentEvent = "attendance.face.retirement.intent.v1";
    public const string OutcomeEvent = "attendance.face.retirement.outcome.v1";
    public const string DisposedEvent = "attendance.face.retirement.disposed.v1";
    public const string EnrollmentTarget = "FaceEnrollmentRetirement";
    public const string CaptureTarget = "FaceCaptureRetirement";
    public Task StageEnrollmentAsync(AttendanceActor actor, FaceEnrollment enrollment, Guid? replacement,
        AuthRequestContext context, CancellationToken ct)
    {
        repository.RequireTransaction();
        if (enrollment.IsActive || enrollment.RevokedAtUtc is null || enrollment.RevokedBy != actor.Identity.AccountId.ToString() ||
            string.IsNullOrWhiteSpace(enrollment.RevocationReason)) throw FaceRetirementEvidence.Unavailable();
        return StageAsync(EnrollmentIntent(enrollment, replacement), actor.Identity.AccountId.ToString(), actor.RoleCode,
            context.CorrelationId, context.IpAddress, actor.DeviceId?.ToString() ?? actor.Identity.SessionId.ToString(), ct);
    }

    public Task StageCaptureAsync(AttendanceActor actor, FaceCaptureEnvelope capture, string operationKey,
        string tokenSha256, AuthRequestContext context, CancellationToken ct)
    {
        repository.RequireTransaction();
        var b = capture.Binding;
        if (b.AccountId != actor.Identity.AccountId || b.SessionId != actor.Identity.SessionId ||
            b.AttemptId.HasValue && (actor.Identity.Client != SessionClient.Mobile || actor.DeviceId != b.DeviceId ||
                actor.Identity.EmployeeId != b.EmployeeId) ||
            !b.AttemptId.HasValue && (actor.Identity.Client != SessionClient.Web || !actor.Identity.MfaSatisfied))
            throw AttendanceOperation.Denied();
        return StageAsync(new(1, b.EmployeeId, capture.CaptureId, capture.Image,
            FaceCaptureService.Purpose(b.EmployeeId, capture.CaptureId), FaceStorageRetentionReason.CaptureCompleted,
            operationKey, null, null, capture, tokenSha256), actor.Identity.AccountId.ToString(), actor.RoleCode,
            context.CorrelationId, context.IpAddress, b.DeviceId.ToString(), ct);
    }

    private async Task StageAsync(FaceRetirementIntent intent, string actor, string role, string correlation,
        string? ip, string? device, CancellationToken ct)
    {
        repository.RequireTransaction(); ct.ThrowIfCancellationRequested();
        FaceRetirementEvidence.RequireIntent(intent);
        var type = Target(intent);
        var existing = await repository.FindIntentAsync(type, intent.SubjectId, ct);
        if (existing is not null)
        {
            if (!FaceRetirementEvidence.Same(await ReadIntentAsync(existing, ct), intent))
                throw FaceRetirementEvidence.Unavailable();
            return;
        }
        var reference = await FaceRetirementEvidence.WriteAsync(evidence, intent,
            FaceRetirementEvidence.IntentPurpose(type, intent.SubjectId), ct);
        repository.Add(new(IntentEvent, actor, role, type, intent.SubjectId.ToString(), clock.GetUtcNow(),
            correlation, intent.EmployeeId, reference, ip, device));
        // The caller's outer authorization transaction saves/commits. Staging never touches the vault.
    }

    public async Task<FaceRetirementIntent> ReadIntentAsync(AuditEvent audit, CancellationToken ct)
    {
        if (audit.EventType != IntentEvent || audit.TargetType is not (EnrollmentTarget or CaptureTarget) ||
            !Guid.TryParseExact(audit.TargetId, "D", out var subject) || subject == Guid.Empty)
            throw FaceRetirementEvidence.Unavailable();
        var intent = await FaceRetirementEvidence.ReadAsync<FaceRetirementIntent>(evidence, audit.ProtectedChangesReference,
            FaceRetirementEvidence.IntentPurpose(audit.TargetType, subject), ct);
        FaceRetirementEvidence.RequireIntent(intent);
        if (intent.EmployeeId != audit.EmployeeScopeId || intent.SubjectId != subject || Target(intent) != audit.TargetType ||
            intent.Capture is not null && intent.Capture.Binding.AccountId.ToString() != audit.ActorUserId)
            throw FaceRetirementEvidence.Unavailable();
        return intent;
    }

    public async Task<FaceRetirementOutcome?> ReadOutcomeAsync(AuditEvent source, CancellationToken ct)
    {
        var audit = await repository.LatestOutcomeAsync(source.Id, ct);
        if (audit is null) return null;
        var result = await FaceRetirementEvidence.ReadAsync<FaceRetirementOutcome>(evidence, audit.ProtectedChangesReference,
            FaceRetirementEvidence.OutcomePurpose(source.Id), ct);
        if (result.Version != 1 || result.IntentId != source.Id || !Enum.IsDefined(result.State) ||
            result.Code != Code(result.State) || result.ObservedAtUtc != audit.OccurredAt ||
            audit.TargetType != "FaceRetirementOutcome" || audit.TargetId != source.Id.ToString() ||
            audit.EmployeeScopeId != source.EmployeeScopeId || audit.ActorUserId != source.ActorUserId ||
            audit.ActorRoleCode != source.ActorRoleCode ||
            audit.EventType != (result.State == FaceRetirementState.Disposed ? DisposedEvent : OutcomeEvent) ||
            result.State == FaceRetirementState.Disposed && !result.UseClosed)
            throw FaceRetirementEvidence.Unavailable();
        return result;
    }

    public async Task RecordOutcomeAsync(AuditEvent source, FaceRetirementOutcome outcome, CancellationToken ct)
    {
        repository.RequireOutsideTransaction();
        if (source.EventType != IntentEvent || outcome.Version != 1 || outcome.IntentId != source.Id ||
            !Enum.IsDefined(outcome.State) || outcome.Code != Code(outcome.State) ||
            outcome.ObservedAtUtc.Offset != TimeSpan.Zero || outcome.State == FaceRetirementState.Disposed && !outcome.UseClosed)
            throw FaceRetirementEvidence.Unavailable();
        await repository.InAuditTransactionAsync(async token =>
        {
            var previous = await ReadOutcomeAsync(source, token);
            if (previous?.State == FaceRetirementState.Disposed) return true;
            var value = outcome with { UseClosed = outcome.UseClosed || previous?.UseClosed == true };
            if (previous is not null && previous.State == value.State && previous.UseClosed == value.UseClosed && previous.Code == value.Code)
                return true;
            // Audit order remains unambiguous even when the injected clock has not advanced.
            if (previous is not null && value.ObservedAtUtc <= previous.ObservedAtUtc)
                value = value with { ObservedAtUtc = previous.ObservedAtUtc.AddTicks(1) };
            var reference = await FaceRetirementEvidence.WriteAsync(evidence, value,
                FaceRetirementEvidence.OutcomePurpose(source.Id), token);
            repository.Add(new(value.State == FaceRetirementState.Disposed ? DisposedEvent : OutcomeEvent,
                source.ActorUserId, source.ActorRoleCode, "FaceRetirementOutcome", source.Id.ToString(),
                value.ObservedAtUtc, source.CorrelationId, source.EmployeeScopeId, reference));
            await repository.SaveChangesAsync(token);
            return true;
        }, ct);
    }

    public async Task<FaceRetirementRecovery> RecoverRevocationsAsync(int size, CancellationToken ct, FaceRetirementCursor? after = null)
    {
        repository.RequireOutsideTransaction();
        if (size is < 1 or > 100) throw FaceRetirementEvidence.Unavailable();
        var sources = await repository.UnstagedRevocationsAsync(after, size, ct);
        var count = 0; var pending = 0; var failed = 0;
        foreach (var source in sources)
        {
            var row = source.Enrollment; var authority = source.AuthorityAudit;
            // The revoked row remains a durable source if its original audit cannot be reconciled.
            if (authority is null || authority.EmployeeScopeId != row.EmployeeId || authority.ActorUserId != row.RevokedBy)
            { pending++; continue; }
            try
            {
                await repository.InAuditTransactionAsync(async token =>
                {
                    await StageAsync(EnrollmentIntent(row, source.ReplacementEnrollmentId), authority.ActorUserId,
                        authority.ActorRoleCode, authority.CorrelationId, authority.IpAddress, authority.DeviceReference, token);
                    await repository.SaveChangesAsync(token);
                    return true;
                }, ct);
                count++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { ct.ThrowIfCancellationRequested(); failed++; }
        }
        var last = sources.LastOrDefault()?.Enrollment;
        return new(count, pending, failed, sources.Count == size && last?.RevokedAtUtc is DateTimeOffset at ? new(at, last.Id) : null);
    }

    private static FaceRetirementIntent EnrollmentIntent(FaceEnrollment row, Guid? replacement)
    {
        if (row.IsActive || row.RevokedAtUtc is null) throw FaceRetirementEvidence.Unavailable();
        var reference = FaceRetirementEvidence.Deserialize<PrivateFileReference>(System.Text.Encoding.UTF8.GetBytes(row.EncryptedTemplateReference));
        return new(1, row.EmployeeId, row.Id, reference, $"attendance:face:template:{row.EmployeeId:N}",
            replacement.HasValue ? FaceStorageRetentionReason.EnrollmentReplaced : FaceStorageRetentionReason.EnrollmentRevoked,
            $"enrollment:{row.Id:N}", row.RevokedAtUtc, replacement, null, null);
    }
    private static string Target(FaceRetirementIntent value) => value.Capture is null ? EnrollmentTarget : CaptureTarget;
    public static string Code(FaceRetirementState state) => state switch
    {
        FaceRetirementState.AwaitingTerminal => "face_retirement_pending_terminal",
        FaceRetirementState.PendingPolicy => "face_disposal_pending_policy",
        FaceRetirementState.PendingHold => "face_disposal_pending_hold",
        FaceRetirementState.PendingBoundary => "face_disposal_pending_boundary",
        FaceRetirementState.PendingDisposalAdapter => "face_disposal_pending_adapter",
        FaceRetirementState.RetryRequired => "face_retirement_retry_required",
        FaceRetirementState.ScopeRejected => "face_retirement_scope_rejected",
        FaceRetirementState.Disposed => "face_disposal_confirmed",
        _ => throw FaceRetirementEvidence.Unavailable()
    };
}
