using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class FaceRepository(CosmixDbContext db, TimeProvider clock) : IFaceRepository
{
    public async Task<FaceEmployeeDevice> EmployeeDeviceAsync(Guid employeeId, Guid deviceId, CancellationToken ct)
    {
        var account = await db.UserAccounts.Include(x => x.Employee).SingleOrDefaultAsync(x => x.EmployeeId == employeeId, ct);
        var now = clock.GetUtcNow();
        var history = await db.Set<EmployeeStatusChange>().AsNoTracking().Where(x => x.EmployeeId == employeeId && x.EffectiveAt <= now)
            .OrderByDescending(x => x.EffectiveAt).Take(2).ToArrayAsync(ct);
        if (account is null || account.Status != AccountStatus.Active || account.Employee.Status != EmployeeStatus.Active ||
            account.Employee.OnboardingStatus != OnboardingStatus.Approved || account.Employee.JoinedOn is not DateOnly joined || joined > RosterSchedule.Today(now) ||
            history.Length > 0 && history[0].NewStatus != EmployeeStatus.Active ||
            history.Length > 1 && history[0].EffectiveAt == history[1].EffectiveAt)
            throw new AuthenticationFailure("face_employee_ineligible", 409);
        var device = await db.RegisteredDevices.SingleOrDefaultAsync(x => x.Id == deviceId && x.UserAccountId == account.Id, ct);
        if (device is null || device.RevokedAt.HasValue || !device.VerifiedAt.HasValue || device.VerifiedAt > now)
            throw new AuthenticationFailure("face_device_ineligible", 409);
        return new(account, device);
    }
    public async Task<IReadOnlyList<AccessResource>> ResourcesAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == employeeId, ct)) throw new AuthenticationFailure("attendance_resource_not_found", 404);
        var assignments = await db.EmployeeAssignments.AsNoTracking().Where(x => x.EmployeeId == employeeId &&
            x.EffectiveFrom <= date && (x.EffectiveTo == null || date < x.EffectiveTo)).Take(2).ToArrayAsync(ct);
        if (assignments.Length > 1) throw new AuthenticationFailure("employee_assignment_history_conflict", 409);
        // Enrollment scope never selects an arbitrary team/department. Employee or historical site is explicit.
        return [new AccessResource(EmployeeId: employeeId, SiteId: assignments.SingleOrDefault()?.SiteId)];
    }
    public Task<FaceEnrollment?> ActiveAsync(Guid employeeId, CancellationToken ct) =>
        db.FaceEnrollments.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.IsActive, ct);
    public Task<FaceEnrollment?> EnrollmentAsync(Guid employeeId, Guid id, CancellationToken ct) =>
        db.FaceEnrollments.SingleOrDefaultAsync(x => x.Id == id && x.EmployeeId == employeeId, ct);
    public Task<FaceVerificationAttempt?> AttemptAsync(Guid employeeId, Guid id, CancellationToken ct) =>
        db.FaceVerificationAttempts.Include(x => x.Enrollment).SingleOrDefaultAsync(x => x.Id == id && x.EmployeeId == employeeId, ct);
    public Task<int> AttemptCountAsync(Guid employeeId, string punchKey, CancellationToken ct) =>
        db.FaceVerificationAttempts.CountAsync(x => x.EmployeeId == employeeId && x.PunchIdempotencyKey == punchKey, ct);
    public async Task<PagedResult<FaceEnrollmentResponse>> EnrollmentsAsync(Guid employeeId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.FaceEnrollments.AsNoTracking().Where(x => x.EmployeeId == employeeId);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.EnrolledAtUtc).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(rows.Select(FaceService.Map).ToArray(), count, page, pageSize);
    }
    public async Task<PagedResult<FaceAttemptResponse>> AttemptsAsync(Guid employeeId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.FaceVerificationAttempts.AsNoTracking().Where(x => x.EmployeeId == employeeId);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.IssuedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(rows.Select(x => new FaceAttemptResponse(x.Id, x.FaceEnrollmentId, x.Outcome.ToString(), x.IssuedAtUtc,
            x.ExpiresAtUtc, x.CompletedAtUtc, x.ConsumedAtUtc, x.Provider, x.ProviderVersion, x.ResultReasonCode)).ToArray(), count, page, pageSize);
    }
    public void Add(FaceEnrollment enrollment) => db.FaceEnrollments.Add(enrollment);
    public void Add(FaceVerificationAttempt attempt) => db.FaceVerificationAttempts.Add(attempt);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
