using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class AttendanceCaptureRepository(CosmixDbContext db) : IAttendanceCaptureRepository
{
    public async Task<CaptureIdentity> IdentityAsync(Guid accountId, Guid deviceId, CancellationToken ct)
    {
        var account = await db.UserAccounts.SingleOrDefaultAsync(x => x.Id == accountId, ct);
        var device = await db.RegisteredDevices.SingleOrDefaultAsync(x => x.Id == deviceId && x.UserAccountId == accountId, ct);
        if (account is null || device is null || device.RevokedAt.HasValue || !device.VerifiedAt.HasValue)
            throw new AuthenticationFailure("invalid_authentication", 401);
        return new(account, device);
    }
    public Task<RawPunchEvent?> FindPunchAsync(Guid employeeId, string key, CancellationToken ct) =>
        db.RawPunchEvents.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.IdempotencyKey == key, ct);
    public Task<RawPunchEvent?> PunchAsync(Guid employeeId, Guid id, CancellationToken ct) =>
        db.RawPunchEvents.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.Id == id, ct);
    public Task<PunchAssessment?> LatestAssessmentAsync(Guid punchId, CancellationToken ct) =>
        db.PunchAssessments.AsNoTracking().Where(x => x.RawPunchEventId == punchId).OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
    public async Task<IReadOnlyList<WorkSession>> ActiveSessionsAsync(Guid employeeId, CancellationToken ct) =>
        await LatestSessions(employeeId).Where(x => x.EndsAtUtc == null).OrderBy(x => x.StartsAtUtc).Take(2).ToListAsync(ct);
    private IQueryable<WorkSession> LatestSessions(Guid employeeId) => db.WorkSessions.AsNoTracking().Where(x =>
        x.EmployeeId == employeeId && !db.WorkSessions.Any(y => y.SessionKey == x.SessionKey && y.Revision > x.Revision));
    public Task<bool> IsLockedAsync(DateOnly date, CancellationToken ct) => db.AttendancePeriodLocks.AsNoTracking()
        .AnyAsync(x => x.IsLocked && x.PeriodStart <= date && date < x.PeriodEndExclusive, ct);
    public Task<FaceVerificationAttempt?> AttemptAsync(Guid employeeId, Guid id, CancellationToken ct) =>
        db.FaceVerificationAttempts.Include(x => x.Enrollment).SingleOrDefaultAsync(x => x.Id == id && x.EmployeeId == employeeId, ct);
    public Task<FaceEnrollment?> ActiveEnrollmentAsync(Guid employeeId, CancellationToken ct) =>
        db.FaceEnrollments.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.IsActive, ct);
    public async Task<PagedResult<RawPunchEvent>> PunchesAsync(Guid employeeId, AttendanceHistoryRequest request, CancellationToken ct)
    {
        var query = HistoryQuery(employeeId, request);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.ReceivedAtUtc).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArrayAsync(ct);
        return new(rows, count, request.Page, request.PageSize);
    }
    internal IQueryable<RawPunchEvent> HistoryQuery(Guid employeeId, AttendanceHistoryRequest request)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi");
        var localFrom = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localTo = request.To.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var fromUtc = new DateTimeOffset(localFrom, zone.GetUtcOffset(localFrom)).ToUniversalTime();
        var toUtc = new DateTimeOffset(localTo, zone.GetUtcOffset(localTo)).ToUniversalTime();
        return db.RawPunchEvents.AsNoTracking().Where(x => x.EmployeeId == employeeId &&
            (x.BusinessDate >= request.From && x.BusinessDate < request.To ||
             x.BusinessDate == null && x.ReceivedAtUtc >= fromUtc && x.ReceivedAtUtc < toUtc));
    }
    public async Task<PagedResult<WorkSession>> SessionsAsync(Guid employeeId, AttendanceHistoryRequest request, CancellationToken ct)
    {
        var query = LatestSessions(employeeId).Where(x => x.AttendanceDate >= request.From && x.AttendanceDate < request.To);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.StartsAtUtc).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArrayAsync(ct);
        return new(rows, count, request.Page, request.PageSize);
    }
    public void Add(RawPunchEvent punch) => db.RawPunchEvents.Add(punch);
    public void Add(PunchAssessment assessment) => db.PunchAssessments.Add(assessment);
    public void Add(WorkSession session) => db.WorkSessions.Add(session);
    public void Add(AttendanceException exception) => db.AttendanceExceptions.Add(exception);
}
