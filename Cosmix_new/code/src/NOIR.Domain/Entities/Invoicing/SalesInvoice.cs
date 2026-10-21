namespace NOIR.Domain.Entities.Invoicing;

/// <summary>
/// Customer Sales Invoice aggregate root.
/// Calculates taxes, discounts, milestone project billing, and customer payment status.
/// </summary>
public class SalesInvoice : TenantAggregateRoot<Guid>
{
    private readonly List<SalesInvoiceItem> _items = [];

    private SalesInvoice() : base() { }

    private SalesInvoice(Guid id, string? tenantId) : base(id, tenantId) { }

    public string InvoiceNumber { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public DateTimeOffset IssueDate { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset DueDate { get; private set; }
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;
    public string Currency { get; private set; } = "USD";
    public decimal Subtotal { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal OutstandingAmount => TotalAmount - PaidAmount;
    public string? PaymentTerms { get; private set; }
    public string? Notes { get; private set; }

    public IReadOnlyCollection<SalesInvoiceItem> Items => _items.AsReadOnly();

    public static SalesInvoice Create(
        string invoiceNumber,
        Guid customerId,
        Guid? projectId,
        DateTimeOffset dueDate,
        string currency,
        string? paymentTerms,
        string? notes,
        string? tenantId = null)
    {
        return new SalesInvoice(Guid.NewGuid(), tenantId)
        {
            InvoiceNumber = invoiceNumber,
            CustomerId = customerId,
            ProjectId = projectId,
            IssueDate = DateTimeOffset.UtcNow,
            DueDate = dueDate,
            Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency,
            PaymentTerms = paymentTerms,
            Notes = notes,
            Status = InvoiceStatus.Draft
        };
    }

    public void AddItem(Guid productId, string description, decimal quantity, string unit, decimal unitPrice, decimal taxRate)
    {
        var item = new SalesInvoiceItem(Id, productId, description, quantity, unit, unitPrice, taxRate, TenantId);
        _items.Add(item);
        RecalculateTotals();
    }

    public void Issue()
    {
        Status = InvoiceStatus.Issued;
    }

    public void RecordPayment(decimal paymentAmount)
    {
        PaidAmount += paymentAmount;
        if (PaidAmount >= TotalAmount)
        {
            Status = InvoiceStatus.Paid;
        }
        else if (PaidAmount > 0)
        {
            Status = InvoiceStatus.PartiallyPaid;
        }
    }

    public void MarkVoid()
    {
        Status = InvoiceStatus.Void;
    }

    private void RecalculateTotals()
    {
        Subtotal = _items.Sum(x => x.Subtotal);
        TaxAmount = _items.Sum(x => x.TaxAmount);
        TotalAmount = Subtotal + TaxAmount;
    }
}
