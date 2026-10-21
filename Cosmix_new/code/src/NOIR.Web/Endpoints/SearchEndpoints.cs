namespace NOIR.Web.Endpoints;

/// <summary>
/// Global search endpoint for searching across multiple entity types.
/// </summary>
public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/search")
            .WithTags("Search")
            .RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery(Name = "q")] string? query,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return Results.Ok(new GlobalSearchResponseDto([], [], [], [], [], 0));

            var result = await bus.InvokeAsync<Result<GlobalSearchResponseDto>>(
                new GlobalSearchQuery(query), ct);

            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.SearchGlobal)
        .WithName("GlobalSearch")
        .WithSummary("Search across all entities")
        .WithDescription("Returns search results from products, orders, customers, blog posts, and users.")
        .Produces<GlobalSearchResponseDto>(StatusCodes.Status200OK);
    }
}
