using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;
using System.Text.Json.Serialization;

namespace Cosmix.Application.HR.Leaves;

public sealed record LeaveBenefitAwardProof(int SchemaVersion, Guid Id, Guid EmployeeId, Guid LeaveTypeId,
    LeaveKind Kind, DateOnly YearFrom, DateOnly YearTo, DateOnly AvailableFrom, int Minutes,
    EncashmentPolicySource Policy, LeaveBenefitKpiEvidence Kpi, Guid ActorAccountId, Guid ActorEmployeeId,
    string ActorRole, DateTimeOffset RecordedAtUtc, string Reason);
public sealed record LeaveBenefitRevocationProof(int SchemaVersion, Guid Id, Guid AwardId, Guid EmployeeId,
    string AwardFingerprint, Guid ActorAccountId, Guid ActorEmployeeId, string ActorRole, DateTimeOffset RecordedAtUtc, string Reason)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<LeaveBenefitUsageEntry>? ReleasedUsage { get; init; }
}

public sealed partial class LeaveBenefitEvidence(AttendanceOperation operation, ILeaveBenefitRepository repository) : ILeaveBenefitCreditReader
{
    public const string TargetType = "LeaveBenefit";
    public const string AwardEvent = "attendance.leave.benefit.awarded";
    public const string RevokeEvent = "attendance.leave.benefit.revoked";
    public static string Purpose(Guid employee, Guid id) => $"leave:benefit:{employee:N}:{id:N}";
    public static string SourceKey(DateOnly from) => "leave-benefit:" + from.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    public static string RevokeKey(Guid id) => $"leave-benefit-revoke:{id:N}";
    public static string Fingerprint(LeaveBenefitAwardProof proof) => LeaveProvenance.Digest(proof);
    public static DateOnly TargetYear(DateOnly sourceFrom, LeavePolicySettings settings) =>
        new(sourceFrom.Year + settings.Benefits!.TargetYearOffset, settings.YearStartMonth!.Value, settings.YearStartDay!.Value);

    public async Task<LeaveBenefitAwardProof> AwardAsync(LeaveBenefitRow row, CancellationToken ct)
    {
        var entry = row.Entry; var audit = row.Evidence;
        if (entry.EntryType != LeaveLedgerEntryType.Entitlement || entry.MinutesDelta <= 0 || entry.ReversesEntryId is not null ||
            entry.LeaveRequestId is not null || audit is null || audit.EventType != AwardEvent || audit.TargetType != TargetType ||
            audit.TargetId != entry.Id.ToString() || audit.EmployeeScopeId != entry.EmployeeId || audit.ProtectedChangesReference is null)
            throw AttendanceOperation.Conflict("leave_benefit_evidence_conflict");
        var proof = await operation.ReadPrivateAsync<LeaveBenefitAwardProof>(audit.ProtectedChangesReference, Purpose(entry.EmployeeId, entry.Id), ct);
        if (proof.SchemaVersion != 1 || proof.Id != entry.Id || proof.EmployeeId != entry.EmployeeId || proof.LeaveTypeId != entry.LeaveTypeId ||
            proof.Minutes != entry.MinutesDelta || proof.YearFrom != entry.LeaveYearStart || proof.YearTo != entry.LeaveYearEnd ||
            proof.ActorAccountId == Guid.Empty || proof.ActorEmployeeId == Guid.Empty || proof.ActorEmployeeId == entry.EmployeeId || !LeaveRoleCodes.IsRetainedAdmin(proof.ActorRole) ||
            proof.ActorRole != audit.ActorRoleCode || proof.ActorAccountId.ToString() != audit.ActorUserId ||
            proof.ActorAccountId.ToString() != entry.RecordedByUserId || proof.RecordedAtUtc != entry.RecordedAt ||
            proof.RecordedAtUtc != audit.OccurredAt || proof.Reason != entry.Reason || proof.Policy is null || proof.Kpi?.Score is null ||
            proof.Policy.PolicyId == Guid.Empty || proof.Policy.VersionId != entry.PolicyVersionId || proof.AvailableFrom != LeaveRules.LocalDate(proof.RecordedAtUtc))
            throw AttendanceOperation.Conflict("leave_benefit_evidence_conflict");
        var settings = LeavePolicySettings.Parse(proof.Policy.RulesJson, proof.Policy.Schema);
        var rules = settings.Benefits ?? throw AttendanceOperation.Conflict("leave_benefit_policy_not_configured");
        var score = proof.Kpi.Score;
        if (score.From.Year is < 1901 or > 9991 || proof.YearFrom.Year is < 1901 or > 9997)
            throw AttendanceOperation.Conflict("leave_benefit_evidence_conflict");
        KpiDisputeRules.Final(score, proof.EmployeeId, score.From);
        if (proof.Kind != rules.EligibleKind || proof.Minutes != rules.AwardMinutes || score.EmployeeId != entry.EmployeeId ||
            score.Id == Guid.Empty || score.ToExclusive != score.From.AddYears(1) || score.ExactScore < rules.MinimumKpiPercent ||
            score.ExactScore > 100 || score.FinalizedAtUtc > proof.RecordedAtUtc || score.ToExclusive > proof.AvailableFrom ||
            score.SourceFingerprint != proof.Kpi.VerifiedSourceFingerprint ||
            entry.IdempotencyKey != SourceKey(score.From) || proof.YearFrom != TargetYear(score.From, settings) ||
            proof.YearTo != proof.YearFrom.AddYears(1) || LeavePolicySettings.Year(proof.AvailableFrom, settings) != (proof.YearFrom, proof.YearTo) ||
            proof.AvailableFrom < proof.YearFrom || proof.AvailableFrom >= proof.YearTo ||
            proof.AvailableFrom.DayNumber - score.ToExclusive.DayNumber >= rules.AwardWindowDaysAfterSourceEnd ||
            proof.Policy.EffectiveFrom > proof.AvailableFrom || proof.Policy.EffectiveTo <= proof.AvailableFrom ||
            proof.Policy.PublishedAt > proof.RecordedAtUtc || !settings.EligibleStaffCategoryIds.Contains(proof.Policy.StaffCategoryId) ||
            !settings.ServiceEligible(proof.Policy.JoinedOn, proof.AvailableFrom) ||
            !(settings with { MinimumServiceMonths = rules.MinimumServiceMonths }).ServiceEligible(proof.Policy.JoinedOn, proof.AvailableFrom) ||
            proof.Policy.AssignmentIds is not { Count: > 0 and <= 1000 } || proof.Policy.AssignmentIds.Any(x => x == Guid.Empty) ||
            proof.Policy.AssignmentIds.Distinct().Count() != proof.Policy.AssignmentIds.Count)
            throw AttendanceOperation.Conflict("leave_benefit_evidence_conflict");
        return proof;
    }

