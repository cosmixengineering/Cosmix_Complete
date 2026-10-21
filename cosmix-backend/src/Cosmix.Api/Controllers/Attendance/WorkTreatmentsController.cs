using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/attendance/work-treatments")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WorkTreatmentsController(WorkTreatmentService service) : ControllerBase
{
    [HttpGet("employees/{employeeId:guid}/sources")]
    public Task<WorkTreatmentSourceResponse> Sources(Guid employeeId, [FromQuery] WorkTreatmentKind kind,
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct, [FromQuery] Guid? annualSourceId = null) =>
        service.SourcesAsync(employeeId, kind, from, to, Context(), ct, annualSourceId);

    [HttpPost("employees/{employeeId:guid}"), RequestSizeLimit(128 * 1024)]
    public async Task<ActionResult<WorkTreatmentView>> Submit(Guid employeeId, WorkTreatmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) {
        var result = await service.SubmitAsync(employeeId, request, key, Context(), ct);
        return Created($"/api/v1/attendance/work-treatments/employees/{employeeId:D}/{result.Id:D}", result);
    }
    [HttpGet("employees/{employeeId:guid}")]
    public Task<IReadOnlyList<WorkTreatmentView>> History(Guid employeeId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        service.HistoryAsync(employeeId, from, to, Context(), ct);

    [HttpGet("employees/{employeeId:guid}/{id:guid}")]
    public Task<WorkTreatmentDetail> Detail(Guid employeeId, Guid id, CancellationToken ct) => service.DetailAsync(employeeId, id, false, Context(), ct);

    [HttpGet("employees/{employeeId:guid}/reconcile")]
    public Task<WorkTreatmentDetail> Reconcile(Guid employeeId, [FromHeader(Name = "Submission-Key"), Required] string key, CancellationToken ct) =>
        service.ReconcileAsync(employeeId, key, Context(), ct);

    [HttpGet("mine/{id:guid}")]
    public Task<WorkTreatmentDetail> Mine(Guid id, CancellationToken ct) => service.DetailAsync(null, id, true, Context(), ct);

    [HttpPost("employees/{employeeId:guid}/{id:guid}/decisions"), RequestSizeLimit(32 * 1024)]
    public Task<WorkTreatmentView> Decide(Guid employeeId, Guid id, [FromQuery] WorkTreatmentKind kind, DecideWorkTreatmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.DecideAsync(employeeId, id, kind, request, key, Context(), ct);

    [HttpPost("employees/{employeeId:guid}/{id:guid}/disputes"), RequestSizeLimit(32 * 1024)]
    public Task<WorkTreatmentView> Dispute(Guid employeeId, Guid id, DisputeWorkTreatmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.DisputeAsync(employeeId, id, request, key, false, Context(), ct);

    [HttpPost("mine/{id:guid}/disputes"), RequestSizeLimit(32 * 1024)]
    public Task<WorkTreatmentView> OwnDispute(Guid id, DisputeWorkTreatmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.DisputeAsync(null, id, request, key, true, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
