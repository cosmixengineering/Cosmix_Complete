using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Overtime;

/// <summary>One employee per request, including requests belonging to a shared bulk submission.</summary>
public sealed class OvertimeRequest : IAppendOnlyEntity
{
    private OvertimeRequest() { }
    public OvertimeRequest(Guid employeeId, Guid workSessionId, DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc,
        int requestedMinutes, Guid? policyVersionId, string? rateRuleCode, string reason, string privateEvidenceReference,
        string requestedBy, DateTimeOffset requestedAtUtc, Guid approvalRequestId, Guid? siteId = null,
        string? jobReference = null, Guid? bulkSubmissionId = null)
    {
        AttendanceModelGuard.Window(startsAtUtc, endsAtUtc);
        if (requestedMinutes <= 0 || requestedMinutes > (endsAtUtc - startsAtUtc).TotalMinutes) throw new ArgumentOutOfRangeException(nameof(requestedMinutes));
        Id = Guid.NewGuid(); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId)); WorkSessionId = ModelGuard.Id(workSessionId, nameof(workSessionId));
        StartsAtUtc = ModelGuard.Utc(startsAtUtc); EndsAtUtc = ModelGuard.Utc(endsAtUtc); RequestedMinutes = requestedMinutes;
        PolicyVersionId = AttendanceModelGuard.OptionalId(policyVersionId, nameof(policyVersionId));
        RateRuleCode = rateRuleCode is null ? null : ModelGuard.Code(rateRuleCode, 100);
        Reason = ModelGuard.Required(reason, 2000, nameof(reason)); PrivateEvidenceReference = ModelGuard.Required(privateEvidenceReference, 500, nameof(privateEvidenceReference));
        RequestedBy = ModelGuard.Required(requestedBy, 200, nameof(requestedBy)); RequestedAtUtc = ModelGuard.Utc(requestedAtUtc);
        ApprovalRequestId = ModelGuard.Id(approvalRequestId, nameof(approvalRequestId)); SiteId = AttendanceModelGuard.OptionalId(siteId, nameof(siteId));
        JobReference = ModelGuard.Optional(jobReference, 200, nameof(jobReference)); BulkSubmissionId = AttendanceModelGuard.OptionalId(bulkSubmissionId, nameof(bulkSubmissionId));
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid WorkSessionId { get; private set; }
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset EndsAtUtc { get; private set; }
    public int RequestedMinutes { get; private set; }
    public Guid? PolicyVersionId { get; private set; }
    public string? RateRuleCode { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string PrivateEvidenceReference { get; private set; } = string.Empty;
    public string RequestedBy { get; private set; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public Guid ApprovalRequestId { get; private set; }
    public Guid? SiteId { get; private set; }
    public string? JobReference { get; private set; }
    public Guid? BulkSubmissionId { get; private set; }
}
