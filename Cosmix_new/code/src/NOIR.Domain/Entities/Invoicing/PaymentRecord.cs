namespace NOIR.Domain.Entities.Invoicing;

/// <summary>
/// Customer payment receipt for settling Sales Invoices.
/// </summary>
public class PaymentRecord : TenantAggregateRoot<Guid>
{
    private PaymentRecord() : base() { }

    private PaymentRecord(Guid id, string? tenantId) : base(id, tenantId) { }

    public string PaymentNumber { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public Guid SalesInvoiceId { get; private set; }
    public decimal Amount { get; private set; }
    public InvoicePaymentMethod Method { get; private set; } = InvoicePaymentMethod.BankTransfer;
    public string? TransactionReference { get; private set; }
    public DateTimeOffset PaymentDate { get; private set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; private set; }

    public static PaymentRecord Create(
        string paymentNumber,
        Guid customerId,
        Guid salesInvoiceId,
        decimal amount,
        InvoicePaymentMethod method,
        string? transactionReference,
        string? notes,
        string? tenantId = null)
    {
        return new PaymentRecord(Guid.NewGuid(), tenantId)
        {
            PaymentNumber = paymentNumber,
            CustomerId = customerId,
            SalesInvoiceId = salesInvoiceId,
            Amount = amount,
            Method = method,
            TransactionReference = transactionReference,
            Notes = notes,
            PaymentDate = DateTimeOffset.UtcNow
        };
    }
}
