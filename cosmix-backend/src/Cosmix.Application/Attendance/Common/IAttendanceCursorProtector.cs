namespace Cosmix.Application.Attendance.Common;

/// <summary>Exact server-derived position in a deterministic CreatedAtUtc/Id ordering.</summary>
public sealed record AttendanceCursorPosition(DateTimeOffset CreatedAtUtc, Guid Id);

/// <summary>Opaque continuation bound to the current account, feature and SHA-256 filter digest.
/// This carries no authorization: every page must recheck live grants and original resource scopes.</summary>
public interface IAttendanceCursorProtector
{
    string Protect(Guid accountId, string purpose, string filterFingerprint, AttendanceCursorPosition position);
    AttendanceCursorPosition Unprotect(string token, Guid accountId, string purpose, string filterFingerprint);
}
