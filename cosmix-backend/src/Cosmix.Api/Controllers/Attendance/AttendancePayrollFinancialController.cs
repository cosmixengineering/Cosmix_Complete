using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize]
[Route("api/v1/attendance/employees/{employeeId:guid}/payroll-inputs/{year:int}/{month:int}/financial-adjustments")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(413), ProducesResponseType<ProblemDetails>(503)]
public sealed class AttendancePayrollFinancialController(AttendancePayrollFinancialService service) : ControllerBase
{
    [HttpGet("preview")]
    [EndpointSummary("Value approved factual corrections against an exact confirmed deduction and show the first unlocked target")]
    public Task<AttendancePayrollFinancialPreview> Preview(Guid employeeId, int year, int month, CancellationToken ct) =>
        service.PreviewAsync(employeeId, Period(year, month), Context(), ct);

    [HttpGet]
    [EndpointSummary("Read bounded protected financial adjustment and posting/cancellation history")]
    public Task<IReadOnlyList<AttendancePayrollFinancialResponse>> History(Guid employeeId, int year, int month, CancellationToken ct) =>
        service.HistoryAsync(employeeId, Period(year, month), Context(), ct);

    [HttpPost, RequestSizeLimit(65536), StrictPayrollFinancialJson(typeof(ApproveAttendancePayrollFinancialRequest))]
    [EndpointSummary("Approve the exact previewed financial input; zero difference records no-change coverage")]
    [EndpointDescription("Current web MFA/CSRF, assigned Accounts/Finance attestation authority and both financial read permissions across original/current source and target scopes. Approval is not posting, legal activation or payment. Target, calculation, correction history and prior financial effects are rechecked at commit.")]
    public Task<AttendancePayrollFinancialResponse> Approve(Guid employeeId, int year, int month,
        ApproveAttendancePayrollFinancialRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) => service.ApproveAsync(employeeId, Period(year, month), request, key, Context(), ct);

    [HttpPost("{adjustmentId:guid}/posting"), RequestSizeLimit(65536), StrictPayrollFinancialJson(typeof(PostAttendancePayrollFinancialRequest))]
    [EndpointSummary("Attest the exact nonzero approved adjustment was externally posted; never execute payment")]
    public Task<AttendancePayrollFinancialResponse> Post(Guid employeeId, int year, int month, Guid adjustmentId,
        PostAttendancePayrollFinancialRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) => service.PostAsync(employeeId, Period(year, month), adjustmentId, request, key, Context(), ct);

    [HttpPost("{adjustmentId:guid}/cancel"), RequestSizeLimit(65536), StrictPayrollFinancialJson(typeof(CancelAttendancePayrollFinancialRequest))]
    [EndpointSummary("Cancel the latest unposted approval while its target is unlocked; moves no money")]
    public Task<AttendancePayrollFinancialResponse> Cancel(Guid employeeId, int year, int month, Guid adjustmentId,
        CancelAttendancePayrollFinancialRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) => service.CancelAsync(employeeId, Period(year, month), adjustmentId, request, key, Context(), ct);

    private static DateOnly Period(int year, int month) => year is >= 1900 and <= 9998 && month is >= 1 and <= 12
        ? new(year, month, 1) : throw new AuthenticationFailure("invalid_financial_carry_forward_month", 400);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class StrictPayrollFinancialJsonAttribute(Type requestType) : Attribute, IAsyncResourceFilter
{
    private const int MaximumBytes = 65536;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false, AllowDuplicateProperties = false,
        NumberHandling = JsonNumberHandling.Strict, MaxDepth = 16
    };
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (request.ContentLength > MaximumBytes) { context.Result = Failure(413); return; }
        request.EnableBuffering(bufferThreshold: MaximumBytes + 1, bufferLimit: MaximumBytes + 1);
        var bytes = new byte[MaximumBytes + 1]; var length = 0;
        try
        {
            while (length < bytes.Length)
            {
                var count = await request.Body.ReadAsync(bytes.AsMemory(length), context.HttpContext.RequestAborted);
                if (count == 0) break;
                length += count;
            }
            if (length > MaximumBytes) { context.Result = Failure(413); return; }
            if (JsonSerializer.Deserialize(bytes.AsSpan(0, length), requestType, Options) is null) throw new JsonException();
        }
        catch (JsonException) { context.Result = Failure(400); return; }
        finally { request.Body.Position = 0; }
        await next();
    }
    private static ObjectResult Failure(int status) => new(new ProblemDetails
    {
        Status = status, Title = status == 413 ? "Financial adjustment request exceeds the limit." : "Complete unambiguous financial adjustment JSON is required."
    }) { StatusCode = status };
}
