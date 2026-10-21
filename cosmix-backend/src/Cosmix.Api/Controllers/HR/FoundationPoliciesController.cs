using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.FoundationPolicies;
using Cosmix.Domain.Common.Approvals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/hr/foundation-policies")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class FoundationPoliciesController(HrFoundationPolicyService service) : ControllerBase
{
    [HttpGet("{kind}/current")]
    [ProducesResponseType<HrFoundationPolicyCurrent>(200)]
    public Task<HrFoundationPolicyCurrent> Current(HrFoundationPolicyKind kind, [FromQuery] DateOnly? date, CancellationToken ct) =>
        service.CurrentAsync(kind, date, Context(), ct);
    [HttpGet("{kind}/history")]
    [ProducesResponseType<PagedResult<HrFoundationPolicyVersionView>>(200)]
    public Task<PagedResult<HrFoundationPolicyVersionView>> History(HrFoundationPolicyKind kind,
        CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 25) => service.HistoryAsync(kind, page, pageSize, Context(), ct);
    [HttpGet("{kind}/requests")]
    [ProducesResponseType<PagedResult<HrFoundationPolicyRequestView>>(200)]
    public Task<PagedResult<HrFoundationPolicyRequestView>> Requests(HrFoundationPolicyKind kind, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] ApprovalState? state = null) =>
        service.RequestsAsync(kind, page, pageSize, state, Context(), ct);
    [HttpGet("{kind}/requests/{requestId:guid}")]
    [ProducesResponseType<HrFoundationPolicyRequestDetail>(200)]
    public Task<HrFoundationPolicyRequestDetail> Detail(HrFoundationPolicyKind kind, Guid requestId, CancellationToken ct) =>
        service.DetailAsync(kind, requestId, Context(), ct);
    [HttpPost("{kind}/requests"), RequestSizeLimit(65536), StrictHrFoundationPolicyJson(typeof(RequestHrFoundationPolicy))]
    [ProducesResponseType<HrFoundationPolicyWriteResult>(200), ProducesResponseType<ProblemDetails>(413)]
    public Task<HrFoundationPolicyWriteResult> RequestPolicy(HrFoundationPolicyKind kind, RequestHrFoundationPolicy input,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) => service.RequestAsync(kind, input, key, Context(), ct);
    [HttpPost("{kind}/requests/{requestId:guid}/decision"), RequestSizeLimit(65536), StrictHrFoundationPolicyJson(typeof(DecideHrFoundationPolicy))]
    [ProducesResponseType<HrFoundationPolicyWriteResult>(200), ProducesResponseType<ProblemDetails>(413)]
    public Task<HrFoundationPolicyWriteResult> Decide(HrFoundationPolicyKind kind, Guid requestId, DecideHrFoundationPolicy input,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) => service.DecideAsync(kind, requestId, input, key, Context(), ct);
    [HttpPost("{kind}/requests/{requestId:guid}/cancel"), RequestSizeLimit(65536), StrictHrFoundationPolicyJson(typeof(CancelHrFoundationPolicy))]
    [ProducesResponseType<HrFoundationPolicyWriteResult>(200), ProducesResponseType<ProblemDetails>(413)]
    public Task<HrFoundationPolicyWriteResult> Cancel(HrFoundationPolicyKind kind, Guid requestId, CancelHrFoundationPolicy input,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) => service.CancelAsync(kind, requestId, input, key, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

/// <summary>Inspect bounded bytes before MVC can discard duplicate, case-mismatched or unknown rule fields.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class StrictHrFoundationPolicyJsonAttribute(Type contract) : Attribute, IAsyncResourceFilter
{
    private const int MaximumBytes = 65536;
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (request.ContentLength > MaximumBytes) { context.Result = Failure(413); return; }
        request.EnableBuffering(bufferThreshold: MaximumBytes + 1, bufferLimit: MaximumBytes + 1);
        var bytes = new byte[MaximumBytes + 1];
        var length = 0;
        try
        {
            while (length < bytes.Length)
            {
                var read = await request.Body.ReadAsync(bytes.AsMemory(length), context.HttpContext.RequestAborted);
                if (read == 0) break;
                length += read;
            }
            if (length > MaximumBytes) { context.Result = Failure(413); return; }
            if (JsonSerializer.Deserialize(bytes.AsSpan(0, length), contract, HrFoundationPolicyRulesCodec.JsonOptions) is null) throw new JsonException();
        }
        catch (JsonException) { context.Result = Failure(400); return; }
        finally { request.Body.Position = 0; }
        await next();
    }
    private static ObjectResult Failure(int status) => new(new ProblemDetails { Status = status,
        Title = status == 413 ? "HR policy request exceeds its byte limit." : "HR policy request requires unambiguous explicit typed JSON." }) { StatusCode = status };
}
