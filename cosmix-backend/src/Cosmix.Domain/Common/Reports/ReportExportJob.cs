using Cosmix.Domain.Common.Files;

namespace Cosmix.Domain.Common.Reports;

public enum ReportFormat { Csv = 1, Xlsx = 2, Pdf = 3 }
public enum ReportJobState { Pending = 0, Running = 1, Completed = 2, Failed = 3 }

/// <summary>Persisted job provenance, not a report table or an authorization decision.</summary>
public sealed class ReportExportJob
{
    private ReportExportJob() { }
    public ReportExportJob(string requestedByUserId, string reportType, ReportFormat format,
        string protectedFilterScopeReference, DateTimeOffset requestedAt, DateOnly? periodFrom = null, DateOnly? periodTo = null)
    {
        if (!Enum.IsDefined(format)) throw new ArgumentOutOfRangeException(nameof(format));
        if (periodFrom.HasValue != periodTo.HasValue || periodTo < periodFrom) throw new ArgumentException("Invalid report period.");
        Id = Guid.NewGuid(); RequestedByUserId = ModelGuard.Required(requestedByUserId, 200, nameof(requestedByUserId));
        ReportType = ModelGuard.Required(reportType, 100, nameof(reportType)); Format = format;
        ProtectedFilterScopeReference = ModelGuard.Required(protectedFilterScopeReference, 500, nameof(protectedFilterScopeReference));
        RequestedAt = ModelGuard.Utc(requestedAt); PeriodFrom = periodFrom; PeriodTo = periodTo;
    }
    public Guid Id { get; private set; }
    public string RequestedByUserId { get; private set; } = string.Empty;
    public string ReportType { get; private set; } = string.Empty;
    public ReportFormat Format { get; private set; }
    public string ProtectedFilterScopeReference { get; private set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; private set; }
    public DateOnly? PeriodFrom { get; private set; }
    public DateOnly? PeriodTo { get; private set; }
    public ReportJobState State { get; private set; }
    public Guid? StoredFileId { get; private set; }
    public StoredFile? StoredFile { get; private set; }
    public DateTimeOffset? GeneratedAt { get; private set; }
    public DateTimeOffset? DownloadExpiresAt { get; private set; }
    public string? SourceLockReference { get; private set; }
    public string? FailureCode { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Start()
    {
        if (State != ReportJobState.Pending) throw new InvalidOperationException("Only a pending report can start.");
        State = ReportJobState.Running;
    }
    public void Complete(Guid storedFileId, string sourceLockReference, DateTimeOffset generatedAt, DateTimeOffset downloadExpiresAt)
    {
        if (State != ReportJobState.Running || generatedAt < RequestedAt || downloadExpiresAt <= generatedAt)
            throw new InvalidOperationException("Invalid report completion.");
        var fileId = ModelGuard.Id(storedFileId, nameof(storedFileId));
        var locks = ModelGuard.Required(sourceLockReference, 500, nameof(sourceLockReference));
        StoredFileId = fileId; SourceLockReference = locks; GeneratedAt = ModelGuard.Utc(generatedAt);
        DownloadExpiresAt = ModelGuard.Utc(downloadExpiresAt); State = ReportJobState.Completed;
    }
    public void Fail(string failureCode)
    {
        if (State != ReportJobState.Running) throw new InvalidOperationException("Only a running report can fail.");
        FailureCode = ModelGuard.Required(failureCode, 100, nameof(failureCode)); State = ReportJobState.Failed;
    }
}
