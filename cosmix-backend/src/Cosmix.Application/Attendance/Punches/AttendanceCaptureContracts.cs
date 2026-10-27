using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;

namespace Cosmix.Application.Attendance.Punches;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitPunchRequest([property: JsonRequired] bool IsOffline,
    DateTimeOffset? DeviceCapturedAtUtc, decimal? Latitude, decimal? Longitude, decimal? AccuracyMeters,
    Guid? FaceAttemptId = null, [StringLength(128)] string? Proof = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CheckInPreflightRequest(
    [property: JsonRequired] decimal Latitude,
    [property: JsonRequired] decimal Longitude,
    [property: JsonRequired] decimal AccuracyMeters,
    [property: JsonRequired] DateTimeOffset DeviceCapturedAtUtc);
public sealed record CheckInPreflightResponse(bool CanOpenCamera, DateTimeOffset CheckedAtUtc);

public sealed record AttendanceHistoryRequest(DateOnly From, DateOnly To,
    [Range(1, 1000000)] int Page = 1, [Range(1, 100)] int PageSize = 25);
public sealed record PunchValidationResponse(Guid EvidenceId, Guid? CapturePolicyVersionId, int? CapturePolicyRevision,
    string? CapturePolicyFingerprint, IReadOnlyList<string> Codes, IReadOnlyList<Guid> RelatedRawPunchIds);
public sealed record PunchResponse(Guid Id, string Action, DateTimeOffset ReceivedAtUtc, DateOnly BusinessDate,
    string Outcome, string ReasonCode, Guid? SiteId, Guid? ShiftVersionId, Guid? SessionKey,
    bool IsOffline, Guid? AssessmentId, string CalculationStatus = "factual_only_policy_pending",
    string BusinessDateSource = "server_roster",
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? BranchId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OfficeRosterAssignmentId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OfficeGeofenceVersionId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PunchValidationResponse? Validation = null);
public sealed record WorkSessionResponse(Guid Id, Guid SessionKey, int Revision, DateOnly BusinessDate,
    DateTimeOffset StartsAtUtc, DateTimeOffset? EndsAtUtc, Guid ShiftVersionId, Guid? SiteId,
    Guid? CheckInPunchId, Guid? CheckOutPunchId, decimal? FactualWorkedMinutes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? BranchId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OfficeRosterAssignmentId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? EmployeeAssignmentId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OfficeGeofenceVersionId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? HolidayCalendarId = null);
public sealed record AttendanceStateResponse(Guid EmployeeId, DateTimeOffset ServerTimeUtc, string TimeZone,
    string State, string NextAction, bool CheckInEnabled, bool CheckOutEnabled, string FaceStatus,
    WorkSessionResponse? ActiveSession, IReadOnlyList<string> Limitations);
public sealed record CaptureIdentity(UserAccount Account, RegisteredDevice Device);

public interface IAttendanceCaptureRepository
{
    Task<CaptureIdentity> IdentityAsync(Guid accountId, Guid deviceId, CancellationToken ct);
    Task<RawPunchEvent?> FindPunchAsync(Guid employeeId, string key, CancellationToken ct);
    Task<RawPunchEvent?> PunchAsync(Guid employeeId, Guid id, CancellationToken ct);
    Task<PunchAssessment?> LatestAssessmentAsync(Guid punchId, CancellationToken ct);
    Task<IReadOnlyList<WorkSession>> ActiveSessionsAsync(Guid employeeId, CancellationToken ct);
    Task<bool> IsLockedAsync(DateOnly date, CancellationToken ct);
    Task<FaceVerificationAttempt?> AttemptAsync(Guid employeeId, Guid id, CancellationToken ct);
    Task<FaceEnrollment?> ActiveEnrollmentAsync(Guid employeeId, CancellationToken ct);
    Task<PagedResult<RawPunchEvent>> PunchesAsync(Guid employeeId, AttendanceHistoryRequest request, CancellationToken ct);
    Task<PagedResult<WorkSession>> SessionsAsync(Guid employeeId, AttendanceHistoryRequest request, CancellationToken ct);
    void Add(RawPunchEvent punch);
    void Add(PunchAssessment assessment);
    void Add(WorkSession session);
    void Add(AttendanceException exception);
}
