using System.ComponentModel.DataAnnotations;
using Cosmix.Api.Authentication;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Authentication;

[ApiController, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None), RequestSizeLimit(8192)]
public sealed class PhoneRecoveryController(PhoneRecoveryService service) : ControllerBase
{
    [Authorize, HttpPost("api/v1/access/accounts/{accountId:guid}/phone-recovery")]
    public async Task<ActionResult<PhoneRecoveryApprovalResponse>> Approve(Guid accountId, ApprovePhoneRecoveryRequest body, CancellationToken ct) =>
        Ok(await service.ApproveAsync(accountId, body, Context(), ct));

    [AllowAnonymous, HttpPost("api/v1/auth/mobile/phone-recovery/request")]
    public async Task<ActionResult<OtpRequestResponse>> MobileRequest(MobilePhoneRecoveryBody body, CancellationToken ct) =>
        Accepted(await service.RequestAsync(new(body.PhoneNumber, body.DeviceSecret, body.Platform), Context(), ct, body.PreviousChallengeId));

    [AllowAnonymous, HttpPost("api/v1/auth/mobile/phone-recovery/verify")]
    public async Task<IActionResult> MobileVerify(MobilePhoneRecoveryVerifyBody body, CancellationToken ct)
    {
        await service.VerifyAsync(new(body.ChallengeId, body.Code, body.DeviceSecret, body.Platform), Context(), ct);
        return NoContent();
    }

    [AllowAnonymous, HttpPost("api/v1/auth/web/phone-recovery/request")]
    public async Task<ActionResult<OtpRequestResponse>> WebRequest(WebPhoneRecoveryBody body, CancellationToken ct) =>
        Accepted(await service.RequestAsync(new(body.PhoneNumber, WebSessionCookies.RequireBrowser(HttpContext), "web"),
            Context(), ct, body.PreviousChallengeId));

    [AllowAnonymous, HttpPost("api/v1/auth/web/phone-recovery/verify")]
    public async Task<IActionResult> WebVerify(WebVerifyBody body, CancellationToken ct)
    {
        await service.VerifyAsync(new(body.ChallengeId, body.Code, WebSessionCookies.RequireBrowser(HttpContext), "web"), Context(), ct);
        WebSessionCookies.Clear(HttpContext);
        return NoContent();
    }
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", HttpContext.TraceIdentifier);
}

public sealed record MobilePhoneRecoveryBody([Required, StringLength(40)] string PhoneNumber,
    [Required, StringLength(256, MinimumLength = 32)] string DeviceSecret,
    [Required, RegularExpression("^(android|ios)$")] string Platform, Guid? PreviousChallengeId = null);
public sealed record MobilePhoneRecoveryVerifyBody(Guid ChallengeId, [Required, RegularExpression("^[0-9]{6}$")] string Code,
    [Required, StringLength(256, MinimumLength = 32)] string DeviceSecret, [Required, RegularExpression("^(android|ios)$")] string Platform);
public sealed record WebPhoneRecoveryBody([Required, StringLength(40)] string PhoneNumber, Guid? PreviousChallengeId = null);
