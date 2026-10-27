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
[Route("api/v1/attendance/employees/{employeeId:guid}/payroll-inputs/{year:int}/{month:int}/revisions/{revisionId:guid}/activation")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AttendancePayrollActivationController(AttendancePayrollActivationService service) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Read company operational activation for an exact protected payroll source")]
    public Task<AttendancePayrollActivationStatus> Status(Guid employeeId, int year, int month, Guid revisionId, CancellationToken ct) =>
        service.StatusAsync(employeeId, Period(year, month), revisionId, Context(), ct);

    [HttpPost("requests"), RequestSizeLimit(32768), StrictPayrollActivationJson(typeof(RequestAttendancePayrollActivation))]
    [EndpointSummary("Request company operational activation with declared documentary evidence")]
    [EndpointDescription("Current web MFA, CSRF, explicit All-scoped activation.request and activation.read, and payroll-inputs.read across current and original scopes. Exact latest saved source required. Does not certify law or issue payment instructions.")]
    public Task<AttendancePayrollActivationStatus> SubmitRequest(Guid employeeId, int year, int month, Guid revisionId,
        RequestAttendancePayrollActivation request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.RequestAsync(employeeId, Period(year, month), revisionId, request, key, Context(), ct);

    [HttpPost("requests/{requestId:guid}/decision"), RequestSizeLimit(32768), StrictPayrollActivationJson(typeof(DecideAttendancePayrollActivation))]
    [EndpointSummary("Independently approve, return or reject exact-source company payroll activation")]
    [EndpointDescription("Explicit All-scoped activation.approve and activation.read plus scoped payroll-inputs.read. Reviewer cannot be requester or beneficiary. Current ready source, expected version, reason and idempotency key required; documentary assertions are retained rather than independently certified.")]
    public Task<AttendancePayrollActivationStatus> Decide(Guid employeeId, int year, int month, Guid revisionId, Guid requestId,
        DecideAttendancePayrollActivation request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.DecideAsync(employeeId, Period(year, month), revisionId, requestId, request, key, Context(), ct);

    private static DateOnly Period(int year, int month) => year is >= 1900 and <= 9998 && month is >= 1 and <= 12
        ? new(year, month, 1) : throw new AuthenticationFailure("invalid_payroll_activation_period", 400);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class StrictPayrollActivationJsonAttribute(Type bodyType) : Attribute, IAsyncResourceFilter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false, AllowDuplicateProperties = false,
        NumberHandling = JsonNumberHandling.Strict, MaxDepth = 16
    };
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        const int maximum = 32768;
        var request = context.HttpContext.Request;
        if (request.ContentLength > maximum) { context.Result = Failure(413); return; }
        request.EnableBuffering(maximum + 1, maximum + 1);
        var bytes = new byte[maximum + 1]; var length = 0;
        try
        {
            while (length < bytes.Length)
            {
                var read = await request.Body.ReadAsync(bytes.AsMemory(length), context.HttpContext.RequestAborted);
                if (read == 0) break;
                length += read;
            }
            if (length > maximum) { context.Result = Failure(413); return; }
            if (JsonSerializer.Deserialize(bytes.AsSpan(0, length), bodyType, Options) is null) throw new JsonException();
        }
        catch (JsonException) { context.Result = Failure(400); return; }
        finally { request.Body.Position = 0; }
        await next();
    }
    private static ObjectResult Failure(int status) => new(new ProblemDetails { Status = status,
        Title = status == 413 ? "Payroll activation exceeds the request limit." : "Unambiguous complete payroll activation JSON is required." })
        { StatusCode = status };
}
