using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Authentication.Web;
using Cosmix.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Authentication;

[ApiController]
[Authorize]
[Route("api/v1/access")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccessController(IAccessManagementService service) : ControllerBase
{
    [HttpGet("/api/v1/me/permissions")]
    public Task<ActionResult<IReadOnlyList<PermissionGrant>>> CurrentPermissions(CancellationToken cancellationToken) =>
        Respond(() => service.GetCurrentPermissionsAsync(Context(), cancellationToken));

    [HttpGet("permissions")]
    public Task<ActionResult<IReadOnlyList<AccessPermissionDefinition>>> Catalog(CancellationToken cancellationToken) =>
        Respond(() => service.GetCatalogAsync(Context(), cancellationToken));

    [HttpGet("roles")]
    public Task<ActionResult<PagedResult<AccessRoleResponse>>> Roles([FromQuery] AccessListRequest request,
        CancellationToken cancellationToken) => Respond(() => service.ListRolesAsync(request, Context(), cancellationToken));

    [HttpPost("roles")]
    [EndpointDescription("Requires a current fixed administrator web MFA session and access.roles.create. Send an Idempotency-Key of 8-100 ASCII letters, digits, underscore, dot, colon or hyphen. Identical authorized retries within 24 hours return the original result; changed payloads with the same key conflict.")]
    public Task<ActionResult<AccessRoleResponse>> CreateRole(CreateAccessRoleRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken) => Respond(() => service.CreateRoleAsync(request, idempotencyKey, Context(), cancellationToken));

    [HttpPost("roles/{roleId:guid}/permissions")]
    [EndpointSummary("Add selected catalog permissions to an unassigned role")]
    [EndpointDescription("Current fixed Admin/SuperAdmin web MFA and access.roles.permissions.manage are required. Send 1-10 supported permission codes, expectedRoleVersion, a nonempty reason (up to 1000 characters, no control characters) and Idempotency-Key. The reason and precise change are retained in protected audit evidence. Fixed or previously assigned roles cannot change. Identical authorized retries within 24 hours return the original result; changed payloads with the same key conflict. New writes require current role versions.")]
    public Task<ActionResult<AccessRoleResponse>> GrantPermissions(Guid roleId, GrantRolePermissionsRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken) =>
        Respond(() => service.GrantRolePermissionsAsync(roleId, request, idempotencyKey, Context(), cancellationToken));

    [HttpGet("accounts/{accountId:guid}")]
    public Task<ActionResult<AccessAccountResponse>> Account(Guid accountId, CancellationToken cancellationToken) =>
        Respond(() => service.GetAccountAsync(accountId, Context(), cancellationToken));

    [HttpGet("accounts/{accountId:guid}/assignments")]
    public Task<ActionResult<PagedResult<AccessAssignmentResponse>>> Assignments(Guid accountId,
        [FromQuery] AccessListRequest request, CancellationToken cancellationToken) =>
        Respond(() => service.ListAssignmentsAsync(accountId, request, Context(), cancellationToken));

    [HttpPost("accounts/{accountId:guid}/assignments")]
    [EndpointSummary("Assign an explicit role and scope to an employee account")]
    [EndpointDescription("Current fixed Admin/SuperAdmin web MFA with the existing access-management permissions is required. Only SuperAdmin may change protected Admin/SuperAdmin access. Send the selected role, explicit scopes and dates, expected account/role versions, a nonempty reason (up to 1000 characters, no control characters) and Idempotency-Key. Identical retries within 24 hours return the original assignment and versions after current actor, target employment, role, scope and protected-account checks. Changed payloads with the same key conflict. Self-assignment is denied. New writes require current versions; replay is historical acknowledgement, not renewed access.")]
    public Task<ActionResult<AccessAssignmentResult>> Assign(Guid accountId, AssignAccessRoleRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken) => Respond(() => service.AssignRoleAsync(accountId, request, idempotencyKey, Context(), cancellationToken));

    [HttpPost("accounts/{accountId:guid}/assignments/{assignmentId:guid}/revoke")]
    public Task<ActionResult<AccessAccountResponse>> Revoke(Guid accountId, Guid assignmentId,
        RevokeAccessAssignmentRequest request, CancellationToken cancellationToken) =>
        Respond(() => service.RevokeAssignmentAsync(accountId, assignmentId, request, Context(), cancellationToken));

    [HttpPut("accounts/{accountId:guid}/mobile-access")]
    public Task<ActionResult<AccessAccountResponse>> MobileAccess(Guid accountId, SetAccountChannelRequest request,
        CancellationToken cancellationToken) => Respond(() => service.SetMobileAccessAsync(accountId, request, Context(), cancellationToken));

    [HttpPut("accounts/{accountId:guid}/web-access")]
    public Task<ActionResult<AccessAccountResponse>> WebAccess(Guid accountId, SetAccountChannelRequest request,
        CancellationToken cancellationToken) => Respond(() => service.SetWebAccessAsync(accountId, request, Context(), cancellationToken));

    [HttpPost("accounts/{accountId:guid}/web-credentials"), RequestSizeLimit(8192)]
    public Task<ActionResult<AccessAccountResponse>> ProvisionWebCredentials(Guid accountId,
        ProvisionWebCredentialsRequest request, [FromServices] WebCredentialProvisioningService provisioning,
        CancellationToken cancellationToken) =>
        Respond(() => provisioning.ProvisionAsync(accountId, request, Context(), cancellationToken));

    [HttpPost("accounts/{accountId:guid}/web-credentials/reset-initial-password"), RequestSizeLimit(16384)]
    public Task<ActionResult<AccessAccountResponse>> ResetInitialWebPassword(Guid accountId,
        ResetInitialWebPasswordRequest request, [FromServices] WebCredentialProvisioningService provisioning,
        CancellationToken cancellationToken) =>
        Respond(() => provisioning.ResetInitialPasswordAsync(accountId, request, Context(), cancellationToken));

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
        HttpContext.TraceIdentifier);

    private async Task<ActionResult<T>> Respond<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (AuthenticationFailure failure)
        {
            return Problem(statusCode: failure.StatusCode, title: failure.Code,
                extensions: new Dictionary<string, object?> { ["code"] = failure.Code });
        }
    }
}
