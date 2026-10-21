using NOIR.Domain.Entities.MasterData;

namespace NOIR.Web.Endpoints;

public static class UnitConversionEndpoints
{
    public static void MapUnitConversionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/master-data/unit-conversions")
            .WithTags("Master Data - Unit Conversions")
            .RequireAuthorization();

        // Get conversions
        group.MapGet("/", async ([FromQuery] string? category, IApplicationDbContext dbContext) =>
        {
            var query = dbContext.UnitConversions.AsNoTracking();
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(x => x.Category == category);
            }
            var list = await query.OrderBy(x => x.FromUnit).ToListAsync();
            return Results.Ok(list);
        })
        .WithName("GetUnitConversions");

        // Create conversion
        group.MapPost("/", async (CreateUnitConversionRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var conversion = UnitConversion.Create(
                req.FromUnit,
                req.ToUnit,
                req.ConversionFactor,
                req.Category,
                req.Notes);

            dbContext.UnitConversions.Add(conversion);
            await unitOfWork.SaveChangesAsync();
            return Results.Created($"/api/master-data/unit-conversions/{conversion.Id}", conversion);
        })
        .WithName("CreateUnitConversion");
    }
}

public record CreateUnitConversionRequest(
    string FromUnit,
    string ToUnit,
    decimal ConversionFactor,
    string Category,
    string? Notes);
