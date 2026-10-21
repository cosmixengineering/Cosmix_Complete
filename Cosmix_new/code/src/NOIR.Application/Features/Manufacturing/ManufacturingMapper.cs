namespace NOIR.Application.Features.Manufacturing;

/// <summary>
/// Mapper for Manufacturing entities to DTOs.
/// </summary>
public static class ManufacturingMapper
{
    /// <summary>
    /// Rounds a quantity to 4 decimal places (snapshot precision).
    /// </summary>
    public static decimal RoundQuantity(decimal value) =>
        Math.Round(value, 4, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Resolves display names for the given product IDs.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, string>> GetProductNamesAsync(
        IRepository<Product, Guid> productRepository,
        IEnumerable<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        var ids = productIds.Where(id => id != Guid.Empty).Distinct().ToList();
        var products = await productRepository.ListAsync(new ProductsByIdsSpec(ids), cancellationToken);
        return products.ToDictionary(p => p.Id, p => p.Name);
    }

    /// <summary>
    /// Maps a BOM component item to its DTO.
    /// </summary>
    public static BillOfMaterialItemDto ToItemDto(
        BillOfMaterialItem item,
        IReadOnlyDictionary<Guid, string>? productNames = null) => new(
        item.Id,
        item.BillOfMaterialId,
        item.ProductId,
        productNames is not null && productNames.TryGetValue(item.ProductId, out var itemName) ? itemName : null,
        item.Quantity,
        item.Unit,
        item.WastePercent,
        item.EffectiveQuantity,
        item.Notes);

    /// <summary>
    /// Maps a BillOfMaterial entity to a full BillOfMaterialDto.
    /// </summary>
    public static BillOfMaterialDto ToDto(
        BillOfMaterial bom,
        IReadOnlyDictionary<Guid, string>? productNames = null) => new(
        bom.Id,
        bom.Code,
        bom.Name,
        bom.ProductId,
        productNames is not null && productNames.TryGetValue(bom.ProductId, out var productName) ? productName : null,
        bom.OutputQuantity,
        bom.Unit,
        bom.IsActive,
        bom.Description,
        bom.Items.Count,
        bom.CreatedAt,
        bom.ModifiedAt,
        bom.Items.Select(i => ToItemDto(i, productNames)).ToList());

    /// <summary>
    /// Maps a BillOfMaterial entity to a BillOfMaterialListDto.
    /// </summary>
    public static BillOfMaterialListDto ToListDto(BillOfMaterial bom) => new(
        bom.Id,
        bom.Code,
        bom.Name,
        bom.ProductId,
        bom.IsActive,
        bom.Items.Count,
        bom.OutputQuantity,
        bom.Unit,
        bom.CreatedAt,
        bom.ModifiedAt);

    /// <summary>
    /// Maps a production order material item to its DTO.
    /// </summary>
    public static ProductionOrderItemDto ToItemDto(
        ProductionOrderItem item,
        IReadOnlyDictionary<Guid, string>? productNames = null) => new(
        item.Id,
        item.ProductionOrderId,
        item.ProductId,
        productNames is not null && productNames.TryGetValue(item.ProductId, out var itemName) ? itemName : null,
        item.RequiredQuantity,
        item.IssuedQuantity,
        item.Unit,
        item.Notes);

    /// <summary>
    /// Maps a ProductionOrder entity to a full ProductionOrderDto.
    /// </summary>
    public static ProductionOrderDto ToDto(
        ProductionOrder order,
        IReadOnlyDictionary<Guid, string>? productNames = null,
        string? bomCode = null) => new(
        order.Id,
        order.OrderNumber,
        order.BillOfMaterialId,
        bomCode,
        order.ProductId,
        productNames is not null && productNames.TryGetValue(order.ProductId, out var productName) ? productName : null,
        order.PlannedQuantity,
        order.ProducedQuantity,
        order.ScrapQuantity,
        order.Status,
        order.PlannedStartDate,
        order.PlannedEndDate,
        order.Notes,
        order.CreatedByUserId,
        order.CompletedByUserId,
        order.CompletedAt,
        order.CreatedAt,
        order.ModifiedAt,
        order.Items.Select(i => ToItemDto(i, productNames)).ToList());

    /// <summary>
    /// Maps a ProductionOrder entity to a ProductionOrderListDto.
    /// </summary>
    public static ProductionOrderListDto ToListDto(ProductionOrder order) => new(
        order.Id,
        order.OrderNumber,
        order.ProductId,
        order.Status,
        order.PlannedQuantity,
        order.ProducedQuantity,
        order.PlannedStartDate,
        order.PlannedEndDate,
        order.CreatedAt,
        order.ModifiedAt);
}
