namespace NOIR.Application.Features.Accounting.Queries.GetAccounts;

/// <summary>
/// Query to get paged list of accounts.
/// </summary>
public sealed record GetAccountsQuery(
    string? Search = null,
    AccountType? Type = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 100);
