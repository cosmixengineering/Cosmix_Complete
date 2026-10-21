using System.Security.Cryptography;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Attendance.SiteReports;

namespace Cosmix.Application.Attendance.SiteReports;

public sealed class SiteReportReviewService(AttendanceOperation operation, ISiteReportRepository reports,
    ISiteReportReviewRepository repository, SiteReportResponseEvidence evidence, INotificationOutbox notifications)
{
    public Task<SiteReportDetailResponse> DecideAsync(Guid siteId, Guid id, DecideSiteReportRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        ValidateIds(siteId, id);
        if (request is null || request.ExpectedReviewSequence is < 0 or > 256 || request.ExpectedResponseSequence is < 0 or > 256 ||
            request.Outcome is not (SiteReportReviewOutcome.Accepted or SiteReportReviewOutcome.Returned or SiteReportReviewOutcome.Rejected))
            throw SiteReportRules.Invalid("invalid_site_report_decision");
        return operation.WriteAsync(SiteReportPermissions.Decide, "attendance.site-report.decided", null, key,
            new { siteId, id, request }, request.Reason, context, async (actor, token) =>
            {
                var row = await Find(id, null, siteId, token);
                if (row.Report.UserAccountId == actor.Identity.AccountId || row.Report.EmployeeId == actor.Identity.EmployeeId)
                    throw AttendanceOperation.Denied();
                Head(row, request.ExpectedReviewSequence, request.ExpectedResponseSequence);
                if (SiteReportRules.Summary(row).SubmissionStatus != "received_pending_review")
                    throw SiteReportRules.Conflict("site_report_not_pending_review");
                if (row.Reviews.Count >= 256) throw SiteReportRules.Conflict("site_report_review_history_limit");
                var before = await evidence.DetailAsync(row, token);
                var review = new SiteReportReview(id, request.ExpectedReviewSequence + 1, request.Outcome,
                    actor.Identity.AccountId.ToString(), actor.RoleCode, request.Reason, actor.Now);
                repository.Add(review);
                await notifications.StageAsync(new(review.Id, row.Report.UserAccountId, NotificationTemplates.SiteReport, id), context, token);
                var after = await evidence.DetailAsync(row with { Reviews = row.Reviews.Append(review).ToArray() }, token);
                return new AttendanceChange<SiteReportDetailResponse>(review.Id, before, after);
            }, ct, resources: (_, _) => Scope(siteId));
    }

    public Task<SiteReportDetailResponse> RespondAsync(Guid id, RespondToSiteReportRequest input, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (id == Guid.Empty || input is null || input.ReturnedReviewId == Guid.Empty || input.ExpectedReviewSequence is < 1 or > 256 ||
            input.ExpectedResponseSequence is < 0 or > 255 || string.IsNullOrWhiteSpace(input.WrittenResponse) ||
            input.WrittenResponse.Length > 4000 || input.WrittenResponse.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw SiteReportRules.Invalid("invalid_site_report_response");
        if (input.Attachment is not null) SiteReportRules.ValidateAttachment(input.Attachment);
        var request = input with { Attachment = input.Attachment is null ? null : input.Attachment with { Content = input.Attachment.Content.ToArray() } };
        var attachment = request.Attachment;
        var payload = new { id, request.ExpectedReviewSequence, request.ExpectedResponseSequence, request.ReturnedReviewId,
            request.WrittenResponse, attachment = attachment is null ? null : new { attachment.FileName, attachment.ContentType,
                sizeBytes = attachment.Content.Length, sha256 = Convert.ToHexString(SHA256.HashData(attachment.Content)) } };
        return operation.WriteAsync(SiteReportPermissions.Self, "attendance.site-report.responded", null, key, payload,
            "Employee answered and resubmitted a returned operational report", context, async (actor, token) =>
            {
                var row = await Find(id, actor.Identity.EmployeeId, null, token);
                if (row.Report.UserAccountId != actor.Identity.AccountId) throw SiteReportRules.Missing();
                Head(row, request.ExpectedReviewSequence, request.ExpectedResponseSequence);
                var returned = row.Reviews.LastOrDefault(x => x.Sequence == request.ExpectedReviewSequence);
                if (SiteReportRules.Summary(row).SubmissionStatus != "returned" || returned?.Id != request.ReturnedReviewId)
                    throw SiteReportRules.Conflict("site_report_not_awaiting_response");
                var before = await evidence.DetailAsync(row, token);
                var responseId = Guid.NewGuid();
                var reference = await evidence.StoreAsync(responseId, row.Report, returned, request, actor.Now, token);
                var response = new SiteReportResponse(responseId, id, request.ExpectedResponseSequence + 1, returned.Id,
                    actor.Identity.EmployeeId, actor.Identity.AccountId, actor.Now, reference);
                repository.Add(response);
                await notifications.StageAsync(new(response.Id, actor.Identity.AccountId, NotificationTemplates.SiteReport, id), context, token);
                var after = await evidence.DetailAsync(row with { Responses = (row.Responses ?? []).Append(response).ToArray() }, token);
                return new AttendanceChange<SiteReportDetailResponse>(response.Id, before, after);
            }, ct, self: true);
    }

    public Task<SiteReportEvidenceDownload> OwnResponseEvidenceAsync(Guid id, Guid responseId, AuthRequestContext context, CancellationToken ct)
    {
        ValidateIds(id, responseId);
        return operation.ReadAsync(SiteReportPermissions.Self, "attendance.site-report.own-response-evidence", null, context,
            async (actor, token) => await evidence.DownloadAsync(await Find(id, actor.Identity.EmployeeId, null, token), responseId, token), ct, self: true);
    }
    public Task<SiteReportEvidenceDownload> ResponseEvidenceAsync(Guid siteId, Guid id, Guid responseId, AuthRequestContext context, CancellationToken ct)
    {
        ValidateIds(siteId, id); if (responseId == Guid.Empty) throw SiteReportRules.Invalid();
        return operation.ReadAsync(SiteReportPermissions.Read, "attendance.site-report.response-evidence", null, context,
            async (_, token) => await evidence.DownloadAsync(await Find(id, null, siteId, token), responseId, token), ct,
            resources: (_, _) => Scope(siteId));
    }

    private async Task<SiteReportReadModel> Find(Guid id, Guid? employeeId, Guid? siteId, CancellationToken ct)
    {
        var row = await reports.FindAsync(id, employeeId, siteId, ct) ?? throw SiteReportRules.Missing();
        if (row.Report.Id != id || employeeId.HasValue && row.Report.EmployeeId != employeeId || siteId.HasValue && row.Report.SiteId != siteId)
            throw SiteReportRules.Missing();
        return row;
    }
    private static void Head(SiteReportReadModel row, int reviewSequence, int responseSequence)
    {
        var reviews = row.Reviews.OrderBy(x => x.Sequence).ToArray();
        var responses = (row.Responses ?? []).OrderBy(x => x.Sequence).ToArray();
        if (reviews.Length > 256 || responses.Length > 256 || reviews.Where((x, i) => x.SiteReportId != row.Report.Id || x.Sequence != i + 1).Any() ||
            responses.Where((x, i) => x.SiteReportId != row.Report.Id || x.EmployeeId != row.Report.EmployeeId ||
                x.SubmittedByAccountId != row.Report.UserAccountId || x.Sequence != i + 1 ||
                !reviews.Any(r => r.Id == x.ReturnedReviewId && r.Outcome == SiteReportReviewOutcome.Returned)).Any() ||
            responses.Select(x => x.ReturnedReviewId).Distinct().Count() != responses.Length)
            throw SiteReportRules.Conflict("site_report_history_unavailable");
        if (reviewSequence != reviews.Length || responseSequence != responses.Length)
            throw SiteReportRules.Conflict("site_report_version_conflict");
    }
    private static Task<IReadOnlyList<AccessResource>> Scope(Guid id) => Task.FromResult<IReadOnlyList<AccessResource>>([new(SiteId: id)]);
    private static void ValidateIds(Guid first, Guid second)
    { if (first == Guid.Empty || second == Guid.Empty) throw SiteReportRules.Invalid(); }
}
