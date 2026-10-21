using NOIR.Domain.Entities.Helpdesk;

namespace NOIR.Web.Endpoints;

public static class TicketEndpoints
{
    public static void MapTicketEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/helpdesk/tickets")
            .WithTags("Helpdesk - Tickets")
            .RequireAuthorization();

        // Get all tickets
        group.MapGet("/", async ([FromQuery] TicketStatus? status, [FromQuery] TicketPriority? priority, IApplicationDbContext dbContext) =>
        {
            var query = dbContext.HelpdeskTickets.Include(x => x.Comments).AsNoTracking();
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (priority.HasValue) query = query.Where(x => x.Priority == priority.Value);

            var tickets = await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
            return Results.Ok(tickets);
        })
        .WithName("GetHelpdeskTickets");

        // Get ticket by ID
        group.MapGet("/{id:guid}", async (Guid id, IApplicationDbContext dbContext) =>
        {
            var ticket = await dbContext.HelpdeskTickets.Include(x => x.Comments).FirstOrDefaultAsync(x => x.Id == id);
            return ticket is not null ? Results.Ok(ticket) : Results.NotFound();
        })
        .WithName("GetHelpdeskTicketById");

        // Create ticket
        group.MapPost("/", async (CreateTicketRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var ticket = Ticket.Create(req.Title, req.Description, req.Priority, req.UserId, req.AssignedToId);
            dbContext.HelpdeskTickets.Add(ticket);
            await unitOfWork.SaveChangesAsync();
            return Results.Created($"/api/helpdesk/tickets/{ticket.Id}", ticket);
        })
        .WithName("CreateHelpdeskTicket");

        // Add comment to ticket
        group.MapPost("/{id:guid}/comments", async (Guid id, [FromBody] AddTicketCommentRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var ticket = await dbContext.HelpdeskTickets.FindAsync(id);
            if (ticket is null) return Results.NotFound();

            ticket.AddComment(req.Text, req.UserId);
            await unitOfWork.SaveChangesAsync();
            return Results.Ok(ticket);
        })
        .WithName("AddTicketComment");

        // Update status
        group.MapPut("/{id:guid}/status", async (Guid id, [FromBody] UpdateTicketStatusRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var ticket = await dbContext.HelpdeskTickets.FindAsync(id);
            if (ticket is null) return Results.NotFound();

            ticket.UpdateStatus(req.Status);
            await unitOfWork.SaveChangesAsync();
            return Results.Ok(ticket);
        })
        .WithName("UpdateTicketStatus");
    }
}

public record CreateTicketRequest(
    string Title,
    string Description,
    TicketPriority Priority,
    Guid UserId,
    Guid? AssignedToId);

public record AddTicketCommentRequest(string Text, Guid UserId);
public record UpdateTicketStatusRequest(TicketStatus Status);
