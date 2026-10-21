using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.Leaves;

public sealed partial class LeaveBenefitService
{
    public const string ResolutionOperation = "attendance.leave.benefit.correction.resolve";
    public Task<LeaveBenefitView> ResolveCorrectionAsync(Guid employeeId, Guid id,
        ResolveLeaveBenefitCorrectionRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || id == Guid.Empty || request is null || request.PolicyVersionId == Guid.Empty ||
            request.Decision != LeaveBenefitResolutionDecision.Waive || request.ExpectedCorrectionFingerprint is not { Length: 64 } ||
            request.ExpectedUsageFingerprint is not { Length: 64 } || request.ExpectedResolutionVersion is not { Length: 64 })
            throw AttendanceOperation.Invalid("invalid_leave_benefit_resolution");
        return operation.WriteAsync(LeaveBenefitPermissions.Review, ResolutionOperation, employeeId, key,
            new { id, request }, request.Reason, context, async (actor, token) => {
                await RequireAdmin(actor, employeeId, token);
                var prepared = await PrepareResolution(employeeId, id, request, actor.Now, token);
                var proof = new LeaveBenefitWaiverProof(1, Guid.NewGuid(), id, prepared.Correction.Id, employeeId,
                    request.ExpectedCorrectionFingerprint, request.ExpectedResolutionVersion, prepared.Usage, prepared.Policy,
                    prepared.Correction.ConsumedMinutes, actor.Identity.AccountId, actor.Identity.EmployeeId,
                    actor.Identity.SessionId, actor.Now, request.Reason.Trim(), key);
                var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(proof),
                    LeaveBenefitEvidence.ResolutionPurpose(employeeId, prepared.Correction.Id), token));
                var again = await PrepareResolution(employeeId, id, request, clock.GetUtcNow(), token);
                if (LeaveProvenance.Digest(prepared.Policy) != LeaveProvenance.Digest(again.Policy))
                    throw AttendanceOperation.Conflict("leave_benefit_policy_changed");
                await RequireAdmin(actor, employeeId, token);
                // Resolution is an immutable review fact, never a financial ledger entry.
                var fact = new AuditEvent(LeaveBenefitEvidence.WaiverEvent, actor.Identity.AccountId.ToString(), LeaveRoleCodes.Admin,
                    LeaveBenefitEvidence.ResolutionTargetType, prepared.Correction.Id.ToString(), actor.Now, context.CorrelationId,
                    employeeId, reference, context.IpAddress, actor.Identity.SessionId.ToString());
                repository.AddResolution(fact);
                var recipient = await repository.RecipientAsync(employeeId, token);
                if (recipient.HasValue) await notifications.StageAsync(new(proof.Id, recipient.Value,
                    NotificationTemplates.Decision, prepared.Correction.Id), context, token);
                await RequireAdmin(actor, employeeId, token);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<LeaveBenefitView>(proof.Id,
                    LeaveBenefitEvidence.ResolvedView(prepared.Award, prepared.Correction, prepared.Usage, null),
                    LeaveBenefitEvidence.ResolvedView(prepared.Award, prepared.Correction, prepared.Usage, proof));
            }, ct,
            beforeReplay: async (actor, token) => {
                await RequireAdmin(actor, employeeId, token);
                var current = await ResolutionSource(employeeId, id, token);
                var waiver = await evidence.WaiverAsync(current.Award, current.Correction, current.Usage, token);
                var receipt = await repository.ResolutionReceiptAsync(actor.Identity.AccountId, key, token);
                if (receipt is not null)
                {
                    if (waiver is null) throw AttendanceOperation.Conflict("leave_benefit_resolution_evidence_conflict");
                    if (waiver.IdempotencyKey != key || waiver.ActorAccountId != actor.Identity.AccountId ||
                        waiver.Policy.VersionId != request.PolicyVersionId || waiver.CorrectionFingerprint != request.ExpectedCorrectionFingerprint ||
                        waiver.ExpectedResolutionVersion != request.ExpectedResolutionVersion ||
                        LeaveBenefitUsage.Fingerprint(waiver.Usage) != request.ExpectedUsageFingerprint || waiver.Reason != request.Reason.Trim())
                        throw AttendanceOperation.Conflict("idempotency_key_reused");
                }
                await RequireAdmin(actor, employeeId, token);
            }, beforeCommit: (actor, token) => RequireAdmin(actor, employeeId, token));
    }

    private async Task<(LeaveBenefitAwardProof Award, LeaveBenefitCorrectionProof Correction, IReadOnlyList<LeaveBenefitUsageEntry> Usage)>
        ResolutionSource(Guid employee, Guid id, CancellationToken ct)
    {
        var row = await repository.FindAsync(employee, id, ct) ?? throw AttendanceOperation.Missing("leave_benefit_not_found");
        var award = await evidence.AwardAsync(row, ct);
        var rows = await repository.YearAsync(employee, award.YearFrom, award.YearTo, ct);
        var correction = await evidence.CorrectionAsync(award, rows, ct)
            ?? throw AttendanceOperation.Conflict("leave_benefit_correction_required");
        _ = await evidence.ReadAsync(employee, award.YearFrom, award.YearTo, award.AvailableFrom, ct);
        var usage = await repository.UsageAsync(employee, award.YearFrom, award.YearTo, ct);
        _ = LeaveBenefitEvidence.ProtectedMinutes(award, correction, usage);
        return (award, correction, usage);
    }

    private async Task<(LeaveBenefitAwardProof Award, LeaveBenefitCorrectionProof Correction,
        IReadOnlyList<LeaveBenefitUsageEntry> Usage, EncashmentPolicySource Policy)> PrepareResolution(Guid employee, Guid id,
        ResolveLeaveBenefitCorrectionRequest request, DateTimeOffset now, CancellationToken ct)
    {
        var source = await ResolutionSource(employee, id, ct);
        var waiver = await evidence.WaiverAsync(source.Award, source.Correction, source.Usage, ct);
        if (waiver is not null || LeaveProvenance.Digest(source.Correction) != request.ExpectedCorrectionFingerprint ||
            LeaveBenefitEvidence.ResolutionVersion(source.Correction, waiver) != request.ExpectedResolutionVersion)
            throw AttendanceOperation.Conflict("leave_benefit_resolution_changed");
        if (source.Correction.ConsumedMinutes <= 0) throw AttendanceOperation.Conflict("leave_benefit_consumed_obligation_required");
        if (LeaveBenefitUsage.Fingerprint(source.Usage) != request.ExpectedUsageFingerprint ||
            source.Usage.Any(x => x.RecordedAt > now) || source.Correction.RecordedAtUtc > now)
            throw AttendanceOperation.Conflict("leave_benefit_usage_changed");
        var date = LeaveRules.LocalDate(now);
        var policy = await leaves.PolicyAsync(employee, source.Award.LeaveTypeId, request.PolicyVersionId, date, date, ct);
        if (policy is null || !policy.ProvenanceValid || policy.Version.Id != request.PolicyVersionId ||
            policy.Version.Authority != PolicyAuthority.DirectHr || policy.Version.ApprovalRequestId is not null ||
            policy.Version.Policy.Category != PolicyCategory.Leave || policy.JoinedOn is null)
            throw AttendanceOperation.Conflict("leave_benefit_policy_unavailable");
        var retained = new EncashmentPolicySource(policy.Version.PolicyId, policy.Version.Id, policy.Version.RulesSchemaVersion,
            policy.Version.RulesJson, policy.Version.EffectiveFrom, policy.Version.EffectiveTo, policy.Version.PublishedAt,
            policy.Version.PublishedByUserId, policy.StaffCategoryId, policy.JoinedOn.Value, policy.AssignmentIds!);
        LeaveBenefitEvidence.ValidateResolutionPolicy(retained, source.Award, now);
        return (source.Award, source.Correction, source.Usage, retained);
    }
}
