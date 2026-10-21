namespace NOIR.Application.Features.Stock.DTOs;

/// <summary>
/// Full detail of a stock indent including its line items.
/// </summary>
public sealed record StockIndentDto(
    Guid Id,
    string IndentNumber,
    Guid? RequestedByUserId,
    DateOnly? NeededByDate,
    string? Department,
    string? Notes,
    IndentStatus Status,
    string? ApprovedByUserId,
    DateTimeOffset? ApprovedAt,
    string? RejectReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt,
    IReadOnlyList<StockIndentItemDto> Items,
    decimal TotalQuantity);

/// <summary>
/// Summary row of a stock indent used in list views.
/// </summary>
public sealed record StockIndentListDto(
    Guid Id,
    string IndentNumber,
    IndentStatus Status,
    string? Department,
    DateOnly? NeededByDate,
    int ItemCount,
    decimal TotalQuantity,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

/// <summary>
/// A single product line of a stock indent.
/// </summary>
public sealed record StockIndentItemDto(
    Guid Id,
    Guid IndentId,
    Guid ProductId,
    string? ProductName,
    decimal Quantity,
    string Unit,
    string? Remarks);

/// <summary>
/// Full detail of a stock transfer including its line items.
/// </summary>
public sealed record StockTransferDto(
    Guid Id,
    string TransferNumber,
    string FromLocation,
    string ToLocation,
    DateOnly? TransferDate,
    string? Notes,
    TransferStatus Status,
    Guid? ApprovedByUserId,
    DateTimeOffset? ApprovedAt,
    string? RejectReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt,
    IReadOnlyList<StockTransferItemDto> Items,
    decimal TotalQuantity);

/// <summary>
/// Summary row of a stock transfer used in list views.
/// </summary>
public sealed record StockTransferListDto(
    Guid Id,
    string TransferNumber,
    TransferStatus Status,
    string FromLocation,
    string ToLocation,
    DateOnly? TransferDate,
    int ItemCount,
    decimal TotalQuantity,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

/// <summary>
/// A single product line of a stock transfer.
/// </summary>
public sealed record StockTransferItemDto(
    Guid Id,
    Guid StockTransferId,
    Guid ProductId,
    string? ProductName,
    decimal Quantity,
    string Unit,
    string? Remarks);

/// <summary>
/// Item payload used when creating or updating a stock indent.
/// </summary>
public sealed record StockIndentItemRequest(
    Guid ProductId,
    decimal Quantity,
    string Unit,
    string? Remarks);

/// <summary>
/// Item payload used when creating or updating a stock transfer.
/// </summary>
public sealed record StockTransferItemRequest(
    Guid ProductId,
    decimal Quantity,
    string Unit,
    string? Remarks);

/// <summary>
/// HTTP request body for creating a stock indent.
/// </summary>
public sealed record CreateStockIndentRequest(
    string IndentNumber,
    DateOnly? NeededByDate,
    string? Department,
    string? Notes,
    IReadOnlyList<StockIndentItemRequest> Items);

/// <summary>
/// HTTP request body for updating a stock indent header.
/// </summary>
public sealed record UpdateStockIndentRequest(
    DateOnly? NeededByDate,
    string? Department,
    string? Notes);

/// <summary>
/// HTTP request body for adding one item to a stock indent.
/// </summary>
public sealed record AddStockIndentItemRequest(
    Guid ProductId,
    decimal Quantity,
    string Unit,
    string? Remarks);

/// <summary>
/// HTTP request body for rejecting a stock indent.
/// </summary>
public sealed record RejectStockIndentRequest(string? Reason);

/// <summary>
/// HTTP request body for creating a stock transfer.
/// </summary>
public sealed record CreateStockTransferRequest(
    string TransferNumber,
    string FromLocation,
    string ToLocation,
    DateOnly? TransferDate,
    string? Notes,
    IReadOnlyList<StockTransferItemRequest> Items);

/// <summary>
/// HTTP request body for updating a stock transfer header.
/// </summary>
public sealed record UpdateStockTransferRequest(
    string? FromLocation,
    string? ToLocation,
    DateOnly? TransferDate,
    string? Notes);

/// <summary>
/// HTTP request body for adding one item to a stock transfer.
/// </summary>
public sealed record AddStockTransferItemRequest(
    Guid ProductId,
    decimal Quantity,
    string Unit,
    string? Remarks);

/// <summary>
/// HTTP request body for rejecting a stock transfer.
/// </summary>
public sealed record RejectStockTransferRequest(string? Reason);
