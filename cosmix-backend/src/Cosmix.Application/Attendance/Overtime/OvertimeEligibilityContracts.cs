using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Attendance.Overtime;

namespace Cosmix.Application.Attendance.Overtime;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetOvertimeEligibilityRequest(DateOnly EffectiveFrom, [property: JsonRequired] bool IsEnabled, [property: JsonRequired] [Range(0, int.MaxValue)] int ExpectedRevision,
    [Required, StringLength(64, MinimumLength = 64)] string ExpectedTimeline, [Required, StringLength(1000)] string Reason);
public sealed record OvertimeEligibilityEntry(Guid Id, int Revision, Guid? SupersedesVersionId, DateOnly EffectiveFrom,
    bool IsEnabled, string Effect, string Actor, DateTimeOffset RecordedAtUtc, string Reason);
public sealed record OvertimeEligibilityPreview(string Target, Guid? EmployeeId, DateOnly AsOf, int LatestRevision,
    string TimelineFingerprint, OvertimeEligibilityEntry? Effective, string State, OvertimeEligibility? Eligibility = null);
public sealed record OvertimeEligibilityEvidence(int SchemaVersion, Guid Id, OvertimeEligibilityTarget Target, Guid? EmployeeId,
    int Revision, Guid? SupersedesVersionId, DateOnly EffectiveFrom, bool IsEnabled, string RecordedBy,
    DateTimeOffset RecordedAtUtc, string Reason, IReadOnlyList<AttendanceResourceScope>? OriginalScopes);
public interface IOvertimeEligibilityRepository
{
    Task<IReadOnlyList<OvertimeEligibilityVersion>> TimelineAsync(OvertimeEligibilityTarget target, Guid? employeeId, CancellationToken ct);
    void Add(OvertimeEligibilityVersion version);
    Task SaveChangesAsync(CancellationToken ct);
}
