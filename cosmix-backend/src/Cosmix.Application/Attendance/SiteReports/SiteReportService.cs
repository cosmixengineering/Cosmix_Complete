using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Attendance.SiteReports;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.EmployeeDocuments;

namespace Cosmix.Application.Attendance.SiteReports;

public sealed class SiteReportService(AttendanceOperation operation, ISiteReportRepository repository,
    IPrivateFileStore files, IEmployeeFileScanner scanner, INotificationOutbox notifications,
    SiteReportResponseEvidence responseEvidence, IAccountAccessReader access)
{
    public Task<SiteReportDetailResponse> SubmitAsync(SubmitSiteReportRequest input, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (input is null) throw SiteReportRules.Invalid();
        if (input.Attachment is not null) SiteReportRules.ValidateAttachment(input.Attachment);
        // Capture a stable byte array before digesting/scanning. Never put attachment bytes in audit JSON.
        var request = input with { Attachment = input.Attachment is null ? null :
            input.Attachment with { Content = input.Attachment.Content.ToArray() } };
        var attachment = request.Attachment;
        var payload = new { request.SiteId, request.Kind, request.Notes, request.TaskReference,
            request.DeviceCapturedAtUtc, request.Latitude, request.Longitude, request.AccuracyMeters, request.IsOffline,
            attachment = attachment is null ? null : new { attachment.FileName, attachment.ContentType,
                sizeBytes = attachment.Content.Length, sha256 = Convert.ToHexString(SHA256.HashData(attachment.Content)) } };
        return operation.WriteAsync(SiteReportPermissions.Self, "attendance.site-report.submitted", null,
            key, payload, "Employee submitted an operational site report", context, async (actor, token) =>
            {
                var date = SiteReportRules.ValidateSubmission(request, actor.Now);
                // A permanent business-key check prevents a purged/expired receipt from creating another report.
                if (await repository.HasSubmissionKeyAsync(actor.Identity.EmployeeId, key, token))
                    throw SiteReportRules.Conflict("site_report_already_received");
                var author = await repository.FindAuthorAsync(actor.Identity.AccountId, actor.DeviceId!.Value, token);
                if (author is null || author.Account.EmployeeId != actor.Identity.EmployeeId ||
                    author.Device.UserAccountId != author.Account.Id || author.Device.RevokedAt.HasValue ||
                    author.Device.VerifiedAt is null || author.Device.VerifiedAt > actor.Now)
                    throw new AuthenticationFailure("invalid_authentication", 401);
                if (request.IsOffline && request.DeviceCapturedAtUtc < author.Device.RegisteredAt)
                    throw SiteReportRules.Invalid("report_capture_precedes_registered_device");
                var assignment = SiteReportRules.SelectAssignment(request,
                    await repository.AssignmentsAsync(actor.Identity.EmployeeId, date, token), date, actor.Now);
                if (assignment.Assignment.EmployeeId != actor.Identity.EmployeeId)
                    throw SiteReportRules.Conflict("report_site_assignment_unavailable");
                string? evidence = null;
                if (attachment is not null)
                {
                    // The existing content scanner has a configured bounded timeout. No unscanned file is made available.
                    var scan = await scanner.ScanAsync(attachment.Content, token);
                    if (scan.State == DocumentScanStatus.Rejected)
                        throw SiteReportRules.Invalid("report_attachment_rejected");
                    if (scan.State != DocumentScanStatus.Clean || string.IsNullOrWhiteSpace(scan.ScannerVersion))
                        throw new AuthenticationFailure("report_attachment_scanner_unavailable", 503);
                    var contentReference = await files.WriteAsync(attachment.Content,
                        EvidencePurpose(actor.Identity.EmployeeId, key) + ":content", token);
                    var envelope = new EvidenceEnvelope(actor.Identity.EmployeeId, request.SiteId,
                        attachment.FileName, attachment.ContentType, contentReference,
                        scan.ScannerVersion, actor.Now);
                    evidence = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(envelope),
                        EvidencePurpose(actor.Identity.EmployeeId, key), token));
                }
                var report = new SiteReport(actor.Identity.EmployeeId, request.SiteId, assignment.Assignment.Id,
                    request.Kind, request.TaskReference, request.Notes, author.Account, author.Device, key, actor.Now,
                    request.DeviceCapturedAtUtc, request.Latitude, request.Longitude, request.AccuracyMeters,
                    request.IsOffline, evidence);
                repository.Add(report);
                // Only the submitter receives this confirmation. Reviewer recipients require confirmed authority.
                await notifications.StageAsync(new(report.Id, actor.Identity.AccountId,
                    NotificationTemplates.SiteReport, report.Id), context, token);
                return new AttendanceChange<SiteReportDetailResponse>(report.Id, null,
                    SiteReportRules.Detail(new(report, [])));
            }, ct, self: true, requireDevice: true);
    }

    public Task<PagedResult<SiteReportSummaryResponse>> MineAsync(SiteReportListRequest request,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(SiteReportPermissions.Self, "attendance.site-report.mine", null, context,
            (actor, token) => ListAsync(actor.Identity.EmployeeId, null, request, token), ct, self: true);

    public Task<SiteReportDetailResponse> MineDetailAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(SiteReportPermissions.Self, "attendance.site-report.own-detail", null, context,
            async (actor, token) => await responseEvidence.DetailAsync(await FindAsync(id, actor.Identity.EmployeeId, null, token), token), ct, self: true);

    public Task<SiteReportReconciliationResponse> ReconcileAsync(string key, AuthRequestContext context, CancellationToken ct)
    {
        SiteReportRules.ValidateSubmissionKey(key);
        return operation.ReadAsync(SiteReportPermissions.Self, "attendance.site-report.reconciled", null, context,
            async (actor, token) =>
            {
                var row = await repository.FindSubmissionAsync(actor.Identity.EmployeeId, actor.Identity.AccountId, key, token)
                    ?? throw SiteReportRules.Missing();
                if (row.Report.EmployeeId != actor.Identity.EmployeeId || row.Report.UserAccountId != actor.Identity.AccountId ||
                    !string.Equals(row.Report.IdempotencyKey, key, StringComparison.Ordinal))
                    throw SiteReportRules.Missing();
                if (row.Reviews.Count > 256) throw SiteReportRules.Conflict("site_report_review_history_limit");
                if (row.Responses?.Count > 256) throw SiteReportRules.Conflict("site_report_response_history_limit");
                return new SiteReportReconciliationResponse("current_retained_report", await responseEvidence.DetailAsync(row, token));
            }, ct, self: true);
    }

    public Task<SiteReportEvidenceDownload> MineEvidenceAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(SiteReportPermissions.Self, "attendance.site-report.own-evidence", null, context,
            async (actor, token) => await DownloadAsync(await FindAsync(id, actor.Identity.EmployeeId, null, token), token), ct, self: true);

    public Task<PagedResult<SiteReportSummaryResponse>> QueueAsync(Guid siteId, SiteReportListRequest request,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(SiteReportPermissions.Read, "attendance.site-report.queue", null, context,
            (_, token) => ListAsync(null, siteId, request, token), ct, resources: (_, _) => Scope(siteId));

    public Task<PagedResult<SiteReportQueueSiteResponse>> QueueSitesAsync(SiteReportQueueListRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        SiteReportRules.ValidateQueueList(request);
        var allSites = false;
        IReadOnlyList<Guid> siteIds = [];
        return operation.ReadAsync(SiteReportPermissions.Read, "attendance.site-report.queue-sites", null, context,
            async (_, token) =>
            {
                var page = await repository.QueueSitesAsync(allSites, siteIds, request, token);
                if (page.Items.Any(x => x.SiteId == Guid.Empty || !allSites && !siteIds.Contains(x.SiteId)))
                    throw AttendanceOperation.Denied();
                return page;
            }, ct, resources: async (actor, token) =>
            {
                // Resolve only this permission's current grants inside the existing authorization transaction.
                var grants = AccessPermissionEvaluator.ForSession(actor.Identity,
                    await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, token))
                    .Where(x => x.Permission == SiteReportPermissions.Read).ToArray();
                allSites = AccessPermissionEvaluator.HasAllScope(actor.Identity, grants, SiteReportPermissions.Read);
                siteIds = allSites ? [] : grants.Where(x => x.ScopeKind == AccessScopeKind.Site)
                    .Select(x => x.TargetId!.Value).Distinct().ToArray();
                return allSites ? [new AccessResource()] : siteIds.Select(x => new AccessResource(SiteId: x)).ToArray();
            });
    }

    public Task<SiteReportDetailResponse> DetailAsync(Guid siteId, Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(SiteReportPermissions.Read, "attendance.site-report.detail", null, context,
            async (_, token) => await responseEvidence.DetailAsync(await FindAsync(id, null, siteId, token), token), ct,
            resources: (_, _) => Scope(siteId));

    public Task<SiteReportEvidenceDownload> EvidenceAsync(Guid siteId, Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(SiteReportPermissions.Read, "attendance.site-report.evidence", null, context,
            async (_, token) => await DownloadAsync(await FindAsync(id, null, siteId, token), token), ct,
            resources: (_, _) => Scope(siteId));

    private async Task<PagedResult<SiteReportSummaryResponse>> ListAsync(Guid? employeeId, Guid? siteId,
        SiteReportListRequest request, CancellationToken ct)
    {
        var dates = SiteReportRules.ValidateList(request);
        var rows = await repository.ListAsync(employeeId, siteId, dates.From, dates.ToExclusive, request, ct);
        return new(rows.Items.Select(SiteReportRules.Summary).ToArray(), rows.TotalCount, rows.Page, rows.PageSize);
    }

    private async Task<SiteReportReadModel> FindAsync(Guid id, Guid? employeeId, Guid? siteId, CancellationToken ct)
    {
        if (id == Guid.Empty) throw SiteReportRules.Invalid();
        var row = await repository.FindAsync(id, employeeId, siteId, ct) ?? throw SiteReportRules.Missing();
        if (row.Report.Id != id || employeeId.HasValue && row.Report.EmployeeId != employeeId || siteId.HasValue && row.Report.SiteId != siteId)
            throw SiteReportRules.Missing();
        return row;
    }

    private async Task<SiteReportEvidenceDownload> DownloadAsync(SiteReportReadModel row, CancellationToken ct)
    {
        var report = row.Report;
        if (report.PrivateEvidenceReference is null) throw SiteReportRules.Missing();
        var envelope = await operation.ReadPrivateAsync<EvidenceEnvelope>(report.PrivateEvidenceReference,
            EvidencePurpose(report.EmployeeId, report.IdempotencyKey), ct);
        if (envelope.EmployeeId != report.EmployeeId || envelope.SiteId != report.SiteId ||
            envelope.File is null || string.IsNullOrWhiteSpace(envelope.ScannerVersion))
            throw SiteReportRules.Conflict("report_evidence_unavailable");
        var content = await files.ReadAsync(envelope.File, EvidencePurpose(report.EmployeeId, report.IdempotencyKey) + ":content", ct);
        if (content.LongLength != envelope.File.SizeBytes || envelope.File.Sha256 != Convert.ToHexString(SHA256.HashData(content)))
            throw SiteReportRules.Conflict("report_evidence_unavailable");
        SiteReportRules.ValidateAttachment(new(envelope.FileName, envelope.ContentType, content));
        return new(content, envelope.FileName, envelope.ContentType);
    }

    private static Task<IReadOnlyList<AccessResource>> Scope(Guid siteId)
    {
        if (siteId == Guid.Empty) throw SiteReportRules.Invalid();
        // The query is constrained to this exact site; a client cannot broaden it with an employee/team claim.
        return Task.FromResult<IReadOnlyList<AccessResource>>([new(SiteId: siteId)]);
    }
    private static string EvidencePurpose(Guid employeeId, string key) =>
        $"attendance:site-report:{employeeId:N}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))}";
    private sealed record EvidenceEnvelope(Guid EmployeeId, Guid SiteId, string FileName, string ContentType,
        PrivateFileReference File, string ScannerVersion, DateTimeOffset ScannedAtUtc);
}
