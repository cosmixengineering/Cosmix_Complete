using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.SiteReports;
using Cosmix.Domain.HR.EmployeeDocuments;

namespace Cosmix.Application.Attendance.SiteReports;

/// <summary>Purpose-bound employee answers and optional scanned attachment; never exposes storage references.</summary>
public sealed class SiteReportResponseEvidence(AttendanceOperation operation, IPrivateFileStore files, IEmployeeFileScanner scanner)
{
    public async Task<string> StoreAsync(Guid id, SiteReport report, SiteReportReview returned,
        RespondToSiteReportRequest request, DateTimeOffset now, CancellationToken ct)
    {
        Attachment? attachment = null;
        if (request.Attachment is { } upload)
        {
            SiteReportRules.ValidateAttachment(upload);
            var scan = await scanner.ScanAsync(upload.Content, ct);
            if (scan.State == DocumentScanStatus.Rejected) throw SiteReportRules.Invalid("report_attachment_rejected");
            if (scan.State != DocumentScanStatus.Clean || string.IsNullOrWhiteSpace(scan.ScannerVersion))
                throw new AuthenticationFailure("report_attachment_scanner_unavailable", 503);
            var content = await files.WriteAsync(upload.Content, Purpose(report.Id, id) + ":content", ct);
            attachment = new(upload.FileName, upload.ContentType, content, scan.ScannerVersion, now);
        }
        return AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new Envelope(
            1, id, report.Id, report.EmployeeId, returned.Id, request.WrittenResponse.Trim(), attachment)), Purpose(report.Id, id), ct));
    }

    public async Task<SiteReportDetailResponse> DetailAsync(SiteReportReadModel row, CancellationToken ct)
    {
        var result = new List<SiteReportEmployeeResponse>();
        foreach (var response in (row.Responses ?? []).OrderBy(x => x.Sequence))
        {
            var envelope = await Read(row, response, ct);
            result.Add(new(response.Id, response.Sequence, response.ReturnedReviewId, response.SubmittedAtUtc,
                envelope.WrittenResponse, envelope.Attachment is not null));
        }
        return SiteReportRules.Detail(row) with { Responses = result };
    }

    public async Task<SiteReportEvidenceDownload> DownloadAsync(SiteReportReadModel row, Guid responseId, CancellationToken ct)
    {
        var response = row.Responses?.SingleOrDefault(x => x.Id == responseId) ?? throw SiteReportRules.Missing();
        var envelope = await Read(row, response, ct);
        var attachment = envelope.Attachment ?? throw SiteReportRules.Missing();
        var bytes = await files.ReadAsync(attachment.File, Purpose(row.Report.Id, responseId) + ":content", ct);
        if (bytes.LongLength != attachment.File.SizeBytes || Convert.ToHexString(SHA256.HashData(bytes)) != attachment.File.Sha256)
            throw SiteReportRules.Conflict("report_response_evidence_unavailable");
        SiteReportRules.ValidateAttachment(new(attachment.FileName, attachment.ContentType, bytes));
        return new(bytes, attachment.FileName, attachment.ContentType);
    }

    private async Task<Envelope> Read(SiteReportReadModel row, SiteReportResponse response, CancellationToken ct)
    {
        var returned = row.Reviews.SingleOrDefault(x => x.Id == response.ReturnedReviewId);
        if (response.SiteReportId != row.Report.Id || response.EmployeeId != row.Report.EmployeeId ||
            response.SubmittedByAccountId != row.Report.UserAccountId || returned?.Outcome != SiteReportReviewOutcome.Returned)
            throw SiteReportRules.Conflict("report_response_evidence_unavailable");
        var envelope = await operation.ReadPrivateAsync<Envelope>(response.PrivateEvidenceReference, Purpose(row.Report.Id, response.Id), ct);
        if (envelope.Version != 1 || envelope.Id != response.Id || envelope.ReportId != row.Report.Id ||
            envelope.EmployeeId != row.Report.EmployeeId || envelope.ReturnedReviewId != response.ReturnedReviewId ||
            string.IsNullOrWhiteSpace(envelope.WrittenResponse) || envelope.WrittenResponse.Length > 4000 ||
            envelope.Attachment is { } attachment && (attachment.File is null || string.IsNullOrWhiteSpace(attachment.ScannerVersion)))
            throw SiteReportRules.Conflict("report_response_evidence_unavailable");
        return envelope;
    }

    private static string Purpose(Guid reportId, Guid responseId) => $"attendance:site-report-response:{reportId:N}:{responseId:N}";
    private sealed record Envelope(int Version, Guid Id, Guid ReportId, Guid EmployeeId, Guid ReturnedReviewId,
        string WrittenResponse, Attachment? Attachment);
    private sealed record Attachment(string FileName, string ContentType, PrivateFileReference File,
        string ScannerVersion, DateTimeOffset ScannedAtUtc);
}
