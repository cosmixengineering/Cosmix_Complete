using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.SiteReports;

/// <summary>Immutable employee answer to one returned review; original received evidence stays unchanged.</summary>
public sealed class SiteReportResponse : IAppendOnlyEntity
{
    private SiteReportResponse() { }
    public SiteReportResponse(Guid id, Guid siteReportId, int sequence, Guid returnedReviewId, Guid employeeId,
        Guid submittedByAccountId, DateTimeOffset submittedAtUtc, string privateEvidenceReference)
    {
        Id = ModelGuard.Id(id, nameof(id)); SiteReportId = ModelGuard.Id(siteReportId, nameof(siteReportId));
        Sequence = AttendanceModelGuard.Revision(sequence); ReturnedReviewId = ModelGuard.Id(returnedReviewId, nameof(returnedReviewId));
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId)); SubmittedByAccountId = ModelGuard.Id(submittedByAccountId, nameof(submittedByAccountId));
        SubmittedAtUtc = ModelGuard.Utc(submittedAtUtc);
        PrivateEvidenceReference = ModelGuard.Required(privateEvidenceReference, 500, nameof(privateEvidenceReference));
    }
    public Guid Id { get; private set; }
    public Guid SiteReportId { get; private set; }
    public int Sequence { get; private set; }
    public Guid ReturnedReviewId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid SubmittedByAccountId { get; private set; }
    public DateTimeOffset SubmittedAtUtc { get; private set; }
    public string PrivateEvidenceReference { get; private set; } = string.Empty;
}
