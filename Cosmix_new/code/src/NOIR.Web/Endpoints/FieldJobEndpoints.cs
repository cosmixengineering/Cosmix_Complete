using NOIR.Domain.Entities.Fsm;

namespace NOIR.Web.Endpoints;

/// <summary>
/// Field Service Management (FSM) API endpoints.
/// Manages field jobs, technician dispatch, check-in GPS coordinates, and OTP completion.
/// </summary>
public static class FieldJobEndpoints
{
    public static void MapFieldJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/fsm/jobs")
            .WithTags("Field Service Management")
            .RequireAuthorization();

        // Get field jobs
        group.MapGet("/", async (
            [FromQuery] JobStatus? status,
            [FromQuery] JobType? type,
            [FromQuery] Guid? technicianId,
            IApplicationDbContext dbContext) =>
        {
            var query = dbContext.FieldJobs.AsNoTracking();

            if (status.HasValue)
            {
                query = query.Where(x => x.Status == status.Value);
            }

            if (type.HasValue)
            {
                query = query.Where(x => x.Type == type.Value);
            }

            if (technicianId.HasValue)
            {
                query = query.Where(x => x.TechnicianId == technicianId.Value);
            }

            var jobs = await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
            return Results.Ok(jobs);
        })
        .WithName("GetFieldJobs")
        .WithSummary("Get list of field jobs with optional status/technician filter");

        // Get field job by ID
        group.MapGet("/{id:guid}", async (Guid id, IApplicationDbContext dbContext) =>
        {
            var job = await dbContext.FieldJobs.FindAsync(id);
            return job is not null ? Results.Ok(job) : Results.NotFound();
        })
        .WithName("GetFieldJobById");

        // Create field job
        group.MapPost("/", async (CreateFieldJobRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var job = FieldJob.Create(
                req.JobNumber,
                req.Title,
                req.CustomerId,
                req.ProjectId,
                req.TechnicianId,
                req.EquipmentDetails,
                req.Type,
                req.SiteAddress,
                req.SiteLatitude,
                req.SiteLongitude,
                req.ScheduledDate,
                req.CustomerOtp,
                req.Notes);

            dbContext.FieldJobs.Add(job);
            await unitOfWork.SaveChangesAsync();

            return Results.Created($"/api/fsm/jobs/{job.Id}", job);
        })
        .WithName("CreateFieldJob");

        // Assign technician
        group.MapPost("/{id:guid}/assign", async (Guid id, [FromBody] AssignTechnicianRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var job = await dbContext.FieldJobs.FindAsync(id);
            if (job is null) return Results.NotFound();

            job.AssignTechnician(req.TechnicianId);
            await unitOfWork.SaveChangesAsync();

            return Results.Ok(job);
        })
        .WithName("AssignTechnicianToFieldJob");

        // Start field job
        group.MapPost("/{id:guid}/start", async (Guid id, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var job = await dbContext.FieldJobs.FindAsync(id);
            if (job is null) return Results.NotFound();

            job.StartJob();
            await unitOfWork.SaveChangesAsync();

            return Results.Ok(job);
        })
        .WithName("StartFieldJob");

        // Complete field job with OTP
        group.MapPost("/{id:guid}/complete", async (Guid id, [FromBody] CompleteFieldJobRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var job = await dbContext.FieldJobs.FindAsync(id);
            if (job is null) return Results.NotFound();

            job.CompleteJob(req.EnteredOtp, req.MaterialUsage);
            await unitOfWork.SaveChangesAsync();

            return Results.Ok(job);
        })
        .WithName("CompleteFieldJob");
    }
}

public record CreateFieldJobRequest(
    string JobNumber,
    string Title,
    Guid? CustomerId,
    Guid? ProjectId,
    Guid? TechnicianId,
    string? EquipmentDetails,
    JobType Type,
    string? SiteAddress,
    double? SiteLatitude,
    double? SiteLongitude,
    DateTimeOffset? ScheduledDate,
    string? CustomerOtp,
    string? Notes);

public record AssignTechnicianRequest(Guid TechnicianId);
public record CompleteFieldJobRequest(string? EnteredOtp, string? MaterialUsage);
