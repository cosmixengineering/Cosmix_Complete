namespace NOIR.Application.Features.Media.Dtos;

/// <summary>
/// Result of a bulk media operation.
/// </summary>
public sealed record BulkMediaOperationResultDto(
    int Success,
    int Failed,
    List<BulkMediaOperationErrorDto> Errors);

/// <summary>
/// Error details for a failed bulk media operation item.
/// </summary>
public sealed record BulkMediaOperationErrorDto(
    Guid MediaFileId,
    string? FileName,
    string Message);
