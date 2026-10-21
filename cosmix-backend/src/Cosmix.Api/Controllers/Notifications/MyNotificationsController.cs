using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Notifications;

[ApiController, Authorize, Route("api/v1/me/notifications")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class MyNotificationsController(NotificationService notifications) : ControllerBase
{
    [HttpGet, ProducesResponseType<PagedResult<NotificationResponse>>(200)]
    [EndpointSummary("List my in-app notifications")]
    [EndpointDescription("Current validated account only. Optional unreadOnly and bounded page/pageSize. Static safe content only; no private payloads, payroll, medical, CNIC or bank data. Mobile bearer or web MFA session.")]
    public Task<PagedResult<NotificationResponse>> List([FromQuery] NotificationListRequest request, CancellationToken ct) =>
        notifications.ListAsync(request, Context(), ct);

    [HttpGet("{id:guid}"), ProducesResponseType<NotificationResponse>(200)]
    [EndpointSummary("Read my notification")]
    [EndpointDescription("Another account's identifier returns the same 404 as an unknown notification. Reading does not mark it read.")]
    public Task<NotificationResponse> Get(Guid id, CancellationToken ct) => notifications.GetAsync(id, Context(), ct);

    [HttpPost("{id:guid}/read"), ProducesResponseType<NotificationReadResponse>(200)]
    [EndpointSummary("Mark my notification read")]
    [EndpointDescription("ExpectedVersion is the base64 SQL rowversion. An unread stale version conflicts. Once read, retries return the preserved read timestamp with changed=false without another mutation. Identity is server-derived; unsafe web cookie calls require CSRF.")]
    public Task<NotificationReadResponse> MarkRead(Guid id, MarkNotificationReadRequest request, CancellationToken ct) =>
        notifications.MarkReadAsync(id, request, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
