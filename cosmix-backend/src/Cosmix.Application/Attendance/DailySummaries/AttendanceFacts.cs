using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Attendance.WorkSessions;

namespace Cosmix.Application.Attendance.DailySummaries;

public sealed record FactualAttendance(long WorkedSeconds, int WorkedMinutes, int OpenSessions,
    IReadOnlyList<Guid> SessionIds, string InputFingerprint);

/// <summary>Elapsed accepted session evidence assigned to the shift-start business date. This is
/// neither a payable-time formula nor a determination of presence, absence, late or overtime.</summary>
public static class AttendanceFacts
{
    public const string CalculationVersion = "factual-session-elapsed-v1";
    public static FactualAttendance Calculate(Guid employeeId, DateOnly date, IReadOnlyList<WorkSession> revisions)
    {
        if (employeeId == Guid.Empty || date == default || revisions is null || revisions.Count > 4096)
            throw AttendanceOperation.Invalid();
        if (revisions.Any(x => x.EmployeeId != employeeId || x.AttendanceDate != date))
            throw AttendanceOperation.Conflict("attendance_session_identity_conflict");
        if (revisions.GroupBy(x => (x.SessionKey, x.Revision)).Any(x => x.Count() > 1))
            throw AttendanceOperation.Conflict("attendance_session_revision_conflict");
        var sessions = revisions.GroupBy(x => x.SessionKey).Select(x => x.MaxBy(y => y.Revision)!)
            .OrderBy(x => x.StartsAtUtc).ThenBy(x => x.SessionKey).ToArray();
        long ticks = 0;
        for (var i = 0; i < sessions.Length; i++)
        {
            var session = sessions[i];
            if (i > 0 && (sessions[i - 1].EndsAtUtc is null || session.StartsAtUtc < sessions[i - 1].EndsAtUtc))
                throw AttendanceOperation.Conflict("attendance_session_overlap");
            if (session.EndsAtUtc is not DateTimeOffset end) continue;
            if (end <= session.StartsAtUtc) throw AttendanceOperation.Conflict("attendance_session_window_conflict");
            ticks = checked(ticks + (end - session.StartsAtUtc).Ticks);
        }
        var minutes = ticks / TimeSpan.TicksPerMinute;
        if (minutes > int.MaxValue) throw AttendanceOperation.Conflict("attendance_duration_limit");
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            employeeId, date, calculation = CalculationVersion,
            sessions = sessions.Select(x => new SessionFingerprint(x.Id, x.SessionKey, x.Revision, x.StartsAtUtc, x.EndsAtUtc,
                x.CheckInPunchId, x.CheckOutPunchId, x.CorrectionId, x.SiteId, x.ShiftVersionId, x.PolicyVersionId,
                x.BranchId, x.OfficeRosterAssignmentId, x.BranchId.HasValue ? x.EmployeeAssignmentId : null,
                x.OfficeGeofenceVersionId, x.BranchId.HasValue ? x.HolidayCalendarId : null))
        })));
        return new(ticks / TimeSpan.TicksPerSecond, (int)minutes, sessions.Count(x => x.EndsAtUtc is null),
            sessions.Select(x => x.Id).ToArray(), fingerprint);
    }
    private sealed record SessionFingerprint(Guid Id, Guid SessionKey, int Revision, DateTimeOffset StartsAtUtc,
        DateTimeOffset? EndsAtUtc, Guid? CheckInPunchId, Guid? CheckOutPunchId, Guid? CorrectionId,
        Guid? SiteId, Guid ShiftVersionId, Guid? PolicyVersionId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? BranchId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OfficeRosterAssignmentId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? EmployeeAssignmentId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OfficeGeofenceVersionId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? HolidayCalendarId);
}
