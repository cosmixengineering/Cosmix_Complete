using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.CompanySettings;
using Cosmix.Application.HR.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/hr/company-settings")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class CompanySettingsController(CompanySettingsService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<CompanySettingsResponse>(200)]
    [EndpointSummary("Read company configuration and supported internal jurisdiction identifiers")]
    [EndpointDescription("Current web MFA and explicit org.company.read All permission. Unconfigured profile is null/unavailable; no seeded address, business type or default province. Configured location does not verify legal applicability; LegalStatus remains review_required.")]
    public Task<CompanySettingsResponse> Get(CancellationToken ct) => service.GetAsync(ct);
    [HttpPut, RequestSizeLimit(16384), ProducesResponseType<CompanyProfileResponse>(200)]
    [EndpointSummary("SuperAdmin configures the company and existing head-office references")]
    [EndpointDescription("Current SuperAdmin, web MFA and explicit org.company.manage All permission. Idempotency-Key and reason required. Initial creation omits ExpectedVersion; every amendment requires current base64 rowversion. Explicit company/business names, existing active Branch/City pair, address and province; optional coordinates must be a pair with at most six decimals. No legal activation, salary or policy values are accepted. History/audit and exact retry are atomic.")]
    public Task<CompanyProfileResponse> Save(SaveCompanyProfileRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.SaveAsync(request, key, Context(), ct);
    [HttpGet("history"), ProducesResponseType<PagedResult<OrganizationChangeResponse>>(200)]
    [EndpointSummary("Read bounded company and site configuration audit history")]
    public Task<PagedResult<OrganizationChangeResponse>> History([FromQuery] OrganizationListRequest request, CancellationToken ct) => service.HistoryAsync(request, ct);
    [HttpGet("sites/{siteId:guid}/jurisdictions"), ProducesResponseType<IReadOnlyList<CompanySiteJurisdictionResponse>>(200)]
    [EndpointSummary("Read retained explicit jurisdiction timeline for an existing site")]
    [EndpointDescription("org.company.read with current web MFA and All scope. Includes original and superseding versions, bounded at 5000. EffectiveFrom is inclusive; the next later active version ends the previous interval. Site mappings never fall back to head-office province or city text.")]
    public Task<IReadOnlyList<CompanySiteJurisdictionResponse>> SiteHistory(Guid siteId, CancellationToken ct) => service.SiteHistoryAsync(siteId, ct);
    [HttpPost("site-jurisdictions"), RequestSizeLimit(16384), ProducesResponseType<CompanySiteJurisdictionResult>(200)]
    [EndpointSummary("SuperAdmin publishes an immutable dated site jurisdiction")]
    [EndpointDescription("Current SuperAdmin, web MFA and org.company.manage All permission. Explicit existing active SiteId, EffectiveFrom and internal jurisdiction code; ExpectedCompanyVersion and Idempotency-Key required. A future date changes the mapping without editing earlier history. New retrospective changes and affected locked intervals are rejected. A correction at an existing date must name its active SupersedesVersionId; parallel/stale writes or ambiguous same-date mappings fail. Returned CompanyVersion is required for the next change. No certified legal status is granted.")]
    public Task<CompanySiteJurisdictionResult> Publish(PublishCompanySiteJurisdictionRequest request, [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.PublishSiteAsync(request, key, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
