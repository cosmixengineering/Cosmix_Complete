using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.PayrollHandoff;

/// <summary>Accounts processing confirmation for a locked batch; does not implement salary disbursement or the finance general ledger.</summary>
public sealed class PayrollHandoffProcessingRecord : IAppendOnlyEntity
{
    private PayrollHandoffProcessingRecord() { }

    public PayrollHandoffProcessingRecord(Guid payrollHandoffBatchId, string externalProcessingReference, string protectedResultReference, string processedByUserId, DateTimeOffset processedAt)
    {
        Id = Guid.NewGuid();
        PayrollHandoffBatchId = ModelGuard.Id(payrollHandoffBatchId, nameof(payrollHandoffBatchId));
        ExternalProcessingReference = ModelGuard.Required(externalProcessingReference, 150, nameof(externalProcessingReference));
        ProtectedResultReference = ModelGuard.Required(protectedResultReference, 500, nameof(protectedResultReference));
        ProcessedByUserId = ModelGuard.Required(processedByUserId, 200, nameof(processedByUserId));
        ProcessedAt = ModelGuard.Utc(processedAt);
    }

    public Guid Id { get; private set; }
    public Guid PayrollHandoffBatchId { get; private set; }
    public string ExternalProcessingReference { get; private set; } = string.Empty;
    public string ProtectedResultReference { get; private set; } = string.Empty;
    public string ProcessedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; private set; }
    public PayrollHandoffBatch PayrollHandoffBatch { get; private set; } = null!;
}
