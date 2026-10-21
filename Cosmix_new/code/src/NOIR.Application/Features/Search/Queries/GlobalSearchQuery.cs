namespace NOIR.Application.Features.Search.Queries;

public sealed record GlobalSearchQuery(
    string Search,
    int MaxPerCategory = 5);
