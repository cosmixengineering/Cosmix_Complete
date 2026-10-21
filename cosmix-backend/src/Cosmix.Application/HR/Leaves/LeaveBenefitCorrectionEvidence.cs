using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

public sealed record LeaveBenefitCorrectionProof(int SchemaVersion, Guid Id, Guid AwardId, Guid EmployeeId,
    string AwardFingerprint, IReadOnlyList<LeaveBenefitUsageEntry> Usage, IReadOnlyList<LeaveBenefitAllocation> Allocations,
    int ConsumedMinutes, Guid ActorAccountId, Guid ActorEmployeeId, DateTimeOffset RecordedAtUtc, string Reason);

public sealed partial class LeaveBenefitEvidence
{
    public const string CorrectionEvent = "attendance.leave.benefit.corrected";
    public async Task<LeaveBenefitCorrectionProof?> CorrectionAsync(LeaveBenefitAwardProof award,
        IReadOnlyList<LeaveBenefitRow> rows, CancellationToken ct)
    {
        var changes = rows.Where(x => x.Entry.ReversesEntryId == award.Id).ToArray();
        if (changes.Length > 1) throw AttendanceOperation.Conflict("leave_benefit_reversal_conflict");
        if (changes.Length == 0 || changes[0].Evidence?.EventType != CorrectionEvent) return null;
        var row = changes[0]; var entry = row.Entry; var audit = row.Evidence!;
        if (entry.EntryType != LeaveLedgerEntryType.Adjustment || entry.MinutesDelta != -award.Minutes ||
            entry.EmployeeId != award.EmployeeId || entry.LeaveTypeId != award.LeaveTypeId || entry.PolicyVersionId != award.Policy.VersionId ||
            entry.LeaveRequestId is not null || entry.LeaveYearStart != award.YearFrom || entry.LeaveYearEnd != award.YearTo ||
            entry.IdempotencyKey != RevokeKey(award.Id) || audit.TargetType != TargetType || audit.TargetId != entry.Id.ToString() ||
            audit.EmployeeScopeId != award.EmployeeId || audit.ProtectedChangesReference is null)
            throw AttendanceOperation.Conflict("leave_benefit_correction_evidence_conflict");
        var proof = await operation.ReadPrivateAsync<LeaveBenefitCorrectionProof>(audit.ProtectedChangesReference, Purpose(award.EmployeeId, entry.Id), ct);
        if (proof.SchemaVersion != 1 || proof.Id != entry.Id || proof.AwardId != award.Id || proof.EmployeeId != award.EmployeeId ||
            proof.AwardFingerprint != Fingerprint(award) || proof.ActorAccountId == Guid.Empty || proof.ActorEmployeeId == Guid.Empty ||
            proof.ActorEmployeeId == award.EmployeeId || proof.ActorAccountId.ToString() != audit.ActorUserId ||
            proof.ActorAccountId.ToString() != entry.RecordedByUserId || !LeaveRoleCodes.IsRetainedAdmin(audit.ActorRoleCode) || proof.Reason != entry.Reason ||
            proof.RecordedAtUtc != entry.RecordedAt || proof.RecordedAtUtc != audit.OccurredAt || proof.RecordedAtUtc < award.RecordedAtUtc ||
            proof.Usage is null || proof.Usage.Any(x => x is null || x.RecordedAt > proof.RecordedAtUtc))
            throw AttendanceOperation.Conflict("leave_benefit_correction_evidence_conflict");
        var active = LeaveBenefitUsage.Active(award.EmployeeId, award.YearFrom, award.YearTo, proof.Usage);
        if (active.Values.Any(x => x.Type == LeaveLedgerEntryType.Reservation) ||
            LeaveBenefitUsage.AllocatedMinutes(award, proof.Allocations, active) != proof.ConsumedMinutes)
            throw AttendanceOperation.Conflict("leave_benefit_correction_evidence_conflict");
        return proof;
    }

    public static int ProtectedMinutes(LeaveBenefitAwardProof award, LeaveBenefitCorrectionProof proof,
        IReadOnlyList<LeaveBenefitUsageEntry> current)
    {
        var active = LeaveBenefitUsage.Active(award.EmployeeId, award.YearFrom, award.YearTo, current);
        var indexed = current.ToDictionary(x => x.Id);
        if (proof.Usage.Any(x => !indexed.TryGetValue(x.Id, out var retained) || retained != x))
            throw AttendanceOperation.Conflict("leave_benefit_correction_source_changed");
        return LeaveBenefitUsage.AllocatedMinutes(award, proof.Allocations, active, allowReleased: true);
    }

    public static LeaveBenefitView CorrectedView(LeaveBenefitAwardProof award, LeaveBenefitCorrectionProof correction, int protectedMinutes) =>
        View(award, false) with {
            Status = "corrected",
            EncashmentEligible = false,
            Correction = new(correction.Id, award.Minutes - correction.ConsumedMinutes, correction.ConsumedMinutes, protectedMinutes,
                correction.ConsumedMinutes > 0 ? "pending_admin_review" : "corrected_unused", correction.Allocations, correction.RecordedAtUtc)
        };
}
