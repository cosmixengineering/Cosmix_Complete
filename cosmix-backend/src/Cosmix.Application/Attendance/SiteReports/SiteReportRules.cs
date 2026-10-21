using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.SiteReports;

namespace Cosmix.Application.Attendance.SiteReports;

/// <summary>Report evidence is separate from validated attendance. Device time never proves arrival or work.</summary>
public static class SiteReportRules
{
    public const int MaximumAttachmentBytes = 10 * 1024 * 1024;
    public const string SubmissionKeyPattern = @"\A[A-Za-z0-9_.:-]{8,100}\z";
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi");

    public static DateOnly ValidateSubmission(SubmitSiteReportRequest request, DateTimeOffset now)
    {
        Validate(request);
        if (request.SiteId == Guid.Empty || !Enum.IsDefined(request.Kind) ||
            string.IsNullOrWhiteSpace(request.Notes) || request.Notes.Length > 4000 ||
            request.Notes.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) ||
            request.TaskReference?.Length > 200 || request.TaskReference?.Any(char.IsControl) == true)
            throw Invalid();
        if ((request.IsOffline && request.DeviceCapturedAtUtc is null) ||
            request.DeviceCapturedAtUtc > now || request.DeviceCapturedAtUtc == DateTimeOffset.MinValue)
            throw Invalid("invalid_report_capture_time");
        var locationCount = (request.Latitude.HasValue ? 1 : 0) + (request.Longitude.HasValue ? 1 : 0) +
            (request.AccuracyMeters.HasValue ? 1 : 0);
        if (locationCount is not (0 or 3) || request.Latitude is < -90 or > 90 ||
            request.Longitude is < -180 or > 180 || request.AccuracyMeters is < 0 or > 99999999.99m ||
            request.Latitude.HasValue && decimal.Round(request.Latitude.Value, 7) != request.Latitude ||
            request.Longitude.HasValue && decimal.Round(request.Longitude.Value, 7) != request.Longitude ||
            request.AccuracyMeters.HasValue && decimal.Round(request.AccuracyMeters.Value, 2) != request.AccuracyMeters)
            throw Invalid("invalid_report_location");
        if (request.Attachment is not null) ValidateAttachment(request.Attachment);
        // Offline date selects historical context only; it remains explicitly unverified.
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(request.IsOffline ? request.DeviceCapturedAtUtc!.Value : now, Zone).DateTime);
    }

    public static SiteReportAssignment SelectAssignment(SubmitSiteReportRequest request,
        IReadOnlyList<SiteReportAssignment> matches, DateOnly date, DateTimeOffset now)
    {
        if (matches.Count != 1) throw Conflict(matches.Count == 0 ? "report_site_assignment_unavailable" : "report_site_assignment_conflict");
        var found = matches[0];
        var assignment = found.Assignment;
        if (assignment.SiteId != request.SiteId || assignment.EffectiveFrom > date || assignment.EffectiveTo <= date ||
            found.SiteStartsOn > date || found.SiteEndsOn < date)
            throw Conflict("report_site_assignment_unavailable");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Zone).DateTime);
        if (!found.SiteActive && date >= today) throw Conflict("report_site_unavailable");
        return found;
    }

    public static (DateTimeOffset From, DateTimeOffset ToExclusive) ValidateList(SiteReportListRequest request)
    {
        Validate(request);
        if (request.Page is < 1 or > 1000000 || request.PageSize is < 1 or > 100 ||
            request.From == DateOnly.MinValue || request.To >= DateOnly.MaxValue || request.To < request.From ||
            request.To.DayNumber - request.From.DayNumber > 365)
            throw Invalid("invalid_report_date_range");
        return (Midnight(request.From), Midnight(request.To.AddDays(1)));
    }

    public static void ValidateAttachment(SiteReportAttachmentRequest attachment)
    {
        Validate(attachment);
        if (attachment.Content is null || attachment.Content.Length is <= 0 or > MaximumAttachmentBytes ||
            string.IsNullOrWhiteSpace(attachment.FileName) || attachment.FileName.Length > 180 || attachment.FileName.Trim() != attachment.FileName ||
            attachment.FileName.IndexOfAny(['/', '\\', ':']) >= 0 || attachment.FileName.Any(char.IsControl))
            throw Invalid("invalid_report_attachment");
        var ext = Path.GetExtension(attachment.FileName).ToLowerInvariant();
        var bytes = attachment.Content.AsSpan();
        var png = ext == ".png" && attachment.ContentType == "image/png" && bytes.StartsWith(new byte[] {137,80,78,71,13,10,26,10});
        var jpeg = ext is ".jpg" or ".jpeg" && attachment.ContentType == "image/jpeg" && bytes.StartsWith(new byte[] {255,216,255}) && bytes.EndsWith(new byte[] {255,217});
        var pdf = ext == ".pdf" && attachment.ContentType == "application/pdf" && bytes.StartsWith("%PDF-"u8);
        if (!(png || jpeg || pdf)) throw Invalid("unsupported_report_attachment");
    }

    public static void ValidateQueueList(SiteReportQueueListRequest request)
    {
        if (request is null || request.Page is < 1 or > 1000000 || request.PageSize is < 1 or > 100)
            throw Invalid("invalid_site_report_queue_page");
    }

    public static void ValidateSubmissionKey(string key)
    {
        if (key is null || key.Length is < 8 or > 100 ||
            !Regex.IsMatch(key, SubmissionKeyPattern, RegexOptions.CultureInvariant))
            throw Invalid("invalid_idempotency_key");
    }

    public static SiteReportSummaryResponse Summary(SiteReportReadModel row)
    {
        var report = row.Report;
        var review = row.Reviews.OrderByDescending(x => x.Sequence).FirstOrDefault();
        var response = row.Responses?.OrderByDescending(x => x.Sequence).FirstOrDefault();
        var pending = review is null || review.Outcome == SiteReportReviewOutcome.Returned && response?.ReturnedReviewId == review.Id;
        return new(report.Id, report.EmployeeId, report.SiteId, report.Kind, report.TaskReference,
            report.ReceivedAtUtc, report.DeviceCapturedAtUtc, report.IsOffline,
            pending ? "received_pending_review" : review!.Outcome.ToString().ToLowerInvariant(),
            report.IsOffline ? "unverified_offline_evidence" : "received_unverified_evidence",
            report.PrivateEvidenceReference is not null, review?.Sequence ?? 0, response?.Sequence ?? 0);
    }

    public static SiteReportDetailResponse Detail(SiteReportReadModel row) => new(Summary(row),
        row.Report.EmployeeAssignmentId, row.Report.RegisteredDeviceId, row.Report.Notes,
        row.Report.Latitude, row.Report.Longitude, row.Report.AccuracyMeters, Summary(row).SubmissionStatus switch
        {
            "received_pending_review" => "hr_review_pending", "returned" => "employee_response_required",
            "accepted" or "rejected" => "review_complete", _ => "historical_workflow_unavailable"
        },
        row.Reviews.OrderBy(x => x.Sequence).Select(x => new SiteReportReviewResponse(x.Sequence, x.Outcome,
            x.Reviewer, x.ReviewerRole, x.Reason, x.ReviewedAtUtc, x.Id)).ToArray());

    private static DateTimeOffset Midnight(DateOnly date) => new(TimeZoneInfo.ConvertTimeToUtc(
        date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Zone));
    private static void Validate(object? value)
    {
        if (value is null || !Validator.TryValidateObject(value, new ValidationContext(value), [], true)) throw Invalid();
    }
    public static AuthenticationFailure Invalid(string code = "invalid_site_report_request") => new(code, 400);
    public static AuthenticationFailure Conflict(string code) => new(code, 409);
    public static AuthenticationFailure Missing() => new("site_report_not_found", 404);
}
