using System.ComponentModel.DataAnnotations;
using Cosmix.Api.Authentication;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Authentication;

[ApiController, Authorize, Route("api/v1/auth/web/authenticator")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None), RequestSizeLimit(16384)]
public sealed class WebMfaController(WebMfaManagementService management, WebMfaResetService resets) : ControllerBase
{
    [HttpGet]
    public Task<WebMfaStatusResponse> Status(CancellationToken ct) => management.StatusAsync(Browser(), ct);
    [HttpPost("enroll")]
    public Task<WebMfaSetupResponse> Begin(WebMfaPasswordBody body, CancellationToken ct) =>
        management.BeginAsync(new(body.Password, Browser()), Context(), ct);
    [HttpPost("confirm")]
    public async Task<WebMfaRecoveryCodesResponse> Confirm(WebMfaConfirmationBody body, CancellationToken ct)
    {
        var result = await management.ConfirmAsync(new(body.CredentialId, body.Code, Browser()), Context(), ct);
        WebSessionCookies.Clear(HttpContext); return result;
    }
    [HttpPost("recovery-codes")]
    public Task<WebMfaRecoveryCodesResponse> Regenerate(WebMfaRegenerationBody body, CancellationToken ct) =>
        management.RegenerateAsync(new(body.Password, body.Code, Browser()), Context(), ct);
    [HttpPost("accounts/{accountId:guid}/reset")]
    public Task<WebMfaResetResponse> Reset(Guid accountId, WebMfaResetBody body, CancellationToken ct) =>
        resets.ApproveAsync(accountId, new(body.ExpectedAccountVersion, body.Reason, body.ApprovalReference), Context(), ct);
    private string Browser() => WebSessionCookies.RequireBrowser(HttpContext);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", HttpContext.TraceIdentifier);
}
public sealed record WebMfaPasswordBody([Required, StringLength(1024)] string Password);
public sealed record WebMfaConfirmationBody(Guid CredentialId, [Required, RegularExpression("^[0-9]{6}$")] string Code);
public sealed record WebMfaRegenerationBody([Required, StringLength(1024)] string Password, [Required, RegularExpression("^[0-9]{6}$")] string Code);
public sealed record WebMfaResetBody([Required, StringLength(12, MinimumLength = 12)] string ExpectedAccountVersion,
    [Required, StringLength(1000, MinimumLength = 10)] string Reason, [Required, StringLength(500)] string ApprovalReference);
