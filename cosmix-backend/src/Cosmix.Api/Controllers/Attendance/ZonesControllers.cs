using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.Zones;
using Cosmix.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController,Authorize,Route("api/v1/attendance/sites/{siteId:guid}/zones")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400),ProducesResponseType<ProblemDetails>(401),ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404),ProducesResponseType<ProblemDetails>(409),ProducesResponseType<ProblemDetails>(503)]
public sealed class ZonesController(ZoneAdministrationService service,ZoneMonitoringService monitoring):ControllerBase
{
    [HttpGet,ProducesResponseType<ZonePage<ZoneResponse>>(200),EndpointSummary("List site zones and immutable versions")]
    [EndpointDescription("Web MFA; attendance.zones.read for the exact site. Bounded pagination, no employee locations. New configurations support OPERATIONAL and RED only; legacy zone types remain stored.")]
    public Task<ZonePage<ZoneResponse>> List(Guid siteId,[FromQuery]ZoneListRequest request,CancellationToken ct)=>service.ListAsync(siteId,request,Context(),ct);
    [HttpGet("{zoneId:guid}"),ProducesResponseType<ZoneResponse>(200),EndpointSummary("Read site zone version history")]
    public Task<ZoneResponse> Detail(Guid siteId,Guid zoneId,CancellationToken ct)=>service.DetailAsync(siteId,zoneId,Context(),ct);
    [HttpPost,RequestSizeLimit(65536),ProducesResponseType<ZoneResponse>(200),EndpointSummary("Create an Operational or Red zone with its initial boundary")]
    [EndpointDescription("Web MFA/CSRF and site-scoped attendance.zones.manage. Idempotency-Key and reason required. Circle or simple exterior-ring GeoJSON Polygon; no Amber/exception authority. Records direct authority in protected audit; does not activate runtime monitoring.")]
    public Task<ZoneResponse> Create(Guid siteId,ConfigureZoneRequest request,[FromHeader(Name="Idempotency-Key"),Required]string key,CancellationToken ct)=>service.CreateAsync(siteId,request,key,Context(),ct);
    [HttpPost("{zoneId:guid}/versions"),RequestSizeLimit(65536),ProducesResponseType<ZoneVersionResponse>(200),EndpointSummary("Publish an immutable zone boundary version")]
    [EndpointDescription("Web MFA/CSRF, site-scoped zones.manage; expectedLatestVersion, future effective window, reason and idempotency key. Existing assignments keep their pinned boundary until explicitly replaced; history is not rewritten.")]
    public Task<ZoneVersionResponse> Publish(Guid siteId,Guid zoneId,PublishZoneVersionRequest request,[FromHeader(Name="Idempotency-Key"),Required]string key,CancellationToken ct)=>service.PublishAsync(siteId,zoneId,request,key,Context(),ct);
    [HttpGet("monitoring-configurations"),ProducesResponseType<IReadOnlyList<ZoneConfigurationResponse>>(200),EndpointSummary("Read explicit monitoring configuration history")]
    public Task<IReadOnlyList<ZoneConfigurationResponse>> Configurations(Guid siteId,CancellationToken ct)=>service.ConfigurationsAsync(siteId,Context(),ct);
    [HttpPost("monitoring-configurations"),RequestSizeLimit(8192),ProducesResponseType<ZoneConfigurationResponse>(200),EndpointSummary("Record explicit approved monitoring settings")]
    [EndpointDescription("Web MFA/CSRF, site-scoped zones.manage. Explicit accuracy, warning distance, interval, retention, maximum duty duration, live visibility, visible notice and reason required. No business defaults. A later effective version supersedes earlier settings; existing monitoring windows require renewed notice. Runtime and retention workers remain separately disabled until approved deployment configuration.")]
    public Task<ZoneConfigurationResponse> Configure(Guid siteId,ConfigureZoneMonitoringRequest request,[FromHeader(Name="Idempotency-Key"),Required]string key,CancellationToken ct)=>service.ConfigureAsync(siteId,request,key,Context(),ct);
    [HttpGet("{zoneId:guid}/assignments"),ProducesResponseType<IReadOnlyList<ZoneAssignmentResponse>>(200),EndpointSummary("Read effective-dated zone population assignment history")]
    public Task<IReadOnlyList<ZoneAssignmentResponse>> Assignments(Guid siteId,Guid zoneId,CancellationToken ct)=>service.AssignmentsAsync(siteId,zoneId,Context(),ct);
    [HttpPost("{zoneId:guid}/assignments"),RequestSizeLimit(8192),ProducesResponseType<ZoneAssignmentResponse>(200),EndpointSummary("Assign a version to site, employee, role or team")]
    [EndpointDescription("Web MFA/CSRF, site-scoped zones.manage. Current server-resolved membership determines applicability. Employee override authority is unconfirmed: isEmployeeOverride=true is rejected. Conflicting matching versions remain unavailable. Optional replaceAssignmentId ends the matching old population assignment atomically. No Red-entry exception or attendance/pay change.")]
    public Task<ZoneAssignmentResponse> Assign(Guid siteId,Guid zoneId,AssignZoneRequest request,[FromHeader(Name="Idempotency-Key"),Required]string key,CancellationToken ct)=>service.AssignAsync(siteId,zoneId,request,key,Context(),ct);
    [HttpPost("{zoneId:guid}/assignments/{assignmentId:guid}/end"),RequestSizeLimit(8192),ProducesResponseType<ZoneAssignmentResponse>(200),EndpointSummary("End a zone assignment without rewriting history")]
    public Task<ZoneAssignmentResponse> End(Guid siteId,Guid zoneId,Guid assignmentId,EndZoneAssignmentRequest request,[FromHeader(Name="Idempotency-Key"),Required]string key,CancellationToken ct)=>service.EndAsync(siteId,zoneId,assignmentId,request,key,Context(),ct);
    [HttpGet("events"),ProducesResponseType<ZoneEventPage>(200),EndpointSummary("Review coarse zone events with current and historical scope")]
    [EndpointDescription("Web MFA and zones.review. Opaque actor/filter-bound cursor; 100 candidate scan cap and no unauthorized total count. No GPS, private reference or confidential restriction reason.")]
    public Task<ZoneEventPage> Events(Guid siteId,[FromQuery]ZoneEventPageRequest request,CancellationToken ct)=>monitoring.EventsAsync(siteId,request,false,Context(),ct);
    [HttpPost("events/{eventId:guid}/review"),RequestSizeLimit(8192),ProducesResponseType<ZoneEventResponseDto>(200),EndpointSummary("Append a factual zone evidence review")]
    [EndpointDescription("Web MFA/CSRF and zones.review; kind Reviewed, reason, expectedLastResponseId (explicit null for none) and Idempotency-Key. This records review only: no punishment, pay change, Red exception or punch alteration.")]
    public Task<ZoneEventResponseDto> Review(Guid siteId,Guid eventId,RespondZoneEventRequest request,[FromHeader(Name="Idempotency-Key"),Required]string key,CancellationToken ct)=>monitoring.RespondAsync(siteId,eventId,request,key,false,Context(),ct);
    [HttpGet("employees/{employeeId:guid}/monitoring-windows"),ProducesResponseType<IReadOnlyList<ZoneWindowResponse>>(200),EndpointSummary("List currently authorized duty monitoring windows")]
    public Task<IReadOnlyList<ZoneWindowResponse>> Windows(Guid siteId,Guid employeeId,CancellationToken ct)=>monitoring.LiveWindowsAsync(siteId,employeeId,Context(),ct);
    [HttpGet("employees/{employeeId:guid}/monitoring-windows/{windowId:guid}/location"),ProducesResponseType<ZoneLiveLocation>(200),EndpointSummary("Read the latest unexpired location during accepted active duty")]
    [EndpointDescription("Web MFA and explicit zones.location.read. Both retained and current employee/site scopes are rechecked. Runtime, cleanup health, visible notice, approved configuration, real accepted CheckIn and open duty required. Denied after CheckOut, transfer, revocation, expiry or configured maximum duration. Returns only latest sample; no off-duty history or full route.")]
    public Task<ZoneLiveLocation> Location(Guid siteId,Guid employeeId,Guid windowId,CancellationToken ct)=>monitoring.LiveAsync(siteId,employeeId,windowId,Context(),ct);
    private AuthRequestContext Context()=>new(HttpContext.Connection.RemoteIpAddress?.ToString()??"",HttpContext.TraceIdentifier);
}

