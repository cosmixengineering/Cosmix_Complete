using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

public sealed partial class LeaveBenefitService
{
    public Task<LeaveBenefitImpact> CorrectionImpactAsync(Guid employee, Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync<LeaveBenefitImpact>(LeaveBenefitPermissions.Review, "attendance.leave.benefit.correction-impact", employee,
            context, async (actor, token) => {
                await RequireAdmin(actor, employee, token);
                var row = await repository.FindAsync(employee, id, token) ?? throw AttendanceOperation.Missing("leave_benefit_not_found");
                var award = await evidence.AwardAsync(row, token);
                var usage = await repository.UsageAsync(employee, award.YearFrom, award.YearTo, token);
                _ = LeaveBenefitUsage.Active(employee, award.YearFrom, award.YearTo, usage);
                var rows = await repository.YearAsync(employee, award.YearFrom, award.YearTo, token);
                var correction = await evidence.CorrectionAsync(award, rows, token);
                var resolution = correction is null ? null : LeaveBenefitEvidence.ResolutionView(award, correction, usage,
                    await evidence.WaiverAsync(award, correction, usage, token));
                await RequireAdmin(actor, employee, token);
                return new(LeaveBenefitUsage.Fingerprint(usage), usage) { Resolution = resolution };
            }, ct);

    public Task<LeaveBenefitView> CorrectAsync(Guid employee, Guid id, CorrectLeaveBenefitRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (employee == Guid.Empty || id == Guid.Empty || request is null || request.ExpectedEvidenceFingerprint is not { Length: 64 } ||
            request.ExpectedUsageFingerprint is not { Length: 64 } || request.ConsumedUsage is null || request.ConsumedUsage.Count > 1000 ||
            request.ConsumedUsage.Any(x => x is null))
            throw AttendanceOperation.Invalid("invalid_leave_benefit_correction");
        return operation.WriteAsync(LeaveBenefitPermissions.Review, "attendance.leave.benefit.correct", employee, key,
            new { id, request }, request.Reason, context, async (actor, token) => {
                await RequireAdmin(actor, employee, token);
                var row = await repository.FindAsync(employee, id, token) ?? throw AttendanceOperation.Missing("leave_benefit_not_found");
                var award = await evidence.AwardAsync(row, token);
                if (LeaveBenefitEvidence.Fingerprint(award) != request.ExpectedEvidenceFingerprint)
                    throw AttendanceOperation.Conflict("leave_benefit_source_changed");
                var (usage, consumed) = await PrepareCorrection(award, request, actor.Now, token);
                var entry = new LeaveLedgerEntry(employee, award.LeaveTypeId, award.YearFrom, award.YearTo,
                    LeaveLedgerEntryType.Adjustment, -award.Minutes, null, id, award.Policy.VersionId, request.Reason.Trim(),
                    LeaveBenefitEvidence.RevokeKey(id), actor.Identity.AccountId.ToString(), actor.Now);
                var proof = new LeaveBenefitCorrectionProof(1, entry.Id, id, employee, request.ExpectedEvidenceFingerprint,
                    usage, request.ConsumedUsage.OrderBy(x => x.LedgerEntryId).ToArray(), consumed, actor.Identity.AccountId,
                    actor.Identity.EmployeeId, actor.Now, request.Reason.Trim());
                var reference = await Store(proof, employee, entry.Id, token);
                _ = await PrepareCorrection(award, request, clock.GetUtcNow(), token);
                await RequireAdmin(actor, employee, token);
                repository.Add(entry, Audit(LeaveBenefitEvidence.CorrectionEvent, entry, actor, reference, context));
                await Notify(entry, context, token);
                await RequireAdmin(actor, employee, token);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<LeaveBenefitView>(entry.Id, LeaveBenefitEvidence.View(award, false),
                    LeaveBenefitEvidence.CorrectedView(award, proof, consumed));
            }, ct, beforeReplay: (actor, token) => RequireAdmin(actor, employee, token),
            beforeCommit: (actor, token) => RequireAdmin(actor, employee, token));
    }

    private async Task<(IReadOnlyList<LeaveBenefitUsageEntry> Usage, int Consumed)> PrepareCorrection(LeaveBenefitAwardProof award,
        CorrectLeaveBenefitRequest request, DateTimeOffset now, CancellationToken ct)
    {
        var today = LeaveRules.LocalDate(now);
        if (today < award.AvailableFrom || await leaves.AnyLockedAsync(award.AvailableFrom, today, ct))
            throw AttendanceOperation.Conflict("leave_benefit_period_locked");
        var rows = await repository.YearAsync(award.EmployeeId, award.YearFrom, award.YearTo, ct);
        if (await evidence.RevokedAsync(award, rows, ct)) throw AttendanceOperation.Conflict("leave_benefit_already_revoked");
        // Also verify every older award/correction before accepting shared usage allocation.
        _ = await evidence.ReadAsync(award.EmployeeId, award.YearFrom, award.YearTo, today, ct);
        var usage = await repository.UsageAsync(award.EmployeeId, award.YearFrom, award.YearTo, ct);
        var active = LeaveBenefitUsage.Active(award.EmployeeId, award.YearFrom, award.YearTo, usage);
        if (LeaveBenefitUsage.Fingerprint(usage) != request.ExpectedUsageFingerprint || usage.Any(x => x.RecordedAt > now))
            throw AttendanceOperation.Conflict("leave_benefit_usage_changed");
        if (active.Values.Any(x => x.Type == LeaveLedgerEntryType.Reservation))
            throw AttendanceOperation.Conflict("leave_benefit_pending_reservation");
        var consumed = LeaveBenefitUsage.AllocatedMinutes(award, request.ConsumedUsage, active);
        var allocated = request.ConsumedUsage.ToDictionary(x => x.LedgerEntryId, x => (long)x.Minutes);
        foreach (var row in rows.Where(x => x.Entry.EntryType == LeaveLedgerEntryType.Entitlement))
        {
            var other = await evidence.AwardAsync(row, ct);
            var prior = await evidence.CorrectionAsync(other, rows, ct);
            if (prior is null) continue;
            foreach (var allocation in prior.Allocations)
                allocated[allocation.LedgerEntryId] = allocated.GetValueOrDefault(allocation.LedgerEntryId) + allocation.Minutes;
        }
        foreach (var (sourceId, amount) in allocated)
        {
            var source = usage.SingleOrDefault(x => x.Id == sourceId);
            if (source is null || amount > -(long)source.Minutes)
                throw AttendanceOperation.Conflict("leave_benefit_usage_already_allocated");
        }
        return (usage, consumed);
    }
}
