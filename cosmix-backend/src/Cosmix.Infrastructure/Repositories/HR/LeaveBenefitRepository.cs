using Cosmix.Application.Attendance.Common;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class LeaveBenefitRepository(CosmixDbContext db) : ILeaveBenefitRepository
{
    public async Task<IReadOnlyList<LeaveBenefitRow>> YearAsync(Guid employee, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.LeaveLedgerEntries.Where(x => x.EmployeeId == employee && x.LeaveYearStart < to && x.LeaveYearEnd > from &&
            (x.EntryType == LeaveLedgerEntryType.Entitlement || x.EntryType == LeaveLedgerEntryType.Adjustment))
            .OrderBy(x => x.RecordedAt).ThenBy(x => x.Id).Take(1001).ToArrayAsync(ct);
        if (rows.Length > 1000) throw AttendanceOperation.Conflict("leave_benefit_history_limit");
        if (rows.Any(x => x.LeaveYearStart != from || x.LeaveYearEnd != to))
            throw AttendanceOperation.Conflict("leave_policy_year_transition_unavailable");
        return await Attach(rows, ct);
    }
    public async Task<LeaveBenefitRow?> FindAsync(Guid employee, Guid id, CancellationToken ct)
    {
        var row = await db.LeaveLedgerEntries.SingleOrDefaultAsync(x => x.EmployeeId == employee && x.Id == id &&
            (x.EntryType == LeaveLedgerEntryType.Entitlement || x.EntryType == LeaveLedgerEntryType.Adjustment), ct);
        return row is null ? null : (await Attach([row], ct)).Single();
    }
    private async Task<IReadOnlyList<LeaveBenefitRow>> Attach(LeaveLedgerEntry[] rows, CancellationToken ct)
    {
        var ids = rows.Select(x => x.Id.ToString()).ToArray();
        var proofs = await db.Set<AuditEvent>().Where(x => x.TargetType == LeaveBenefitEvidence.TargetType && ids.Contains(x.TargetId))
            .Take(rows.Length * 2 + 1).ToArrayAsync(ct);
        if (proofs.GroupBy(x => x.TargetId).Any(x => x.Count() != 1)) throw AttendanceOperation.Conflict("leave_benefit_evidence_conflict");
        return rows.Select(x => new LeaveBenefitRow(x, proofs.SingleOrDefault(p => p.TargetId == x.Id.ToString()))).ToArray();
    }
    public Task<bool> SourceExistsAsync(Guid employee, string sourceKey, CancellationToken ct) =>
        db.LeaveLedgerEntries.AnyAsync(x => x.EmployeeId == employee && x.IdempotencyKey == sourceKey, ct);
    public Task<Guid?> RecipientAsync(Guid employee, CancellationToken ct) => db.Set<UserAccount>()
        .Where(x => x.EmployeeId == employee && x.Status == AccountStatus.Active).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
    public async Task<IReadOnlyList<LeaveBenefitUsageEntry>> UsageAsync(Guid employee, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.LeaveLedgerEntries.Where(x => x.EmployeeId == employee && x.LeaveYearStart < to && x.LeaveYearEnd > from &&
            (x.EntryType == LeaveLedgerEntryType.Utilization || x.EntryType == LeaveLedgerEntryType.Reservation ||
             x.EntryType == LeaveLedgerEntryType.Release || x.EntryType == LeaveLedgerEntryType.Encashment))
            .OrderBy(x => x.Id).Take(10001).Select(x => new { Entry = x, x.PolicyVersion.RulesJson, x.PolicyVersion.RulesSchemaVersion,
                x.PolicyVersion.Authority, x.PolicyVersion.Policy.Category }).ToArrayAsync(ct);
        if (rows.Length > 10000) throw AttendanceOperation.Conflict("leave_ledger_history_limit");
        if (rows.Any(x => x.Authority != PolicyAuthority.DirectHr || x.Category != PolicyCategory.Leave))
            throw AttendanceOperation.Conflict("leave_benefit_usage_conflict");
        var result = rows.Select(x => new LeaveBenefitUsageEntry(x.Entry.Id, x.Entry.EmployeeId, x.Entry.LeaveTypeId,
            x.Entry.PolicyVersionId, x.Entry.LeaveYearStart, x.Entry.LeaveYearEnd, x.Entry.EntryType, x.Entry.MinutesDelta,
            x.Entry.LeaveRequestId, x.Entry.ReversesEntryId, x.Entry.RecordedAt,
            LeavePolicySettings.Parse(x.RulesJson, x.RulesSchemaVersion).StandardDayMinutes)).ToArray();
        _ = LeaveBenefitUsage.Active(employee, from, to, result);
        return result;
    }
    public void Add(LeaveLedgerEntry entry, AuditEvent evidence)
    { db.LeaveLedgerEntries.Add(entry); db.Set<AuditEvent>().Add(evidence); }
    public async Task<IReadOnlyList<AuditEvent>> ResolutionFactsAsync(Guid employeeId, Guid correctionId, CancellationToken ct)
    {
        RequireResolutionTransaction();
        var target = correctionId.ToString();
        return await db.Set<AuditEvent>().AsNoTracking().Where(x => x.EmployeeScopeId == employeeId &&
            x.TargetType == LeaveBenefitEvidence.ResolutionTargetType && x.TargetId == target)
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Take(2).ToArrayAsync(ct);
    }
    public void AddResolution(AuditEvent evidence)
    {
        RequireResolutionTransaction();
        db.Set<AuditEvent>().Add(evidence);
    }
    public Task<IdempotencyReceipt?> ResolutionReceiptAsync(Guid actorAccountId, string key, CancellationToken ct)
    {
        RequireResolutionTransaction();
        var actor = actorAccountId.ToString();
        return db.Set<IdempotencyReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.ActorUserId == actor &&
            x.Operation == LeaveBenefitService.ResolutionOperation && x.Key == key, ct);
    }
    private void RequireResolutionTransaction()
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Benefit resolution requires the authorized serializable transaction.");
    }
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
