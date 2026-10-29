using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Allowances;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Allowances;
using Cosmix.Domain.HR.AdvancesLoans;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

/// <summary>Module-specific payroll handoff repository. Approved overtime and allowance sources are
/// read with the same identity, stage and decision predicates as their owning modules; settlement
/// history is appended here so a handoff line and its settlement share one database transaction.</summary>
public sealed class PayrollHandoffRepository(CosmixDbContext db) : IPayrollHandoffRepository, IAllowanceSettlementStore
{
    public Task<ApprovalRequest?> ApprovalAsync(Guid id,CancellationToken ct)=>db.ApprovalRequests.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);
    public async Task<IReadOnlyList<ApprovalDecision>> DecisionRecordsAsync(Guid id,CancellationToken ct)=>await db.ApprovalDecisions.AsNoTracking().Where(x=>x.ApprovalRequestId==id).ToArrayAsync(ct);
    private const int MaximumSourceRows = 65536;

    public async Task<Cosmix.Domain.Common.Audit.AuditEvent?> LoanPostingAuditAsync(Guid ledgerEntryId, CancellationToken ct)
    {
        var target = ledgerEntryId.ToString();
        var rows = await db.AuditEvents.AsNoTracking().Where(x => x.TargetType == "Attendance" &&
            x.TargetId == target && x.EventType == "attendance.payroll-handoff.loan-posting").Take(2).ToArrayAsync(ct);
        if (rows.Length > 1) throw AttendanceOperation.Conflict("payroll_loan_posting_audit_conflict");
        return rows.SingleOrDefault();
    }

    public async Task<IReadOnlyList<PayrollHandoffOvertimeSource>> OvertimeAsync(IReadOnlyList<Guid> employeeIds,
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken ct)
    {
        if (employeeIds.Count == 0) return [];
        var fromDate = DateOnly.FromDateTime(fromUtc.ToOffset(TimeSpan.FromHours(5)).DateTime);
        var toDate = DateOnly.FromDateTime(toUtcExclusive.ToOffset(TimeSpan.FromHours(5)).DateTime);
        var rows = await (from request in db.OvertimeRequests.AsNoTracking()
            join approval in db.ApprovalRequests.AsNoTracking() on request.ApprovalRequestId equals approval.Id
            join session in db.WorkSessions.AsNoTracking() on request.WorkSessionId equals session.Id
            join decision in db.ApprovalDecisions.AsNoTracking() on approval.Id equals decision.ApprovalRequestId
            where session.EmployeeId == request.EmployeeId && approval.RequestType == "attendance.overtime" &&
                approval.Stage == 1 && approval.RequesterUserId == request.RequestedBy &&
                approval.SnapshotPrivateReference == request.PrivateEvidenceReference &&
                approval.State == ApprovalState.Approved && session.AttendanceDate >= fromDate && session.AttendanceDate < toDate &&
                decision.Decision == ApprovalState.Approved && decision.DecidedAt == approval.ClosedAt &&
                decision.ActorUserId != approval.RequesterUserId && decision.ProtectedChangeReference != null &&
                employeeIds.Contains(request.EmployeeId)
            orderby request.EmployeeId, request.Id
            select new PayrollHandoffOvertimeSource(request.Id, request.EmployeeId, approval.SubjectId,
                session.AttendanceDate, approval.Id, decision.Id, approval.RequesterUserId, decision.DecidedAt,
                request.PrivateEvidenceReference, decision.ProtectedChangeReference!, request.PolicyVersionId))
            .Take(MaximumSourceRows).ToArrayAsync(ct);
        if (rows.Length == MaximumSourceRows) throw AttendanceOperation.Conflict("payroll_handoff_source_limit");
        return rows;
    }

    public async Task<IReadOnlyList<PayrollHandoffAllowanceSource>> AllowancesAsync(IReadOnlyList<Guid> employeeIds,
        DateOnly fromDate, DateOnly toExclusive, int skip, int take, CancellationToken ct)
    {
        if (employeeIds.Count == 0) return [];
        if (skip < 0 || take is < 1 or > 100) throw AttendanceOperation.Invalid("invalid_payroll_handoff_page");
        var rows = await (from claim in db.AllowanceClaims.AsNoTracking()
            join approval in db.ApprovalRequests.AsNoTracking() on claim.ApprovalRequestId equals approval.Id
            where approval.RequestType == "attendance.allowance" && approval.Stage == 1 &&
                approval.SubjectId == claim.Id && approval.SnapshotPrivateReference == claim.CalculationPrivateReference &&
                approval.State == ApprovalState.Approved && claim.SettlementState == ClaimSettlementState.NotScheduled &&
                claim.PayableDate >= fromDate && claim.PayableDate < toExclusive && employeeIds.Contains(claim.EmployeeId)
            orderby claim.EmployeeId, claim.PayableDate, claim.Id
            select new PayrollHandoffAllowanceSource(claim.Id, claim.EmployeeId, claim.PayableDate, claim.ClaimedAmount,
                claim.Currency, claim.PolicyVersionId, claim.SubmissionVersion, approval.Id, claim.CalculationPrivateReference))
            .Skip(skip).Take(take).ToArrayAsync(ct);
        return rows;
    }

    public async Task<IReadOnlyList<PayrollHandoffLoanScheduleSource>> LoanSchedulesAsync(IReadOnlyList<Guid> employeeIds,
        DateOnly fromDate, DateOnly toExclusive, CancellationToken ct)
    {
        if (employeeIds.Count == 0) return [];
        var rows = await (from schedule in db.RepaymentScheduleEntries.AsNoTracking()
            join agreement in db.AdvanceLoanAgreements.AsNoTracking() on schedule.AdvanceLoanAgreementId equals agreement.Id
            join request in db.AdvanceLoanRequests.AsNoTracking() on agreement.AdvanceLoanRequestId equals request.Id
            join approval in db.ApprovalRequests.AsNoTracking() on agreement.ApprovalRequestId equals approval.Id
            where approval.State == ApprovalState.Approved &&
                schedule.DueDate >= fromDate && schedule.DueDate < toExclusive &&
                employeeIds.Contains(request.EmployeeId) &&
                !db.RepaymentLedgerEntries.Any(r => r.RepaymentScheduleEntryId == schedule.Id && r.Kind == RepaymentEntryKind.PayrollDeduction) &&
                !db.PayrollHandoffLines.Any(l => l.SourceId == schedule.Id && l.Component == PayrollComponentKind.LoanRepayment &&
                    l.PayrollHandoffBatch.ReviewState != "cancelled" && l.PayrollHandoffBatch.ReviewState != "rejected" &&
                    l.PayrollHandoffBatch.ReviewState != "returned")
            orderby request.EmployeeId, schedule.DueDate, schedule.Sequence
            select new PayrollHandoffLoanScheduleSource(schedule.Id, request.EmployeeId, agreement.Id,
                schedule.Sequence, schedule.DueDate, schedule.PrincipalAmount + schedule.OtherApprovedAmount,
                schedule.Currency, agreement.ApprovalRequestId, agreement.ApprovedTermsPrivateReference, schedule.OtherApprovedAmount))
            .Take(MaximumSourceRows).ToArrayAsync(ct);
        return rows;
    }

    public async Task<IReadOnlyList<PayrollHandoffFuelClaimSource>> FuelClaimsAsync(IReadOnlyList<Guid> employeeIds,
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken ct)
    {
        if (employeeIds.Count == 0) return [];
        var rows = await (from claim in db.FuelClaims.AsNoTracking()
            join approval in db.ApprovalRequests.AsNoTracking() on claim.ApprovalRequestId equals approval.Id
            where approval.State == ApprovalState.Approved &&
                claim.CreatedAt >= fromUtc && claim.CreatedAt < toUtcExclusive &&
                employeeIds.Contains(claim.EmployeeId) &&
                !db.PayrollHandoffLines.Any(l => l.SourceId == claim.Id && l.Component == PayrollComponentKind.FuelClaim &&
                    l.PayrollHandoffBatch.ReviewState != "cancelled" && l.PayrollHandoffBatch.ReviewState != "rejected" &&
                    l.PayrollHandoffBatch.ReviewState != "returned")
            orderby claim.EmployeeId, claim.CreatedAt, claim.Id
            select new PayrollHandoffFuelClaimSource(claim.Id, claim.EmployeeId, claim.VehicleTripId,
                claim.ClaimedAmount, claim.Currency, claim.PolicyVersionId, approval.Id, claim.CalculationPrivateReference))
            .Take(MaximumSourceRows).ToArrayAsync(ct);
        return rows;
    }

    public async Task<Guid?> FindAgreementIdForScheduleAsync(Guid scheduleId, CancellationToken ct) =>
        await db.RepaymentScheduleEntries.AsNoTracking()
            .Where(s => s.Id == scheduleId)
            .Select(s => (Guid?)s.AdvanceLoanAgreementId)
            .FirstOrDefaultAsync(ct);

    public async Task<PayrollHandoffBatch?> FindBatchAsync(Guid id, bool tracked, CancellationToken ct) =>
        await (tracked ? db.PayrollHandoffBatches : db.PayrollHandoffBatches.AsNoTracking())
            .Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> PeriodPreparedAsync(DateOnly start, DateOnly endExclusive, CancellationToken ct) =>
        db.PayrollHandoffBatches.AsNoTracking().AnyAsync(x => x.PeriodStart == start && x.PeriodEnd == endExclusive &&
            x.ReviewState != "cancelled" && x.ReviewState != "rejected" && x.ReviewState != "returned", ct);

    // Called inside the serialized authorization write transaction. Legacy/empty batches block
    // the entire period because their employee coverage cannot safely authorize another salary.
    public Task<bool> EmployeesPreparedAsync(DateOnly start, DateOnly endExclusive, IReadOnlyList<Guid> employeeIds,
        CancellationToken ct) => db.PayrollHandoffBatches.AsNoTracking().AnyAsync(x =>
            x.PeriodStart == start && x.PeriodEnd == endExclusive && x.ReviewState != "cancelled" &&
            x.ReviewState != "rejected" && x.ReviewState != "returned" &&
            (x.CalculationVersion != "salary-v2" || !x.Lines.Any() || x.Lines.Any(line => employeeIds.Contains(line.EmployeeId))), ct);

    public Task<bool> ProcessingReferenceExistsAsync(Guid batchId, string reference, CancellationToken ct) =>
        db.PayrollHandoffProcessingRecords.AsNoTracking().AnyAsync(x =>
            x.PayrollHandoffBatchId == batchId && x.ExternalProcessingReference == reference, ct);

    public async Task<IReadOnlyList<Guid>> DecidersAsync(Guid approvalRequestId, CancellationToken ct)
    {
        var accounts = await db.ApprovalDecisions.AsNoTracking().Where(x => x.ApprovalRequestId == approvalRequestId)
            .OrderBy(x => x.DecidedAt).ThenBy(x => x.Id).Select(x => x.ActorUserId).ToArrayAsync(ct);
        var deciders = new List<Guid>(accounts.Length);
        foreach (var account in accounts)
            if (Guid.TryParse(account, out var id)) deciders.Add(id);
        return deciders;
    }

    public async Task<IReadOnlyList<PayrollHandoffProcessingRecord>> ProcessingAsync(Guid batchId, CancellationToken ct) =>
        await db.PayrollHandoffProcessingRecords.AsNoTracking()
            .Where(x => x.PayrollHandoffBatchId == batchId).OrderBy(x => x.ProcessedAt).ThenBy(x => x.Id)
            .ToArrayAsync(ct);

    public async Task<PagedResult<PayrollHandoffBatch>> BatchesAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = db.PayrollHandoffBatches.AsNoTracking().Include(x => x.Lines);
        var ordered = query.OrderByDescending(x => x.PeriodStart).ThenByDescending(x => x.Id);
        return new(await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct),
            await query.CountAsync(ct), page, pageSize);
    }

    public void Add(PayrollHandoffBatch batch) => db.PayrollHandoffBatches.Add(batch);

    public async Task<IReadOnlyDictionary<Guid, string>> EmployeeNamesAsync(IReadOnlyCollection<Guid> employeeIds,
        CancellationToken ct)
    {
        var ids = employeeIds.ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, string>();
        var rows = await db.Employees.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.FullLegalName })
            .ToArrayAsync(ct);
        return rows.ToDictionary(x => x.Id, x => x.FullLegalName ?? string.Empty);
    }

    public void Add(ApprovalRequest approval, ApprovalDecision decision)
    {
        db.ApprovalRequests.Add(approval);
        db.ApprovalDecisions.Add(decision);
    }

    public void Add(PayrollHandoffProcessingRecord record) => db.PayrollHandoffProcessingRecords.Add(record);

    public void ExpectVersion(PayrollHandoffBatch batch, byte[] version) =>
        db.Entry(batch).Property(x => x.RowVersion).OriginalValue = version;

    public async Task TrackApprovalEvidenceAsync(Guid approvalRequestId, CancellationToken ct)
    {
        if (db.ChangeTracker.Entries<ApprovalRequest>().Any(e => e.Entity.Id == approvalRequestId)) return;
        if (!await db.ApprovalRequests.AnyAsync(x => x.Id == approvalRequestId, ct))
            throw AttendanceOperation.Conflict("allowance_approval_evidence_unavailable");
    }

    public void Add(AllowanceSettlementRecord record) => db.AllowanceSettlementRecords.Add(record);

    public void Add(RepaymentLedgerEntry entry) => db.RepaymentLedgerEntries.Add(entry);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
