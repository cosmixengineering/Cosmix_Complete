using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.PolicyEffects;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class WorkTreatmentRepository(CosmixDbContext db, IRosterRepository rosters,
    AttendanceOperation operation, WorkTreatmentCorrectionProofs correctionProofs) : IWorkTreatmentRepository
{
    public void RequireTransaction() {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Work-treatment reads/writes require the authorized serializable transaction.");
    }
    public async Task<WorkTreatmentEmployment?> EmploymentAsync(Guid employeeId, DateOnly date, CancellationToken ct) {
        var rows = await (from a in db.EmployeeAssignments.AsNoTracking()
            join c in db.StaffCategories.AsNoTracking() on a.StaffCategoryId equals c.Id
            where a.EmployeeId == employeeId && a.EffectiveFrom <= date && (a.EffectiveTo == null || date < a.EffectiveTo)
            select new WorkTreatmentEmployment(a.Id, c.Id, c.Code, a.SiteId, a.BranchId, a.EffectiveFrom, a.EffectiveTo)).Take(2).ToArrayAsync(ct);
        if (rows.Length > 1) throw AttendanceOperation.Conflict("work_treatment_employment_history_conflict");
        return rows.SingleOrDefault();
    }
    internal IQueryable<Cosmix.Domain.Attendance.WorkSessions.WorkSession> SessionQuery(Guid employeeId, DateOnly from, DateOnly to) =>
        db.WorkSessions.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.AttendanceDate >= from && x.AttendanceDate <= to &&
            !db.WorkSessions.Any(n => n.SessionKey == x.SessionKey && n.Revision > x.Revision)).OrderBy(x => x.AttendanceDate).ThenBy(x => x.SessionKey).Take(513);
    public async Task<IReadOnlyList<WorkTreatmentSession>> SessionsAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct) {
        RequireTransaction();
        var rows = await SessionQuery(employeeId, from, to).ToArrayAsync(ct);
        if (rows.Length > 512 || rows.Select(x => x.SessionKey).Distinct().Count() != rows.Length) throw AttendanceOperation.Conflict("work_treatment_session_limit");
        var rawIds = rows.SelectMany(x => new[] { x.CheckInPunchId, x.CheckOutPunchId }).OfType<Guid>().Distinct().ToArray();
        var raw = await db.RawPunchEvents.AsNoTracking().Where(x => x.EmployeeId == employeeId && rawIds.Contains(x.Id)).ToArrayAsync(ct);
        var assessed = await db.PunchAssessments.AsNoTracking().Where(x => x.EmployeeId == employeeId && rawIds.Contains(x.RawPunchEventId) &&
            !db.PunchAssessments.Any(n => n.RawPunchEventId == x.RawPunchEventId && n.Revision > x.Revision)).ToArrayAsync(ct);
        var proofIds = assessed.Select(x => x.FaceVerificationAttemptId).OfType<Guid>().ToArray();
        var proofs = await db.FaceVerificationAttempts.AsNoTracking().Where(x => x.EmployeeId == employeeId && proofIds.Contains(x.Id)).ToArrayAsync(ct);
        var corrected = rows.Select(x => x.CorrectionId).OfType<Guid>().ToArray();
        var corrections = await (from c in db.AttendanceCorrections.AsNoTracking()
            join a in db.ApprovalRequests.AsNoTracking() on c.ApprovalRequestId equals a.Id
            join d in db.ApprovalDecisions.AsNoTracking() on a.Id equals d.ApprovalRequestId
            where c.EmployeeId == employeeId && corrected.Contains(c.Id) && c.AttendanceDate >= @from && c.AttendanceDate <= to &&
                a.RequestType == "attendance.correction" && a.Stage == 1 && a.State == ApprovalState.Approved && a.ClosedAt == d.DecidedAt &&
                a.RequesterUserId == c.RequestedBy && a.SnapshotPrivateReference == c.PrivateEvidenceReference &&
                d.Decision == ApprovalState.Approved && d.ActorUserId != a.RequesterUserId && d.ProtectedChangeReference != null &&
                db.ApprovalDecisions.Count(x => x.ApprovalRequestId == a.Id) == 1
            select new { Correction = c, Approval = a, Decision = d }).ToArrayAsync(ct);
        var result = new List<WorkTreatmentSession>();
        foreach (var s in rows) {
            var first = raw.SingleOrDefault(x => x.Id == s.CheckInPunchId);
            var last = raw.SingleOrDefault(x => x.Id == s.CheckOutPunchId);
            var a = assessed.SingleOrDefault(x => x.RawPunchEventId == s.CheckInPunchId);
            var b = assessed.SingleOrDefault(x => x.RawPunchEventId == s.CheckOutPunchId);
            var proof = proofs.SingleOrDefault(x => x.Id == a?.FaceVerificationAttemptId);
            var valid = first is { Action: PunchAction.CheckIn, IsOffline: false } && last is { Action: PunchAction.CheckOut, IsOffline: false } &&
                Accepted(a) && Accepted(b) && s.StartsAtUtc == first.ReceivedAtUtc && s.EndsAtUtc == last.ReceivedAtUtc &&
                proof is { Outcome: FaceVerificationOutcome.Passed, MatchPassed: true, LivenessPassed: true } && proof.ConsumedByPunchId == first.Id &&
                proof.ConsumedAtUtc.HasValue && proof.RegisteredDeviceId == first.RegisteredDeviceId && proof.PunchIdempotencyKey == first.IdempotencyKey &&
                first.SiteId == s.SiteId && last.SiteId == s.SiteId && first.BranchId == s.BranchId && last.BranchId == s.BranchId &&
                first.BusinessDate == s.AttendanceDate && last.BusinessDate == s.AttendanceDate && first.ShiftVersionId == s.ShiftVersionId && last.ShiftVersionId == s.ShiftVersionId;
            Guid? decisionId = null;
            if (s.CorrectionId.HasValue) {
                var c = corrections.SingleOrDefault(x => x.Correction.Id == s.CorrectionId);
                valid = false;
                if (c is not null) {
                    var snapshot = await operation.ReadPrivateAsync<CorrectionEvidenceSnapshot>(c.Correction.PrivateEvidenceReference,
                        $"attendance:correction:{employeeId:N}:proposal", ct);
                    valid = await correctionProofs.VerifyAsync(s, c.Correction, c.Approval, c.Decision, snapshot, ct);
                    if (valid) decisionId = c.Decision.Id;
                }
            }
            if (s.BranchId.HasValue && (first is not null && (first.OfficeRosterAssignmentId != s.OfficeRosterAssignmentId || first.EmployeeAssignmentId != s.EmployeeAssignmentId || first.OfficeGeofenceVersionId != s.OfficeGeofenceVersionId) ||
                last is not null && (last.OfficeRosterAssignmentId != s.OfficeRosterAssignmentId || last.EmployeeAssignmentId != s.EmployeeAssignmentId || last.OfficeGeofenceVersionId != s.OfficeGeofenceVersionId))) valid = false;
            if (valid && !await correctionProofs.EligibleAsync(s, ct)) valid = false;
            result.Add(new(s.Id, s.SessionKey, s.Revision, s.AttendanceDate, s.StartsAtUtc, s.EndsAtUtc, s.SiteId, s.BranchId, s.ShiftVersionId,
                s.PolicyVersionId, s.EmployeeAssignmentId, s.OfficeRosterAssignmentId, s.OfficeGeofenceVersionId, s.HolidayCalendarId,
                s.CheckInPunchId, s.CheckOutPunchId, a?.Id, b?.Id, s.CorrectionId, decisionId, valid, s.EndsContinuousDuty));
        }
        return result;
    }
    private static bool Accepted(PunchAssessment? assessment) => assessment?.Outcome is PunchAssessmentOutcome.Accepted or PunchAssessmentOutcome.AcceptedWithException;
    public async Task<IReadOnlyList<AttendanceResourceScope>> ScopesAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct) {
        RequireTransaction();
        if (employeeId == Guid.Empty || from == default || to < from || to == DateOnly.MaxValue || to.DayNumber - from.DayNumber > 366)
            throw AttendanceOperation.Invalid("invalid_work_treatment_scope_range");
        if (!await db.Employees.AnyAsync(x => x.Id == employeeId, ct)) throw AttendanceOperation.Missing();
        var history = await rosters.ResourcesAsync(employeeId, from, to.AddDays(1), ct);
        var groups = history.Select(x => new AttendanceResourceScope(x.Alternatives.ToArray())).ToList();
        var sourceSites = await db.WorkSessions.AsNoTracking().Where(s => s.EmployeeId == employeeId && s.AttendanceDate >= from && s.AttendanceDate <= to &&
            !db.EmployeeAssignments.Any(a => a.EmployeeId == employeeId && a.EffectiveFrom <= s.AttendanceDate &&
                (a.EffectiveTo == null || s.AttendanceDate < a.EffectiveTo) && a.SiteId == s.SiteId))
            .Select(x => new { x.AttendanceDate, x.SiteId }).Distinct().Take(4097).ToArrayAsync(ct);
        groups.AddRange(sourceSites.Select(x => new AttendanceResourceScope([new(EmployeeId: employeeId, SiteId: x.SiteId)])));
        if (groups.Count == 0) groups.Add(new([new(EmployeeId: employeeId)]));
        if (!AttendanceScopeEvaluation.IsValid(groups)) throw AttendanceOperation.Conflict("work_treatment_scope_limit");
        return groups;
    }
    public async Task<IReadOnlyList<WorkTreatmentDocument>> DocumentsAsync(Guid employeeId, IReadOnlyList<Guid> ids, DateOnly from, DateOnly to, CancellationToken ct) {
        RequireTransaction();
        var docs = await db.EmployeeDocuments.AsNoTracking().Where(x => x.EmployeeId == employeeId && ids.Contains(x.Id)).Take(33).ToArrayAsync(ct);
        if (docs.Length != ids.Count || docs.Any(x => !x.IsAvailableOn(from) || !x.IsAvailableOn(to) || x.VerifiedByUserId is null))
            throw AttendanceOperation.Conflict("work_treatment_verified_documents_required");
        return docs.OrderBy(x => x.Id).Select(x => new WorkTreatmentDocument(x.Id, x.Sha256, x.HistoryRevision,
            Convert.ToBase64String(x.RowVersion), x.VerifiedAt!.Value, x.VerifiedByUserId!)).ToArray();
    }
    public Task<bool> LockedAsync(DateOnly date, CancellationToken ct) => db.AttendancePeriodLocks.AsNoTracking()
        .AnyAsync(x => x.IsLocked && x.PeriodStart <= date && date < x.PeriodEndExclusive, ct);
    public async Task<WorkTreatmentRow?> FindAsync(Guid id, CancellationToken ct) {
        RequireTransaction();
        var rows = await db.ApprovalRequests.Where(x => x.SubjectId == id && x.RequestType == WorkTreatmentService.RequestType && x.Stage == 1).Take(2).ToArrayAsync(ct);
        if (rows.Length > 1) throw AttendanceOperation.Conflict("work_treatment_approval_identity_conflict");
        if (rows.Length == 0) return null;
        var decisions = await db.ApprovalDecisions.AsNoTracking().Where(x => x.ApprovalRequestId == rows[0].Id).Take(2).ToArrayAsync(ct);
        if (decisions.Length > 1) throw AttendanceOperation.Conflict("work_treatment_decision_identity_conflict");
        return new(rows[0], decisions.SingleOrDefault());
    }
    public async Task<IReadOnlyList<AuditEvent>> FactsAsync(Guid employeeId, string operation, CancellationToken ct) {
        RequireTransaction();
        var rows = await db.AuditEvents.AsNoTracking().Where(x => x.EmployeeScopeId == employeeId && x.TargetType == WorkTreatmentService.FactTarget &&
            x.EventType == operation).OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Take(1025).ToArrayAsync(ct);
        if (rows.Length > 1024) throw AttendanceOperation.Conflict("work_treatment_fact_limit");
        return rows;
    }
    public async Task<IReadOnlyList<WorkCreditRange>> ApprovedOvertimeAsync(Guid employeeId, CancellationToken ct) {
        RequireTransaction();
        var rows = await (from r in db.OvertimeRequests.AsNoTracking()
            join a in db.ApprovalRequests.AsNoTracking() on r.ApprovalRequestId equals a.Id
            join s in db.WorkSessions.AsNoTracking() on r.WorkSessionId equals s.Id
            where r.EmployeeId == employeeId && s.EmployeeId == employeeId && a.RequestType == "attendance.overtime" && a.State == ApprovalState.Approved &&
                a.RequesterUserId == r.RequestedBy && a.SnapshotPrivateReference == r.PrivateEvidenceReference &&
                db.ApprovalDecisions.Any(d => d.ApprovalRequestId == a.Id && d.Decision == ApprovalState.Approved && d.DecidedAt == a.ClosedAt && d.ActorUserId != a.RequesterUserId)
            select new WorkCreditRange(employeeId, r.Id, WorkCreditRole.Overtime, s.SessionKey, s.Id, s.AttendanceDate, r.StartsAtUtc, r.EndsAtUtc)).Take(1025).ToArrayAsync(ct);
        if (rows.Length > 1024) throw AttendanceOperation.Conflict("work_treatment_overtime_limit");
        return rows;
    }
    public void Add(ApprovalRequest approval) { RequireTransaction(); db.ApprovalRequests.Add(approval); }
    public Task<DisciplinaryCase?> ClosedCaseAsync(Guid employeeId, Guid approvalId, Guid caseId, CancellationToken ct) =>
        db.DisciplinaryCases.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.ApprovalRequestId == approvalId && x.Id == caseId, ct);
    public void Add(ApprovalDecision decision) { RequireTransaction(); db.ApprovalDecisions.Add(decision); }
    public void Add(AuditEvent fact) {
        RequireTransaction();
        if (fact.TargetType != WorkTreatmentService.FactTarget || fact.EmployeeScopeId is null || fact.ProtectedChangesReference is null ||
            !Guid.TryParse(fact.TargetId, out _) || !Guid.TryParse(fact.DeviceReference, out _) ||
            fact.EventType is not (WorkTreatmentEvidence.SubmissionOperation or WorkTreatmentEvidence.DisputeOperation or WorkTreatmentEvidence.DecisionOperation))
            throw new InvalidOperationException("Only controlled typed work-treatment facts can be appended.");
        db.AuditEvents.Add(fact);
    }
    public void Add(DisciplinaryCase closedCase) {
        RequireTransaction();
        if (closedCase.ClosedAt is null || closedCase.ApprovedOutcomePrivateReference is null) throw new InvalidOperationException("Only an actual approved closure is materialized.");
        db.DisciplinaryCases.Add(closedCase);
    }
    public void ExpectVersion(ApprovalRequest approval, byte[] version) { RequireTransaction(); db.Entry(approval).Property(x => x.RowVersion).OriginalValue = version; }
    public async Task SaveAsync(CancellationToken ct) { RequireTransaction(); await db.SaveChangesAsync(ct); }
}
