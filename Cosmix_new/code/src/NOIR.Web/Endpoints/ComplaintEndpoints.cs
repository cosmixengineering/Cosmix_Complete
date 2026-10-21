using NOIR.Domain.Entities.Complaints;

namespace NOIR.Web.Endpoints;

/// <summary>
/// After-Sales Customer Complaints API endpoints.
/// Supports complaint logging, technician assignments, and resolution tracking.
/// </summary>
public static class ComplaintEndpoints
{
    public static void MapComplaintEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/after-sales/complaints")
            .WithTags("After-Sales Complaints")
            .RequireAuthorization();

        // Get complaints
        group.MapGet("/", async (
            [FromQuery] ComplaintStatus? status,
            [FromQuery] ComplaintPriority? priority,
            [FromQuery] Guid? customerId,
            IApplicationDbContext dbContext) =>
        {
            var query = dbContext.CustomerComplaints.AsNoTracking();

            if (status.HasValue)
            {
                query = query.Where(x => x.Status == status.Value);
            }

            if (priority.HasValue)
            {
                query = query.Where(x => x.Priority == priority.Value);
            }

            if (customerId.HasValue)
            {
                query = query.Where(x => x.CustomerId == customerId.Value);
            }

            var complaints = await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
            return Results.Ok(complaints);
        })
        .WithName("GetCustomerComplaints");

        // Get complaint by ID
        group.MapGet("/{id:guid}", async (Guid id, IApplicationDbContext dbContext) =>
        {
            var complaint = await dbContext.CustomerComplaints.FindAsync(id);
            return complaint is not null ? Results.Ok(complaint) : Results.NotFound();
        })
        .WithName("GetCustomerComplaintById");

        // Create complaint
        group.MapPost("/", async (CreateComplaintRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var complaint = CustomerComplaint.Create(
                req.ComplaintNumber,
                req.Subject,
                req.Description,
                req.CustomerId,
                req.ProjectId,
                req.EquipmentDetails,
                req.SiteLocation,
                req.Priority,
                req.AssignedTechnicianId);

            dbContext.CustomerComplaints.Add(complaint);
            await unitOfWork.SaveChangesAsync();

            return Results.Created($"/api/after-sales/complaints/{complaint.Id}", complaint);
        })
        .WithName("CreateCustomerComplaint");

        // Assign technician to complaint
        group.MapPost("/{id:guid}/assign", async (Guid id, [FromBody] AssignComplaintTechnicianRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var complaint = await dbContext.CustomerComplaints.FindAsync(id);
            if (complaint is null) return Results.NotFound();

            complaint.AssignTechnician(req.TechnicianId);
            await unitOfWork.SaveChangesAsync();

            return Results.Ok(complaint);
        })
        .WithName("AssignTechnicianToComplaint");

        // Resolve complaint
        group.MapPost("/{id:guid}/resolve", async (Guid id, [FromBody] ResolveComplaintRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var complaint = await dbContext.CustomerComplaints.FindAsync(id);
            if (complaint is null) return Results.NotFound();

            complaint.Resolve(req.ResolutionNotes);
            await unitOfWork.SaveChangesAsync();

            return Results.Ok(complaint);
        })
        .WithName("ResolveCustomerComplaint");
    }
}

public record CreateComplaintRequest(
    string ComplaintNumber,
    string Subject,
    string Description,
    Guid? CustomerId,
    Guid? ProjectId,
    string? EquipmentDetails,
    string? SiteLocation,
    ComplaintPriority Priority,
    Guid? AssignedTechnicianId);

public record AssignComplaintTechnicianRequest(Guid TechnicianId);
public record ResolveComplaintRequest(string ResolutionNotes);
