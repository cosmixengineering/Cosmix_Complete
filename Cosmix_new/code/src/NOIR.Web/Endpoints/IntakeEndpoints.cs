using NOIR.Domain.Entities.Intake;

namespace NOIR.Web.Endpoints;

public static class IntakeEndpoints
{
    public static void MapIntakeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/intake/messages")
            .WithTags("Inbox & Communication")
            .RequireAuthorization();

        // Get intake messages
        group.MapGet("/", async ([FromQuery] IntakeStatus? status, [FromQuery] IntakeChannel? channel, IApplicationDbContext dbContext) =>
        {
            var query = dbContext.IntakeMessages.AsNoTracking();
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (channel.HasValue) query = query.Where(x => x.Channel == channel.Value);

            var messages = await query.OrderByDescending(x => x.ReceivedAt).ToListAsync();
            return Results.Ok(messages);
        })
        .WithName("GetIntakeMessages");

        // Ingest message (from webhook/email/manual)
        group.MapPost("/", async (CreateIntakeMessageRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var msg = IntakeMessage.Create(
                req.MessageNumber,
                req.Channel,
                req.SenderName,
                req.SenderEmail,
                req.SenderPhone,
                req.Subject,
                req.RawBody,
                req.AssignedDepartmentId);

            dbContext.IntakeMessages.Add(msg);
            await unitOfWork.SaveChangesAsync();
            return Results.Created($"/api/intake/messages/{msg.Id}", msg);
        })
        .WithName("CreateIntakeMessage");

        // Assign message
        group.MapPost("/{id:guid}/assign", async (Guid id, [FromBody] AssignIntakeRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var msg = await dbContext.IntakeMessages.FindAsync(id);
            if (msg is null) return Results.NotFound();

            msg.Assign(req.DepartmentId, req.UserId);
            await unitOfWork.SaveChangesAsync();
            return Results.Ok(msg);
        })
        .WithName("AssignIntakeMessage");
    }
}

public record CreateIntakeMessageRequest(
    string MessageNumber,
    IntakeChannel Channel,
    string SenderName,
    string? SenderEmail,
    string? SenderPhone,
    string Subject,
    string RawBody,
    Guid? AssignedDepartmentId);

public record AssignIntakeRequest(Guid DepartmentId, Guid? UserId);
