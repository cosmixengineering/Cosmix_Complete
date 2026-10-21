using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Reminders;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class MissingCheckoutReminderRepository(CosmixDbContext db) : IMissingCheckoutReminderRepository
{
    private IQueryable<MissingCheckoutReminderCandidate> CandidateQuery(long afterEmployeeNumber, DateTimeOffset now) =>
        db.Employees.AsNoTracking().Where(e => e.EmployeeNumber > afterEmployeeNumber &&
            LatestOpenQuery(now).Any(s => s.EmployeeId == e.Id &&
                db.ShiftVersions.Any(v => v.Id == s.ShiftVersionId && v.MissingCheckoutReminderAfterMinutes != null)))
            .OrderBy(e => e.EmployeeNumber).Select(e => new MissingCheckoutReminderCandidate(e.Id, e.EmployeeNumber));

    public async Task<IReadOnlyList<MissingCheckoutReminderCandidate>> CandidatesAsync(long afterEmployeeNumber, int limit,
        DateTimeOffset now, CancellationToken ct)
    {
        if (afterEmployeeNumber < 0 || limit is < 1 or > 100) throw AttendanceOperation.Invalid();
        return await CandidateQuery(afterEmployeeNumber, now).Take(limit).ToArrayAsync(ct);
    }

    private IQueryable<WorkSession> LatestOpenQuery(DateTimeOffset now) => db.WorkSessions.AsNoTracking().Where(s =>
        s.EndsAtUtc == null && s.StartsAtUtc <= now &&
        !db.WorkSessions.Any(later => later.SessionKey == s.SessionKey && later.Revision > s.Revision));

    public async Task<MissingCheckoutReminderState?> LoadAsync(Guid employeeId, DateTimeOffset now, CancellationToken ct)
    {
        var sessions = await LatestOpenQuery(now).Where(s => s.EmployeeId == employeeId)
            .OrderBy(s => s.StartsAtUtc).ThenBy(s => s.Id).Take(2).ToArrayAsync(ct);
        if (sessions.Length > 1) throw AttendanceOperation.Conflict("missing_checkout_reminder_multiple_open_duties");
        if (sessions.Length == 0) return null;
        var session = sessions[0];
        if (!MissingCheckoutReminderRules.HasSupportedLocation(session) || session.CheckInPunchId is not Guid openingId) return null;
        // A pending/offline/failed punch does not become an active duty merely because it exists.
        var accepted = await AcceptedOpeningQuery(employeeId, openingId, session).AnyAsync(ct);
        if (!accepted || await db.AttendancePeriodLocks.AsNoTracking().AnyAsync(l => l.IsLocked &&
                l.PeriodStart <= session.AttendanceDate && session.AttendanceDate < l.PeriodEndExclusive, ct)) return null;
        var today = RosterSchedule.Today(now);
        var accounts = await CurrentAccountsQuery(employeeId, today, now).Take(2).ToArrayAsync(ct);
        if (accounts.Length > 1) throw AttendanceOperation.Conflict("missing_checkout_reminder_account_conflict");
        if (accounts.Length == 0) return null;
        var statuses = await EffectiveStatusesQuery(employeeId, now).Take(2).ToArrayAsync(ct);
        if (statuses.Length > 1 && statuses[0].EffectiveAt == statuses[1].EffectiveAt)
            throw AttendanceOperation.Conflict("missing_checkout_reminder_employment_conflict");
        if (statuses.Length != 0 && statuses[0].NewStatus != EmployeeStatus.Active) return null;
        var version = await db.ShiftVersions.AsNoTracking().SingleOrDefaultAsync(v => v.Id == session.ShiftVersionId, ct)
            ?? throw AttendanceOperation.Conflict("missing_checkout_reminder_shift_unavailable");
        return new(session, version, accounts[0]);
    }

    private IQueryable<RawPunchEvent> AcceptedOpeningQuery(Guid employeeId, Guid openingId, WorkSession session) =>
        db.RawPunchEvents.AsNoTracking().Where(p => p.Id == openingId && p.EmployeeId == employeeId &&
            p.Action == PunchAction.CheckIn && p.SiteId == session.SiteId && p.BranchId == session.BranchId &&
            (!session.BranchId.HasValue || p.OfficeRosterAssignmentId == session.OfficeRosterAssignmentId &&
                p.EmployeeAssignmentId == session.EmployeeAssignmentId && p.OfficeGeofenceVersionId == session.OfficeGeofenceVersionId &&
                p.HolidayCalendarId == session.HolidayCalendarId) && p.ShiftVersionId == session.ShiftVersionId &&
            p.BusinessDate == session.AttendanceDate && p.ReceivedAtUtc == session.StartsAtUtc &&
            db.PunchAssessments.Any(a => a.RawPunchEventId == p.Id && a.EmployeeId == employeeId &&
                (a.Outcome == PunchAssessmentOutcome.Accepted || a.Outcome == PunchAssessmentOutcome.AcceptedWithException) &&
                !db.PunchAssessments.Any(later => later.RawPunchEventId == p.Id && later.Revision > a.Revision)));

    private IQueryable<Guid> CurrentAccountsQuery(Guid employeeId, DateOnly today, DateTimeOffset now) =>
        db.UserAccounts.AsNoTracking().Where(a => a.EmployeeId == employeeId && a.Status == AccountStatus.Active &&
            (a.MobileAccessEnabled || a.WebAccessEnabled) && (a.LockedUntil == null || a.LockedUntil <= now) &&
            a.Employee.Status == EmployeeStatus.Active && a.Employee.JoinedOn != null && a.Employee.JoinedOn <= today &&
            a.Employee.OnboardingStatus == OnboardingStatus.Approved && a.Employee.ApprovedOnboardingDecisionId == a.ApprovalDecisionId &&
            a.ApprovalDecision.EmployeeId == employeeId && a.ApprovalDecision.Decision == OnboardingDecision.Approve)
            .Select(a => a.Id);

    private IOrderedQueryable<EmployeeStatusChange> EffectiveStatusesQuery(Guid employeeId, DateTimeOffset now) =>
        db.EmployeeStatusChanges.AsNoTracking().Where(s => s.EmployeeId == employeeId && s.EffectiveAt <= now)
            .OrderByDescending(s => s.EffectiveAt);

    public Task<bool> AlreadyQueuedAsync(Guid sessionKey, CancellationToken ct)
    {
        if (sessionKey == Guid.Empty) throw AttendanceOperation.Invalid();
        return ExistingIdentityQuery(sessionKey).SingleAsync(ct);
    }

    private IQueryable<bool> ExistingIdentityQuery(Guid sessionKey)
    {
        // The producer already owns the authentication lock. Dispatch owns the outbox row before
        // taking that lock; an ordinary serializable read here could reverse that lock order.
        // Read only an existence hint, never payload/recipient/status/authorization. A dirty positive
        // merely skips this sweep; a missed row is still rejected by the unique outbox identity at save.
        var type = MissingCheckoutReminderProcessor.MessageType;
        var key = sessionKey.ToString("N");
        var exactType = type + "|";
        var exactKey = key + "|";
        return db.Database.SqlQuery<bool>($"""
            SELECT CAST(CASE WHEN EXISTS (
                SELECT 1 FROM [jobs].[OutboxMessages] WITH (READUNCOMMITTED)
                WHERE [MessageType] = {type} AND [DeduplicationKey] = {key}
                  AND ([MessageType] + N'|') COLLATE Latin1_General_100_BIN2 = {exactType}
                  AND ([DeduplicationKey] + N'|') COLLATE Latin1_General_100_BIN2 = {exactKey}
            ) THEN 1 ELSE 0 END AS bit) AS [Value]
            """);
    }

    public Task LockAsync(CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Reminder delivery requires the outbox transaction.");
        return db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource=N'cosmix:authentication',
                @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51001, 'Authentication is temporarily busy.', 1;
            """, ct);
    }
}
