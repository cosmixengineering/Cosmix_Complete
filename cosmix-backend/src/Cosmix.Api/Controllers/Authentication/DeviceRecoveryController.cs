using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Authentication;

[ApiController, Authorize, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None), RequestSizeLimit(8192)]
[Route("api/v1/access/accounts/{accountId:guid}/device-recovery")]
public sealed class DeviceRecoveryController(DeviceRecoveryService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DeviceRecoveryReviewResponse>> Review(Guid accountId,
        [FromQuery] AccessListRequest request, CancellationToken ct) =>
        Ok(await service.ReviewAsync(accountId, request, Context(), ct));

    [HttpPost("{deviceId:guid}")]
    public async Task<ActionResult<DeviceRecoveryApprovalResponse>> Approve(Guid accountId, Guid deviceId,
        ApproveDeviceRecoveryRequest request, CancellationToken ct) =>
        Ok(await service.ApproveAsync(accountId, deviceId, request, Context(), ct));

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", HttpContext.TraceIdentifier);
}
