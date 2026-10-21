using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.SiteReports;

public enum SiteReportReviewOutcome { Accepted = 1, Returned = 2, Rejected = 3, Escalated = 4 }

public sealed class SiteReportReview : IAppendOnlyEntity
{
    private SiteReportReview() { }
    public SiteReportReview(Guid siteReportId, int sequence, SiteReportReviewOutcome outcome, string reviewer,
        string reviewerRole, string reason, DateTimeOffset reviewedAtUtc, Guid? approvalRequestId = null)
    {
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        Id = Guid.NewGuid(); SiteReportId = ModelGuard.Id(siteReportId, nameof(siteReportId)); Sequence = AttendanceModelGuard.Revision(sequence); Outcome = outcome;
        Reviewer = ModelGuard.Required(reviewer, 200, nameof(reviewer)); ReviewerRole = ModelGuard.Required(reviewerRole, 200, nameof(reviewerRole));
        Reason = ModelGuard.Required(reason, 2000, nameof(reason)); ReviewedAtUtc = ModelGuard.Utc(reviewedAtUtc);
        ApprovalRequestId = AttendanceModelGuard.OptionalId(approvalRequestId, nameof(approvalRequestId));
    }
    public Guid Id { get; private set; }
    public Guid SiteReportId { get; private set; }
    public int Sequence { get; private set; }
    public SiteReportReviewOutcome Outcome { get; private set; }
    public string Reviewer { get; private set; } = string.Empty;
    public string ReviewerRole { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset ReviewedAtUtc { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
    public SiteReport Report { get; private set; } = null!;
}
