namespace NOIR.Application.Features.Crm.DTOs;

/// <summary>
/// Full company details for detail view.
/// </summary>
public sealed record CompanyDto(
    Guid Id,
    string Name,
    string? Domain,
    string? Industry,
    string? Address,
    string? Phone,
    string? Website,
    Guid? OwnerId,
    string? OwnerName,
    string? TaxId,
    int? EmployeeCount,
    string? Notes,
    int ContactCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastModifiedAt);

/// <summary>
/// Simplified company for list/table views.
/// </summary>
public sealed record CompanyListDto(
    Guid Id,
    string Name,
    string? Domain,
    string? Industry,
    string? OwnerName,
    int ContactCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt,
    string? CreatedByName = null,
    string? ModifiedByName = null);

/// <summary>
/// Request body for creating a company.
/// </summary>
public sealed record CreateCompanyRequest(
    string Name,
    string? Domain = null,
    string? Industry = null,
    string? Address = null,
    string? Phone = null,
    string? Website = null,
    Guid? OwnerId = null,
    string? TaxId = null,
    int? EmployeeCount = null,
    string? Notes = null);

/// <summary>
/// Request body for updating a company.
/// </summary>
public sealed record UpdateCompanyRequest(
    string Name,
    string? Domain = null,
    string? Industry = null,
    string? Address = null,
    string? Phone = null,
    string? Website = null,
    Guid? OwnerId = null,
    string? TaxId = null,
    int? EmployeeCount = null,
    string? Notes = null);
