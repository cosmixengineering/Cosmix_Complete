using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.StaffCategories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/hr/staff-taxonomy")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class StaffTaxonomyController(StaffTaxonomyService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<StaffTaxonomyPolicyView>(200)]
    public Task<StaffTaxonomyPolicyView?> Policy(CancellationToken ct) => service.PolicyAsync(Context(), ct);
    [HttpGet("effective"), ProducesResponseType<StaffTaxonomyResolution>(200)]
    public Task<StaffTaxonomyResolution> Effective([FromQuery, Required] string categoryCode,
        [FromQuery] DateOnly on, CancellationToken ct) => service.EffectiveAsync(categoryCode, on, Context(), ct);
    [HttpGet("history"), ProducesResponseType<PagedResult<StaffTaxonomyVersionView>>(200)]
    public Task<PagedResult<StaffTaxonomyVersionView>> History(CancellationToken ct, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25) => service.HistoryAsync(page, pageSize, Context(), ct);
    [HttpGet("requests"), ProducesResponseType<PagedResult<StaffTaxonomyRequestView>>(200)]
    public Task<PagedResult<StaffTaxonomyRequestView>> Requests(CancellationToken ct, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25) => service.RequestsAsync(page, pageSize, Context(), ct);
    [HttpGet("requests/{id:guid}"), ProducesResponseType<StaffTaxonomyRequestDetail>(200)]
    public Task<StaffTaxonomyRequestDetail> Detail(Guid id, CancellationToken ct) => service.DetailAsync(id, Context(), ct);
    [HttpPost("requests"), RequestSizeLimit(196608), StrictStaffTaxonomyJson(typeof(RequestStaffTaxonomy), 196608)]
    [ProducesResponseType<StaffTaxonomyWriteResult>(200), ProducesResponseType<ProblemDetails>(413)]
    public Task<StaffTaxonomyWriteResult> RequestTaxonomy(RequestStaffTaxonomy input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.RequestAsync(input, key, Context(), ct);
    [HttpPost("requests/{id:guid}/decision"), RequestSizeLimit(49152), StrictStaffTaxonomyJson(typeof(DecideStaffTaxonomy), 49152)]
    [ProducesResponseType<StaffTaxonomyWriteResult>(200), ProducesResponseType<ProblemDetails>(413)]
    public Task<StaffTaxonomyWriteResult> Decide(Guid id, DecideStaffTaxonomy input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.DecideAsync(id, input, key, Context(), ct);
    [HttpPost("requests/{id:guid}/cancel"), RequestSizeLimit(49152), StrictStaffTaxonomyJson(typeof(CancelStaffTaxonomy), 49152)]
    [ProducesResponseType<StaffTaxonomyWriteResult>(200), ProducesResponseType<ProblemDetails>(413)]
    public Task<StaffTaxonomyWriteResult> Cancel(Guid id, CancelStaffTaxonomy input,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.CancelAsync(id, input, key, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
