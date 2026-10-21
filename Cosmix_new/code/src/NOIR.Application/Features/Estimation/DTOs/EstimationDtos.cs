namespace NOIR.Application.Features.Estimation.DTOs;

/// <summary>
/// Full BOQ details DTO.
/// </summary>
public sealed record BoqDto(
    Guid Id,
    string BoqNumber,
    string Title,
    string? Description,
    Guid? CustomerId,
    Guid? ProjectId,
    BoqStatus Status,
    string Currency,
    decimal Subtotal,
    decimal MarkupPercent,
    decimal MarkupAmount,
    decimal TotalAmount,
    DateTimeOffset? ValidUntil,
    string? Notes,
    int ItemCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

/// <summary>
/// BOQ list item DTO (minimal data for listings).
/// </summary>
public sealed record BoqListDto(
    Guid Id,
    string BoqNumber,
    string Title,
    BoqStatus Status,
    decimal TotalAmount,
    string Currency,
    int ItemCount,
    Guid? CustomerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

/// <summary>
/// BOQ line item DTO.
/// </summary>
public sealed record BoqItemDto(
    Guid Id,
    Guid BoqId,
    string Section,
    string ItemCode,
    string Description,
    decimal Quantity,
    string Unit,
    decimal UnitRate,
    decimal Amount,
    int SortOrder);

/// <summary>
/// Request to create a new BOQ.
/// </summary>
public sealed record CreateBoqRequest(
    string BoqNumber,
    string Title,
    Guid? CustomerId,
    Guid? ProjectId,
    string Currency = "USD",
    string? Description = null,
    string? Notes = null,
    decimal MarkupPercent = 0);

/// <summary>
/// Request to update an existing BOQ header.
/// </summary>
public sealed record UpdateBoqRequest(
    string Title,
    string? Description,
    Guid? CustomerId,
    Guid? ProjectId,
    decimal MarkupPercent,
    string? Notes);

/// <summary>
/// Request to add a line item to a BOQ.
/// </summary>
public sealed record AddBoqItemRequest(
    string Section,
    string ItemCode,
    string Description,
    decimal Quantity,
    string Unit,
    decimal UnitRate,
    int SortOrder);

/// <summary>
/// Request to update a BOQ line item.
/// </summary>
public sealed record UpdateBoqItemRequest(
    string Section,
    string ItemCode,
    string Description,
    decimal Quantity,
    string Unit,
    decimal UnitRate,
    int SortOrder);
