using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Zones;

public sealed class ZoneExceptionRequest : IAppendOnlyEntity
{
    private ZoneExceptionRequest() { }
    public ZoneExceptionRequest(Guid employeeId, Guid zoneVersionId, DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc,
        string reason, string requestedBy, DateTimeOffset requestedAtUtc, Guid approvalRequestId, string? taskReference = null)
    {
        AttendanceModelGuard.Window(startsAtUtc, endsAtUtc); Id = Guid.NewGuid(); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        ZoneVersionId = ModelGuard.Id(zoneVersionId, nameof(zoneVersionId)); StartsAtUtc = ModelGuard.Utc(startsAtUtc); EndsAtUtc = ModelGuard.Utc(endsAtUtc);
        Reason = ModelGuard.Required(reason, 2000, nameof(reason)); RequestedBy = ModelGuard.Required(requestedBy, 200, nameof(requestedBy));
        RequestedAtUtc = ModelGuard.Utc(requestedAtUtc); ApprovalRequestId = ModelGuard.Id(approvalRequestId, nameof(approvalRequestId)); TaskReference = ModelGuard.Optional(taskReference, 200, nameof(taskReference));
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid ZoneVersionId { get; private set; }
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset EndsAtUtc { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string RequestedBy { get; private set; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public Guid ApprovalRequestId { get; private set; }
    public string? TaskReference { get; private set; }
}
