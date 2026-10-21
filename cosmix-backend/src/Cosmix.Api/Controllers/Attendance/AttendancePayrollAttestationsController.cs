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
[Route("api/v1/attendance/employees/{employeeId:guid}/payroll-inputs/{year:int}/{month:int}/revisions/{sourceRevisionId:guid}/attestations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(413), ProducesResponseType<ProblemDetails>(503)]
public sealed class AttendancePayrollAttestationsController(AttendancePayrollAttestationService service) : ControllerBase
{
    [HttpPost, RequestSizeLimit(262144), StrictPayrollAttestationJson]
    [EndpointSummary("Record a reviewable payroll-input handoff or explicit manual deduction posting/settlement evidence")]
    [EndpointDescription("Current web MFA/CSRF and explicitly assigned attendance.payroll-attestations.attest plus payroll-inputs.read in current/original scopes. Permission assignment requires owner approval for Accounts & Finance personnel. Exact saved source revision/fingerprint, all exact line keys/amounts, expected predecessor, external reference, evidence locator/checksum, dated statement, reason and Idempotency-Key required. A complete validated issue-free ready source with exact empty lines permits handoff only, with lines=[] and postedAtUtc=null; posted/settled remain payroll_attestation_no_deduction_lines. Missing or unverified calculations are never zero deductions. Handoff is a review record, not Finance approval or posting proof. Posted/settled are authorized manual assertions, not external integration, legal certification, reconciliation, payroll readiness or payment instructions.")]
    public Task<AttendancePayrollAttestationResponse> Create(Guid employeeId, int year, int month, Guid sourceRevisionId,
        CreateAttendancePayrollAttestationRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.CreateAsync(employeeId, Period(year, month), sourceRevisionId, request, key, Context(), ct);

    [HttpGet]
    [EndpointSummary("Read the bounded private attestation chain for an exact saved source")]
    [EndpointDescription("At most three immutable events. Current web MFA, payroll-inputs.read and dedicated payroll-attestations.read required across current/original scopes. External evidence is an explicitly recorded manual assertion; no external evidence is fetched or independently verified.")]
    public Task<IReadOnlyList<AttendancePayrollAttestationResponse>> History(Guid employeeId, int year, int month,
        Guid sourceRevisionId, CancellationToken ct) =>
        service.HistoryAsync(employeeId, Period(year, month), sourceRevisionId, Context(), ct);

    private static DateOnly Period(int year, int month) => year is >= 1900 and <= 9998 && month is >= 1 and <= 12
        ? new(year, month, 1) : throw new AuthenticationFailure("invalid_payroll_attestation_period", 400);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

/// <summary>Duplicate members must fail before MVC binding can erase ambiguity in a financial receipt.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class StrictPayrollAttestationJsonAttribute : Attribute, IAsyncResourceFilter
{
    public const int MaximumBytes = 262144;
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
                var read = await request.Body.ReadAsync(bytes.AsMemory(length), context.HttpContext.RequestAborted);
                if (read == 0) break;
                length += read;
            }
            if (length > MaximumBytes) { context.Result = Failure(413); return; }
            if (JsonSerializer.Deserialize<CreateAttendancePayrollAttestationRequest>(bytes.AsSpan(0, length), Options) is null)
                throw new JsonException();
        }
        catch (JsonException) { context.Result = Failure(400); return; }
        finally { request.Body.Position = 0; }
        await next();
    }
    private static ObjectResult Failure(int status) => new(new ProblemDetails
    {
        Status = status, Title = status == 413 ? "Payroll attestation exceeds the request limit." : "Unambiguous complete payroll attestation JSON is required."
    }) { StatusCode = status };
}