    public async Task<bool> RevokedAsync(LeaveBenefitAwardProof proof, IReadOnlyList<LeaveBenefitRow> rows, CancellationToken ct)
    {
        var reversals = rows.Where(x => x.Entry.ReversesEntryId == proof.Id).ToArray();
        if (reversals.Length > 1) throw AttendanceOperation.Conflict("leave_benefit_reversal_conflict");
        if (reversals.Length == 0) return false;
        if (reversals[0].Evidence?.EventType == CorrectionEvent)
            return await CorrectionAsync(proof, rows, ct) is not null;
        var row = reversals[0]; var entry = row.Entry; var audit = row.Evidence;
        if (entry.EntryType != LeaveLedgerEntryType.Adjustment || entry.MinutesDelta != -proof.Minutes || entry.EmployeeId != proof.EmployeeId ||
            entry.LeaveTypeId != proof.LeaveTypeId || entry.PolicyVersionId != proof.Policy.VersionId || entry.LeaveRequestId is not null ||
            entry.LeaveYearStart != proof.YearFrom || entry.LeaveYearEnd != proof.YearTo || entry.IdempotencyKey != RevokeKey(proof.Id) ||
            audit is null || audit.EventType != RevokeEvent || audit.TargetType != TargetType || audit.TargetId != entry.Id.ToString() ||
            audit.EmployeeScopeId != proof.EmployeeId || audit.ProtectedChangesReference is null)
            throw AttendanceOperation.Conflict("leave_benefit_reversal_conflict");
        var reversal = await operation.ReadPrivateAsync<LeaveBenefitRevocationProof>(audit.ProtectedChangesReference, Purpose(proof.EmployeeId, entry.Id), ct);
        if (reversal.SchemaVersion is not (1 or 2) || reversal.Id != entry.Id || reversal.AwardId != proof.Id || reversal.EmployeeId != proof.EmployeeId ||
            reversal.AwardFingerprint != Fingerprint(proof) || reversal.ActorEmployeeId == Guid.Empty || reversal.ActorEmployeeId == proof.EmployeeId || reversal.ActorAccountId == Guid.Empty ||
            reversal.ActorAccountId.ToString() != audit.ActorUserId || reversal.ActorAccountId.ToString() != entry.RecordedByUserId ||
            !LeaveRoleCodes.IsRetainedAdmin(reversal.ActorRole) || audit.ActorRoleCode != reversal.ActorRole || reversal.RecordedAtUtc != entry.RecordedAt ||
            reversal.RecordedAtUtc != audit.OccurredAt || reversal.RecordedAtUtc < proof.RecordedAtUtc || reversal.Reason != entry.Reason)
            throw AttendanceOperation.Conflict("leave_benefit_reversal_conflict");
        if (reversal.SchemaVersion == 1 && reversal.ReleasedUsage is not null || reversal.SchemaVersion == 2 &&
            (reversal.ReleasedUsage is null || reversal.ReleasedUsage.Any(x => x is null || x.RecordedAt > reversal.RecordedAtUtc) ||
             LeaveBenefitUsage.Active(proof.EmployeeId, proof.YearFrom, proof.YearTo, reversal.ReleasedUsage).Count != 0))
            throw AttendanceOperation.Conflict("leave_benefit_reversal_conflict");
        return true;
    }

