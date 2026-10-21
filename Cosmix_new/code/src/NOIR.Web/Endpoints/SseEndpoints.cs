namespace NOIR.Web.Endpoints;

/// <summary>
/// Server-Sent Events (SSE) endpoints.
/// Provides lightweight one-way server-to-client push for job progress and operation status.
/// </summary>
public static class SseEndpoints
{
    public static void MapSseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sse")
            .WithTags("SSE")
            .RequireAuthorization();

        // GET /api/sse/channels/{channelId} - Subscribe to an SSE channel
        // Channel IDs are scoped to the current tenant to prevent cross-tenant subscription
        group.MapGet("/channels/{channelId}", async (
            string channelId,
            ISseEventPublisher publisher,
            ICurrentUser currentUser,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            // Scope channel to tenant to prevent cross-tenant access
            var tenantId = currentUser.TenantId ?? "system";
            var scopedChannelId = $"{tenantId}:{channelId}";

            httpContext.Response.Headers.ContentType = "text/event-stream";
            httpContext.Response.Headers.CacheControl = "no-cache";
            httpContext.Response.Headers.Connection = "keep-alive";
            // Disable response buffering for real-time streaming
            httpContext.Response.Headers["X-Accel-Buffering"] = "no";

            await foreach (var evt in publisher.SubscribeAsync(scopedChannelId, ct))
            {
                if (!string.IsNullOrEmpty(evt.Id))
                {
                    await httpContext.Response.WriteAsync($"id: {evt.Id}\n", ct);
                }

                await httpContext.Response.WriteAsync($"event: {evt.EventType}\n", ct);
                await httpContext.Response.WriteAsync($"data: {evt.Data}\n\n", ct);
                await httpContext.Response.Body.FlushAsync(ct);
            }
        })
        .WithName("SubscribeSseChannel")
        .WithSummary("Subscribe to an SSE channel")
        .WithDescription("Opens a Server-Sent Events stream for the specified channel. Events are pushed as they occur. A heartbeat is sent every 20 seconds to keep the connection alive.")
        .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);
    }
}
