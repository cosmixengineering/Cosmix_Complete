using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;

namespace Cosmix.Application.Attendance.Corrections;

public static class AttendanceExceptionPermissions
{
    public const string Read = "attendance.exceptions.read";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = Array.AsReadOnly<AccessPermissionDefinition>([
        new(Read, "Read scoped attendance exceptions and confidential detection reasons without deciding attendance", true, true, false)
    ]);
}

public sealed record AttendanceExceptionQueueRequest(DateOnly From, DateOnly To, CorrectionQueueScope Scope,
    Guid? TargetId = null, AttendanceExceptionKind? Kind = null, int PageSize = 25, string? Cursor = null);
public sealed record AttendanceExceptionListItem(Guid Id, Guid EmployeeId, DateOnly BusinessDate, string Kind,
    DateTimeOffset DetectedAtUtc, Guid? PunchId, Guid? SessionRevisionId, Guid? SummaryId);
public sealed record AttendanceExceptionQueueResponse(IReadOnlyList<AttendanceExceptionListItem> Items, string? NextCursor);
public sealed record AttendanceExceptionDetail(AttendanceExceptionListItem Exception, string DetectionReason);
public sealed record MissingCheckoutReviewItem(Guid EmployeeId, DateOnly BusinessDate, Guid SessionRevisionId,
    Guid SessionKey, DateTimeOffset CheckInAtUtc, DateTimeOffset? ScheduledEndAtUtc, string State,
    string NextAction = "submit_or_review_correction");
public sealed record MissingCheckoutReviewQueue(IReadOnlyList<MissingCheckoutReviewItem> Items, string? NextCursor);

// Internal evidence only. These entities must never be returned from an API.
public sealed record AttendanceExceptionSources(RawPunchEvent? Punch, WorkSession? Session, DailyAttendanceSummary? Summary);
public interface IAttendanceExceptionRepository
{
    Task<AttendanceException?> FindAsync(Guid employeeId, Guid id, CancellationToken ct);
    Task<AttendanceExceptionSources> SourcesAsync(AttendanceException exception, CancellationToken ct);
    Task<IReadOnlyList<AttendanceException>> CandidatesAsync(AttendanceExceptionQueueRequest request,
        AttendanceCursorPosition? after, int limit, CancellationToken ct);
    Task<IReadOnlyList<WorkSession>> OpenSessionCandidatesAsync(AttendanceExceptionQueueRequest request,
        AttendanceCursorPosition? after, int limit, CancellationToken ct);
    Task<bool> ScopeExistsAsync(CorrectionQueueScope scope, Guid id, CancellationToken ct);
}
