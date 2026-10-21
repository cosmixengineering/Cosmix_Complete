using NOIR.Domain.Entities.Engineering;

namespace NOIR.Web.Endpoints;

public static class EngineeringEndpoints
{
    public static void MapEngineeringEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/engineering/submittals")
            .WithTags("Engineering - Technical Submittals")
            .RequireAuthorization();

        // Get submittals
        group.MapGet("/", async ([FromQuery] Guid? projectId, [FromQuery] SubmittalStatus? status, IApplicationDbContext dbContext) =>
        {
            var query = dbContext.Submittals.AsNoTracking();
            if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);

            var list = await query.OrderByDescending(x => x.SubmittedAt).ToListAsync();
            return Results.Ok(list);
        })
        .WithName("GetSubmittals");

        // Create submittal
        group.MapPost("/", async (CreateSubmittalRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var submittal = Submittal.Create(
                req.SubmittalNumber,
                req.ProjectId,
                req.BoqId,
                req.Title,
                req.SpecificationSection,
                req.DocumentUrl);

            dbContext.Submittals.Add(submittal);
            await unitOfWork.SaveChangesAsync();
            return Results.Created($"/api/engineering/submittals/{submittal.Id}", submittal);
        })
        .WithName("CreateSubmittal");

        // Review submittal
        group.MapPost("/{id:guid}/review", async (Guid id, [FromBody] ReviewSubmittalRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var submittal = await dbContext.Submittals.FindAsync(id);
            if (submittal is null) return Results.NotFound();

            submittal.Review(req.Status, req.ReviewedBy, req.Comments);
            await unitOfWork.SaveChangesAsync();
            return Results.Ok(submittal);
        })
        .WithName("ReviewSubmittal");
    }
}

public record CreateSubmittalRequest(
    string SubmittalNumber,
    Guid ProjectId,
    Guid? BoqId,
    string Title,
    string? SpecificationSection,
    string? DocumentUrl);

public record ReviewSubmittalRequest(SubmittalStatus Status, string ReviewedBy, string? Comments);
