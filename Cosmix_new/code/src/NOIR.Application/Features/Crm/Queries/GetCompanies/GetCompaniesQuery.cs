namespace NOIR.Application.Features.Crm.Queries.GetCompanies;

public sealed record GetCompaniesQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20,
    string? OrderBy = null,
    bool IsDescending = true);
