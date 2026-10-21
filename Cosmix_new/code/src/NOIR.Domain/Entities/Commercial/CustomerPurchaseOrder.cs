namespace NOIR.Domain.Entities.Commercial;

public enum CustomerPoStatus
{
    Received = 1,
    ReconciledWithQuote = 2,
    DiscrepancyFound = 3,
    Accepted = 4,
    Rejected = 5
}

/// <summary>
/// Customer Purchase Order (PO) aggregate root for Commercial Hub (SRS Sec 26, 27).
/// Reconciles received customer PO with approved quotations and highlights scope/price discrepancies.
/// </summary>
public class CustomerPurchaseOrder : TenantAggregateRoot<Guid>
{
    private CustomerPurchaseOrder() : base() { }

    private CustomerPurchaseOrder(Guid id, string? tenantId) : base(id, tenantId) { }

    public string CustomerPoNumber { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public Guid? BoqId { get; private set; }
    public DateTimeOffset PoDate { get; private set; } = DateTimeOffset.UtcNow;
    public decimal PoAmount { get; private set; }
    public string Currency { get; private set; } = "USD";
    public CustomerPoStatus Status { get; private set; } = CustomerPoStatus.Received;
    public string? DocumentUrl { get; private set; }
    public string? DeliveryTerms { get; private set; }
    public string? PaymentTerms { get; private set; }
    public string? ScopeOfWork { get; private set; }
    public string? DiscrepancyNotes { get; private set; }

    public static CustomerPurchaseOrder Create(
        string customerPoNumber,
        Guid customerId,
        Guid? projectId,
        Guid? boqId,
        DateTimeOffset poDate,
        decimal poAmount,
        string currency,
        string? documentUrl,
        string? deliveryTerms,
        string? paymentTerms,
        string? scopeOfWork,
        string? tenantId = null)
    {
        return new CustomerPurchaseOrder(Guid.NewGuid(), tenantId)
        {
            CustomerPoNumber = customerPoNumber,
            CustomerId = customerId,
            ProjectId = projectId,
            BoqId = boqId,
            PoDate = poDate,
            PoAmount = poAmount,
            Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency,
            DocumentUrl = documentUrl,
            DeliveryTerms = deliveryTerms,
            PaymentTerms = paymentTerms,
            ScopeOfWork = scopeOfWork,
            Status = CustomerPoStatus.Received
        };
    }

    public void RecordDiscrepancy(string discrepancyNotes)
    {
        DiscrepancyNotes = discrepancyNotes;
        Status = CustomerPoStatus.DiscrepancyFound;
    }

    public void MarkReconciled()
    {
        Status = CustomerPoStatus.ReconciledWithQuote;
    }

    public void Accept()
    {
        Status = CustomerPoStatus.Accepted;
    }

    public void Reject(string reason)
    {
        Status = CustomerPoStatus.Rejected;
        DiscrepancyNotes = string.IsNullOrEmpty(DiscrepancyNotes) ? $"Rejected: {reason}" : $"{DiscrepancyNotes} | Rejected: {reason}";
    }
}
