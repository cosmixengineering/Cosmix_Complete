using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.SiteReports;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

public sealed class LeaveEvidence(AttendanceOperation operation, IPrivateFileStore files, IEmployeeFileScanner scanner)
{
    public async Task<string?> StoreAsync(Guid requestId, Guid employeeId, IReadOnlyList<LeaveAttachmentRequest> attachments, CancellationToken ct)
    {
        if (attachments.Count == 0) return null;
        var stored = new List<StoredLeaveAttachment>();
        foreach (var item in attachments)
        {
            var scan = await scanner.ScanAsync(item.Content, ct);
            if (scan.State == DocumentScanStatus.Rejected) throw AttendanceOperation.Invalid("leave_evidence_rejected");
            if (scan.State != DocumentScanStatus.Clean || string.IsNullOrWhiteSpace(scan.ScannerVersion))
                throw new AuthenticationFailure("leave_evidence_scanner_unavailable", 503);
            var reference = await files.WriteAsync(item.Content, Purpose(requestId) + ":" + item.Kind, ct);
            stored.Add(new(item.Kind, item.FileName, item.ContentType, reference, scan.ScannerVersion));
        }
        return AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new LeaveEvidenceEnvelope(requestId, employeeId, stored)), Purpose(requestId), ct));
    }
    public async Task<IReadOnlyList<StoredLeaveAttachment>> ReadAsync(LeaveRequest request, CancellationToken ct)
    {
        if (request.EvidencePrivateReference is null) return [];
        var evidence = await operation.ReadPrivateAsync<LeaveEvidenceEnvelope>(request.EvidencePrivateReference, Purpose(request.Id), ct);
        if (evidence.RequestId != request.Id || evidence.EmployeeId != request.EmployeeId || evidence.Attachments is not { Count: > 0 and <= 4 } ||
            evidence.Attachments.Any(x => x.File is null || string.IsNullOrWhiteSpace(x.ScannerVersion)))
            throw AttendanceOperation.Conflict("leave_evidence_unavailable");
        return evidence.Attachments;
    }
    public async Task<LeaveEvidenceDownload> DownloadAsync(LeaveRequest request, LeaveEvidenceKind kind, CancellationToken ct,
        Func<CancellationToken, Task>? authorize = null)
    {
        if (authorize is not null) await authorize(ct);
        var attachments = await ReadAsync(request, ct);
        if (authorize is not null) await authorize(ct);
        var item = attachments.SingleOrDefault(x => x.Kind == kind) ?? throw AttendanceOperation.Missing("leave_evidence_not_found");
        var content = await files.ReadAsync(item.File, Purpose(request.Id) + ":" + kind, ct);
        if (authorize is not null) await authorize(ct);
        if (content.LongLength != item.File.SizeBytes || Convert.ToHexString(SHA256.HashData(content)) != item.File.Sha256)
            throw AttendanceOperation.Conflict("leave_evidence_unavailable");
        SiteReportRules.ValidateAttachment(new(item.FileName, item.ContentType, content));
        return new(content, item.FileName, item.ContentType);
    }
    private static string Purpose(Guid id) => $"hr:leave:{id:N}:evidence";
    private sealed record LeaveEvidenceEnvelope(Guid RequestId, Guid EmployeeId, IReadOnlyList<StoredLeaveAttachment> Attachments);
}
public sealed record StoredLeaveAttachment(LeaveEvidenceKind Kind, string FileName, string ContentType, PrivateFileReference File, string ScannerVersion);
