namespace NOIR.Application.Features.Crm.Queries.GetContacts;

public sealed record GetContactsQuery(
    string? Search = null,
    Guid? CompanyId = null,
    Guid? OwnerId = null,
    ContactSource? Source = null,
    int Page = 1,
    int PageSize = 20,
    string? OrderBy = null,
    bool IsDescending = true);
