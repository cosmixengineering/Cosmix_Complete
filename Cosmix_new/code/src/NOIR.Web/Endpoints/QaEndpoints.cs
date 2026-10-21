namespace NOIR.Web.Endpoints;

public static class QaEndpoints
{
    public static void MapQaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/qa/inspections")
            .WithTags("Quality Assurance - Inspections")
            .RequireAuthorization();

        // Get inspections
        group.MapGet("/", async (
            [FromQuery] QaStage? stage,
            [FromQuery] QaResult? result,
            [FromQuery] Guid? referenceEntityId,
            IApplicationDbContext dbContext) =>
        {
            var query = dbContext.QaInspections.AsNoTracking();
            if (stage.HasValue) query = query.Where(x => x.Stage == stage.Value);
            if (result.HasValue) query = query.Where(x => x.Result == result.Value);
            if (referenceEntityId.HasValue) query = query.Where(x => x.ReferenceEntityId == referenceEntityId.Value);

            var list = await query.OrderByDescending(x => x.InspectedAt).ToListAsync();
            return Results.Ok(list);
        })
        .WithName("GetQaInspections");

        // Get inspection by ID
        group.MapGet("/{id:guid}", async (Guid id, IApplicationDbContext dbContext) =>
        {
            var inspection = await dbContext.QaInspections.FindAsync(id);
            return inspection is not null ? Results.Ok(inspection) : Results.NotFound();
        })
        .WithName("GetQaInspectionById");

        // Create inspection
        group.MapPost("/", async (CreateQaInspectionRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var inspection = QaInspection.Create(
                req.InspectionNumber,
                req.Stage,
                req.ReferenceEntityId,
                req.ReferenceEntityType,
                req.Title,
                req.ChecklistSummary);

            dbContext.QaInspections.Add(inspection);
            await unitOfWork.SaveChangesAsync();
            return Results.Created($"/api/qa/inspections/{inspection.Id}", inspection);
        })
        .WithName("CreateQaInspection");

        // Record inspection result
        group.MapPost("/{id:guid}/result", async (Guid id, [FromBody] RecordQaResultRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var inspection = await dbContext.QaInspections.FindAsync(id);
            if (inspection is null) return Results.NotFound();

            inspection.RecordResult(
                req.Result,
                req.InspectedBy,
                req.DefectNotes,
                req.CorrectiveAction,
                req.EvidenceDocumentUrl);

            await unitOfWork.SaveChangesAsync();
            return Results.Ok(inspection);
        })
        .WithName("RecordQaResult");
    }
}

public record CreateQaInspectionRequest(
    string InspectionNumber,
    QaStage Stage,
    Guid? ReferenceEntityId,
    string? ReferenceEntityType,
    string Title,
    string? ChecklistSummary);

public record RecordQaResultRequest(
    QaResult Result,
    string InspectedBy,
    string? DefectNotes,
    string? CorrectiveAction,
    string? EvidenceDocumentUrl);
