using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Authentication;

[ApiController]
[Route("api/v1/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(4096)]
public sealed class MobileAuthenticationController(MobileAuthenticationService service, AccountSessionService sessions) : ControllerBase
{
    [AllowAnonymous, HttpPost("mobile/otp/request")]
    public async Task<ActionResult<OtpRequestResponse>> RequestOtp(OtpRequest request, CancellationToken ct) =>
        Accepted(await service.RequestAsync(request, Context(), ct));

    [AllowAnonymous, HttpPost("mobile/otp/resend")]
    public async Task<ActionResult<OtpRequestResponse>> ResendOtp(OtpResendRequest request, CancellationToken ct)
    {
        if (request.Request is null || request.ChallengeId == Guid.Empty) return BadRequest();
        return Accepted(await service.RequestAsync(request.Request, Context(), ct, request.ChallengeId));
    }

    [AllowAnonymous, HttpPost("mobile/otp/verify")]
    public Task<SessionResponse> VerifyOtp(OtpVerifyRequest request, CancellationToken ct) => service.VerifyAsync(request, Context(), ct);

    [AllowAnonymous, HttpPost("refresh")]
    public Task<SessionResponse> Refresh(RefreshRequest request, CancellationToken ct) => service.RefreshAsync(request, Context(), ct);

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct) { await sessions.LogoutAsync(Context(), ct); return NoContent(); }

    [Authorize, HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct) { await sessions.LogoutAsync(Context(), ct, all: true); return NoContent(); }

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", HttpContext.TraceIdentifier);
}
