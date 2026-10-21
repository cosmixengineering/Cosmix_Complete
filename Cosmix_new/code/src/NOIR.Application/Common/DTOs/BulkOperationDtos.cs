namespace NOIR.Application.Common.DTOs;

/// <summary>
/// Result of a bulk operation.
/// </summary>
public sealed record BulkOperationResultDto(
    int Success,
    int Failed,
    List<BulkOperationErrorDto> Errors);

/// <summary>
/// Error details for a failed bulk operation item.
/// </summary>
public sealed record BulkOperationErrorDto(
    Guid EntityId,
    string? EntityName,
    string Message);
