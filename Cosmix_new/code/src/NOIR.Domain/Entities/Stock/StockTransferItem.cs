namespace NOIR.Domain.Entities.Stock;

/// <summary>
/// A single product line within a <see cref="StockTransfer"/>.
/// </summary>
public class StockTransferItem : TenantEntity<Guid>
{
    private StockTransferItem()
    {
    }

    public Guid StockTransferId { get; private set; }

    public Guid ProductId { get; private set; }

    public decimal Quantity { get; private set; }

    public string Unit { get; private set; } = "pcs";

    public string? Remarks { get; private set; }

    public static StockTransferItem Create(
        Guid stockTransferId,
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

        return new StockTransferItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StockTransferId = stockTransferId,
            ProductId = productId,
            Quantity = quantity,
            Unit = string.IsNullOrWhiteSpace(unit) ? "pcs" : unit.Trim(),
            Remarks = remarks?.Trim()
        };
    }
}
