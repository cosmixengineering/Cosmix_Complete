namespace NOIR.Application.Features.Estimation.Queries.GetBoqs;

/// <summary>
/// Query to get paged list of BOQs with optional filters.
/// </summary>
public sealed record GetBoqsQuery(
    string? Search = null,
    BoqStatus? Status = null,
    int Page = 1,
    int PageSize = 20);
