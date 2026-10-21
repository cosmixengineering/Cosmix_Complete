using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Authentication;

[ApiController, Authorize]
[Route("api/v1/me")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CurrentAccountController(AccountSessionService service) : ControllerBase
{
    [HttpGet] public Task<CurrentAccountResponse> Me(CancellationToken ct) => service.MeAsync(ct);
    [HttpGet("devices")]
    public Task<IReadOnlyList<DeviceResponse>> Devices(CancellationToken ct,
        [FromQuery, Range(1, 1000000)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 100) =>
        service.DevicesAsync(ct, page, pageSize);
    [HttpGet("sessions")]
    public Task<IReadOnlyList<SessionSummary>> Sessions(CancellationToken ct,
        [FromQuery, Range(1, 1000000)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 100) =>
        service.SessionsAsync(ct, page, pageSize);
    [HttpDelete("devices/{id:guid}")]
    public async Task<IActionResult> RevokeDevice(Guid id, CancellationToken ct) { await service.RevokeDeviceAsync(id, Context(), ct); return NoContent(); }
    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> RevokeSession(Guid id, CancellationToken ct) { await service.LogoutAsync(Context(), ct, sessionId: id); return NoContent(); }
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", HttpContext.TraceIdentifier);
}
