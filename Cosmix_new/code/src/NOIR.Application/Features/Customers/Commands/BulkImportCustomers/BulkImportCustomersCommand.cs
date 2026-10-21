namespace NOIR.Application.Features.Customers.Commands.BulkImportCustomers;

/// <summary>
/// Command to bulk import customers from parsed data.
/// </summary>
public sealed record BulkImportCustomersCommand(List<ImportCustomerDto> Customers);

/// <summary>
/// Single customer row for import.
/// </summary>
public sealed record ImportCustomerDto(
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string? Tags);

/// <summary>
/// Result of a bulk import operation.
/// </summary>
public sealed record BulkImportCustomersResultDto(
    int Success,
    int Failed,
    List<CustomerImportErrorDto> Errors);

/// <summary>
/// Error detail for a single import row.
/// </summary>
public sealed record CustomerImportErrorDto(int Row, string Message);
