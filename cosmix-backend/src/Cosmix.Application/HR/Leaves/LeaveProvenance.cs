using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

/// <summary>Approval snapshots bind the immutable submission and exact allocations to their request.</summary>
public sealed class LeaveProvenance(AttendanceOperation operation, IPrivateFileStore files)
{
    public static string Digest(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    public static string SubmissionDigest(LeaveRequest r) => Digest(new { r.Id, r.EmployeeId, r.LeaveTypeId,
        r.StartDate, r.EndDate, r.RequestedMinutes, r.Reason, r.ContactDuringLeave, r.RelieverEmployeeId,
        r.HandoverNotes, r.EvidencePrivateReference, r.RequestedByUserId, r.CreatedAt, r.PreviousRequestId,
        days = r.Days.OrderBy(x => x.Date).Select(x => new { x.Id, x.LeaveRequestId, x.Date, x.StartsAtUtc, x.EndsAtUtc, x.Minutes, x.ShiftVersionId }) });
    public async Task<string> FreezeAsync(LeaveRequest request, CancellationToken ct, IReadOnlyList<OfficeLeaveScheduleSource>? officeSources = null)
    {
        var snapshot = new Submission(SubmissionDigest(request));
        if (officeSources is { Count: > 0 }) snapshot = snapshot with { OfficeSourceSchema = 1, OfficeSources = officeSources.OrderBy(x => x.Date).ToArray() };
        ValidateOfficeSources(snapshot, request);
        return AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot), Purpose(request.Id), ct));
    }
    public async Task VerifyAsync(LeaveRow row, CancellationToken ct)
    {
        if (!LeaveRules.ApprovalValid(row.Approval, row.Decisions, row.Request.Id, row.Request.RequestedByUserId, LeaveRules.RequestType) ||
            row.Request.ApprovalRequestId != row.Approval!.Id || row.Request.SubmittedAt != row.Approval.RequestedAt || row.Request.Days.Count is < 1 or > 366 ||
            row.Request.Days.Sum(x => x.Minutes) != row.Request.RequestedMinutes || row.Request.Days.Any(x => x.LeaveRequestId != row.Request.Id))
            throw AttendanceOperation.Conflict("leave_provenance_unavailable");
        var days = row.Request.Days.OrderBy(x => x.Date).ToArray();
        if (days[0].Date != row.Request.StartDate || days[^1].Date != row.Request.EndDate || days.Select(x => x.Date).Distinct().Count() != days.Length ||
            days.Any(x => x.Minutes is < 1 or > 1440 || x.Minutes != (x.EndsAtUtc - x.StartsAtUtc).TotalMinutes ||
                x.StartsAtUtc.Ticks % TimeSpan.TicksPerMinute != 0 || x.EndsAtUtc.Ticks % TimeSpan.TicksPerMinute != 0 || LeaveRules.LocalDate(x.StartsAtUtc) != x.Date) ||
            days.Zip(days.Skip(1)).Any(x => x.First.EndsAtUtc > x.Second.StartsAtUtc)) throw AttendanceOperation.Conflict("leave_allocation_unavailable");
        var snapshot = await operation.ReadPrivateAsync<Submission>(row.Approval.SnapshotPrivateReference, Purpose(row.Request.Id), ct);
        if (snapshot.Digest != SubmissionDigest(row.Request)) throw AttendanceOperation.Conflict("leave_snapshot_mismatch");
        ValidateOfficeSources(snapshot, row.Request);
    }
    public async Task<IReadOnlyList<OfficeLeaveScheduleSource>?> OfficeSourcesAsync(LeaveRow row, CancellationToken ct)
    {
        var snapshot = await operation.ReadPrivateAsync<Submission>(row.Approval!.SnapshotPrivateReference, Purpose(row.Request.Id), ct);
        if (snapshot.Digest != SubmissionDigest(row.Request)) throw AttendanceOperation.Conflict("leave_snapshot_mismatch");
        ValidateOfficeSources(snapshot, row.Request);
        return snapshot.OfficeSources?.ToArray();
    }
    private static void ValidateOfficeSources(Submission snapshot, LeaveRequest request)
    {
        if (snapshot.OfficeSourceSchema is null && snapshot.OfficeSources is null) return;
        if (snapshot.OfficeSourceSchema != 1 || snapshot.OfficeSources is not { Count: > 0 and <= 366 } sources ||
            sources.Any(x => x is null) ||
            sources.Select(x => x.Date).Distinct().Count() != sources.Count || sources.Any(x =>
                x.EmployeeId != request.EmployeeId || x.BranchId == Guid.Empty || x.OfficeRosterAssignmentId == Guid.Empty ||
                x.EmployeeAssignmentId == Guid.Empty || x.ShiftVersionId == Guid.Empty || x.HolidayCalendarId == Guid.Empty ||
                !request.Days.Any(d => d.Date == x.Date && d.ShiftVersionId == x.ShiftVersionId)))
            throw AttendanceOperation.Conflict("leave_office_schedule_provenance_unavailable");
    }
    public static string Purpose(Guid id) => $"hr:leave:{id:N}:submission";
    private sealed record Submission(string Digest)
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? OfficeSourceSchema { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IReadOnlyList<OfficeLeaveScheduleSource>? OfficeSources { get; init; }
    }
}
