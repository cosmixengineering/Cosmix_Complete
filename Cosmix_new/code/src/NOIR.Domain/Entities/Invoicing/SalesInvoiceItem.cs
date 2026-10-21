namespace NOIR.Domain.Entities.Invoicing;

/// <summary>
/// Line item for customer Sales Invoice.
/// </summary>
public class SalesInvoiceItem : TenantEntity<Guid>
{
    private SalesInvoiceItem() : base() { }

    internal SalesInvoiceItem(
        Guid salesInvoiceId,
        Guid productId,
        string description,
        decimal quantity,
        string unit,
        decimal unitPrice,
        decimal taxRate,
        string? tenantId = null)
        : base(Guid.NewGuid(), tenantId)
    {
        SalesInvoiceId = salesInvoiceId;
        ProductId = productId;
        Description = description;
        Quantity = quantity;
        Unit = unit;
        UnitPrice = unitPrice;
        TaxRate = taxRate;
        Subtotal = quantity * unitPrice;
        TaxAmount = Subtotal * (taxRate / 100m);
        TotalAmount = Subtotal + TaxAmount;
    }

    public Guid SalesInvoiceId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = "Pcs";
    public decimal UnitPrice { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal TaxRate { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
}
