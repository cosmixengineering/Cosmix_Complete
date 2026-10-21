using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;

namespace Cosmix.Application.Attendance.Audit;

public static class AttendanceAuditPermissions
{
    public const string Read = "attendance.punches.read";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = Array.AsReadOnly<AccessPermissionDefinition>([
        new(Read, "Read scoped raw attendance outcomes and immutable session revision history", true, true, false)
    ]);
}

public sealed record AttendanceAuditRequest(DateOnly From, DateOnly To, int PageSize = 25, string? Cursor = null);
public sealed record AttendancePunchAuditItem(PunchResponse Punch, int? AssessmentRevision, DateTimeOffset? AssessedAtUtc,
    DateTimeOffset? ClaimedDeviceCapturedAtUtc);
public sealed record AttendanceSessionAuditItem(WorkSessionResponse Session, DateTimeOffset RecordedAtUtc,
    bool IsLatestRevision, Guid? CorrectionId);
public sealed record AttendancePunchAuditPage(IReadOnlyList<AttendancePunchAuditItem> Items, string? NextCursor);
public sealed record AttendanceSessionAuditPage(IReadOnlyList<AttendanceSessionAuditItem> Items, string? NextCursor);

// Internal records are deliberately separate from safe API DTOs. Never serialize these entities.
public sealed record AttendancePunchAuditRecord(RawPunchEvent Punch, PunchAssessment? Assessment);
public sealed record AttendanceSessionAuditRecord(WorkSession Session, RawPunchEvent? CheckIn, RawPunchEvent? CheckOut,
    bool IsLatestRevision);
public interface IAttendanceAuditRepository
{
    Task<IReadOnlyList<AttendancePunchAuditRecord>> PunchesAsync(Guid employeeId, AttendanceAuditRequest request,
        AttendanceCursorPosition? after, int limit, CancellationToken ct);
    Task<IReadOnlyList<AttendanceSessionAuditRecord>> SessionsAsync(Guid employeeId, AttendanceAuditRequest request,
        AttendanceCursorPosition? after, int limit, CancellationToken ct);
}
