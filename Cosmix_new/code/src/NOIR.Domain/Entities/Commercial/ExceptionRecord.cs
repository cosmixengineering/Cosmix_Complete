namespace NOIR.Domain.Entities.Commercial;

/// <summary>
/// Discrepancy exception record (Ported directly from invoice-processor project).
/// Logs discrepancies between Customer Purchase Orders, Quotations, and Invoices.
/// </summary>
public class ExceptionRecord : TenantEntity<Guid>
{
    private ExceptionRecord() : base() { }

    public ExceptionRecord(Guid documentId, string documentType, string reason, string? tenantId = null)
        : base(Guid.NewGuid(), tenantId)
    {
        DocumentId = documentId;
        DocumentType = documentType;
        Reason = reason;
        Timestamp = DateTimeOffset.UtcNow;
    }

    public Guid DocumentId { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset Timestamp { get; private set; } = DateTimeOffset.UtcNow;
}
