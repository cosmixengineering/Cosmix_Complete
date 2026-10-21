using Cosmix.Application.Attendance.Common;

namespace Cosmix.Application.HR.Leaves;

public sealed record LeaveBenefitWaiverProof(int SchemaVersion, Guid Id, Guid AwardId, Guid CorrectionId,
    Guid EmployeeId, string CorrectionFingerprint, string ExpectedResolutionVersion,
    IReadOnlyList<LeaveBenefitUsageEntry> Usage, EncashmentPolicySource Policy, int WaivedMinutes,
    Guid ActorAccountId, Guid ActorEmployeeId, Guid SessionId, DateTimeOffset RecordedAtUtc, string Reason, string IdempotencyKey);

public sealed partial class LeaveBenefitEvidence
{
    public const string ResolutionTargetType = "LeaveBenefitResolution";
    public const string WaiverEvent = "attendance.leave.benefit.correction.waived";
    public static string ResolutionPurpose(Guid employee, Guid correction) =>
        $"leave:benefit:resolution:{employee:N}:{correction:N}:v1";
    public static string ResolutionVersion(LeaveBenefitCorrectionProof correction, LeaveBenefitWaiverProof? waiver) =>
        LeaveProvenance.Digest(new { CorrectionFingerprint = LeaveProvenance.Digest(correction), Waiver = waiver });

    public async Task<LeaveBenefitWaiverProof?> WaiverAsync(LeaveBenefitAwardProof award,
        LeaveBenefitCorrectionProof correction, IReadOnlyList<LeaveBenefitUsageEntry> usage, CancellationToken ct)
    {
        var facts = await repository.ResolutionFactsAsync(award.EmployeeId, correction.Id, ct);
        if (facts.Count > 1) throw AttendanceOperation.Conflict("leave_benefit_resolution_evidence_conflict");
        if (facts.Count == 0) return null;
        var fact = facts[0];
        if (fact.EventType != WaiverEvent || fact.TargetType != ResolutionTargetType ||
            fact.TargetId != correction.Id.ToString() || fact.EmployeeScopeId != award.EmployeeId ||
            !LeaveRoleCodes.IsRetainedAdmin(fact.ActorRoleCode) || fact.ProtectedChangesReference is null)
            throw AttendanceOperation.Conflict("leave_benefit_resolution_evidence_conflict");
        var proof = await operation.ReadPrivateAsync<LeaveBenefitWaiverProof>(fact.ProtectedChangesReference,
            ResolutionPurpose(award.EmployeeId, correction.Id), ct);
        if (proof.SchemaVersion != 1 || proof.Id == Guid.Empty || proof.AwardId != award.Id ||
            proof.CorrectionId != correction.Id || proof.EmployeeId != award.EmployeeId ||
            proof.CorrectionFingerprint != LeaveProvenance.Digest(correction) ||
            proof.ExpectedResolutionVersion != ResolutionVersion(correction, null) ||
            proof.WaivedMinutes != correction.ConsumedMinutes || proof.WaivedMinutes <= 0 ||
            proof.ActorAccountId == Guid.Empty || proof.ActorEmployeeId == Guid.Empty || proof.ActorEmployeeId == award.EmployeeId ||
            proof.ActorAccountId.ToString() != fact.ActorUserId || proof.SessionId == Guid.Empty ||
            proof.SessionId.ToString() != fact.DeviceReference || proof.RecordedAtUtc != fact.OccurredAt ||
            proof.RecordedAtUtc < correction.RecordedAtUtc || string.IsNullOrWhiteSpace(proof.Reason) ||
            proof.IdempotencyKey is not { Length: >= 8 and <= 100 } ||
            proof.Reason.Length > 1000 || proof.Reason.Any(char.IsControl) || proof.Policy is null ||
            proof.Usage is null || proof.Usage.Any(x => x is null || x.RecordedAt > proof.RecordedAtUtc))
            throw AttendanceOperation.Conflict("leave_benefit_resolution_evidence_conflict");
        ValidateResolutionPolicy(proof.Policy, award, proof.RecordedAtUtc);
        _ = ProtectedMinutes(award, correction, proof.Usage);
        _ = ProtectedMinutes(award, correction, usage);
        var current = usage.ToDictionary(x => x.Id);
        if (proof.Usage.Any(x => !current.TryGetValue(x.Id, out var retained) || retained != x))
            throw AttendanceOperation.Conflict("leave_benefit_correction_source_changed");
        return proof;
    }

    public static void ValidateResolutionPolicy(EncashmentPolicySource policy, LeaveBenefitAwardProof award, DateTimeOffset now)
    {
        var date = LeaveRules.LocalDate(now);
        if (policy.PolicyId == Guid.Empty || policy.VersionId == Guid.Empty || policy.EffectiveFrom > date ||
            policy.EffectiveTo <= date || policy.PublishedAt > now || policy.JoinedOn > date ||
            policy.AssignmentIds is not { Count: > 0 and <= 1000 } || policy.AssignmentIds.Any(x => x == Guid.Empty) ||
            policy.AssignmentIds.Distinct().Count() != policy.AssignmentIds.Count)
            throw AttendanceOperation.Conflict("leave_benefit_policy_unavailable");
        var settings = LeavePolicySettings.Parse(policy.RulesJson, policy.Schema);
        if (!settings.EligibleStaffCategoryIds.Contains(policy.StaffCategoryId) || settings.Benefits?.EligibleKind != award.Kind)
            throw AttendanceOperation.Conflict("leave_benefit_policy_unavailable");
        if (settings.Benefits.ConsumedCorrectionResolution?.AllowAdminWaiver != true)
            throw AttendanceOperation.Conflict("leave_benefit_waiver_not_configured");
    }

    public static LeaveBenefitWaiverView WaiverView(LeaveBenefitWaiverProof proof) =>
        new(proof.Id, proof.Policy.VersionId, proof.WaivedMinutes, proof.ActorAccountId, proof.RecordedAtUtc, proof.Reason);
    public static LeaveBenefitResolutionView ResolutionView(LeaveBenefitAwardProof award, LeaveBenefitCorrectionProof correction,
        IReadOnlyList<LeaveBenefitUsageEntry> usage, LeaveBenefitWaiverProof? waiver) =>
        new(correction.Id, LeaveProvenance.Digest(correction), ResolutionVersion(correction, waiver), correction.ConsumedMinutes,
            ProtectedMinutes(award, correction, usage), waiver?.WaivedMinutes ?? 0,
            waiver is not null ? "waived" : correction.ConsumedMinutes > 0 ? "pending_admin_review" : "corrected_unused",
            waiver is null ? null : WaiverView(waiver));
    public static LeaveBenefitView ResolvedView(LeaveBenefitAwardProof award, LeaveBenefitCorrectionProof correction,
        IReadOnlyList<LeaveBenefitUsageEntry> usage, LeaveBenefitWaiverProof? waiver)
    {
        var result = CorrectedView(award, correction, ProtectedMinutes(award, correction, usage));
        return waiver is null ? result : result with { Correction = result.Correction! with { Status = "waived", Waiver = WaiverView(waiver) } };
    }
}
