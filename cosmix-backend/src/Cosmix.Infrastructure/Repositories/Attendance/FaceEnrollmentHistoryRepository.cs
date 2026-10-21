using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Domain.Common.Audit;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class FaceEnrollmentHistoryRepository(CosmixDbContext db) : IFaceEnrollmentHistoryRepository
{
    public async Task<IReadOnlyList<AuditEvent>> CandidatesAsync(Guid employeeId, DateTimeOffset fromUtc,
        DateTimeOffset toUtc, AttendanceCursorPosition? before, int take, CancellationToken ct)
    {
        if (take is < 1 or > 101) throw new ArgumentOutOfRangeException(nameof(take));
        return await CandidateQuery(employeeId, fromUtc, toUtc, before, take).ToArrayAsync(ct);
    }

    private IQueryable<AuditEvent> CandidateQuery(Guid employeeId, DateTimeOffset fromUtc,
        DateTimeOffset toUtc, AttendanceCursorPosition? before, int take)
    {
        // EmployeeScopeId has the existing FK index. Each candidate is then bound to its immutable
        // start receipt and protected metadata; a missing/corrupt receipt never becomes a made-up outcome.
        var query = db.AuditEvents.AsNoTracking().Where(x => x.EmployeeScopeId == employeeId &&
            x.EventType == "attendance.face.enrollment.started" && x.TargetType == "FaceEnrollmentAttempt" &&
            x.OccurredAt >= fromUtc && x.OccurredAt < toUtc);
        if (before is not null)
            query = query.Where(x => x.OccurredAt < before.CreatedAtUtc ||
                x.OccurredAt == before.CreatedAtUtc && x.Id.CompareTo(before.Id) < 0);
        return query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id).Take(take);
    }
}