[ApiController,Authorize,Route("api/v1/me/attendance/zones")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400),ProducesResponseType<ProblemDetails>(401),ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404),ProducesResponseType<ProblemDetails>(409),ProducesResponseType<ProblemDetails>(429),ProducesResponseType<ProblemDetails>(503)]
public sealed class MyZonesController(ZoneMonitoringService service):ControllerBase
{
    [HttpGet,ProducesResponseType<MyZonesResponse>(200),EndpointSummary("Read My duty zone instructions and visible monitoring notice")]
    [EndpointDescription("Current self permission and server-derived employee identity. No confidential restriction reasons. Monitoring availability remains false until explicitly enabled with healthy encrypted retention.")]
    public Task<MyZonesResponse> Mine(CancellationToken ct)=>service.MineAsync(Context(),ct);
    [HttpPost("monitoring-windows"),RequestSizeLimit(4096),ProducesResponseType<ZoneWindowResponse>(200),EndpointSummary("Acknowledge visible notice and begin a bounded duty monitoring window")]
    [EndpointDescription("Verified mobile device and zones.self. Requires server-returned assignment/configuration/session IDs, SHA-256 of UTF-8 visible notice and explicit noticeDisplayed=true. Real accepted CheckIn required; no manual duty substitutes. Idempotency-Key. Runtime defaults disabled; no hidden outside-duty tracking.")]
    public Task<ZoneWindowResponse> Start(StartZoneMonitoringRequest request,[FromHeader(Name="Idempotency-Key"),Required]string key,CancellationToken ct)=>service.StartAsync(request,key,Context(),ct);
    [HttpPost("monitoring-windows/{windowId:guid}/samples"),RequestSizeLimit(4096),ProducesResponseType<ZoneSampleResponse>(200),EndpointSummary("Evaluate a live duty sample without changing punches or pay")]
    [EndpointDescription("Verified current mobile device. Explicit interval/accuracy and fresh device capture enforced; no offline replay collection. Required independently random requestNonce and expectedCheckpointVersion. Idempotency-Key uses only a nonce-protected request hash in permanent audit/receipt. Exact GPS stays solely in expiring encrypted storage; response/outbox are coarse. Retention and duty rechecked every new sample. Same-key replay returns historical coarse receipt only.")]
    public Task<ZoneSampleResponse> Sample(Guid windowId,ZoneSampleRequest request,[FromHeader(Name="Idempotency-Key"),Required]string key,CancellationToken ct)=>service.SampleAsync(windowId,request,key,Context(),ct);
    [HttpGet("events"),ProducesResponseType<ZoneEventPage>(200),EndpointSummary("Read my coarse zone event history")]
    public Task<ZoneEventPage> Events([FromQuery]ZoneEventPageRequest request,CancellationToken ct)=>service.EventsAsync(null,request,true,Context(),ct);
    [HttpPost("events/{eventId:guid}/responses"),RequestSizeLimit(8192),ProducesResponseType<ZoneEventResponseDto>(200),EndpointSummary("Acknowledge or dispute my zone evidence")]
    [EndpointDescription("zones.self; kind Acknowledged or LocationDisputed, reason and expectedLastResponseId. Idempotency-Key; web requests also require CSRF. A dispute is evidence for review, not an access exception or automatic payroll change.")]
    public Task<ZoneEventResponseDto> Respond(Guid eventId,RespondZoneEventRequest request,[FromHeader(Name="Idempotency-Key"),Required]string key,CancellationToken ct)=>service.RespondAsync(null,eventId,request,key,true,Context(),ct);
    private AuthRequestContext Context()=>new(HttpContext.Connection.RemoteIpAddress?.ToString()??"",HttpContext.TraceIdentifier);
}
