namespace NOIR.Application.Features.Manufacturing.DTOs;

/// <summary>
/// BOM component line DTO.
/// </summary>
public sealed record BillOfMaterialItemDto(
    Guid Id,
    Guid BillOfMaterialId,
    Guid ProductId,
    string? ProductName,
    decimal Quantity,
    string Unit,
    decimal? WastePercent,
    decimal EffectiveQuantity,
    string? Notes);

/// <summary>
/// Full bill of material details DTO.
/// </summary>
public sealed record BillOfMaterialDto(
    Guid Id,
    string Code,
    string Name,
    Guid ProductId,
    string? ProductName,
    decimal OutputQuantity,
    string Unit,
    bool IsActive,
    string? Description,
    int ItemCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt,
    List<BillOfMaterialItemDto> Items);

/// <summary>
/// Bill of material list item DTO (minimal data for listings).
/// </summary>
public sealed record BillOfMaterialListDto(
    Guid Id,
    string Code,
    string Name,
    Guid ProductId,
    bool IsActive,
    int ItemCount,
    decimal OutputQuantity,
    string Unit,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

/// <summary>
/// Production order material requirement DTO.
/// </summary>
public sealed record ProductionOrderItemDto(
    Guid Id,
    Guid ProductionOrderId,
    Guid ProductId,
    string? ProductName,
    decimal RequiredQuantity,
    decimal IssuedQuantity,
    string Unit,
    string? Notes);

/// <summary>
/// Full production order details DTO.
/// </summary>
public sealed record ProductionOrderDto(
    Guid Id,
    string OrderNumber,
    Guid BillOfMaterialId,
    string? BomCode,
    Guid ProductId,
    string? ProductName,
    decimal PlannedQuantity,
    decimal ProducedQuantity,
    decimal ScrapQuantity,
    ProductionOrderStatus Status,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    string? Notes,
    Guid? CreatedByUserId,
    Guid? CompletedByUserId,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt,
    List<ProductionOrderItemDto> Items);

/// <summary>
/// Production order list item DTO (minimal data for listings).
/// </summary>
public sealed record ProductionOrderListDto(
    Guid Id,
    string OrderNumber,
    Guid ProductId,
    ProductionOrderStatus Status,
    decimal PlannedQuantity,
    decimal ProducedQuantity,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

/// <summary>
/// Request to create a new bill of material.
/// </summary>
public sealed record CreateBillOfMaterialRequest(
    string Code,
    string Name,
    Guid ProductId,
    decimal OutputQuantity,
    string Unit,
    string? Description,
    List<CreateBillOfMaterialItemRequest> Items);

/// <summary>
/// Request to create a single BOM component line (used when creating a BOM).
/// </summary>
public sealed record CreateBillOfMaterialItemRequest(
    Guid ProductId,
    decimal Quantity,
    string Unit = "pcs",
    decimal? WastePercent = null,
    string? Notes = null);

/// <summary>
/// Request to add a single component line to an existing BOM.
/// </summary>
public sealed record AddBillOfMaterialItemRequest(
    Guid ProductId,
    decimal Quantity,
    string Unit = "pcs",
    decimal? WastePercent = null,
    string? Notes = null);

/// <summary>
/// Request to update a bill of material header.
/// </summary>
public sealed record UpdateBillOfMaterialRequest(
    string Name,
    Guid ProductId,
    decimal OutputQuantity,
    string Unit,
    string? Description);

/// <summary>
/// Request to create a new production order (items are snapshotted from the BOM).
/// </summary>
public sealed record CreateProductionOrderRequest(
    string OrderNumber,
    Guid BillOfMaterialId,
    decimal PlannedQuantity,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    string? Notes);

/// <summary>
/// Request to update a draft production order header.
/// </summary>
public sealed record UpdateProductionOrderRequest(
    decimal PlannedQuantity,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    string? Notes);

/// <summary>
/// Request to report production progress (increments).
/// </summary>
public sealed record ReportProductionProgressRequest(
    decimal ProducedQuantity,
    decimal ScrapQuantity);

/// <summary>
/// Request to issue material against a production order requirement.
/// </summary>
public sealed record IssueMaterialRequest(
    Guid ProductionOrderItemId,
    decimal Quantity);

/// <summary>
/// Request to cancel a production order.
/// </summary>
public sealed record CancelProductionOrderRequest(
    string? Reason);
