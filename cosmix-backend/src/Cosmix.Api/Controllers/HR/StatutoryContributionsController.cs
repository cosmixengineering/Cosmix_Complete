using System.ComponentModel.DataAnnotations;
using System.Text;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.StatutoryContributions;
using Cosmix.Domain.HR.StatutoryContributions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController]
[Authorize]
[Route("api/v1/statutory-contributions")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
public sealed class StatutoryContributionsController(
    StatutoryContributionService service) : ControllerBase
{
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);

    [HttpGet]
    [Authorize(Policy = "statutory.contributions.manage")]
    [EndpointSummary("List statutory contributions")]
    [ProducesResponseType<ContributionListResponse>(200)]
    public Task<ContributionListResponse> List(
        [FromQuery] StatutoryScheme? scheme,
        [FromQuery] int? year,
        [FromQuery, Range(1, 4096)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25,
        CancellationToken ct = default) =>
        service.ListAsync(scheme, year, page, pageSize, Context(), ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "statutory.contributions.manage")]
    [EndpointSummary("Get statutory contribution detail")]
    [ProducesResponseType<ContributionResponse>(200)]
    public Task<ContributionResponse> Get(Guid id, CancellationToken ct) =>
        service.GetAsync(id, Context(), ct);

    [HttpPost]
    [Authorize(Policy = "statutory.contributions.manage")]
    [EndpointSummary("Create a new draft statutory contribution entry")]
    [ProducesResponseType<ContributionResponse>(200)]
    public Task<ContributionResponse> Create(CreateContributionRequest request, CancellationToken ct)
    {
        return service.CreateAsync(request, Context(), ct);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "statutory.contributions.approve")]
    [EndpointSummary("Approve a draft statutory contribution entry (second-person approval enforced)")]
    [ProducesResponseType<ContributionResponse>(200)]
    public Task<ContributionResponse> Approve(Guid id, CancellationToken ct)
    {
        return service.ApproveAsync(id, Context(), ct);
    }

    [HttpPost("{id:guid}/post")]
    [Authorize(Policy = "statutory.contributions.approve")]
    [EndpointSummary("Post an approved statutory contribution to accounting")]
    [ProducesResponseType<ContributionResponse>(200)]
    public Task<ContributionResponse> Post(Guid id, CancellationToken ct)
    {
        return service.PostAsync(id, Context(), ct);
    }

    [HttpGet("{id:guid}/export")]
    [Authorize(Policy = "statutory.contributions.manage")]
    [Produces("text/csv")]
    [ProducesResponseType<FileContentResult>(200)]
    [EndpointSummary("Export statutory contribution monthly schedule CSV")]
    public async Task<IActionResult> ExportCsv(Guid id, CancellationToken ct)
    {
        var (fileName, bytes) = await service.ExportScheduleCsvAsync(id, Context(), ct);
        Response.Headers.Append("X-Content-Type-Options", "nosniff");
        return File(bytes, "text/csv", fileName);
    }
}

[ApiController]
[Authorize]
[Route("api/v1/employees/{employeeId:guid}/statutory-registrations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
public sealed class EmployeeStatutoryRegistrationsController(
    StatutoryContributionService service) : ControllerBase
{
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);

    [HttpGet]
    [Authorize(Policy = "statutory.contributions.manage")]
    [EndpointSummary("List statutory registrations for an employee")]
    [ProducesResponseType<IReadOnlyList<RegistrationResponse>>(200)]
    public Task<IReadOnlyList<RegistrationResponse>> GetRegistrations(Guid employeeId, CancellationToken ct) =>
        service.GetRegistrationsAsync(employeeId, Context(), ct);

    [HttpPost]
    [Authorize(Policy = "statutory.contributions.manage")]
    [EndpointSummary("Add a statutory scheme registration for an employee")]
    [ProducesResponseType<RegistrationResponse>(200)]
    public Task<RegistrationResponse> AddRegistration(Guid employeeId, AddRegistrationRequest request, CancellationToken ct)
    {
        return service.AddRegistrationAsync(employeeId, request, Context(), ct);
    }
}
