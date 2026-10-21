using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

/// <summary>Read-only financial source projection. Every collection has an explicit rejection bound.</summary>
public sealed class AttendancePayrollInputRepository(CosmixDbContext db) : IAttendancePayrollInputRepository
{
    public async Task<MonthLockPopulation> CoverageAsync(Guid employeeId, DateOnly from, DateOnly endExclusive, CancellationToken ct)
    {
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.Id == employeeId, ct)
            ?? throw AttendanceOperation.Conflict("payroll_employee_unavailable");
        var approved = await db.EmployeeOnboardingDecisions.AsNoTracking().AnyAsync(d =>
            d.Id == employee.ApprovedOnboardingDecisionId && d.EmployeeId == employeeId &&
            d.Decision == OnboardingDecision.Approve && d.Submission.EmployeeId == employeeId &&
            d.Submission.Version == employee.SubmittedVersion && d.Submission.Provenance == d.Provenance &&
            employee.OnboardingStatus == OnboardingStatus.Approved, ct);
        var statuses = await db.EmployeeStatusChanges.AsNoTracking().Where(x => x.EmployeeId == employeeId)
            .OrderBy(x => x.EffectiveAt).ThenBy(x => x.Id)
            .Select(x => new MonthLockStatusEvidence(x.Id, x.PreviousStatus, x.NewStatus, x.EffectiveAt, x.RecordedAt,
                db.ApprovalRequests.Any(a => a.Id == x.ApprovalRequestId && a.SubjectId == employeeId &&
                    a.RequestType == EmployeeLifecycleService.RequestType && a.Stage == 1 && a.State == ApprovalState.Approved &&
                    a.ClosedAt == x.RecordedAt && db.ApprovalDecisions.Any(d => d.ApprovalRequestId == a.Id &&
                        d.Decision == ApprovalState.Approved && d.DecidedAt == x.RecordedAt &&
                        d.ActorUserId == x.ChangedByUserId && d.ActorUserId != a.RequesterUserId))))
            .Take(2049).ToArrayAsync(ct);
        if (statuses.Length > 2048) throw AttendanceOperation.Conflict("payroll_lifecycle_limit");
        var dates = await EvidenceDateQuery(employeeId, from, endExclusive).ToArrayAsync(ct);
        if (dates.Length > 366) throw AttendanceOperation.Conflict("payroll_source_range_limit");
        return MonthLockCoverage.Resolve(from, endExclusive,
            [new(employeeId, employee.JoinedOn, employee.Status, approved, statuses)], dates.Select(x => new MonthLockCell(employeeId, x)).ToArray());
    }

    internal IQueryable<DateOnly> EvidenceDateQuery(Guid employeeId, DateOnly from, DateOnly endExclusive) =>
        db.WorkSessions.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.AttendanceDate >= from && x.AttendanceDate < endExclusive)
            .Select(x => x.AttendanceDate).Union(db.RawPunchEvents.AsNoTracking().Where(x => x.EmployeeId == employeeId &&
                x.BusinessDate >= from && x.BusinessDate < endExclusive).Select(x => x.BusinessDate!.Value))
            .Union(db.DailyAttendanceSummaries.AsNoTracking().Where(x => x.EmployeeId == employeeId &&
                x.AttendanceDate >= from && x.AttendanceDate < endExclusive).Select(x => x.AttendanceDate))
            .OrderBy(x => x).Take(367);

    internal IQueryable<WorkSession> SessionQuery(Guid employeeId, DateOnly from, DateOnly endExclusive) =>
        db.WorkSessions.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.AttendanceDate >= from && x.AttendanceDate < endExclusive)
            .OrderBy(x => x.AttendanceDate).ThenBy(x => x.SessionKey).ThenBy(x => x.Revision).Take(32769);
    public async Task<IReadOnlyList<WorkSession>> SessionsAsync(Guid employeeId, DateOnly from, DateOnly endExclusive, CancellationToken ct)
    {
        var values = await SessionQuery(employeeId, from, endExclusive).ToArrayAsync(ct);
        if (values.Length > 32768) throw AttendanceOperation.Conflict("payroll_session_limit");
        return values;
    }
    public async Task<IReadOnlyList<AttendancePayrollCompensationSource>> CompensationAsync(Guid employeeId, CancellationToken ct)
    {
        var rows = await db.CompensationRecords.AsNoTracking().Where(x => x.EmployeeId == employeeId)
            .OrderBy(x => x.EffectiveFrom).ThenBy(x => x.Revision).Take(4097).ToArrayAsync(ct);
        if (rows.Length > 4096) throw AttendanceOperation.Conflict("payroll_compensation_limit");
        var ids = rows.Where(x => x.ApprovalRequestId.HasValue).Select(x => x.ApprovalRequestId!.Value).ToArray();
        var requests = await db.ApprovalRequests.AsNoTracking().Where(x => ids.Contains(x.Id)).Take(4097).ToArrayAsync(ct);
        var decisions = await db.ApprovalDecisions.AsNoTracking().Where(x => ids.Contains(x.ApprovalRequestId))
            .OrderBy(x => x.Id).Take(8193).ToArrayAsync(ct);
        if (requests.Length > 4096 || decisions.Length > 8192) throw AttendanceOperation.Conflict("payroll_compensation_limit");
        return rows.Select(x => new AttendancePayrollCompensationSource(x,
            requests.SingleOrDefault(r => r.Id == x.ApprovalRequestId), decisions.Where(d => d.ApprovalRequestId == x.ApprovalRequestId).ToArray())).ToArray();
    }
    public async Task<AttendancePayrollHiringSource?> HiringAsync(Guid employeeId, CancellationToken ct)
    {
        var decision = await (from e in db.Employees.AsNoTracking()
            join d in db.EmployeeOnboardingDecisions.AsNoTracking() on e.ApprovedOnboardingDecisionId equals d.Id
            where e.Id == employeeId && e.OnboardingStatus == OnboardingStatus.Approved && d.EmployeeId == e.Id &&
                d.Decision == OnboardingDecision.Approve && d.Submission.Version == e.SubmittedVersion
            select d).SingleOrDefaultAsync(ct);
        if (decision is null) return null;
        var submission = await db.Set<EmployeeSubmission>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == decision.SubmissionId, ct);
        return submission is null ? null : new(submission, decision);
    }
}
