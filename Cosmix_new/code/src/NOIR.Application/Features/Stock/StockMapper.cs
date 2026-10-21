namespace NOIR.Application.Features.Stock;

/// <summary>
/// Manual mapping between stock entities and their DTOs.
/// </summary>
public static class StockMapper
{
    public static StockIndentDto ToDto(StockIndent indent, IReadOnlyDictionary<Guid, string?>? productNames = null)
    {
        return new StockIndentDto(
            indent.Id,
            indent.IndentNumber,
            indent.RequestedByUserId,
            indent.NeededByDate,
            indent.Department,
            indent.Notes,
            indent.Status,
            indent.ApprovedByUserId,
            indent.ApprovedAt,
            indent.RejectReason,
            indent.CreatedAt,
            indent.ModifiedAt,
            indent.Items.Select(i => ToDto(i, productNames)).ToList(),
            indent.Items.Sum(i => i.Quantity));
    }

    public static StockIndentListDto ToListDto(StockIndent indent)
    {
        return new StockIndentListDto(
            indent.Id,
            indent.IndentNumber,
            indent.Status,
            indent.Department,
            indent.NeededByDate,
            indent.Items.Count,
            indent.Items.Sum(i => i.Quantity),
            indent.CreatedAt,
            indent.ModifiedAt);
    }

    public static StockIndentItemDto ToDto(StockIndentItem item, IReadOnlyDictionary<Guid, string?>? productNames = null)
    {
        return new StockIndentItemDto(
            item.Id,
            item.IndentId,
            item.ProductId,
            productNames?.GetValueOrDefault(item.ProductId),
            item.Quantity,
            item.Unit,
            item.Remarks);
    }

    public static StockTransferDto ToDto(StockTransfer transfer, IReadOnlyDictionary<Guid, string?>? productNames = null)
    {
        return new StockTransferDto(
            transfer.Id,
            transfer.TransferNumber,
            transfer.FromLocation,
            transfer.ToLocation,
            transfer.TransferDate,
            transfer.Notes,
            transfer.Status,
            transfer.ApprovedByUserId,
            transfer.ApprovedAt,
            transfer.RejectReason,
            transfer.CreatedAt,
            transfer.ModifiedAt,
            transfer.Items.Select(i => ToDto(i, productNames)).ToList(),
            transfer.Items.Sum(i => i.Quantity));
    }

    public static StockTransferListDto ToListDto(StockTransfer transfer)
    {
        return new StockTransferListDto(
            transfer.Id,
            transfer.TransferNumber,
            transfer.Status,
            transfer.FromLocation,
            transfer.ToLocation,
            transfer.TransferDate,
            transfer.Items.Count,
            transfer.Items.Sum(i => i.Quantity),
            transfer.CreatedAt,
            transfer.ModifiedAt);
    }

    public static StockTransferItemDto ToDto(StockTransferItem item, IReadOnlyDictionary<Guid, string?>? productNames = null)
    {
        return new StockTransferItemDto(
            item.Id,
            item.StockTransferId,
            item.ProductId,
            productNames?.GetValueOrDefault(item.ProductId),
            item.Quantity,
            item.Unit,
            item.Remarks);
    }
}
