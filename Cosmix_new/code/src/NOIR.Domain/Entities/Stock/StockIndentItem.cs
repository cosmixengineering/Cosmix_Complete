namespace NOIR.Domain.Entities.Stock;

/// <summary>
/// A single product line within a <see cref="StockIndent"/>.
/// </summary>
public class StockIndentItem : TenantEntity<Guid>
{
    private StockIndentItem()
    {
    }

    public Guid IndentId { get; private set; }

    public Guid ProductId { get; private set; }

    public decimal Quantity { get; private set; }

    public string Unit { get; private set; } = "pcs";

    public string? Remarks { get; private set; }

    public static StockIndentItem Create(
        Guid indentId,
        Guid productId,
        decimal quantity,
        string unit,
        string? remarks,
        string? tenantId)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Quantity must be greater than zero.");
        }

        return new StockIndentItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IndentId = indentId,
            ProductId = productId,
            Quantity = quantity,
            Unit = string.IsNullOrWhiteSpace(unit) ? "pcs" : unit.Trim(),
            Remarks = remarks?.Trim()
        };
    }
}
