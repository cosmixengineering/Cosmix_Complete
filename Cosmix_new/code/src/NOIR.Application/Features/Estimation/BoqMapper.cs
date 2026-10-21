namespace NOIR.Application.Features.Estimation;

/// <summary>
/// Mapper for Boq entity to DTOs.
/// </summary>
public static class BoqMapper
{
    /// <summary>
    /// Maps a Boq entity to a full BoqDto.
    /// </summary>
    public static BoqDto ToDto(Boq boq) => new(
        boq.Id,
        boq.BoqNumber,
        boq.Title,
        boq.Description,
        boq.CustomerId,
        boq.ProjectId,
        boq.Status,
        boq.Currency,
        boq.Subtotal,
        boq.MarkupPercent,
        boq.MarkupAmount,
        boq.TotalAmount,
        boq.ValidUntil,
        boq.Notes,
        boq.Items.Count,
        boq.CreatedAt,
        boq.ModifiedAt);

    /// <summary>
    /// Maps a Boq entity to a BoqListDto.
    /// </summary>
    public static BoqListDto ToListDto(Boq boq) => new(
        boq.Id,
        boq.BoqNumber,
        boq.Title,
        boq.Status,
        boq.TotalAmount,
        boq.Currency,
        boq.Items.Count,
        boq.CustomerId,
        boq.CreatedAt,
        boq.ModifiedAt);

    /// <summary>
    /// Maps a BoqItem entity to a BoqItemDto.
    /// </summary>
    public static BoqItemDto ToItemDto(BoqItem item) => new(
        item.Id,
        item.BoqId,
        item.Section,
        item.ItemCode,
        item.Description,
        item.Quantity,
        item.Unit,
        item.UnitRate,
        item.Amount,
        item.SortOrder);
}