    public async Task<IReadOnlyList<LeaveBenefitCredit>> ReadAsync(Guid employee, DateOnly yearFrom, DateOnly yearTo, DateOnly useFrom, CancellationToken ct)
    {
        var rows = await repository.YearAsync(employee, yearFrom, yearTo, ct);
        if (rows.Count > 1000 || rows.Select(x => x.Entry.Id).Distinct().Count() != rows.Count || rows.Any(x => x.Entry.EmployeeId != employee ||
            x.Entry.LeaveYearStart != yearFrom || x.Entry.LeaveYearEnd != yearTo)) throw AttendanceOperation.Conflict("leave_benefit_history_conflict");
        var awards = rows.Where(x => x.Entry.EntryType == LeaveLedgerEntryType.Entitlement).ToArray();
        if (rows.Any(x => x.Entry.EntryType == LeaveLedgerEntryType.Adjustment && !awards.Any(a => a.Entry.Id == x.Entry.ReversesEntryId)))
            throw AttendanceOperation.Conflict("leave_benefit_reversal_conflict");
        var result = new List<LeaveBenefitCredit>();
        var sources = new HashSet<DateOnly>();
        var allocated = new Dictionary<Guid, long>();
        IReadOnlyList<LeaveBenefitUsageEntry>? currentUsage = null;
        foreach (var row in awards)
        {
            var proof = await AwardAsync(row, ct);
            if (!sources.Add(proof.Kpi.Score.From)) throw AttendanceOperation.Conflict("leave_benefit_source_already_awarded");
            var revoked = await RevokedAsync(proof, rows, ct);
            var correction = await CorrectionAsync(proof, rows, ct);
            if (correction is not null)
            {
                currentUsage ??= await repository.UsageAsync(employee, yearFrom, yearTo, ct);
                foreach (var allocation in correction.Allocations)
                {
                    allocated[allocation.LedgerEntryId] = allocated.GetValueOrDefault(allocation.LedgerEntryId) + allocation.Minutes;
                    var original = correction.Usage.Single(x => x.Id == allocation.LedgerEntryId);
                    if (allocated[allocation.LedgerEntryId] > -(long)original.Minutes)
                        throw AttendanceOperation.Conflict("leave_benefit_usage_already_allocated");
                }
                var remaining = ProtectedMinutes(proof, correction, currentUsage);
                if (remaining > 0 && proof.AvailableFrom <= useFrom)
                    result.Add(new(proof.Id, remaining, LeavePolicySettings.Parse(proof.Policy.RulesJson, proof.Policy.Schema).StandardDayMinutes,
                        proof.Kind, false) { ConsumptionOffset = true });
                continue;
            }
            if (revoked || proof.AvailableFrom > useFrom) continue;
            var settings = LeavePolicySettings.Parse(proof.Policy.RulesJson, proof.Policy.Schema);
            result.Add(new(proof.Id, proof.Minutes, settings.StandardDayMinutes, proof.Kind, settings.Benefits!.EncashmentEligible));
        }
        return result;
    }
    public static LeaveBenefitView View(LeaveBenefitAwardProof p, bool revoked) => new(p.Id, p.EmployeeId, p.LeaveTypeId,
        p.Kpi.Score.From, p.Kpi.Score.ToExclusive, p.YearFrom, p.YearTo, p.AvailableFrom, p.Minutes,
        LeavePolicySettings.Parse(p.Policy.RulesJson, p.Policy.Schema).StandardDayMinutes,
        LeavePolicySettings.Parse(p.Policy.RulesJson, p.Policy.Schema).Benefits!.EncashmentEligible,
        p.Policy.VersionId, p.Kpi.Score.Id, revoked ? "revoked" : "awarded", Fingerprint(p), p.RecordedAtUtc);
}
