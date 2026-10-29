using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Api.Attendance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.Attendance;

[ApiController, Authorize, Route("api/v1/attendance/face")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(429), ProducesResponseType<ProblemDetails>(503)]
public sealed class FaceVerificationController(FaceService service, FaceCaptureService captures) : ControllerBase
{
    [HttpGet("capability"), ProducesResponseType<FaceCapabilityResponse>(200)]
    [EndpointSummary("Read my approved face-verification availability")]
    [EndpointDescription("attendance.face.self. Unconfigured provider or unconfirmed biometric policy is explicit. This result never grants attendance success.")]
    public Task<FaceCapabilityResponse> Capability(CancellationToken ct) => service.CapabilityAsync(Context(), ct);
    [HttpPost("challenges"), RequestSizeLimit(8192), ProducesResponseType<FaceChallengeResponse>(200)]
    [EndpointSummary("Issue a punch-bound single-use Check In challenge")]
    [EndpointDescription("attendance.face.self, current registered mobile device, active enrollment and explicitly approved provider/policy required. No usable challenge is fabricated while unavailable. Bind PunchIdempotencyKey to the subsequent check-in request.")]
    public Task<FaceChallengeResponse> Challenge(FaceChallengeRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.ChallengeAsync(request, key, Context(), ct);
    [HttpPost("challenges/{id:guid}/verify"), RequestSizeLimit(16384), ProducesResponseType<FaceVerificationResponse>(200)]
    [EndpointSummary("Verify my live face challenge with the approved provider")]
    [EndpointDescription("attendance.face.self. Server adapter validates liveness and a one-to-one match. The client cannot submit match/liveness booleans. SQL finalization rechecks current employee/device/enrollment and expiry after provider execution. Only Passed can be submitted, and the punch must still consume it before expiry.")]
    public Task<FaceVerificationResponse> Verify(Guid id, VerifyFaceRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.VerifyAsync(id, request, key, Context(), ct);
    [HttpPost("challenges/{id:guid}/captures"), RequestSizeLimit(710000), StrictFaceCaptureJson, ProducesResponseType<FaceCaptureResponse>(200)]
    [EndpointSummary("Upload camera evidence bound to my current Check In challenge")]
    [EndpointDescription("Configured self-hosted provider, registered mobile device, current session, exact pending challenge/version and approved biometric policy required. PNG/JPEG up to 512 KiB; server encrypts the evidence and returns an opaque short-lived token. This token is not a liveness result or attendance success; send it to the existing verify operation. No URL, employee identity or face-passed boolean is accepted.")]
    public Task<FaceCaptureResponse> Capture(Guid id, UploadVerificationFaceCaptureRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        captures.UploadVerificationAsync(id, request, key, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

[ApiController, Authorize, Route("api/v1/hr/employees/{employeeId:guid}/face-enrollments")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class FaceEnrollmentsController(FaceService service, FaceEnrollmentHistoryService historyService, FaceCaptureService captures) : ControllerBase
{
    [HttpGet("devices"), ProducesResponseType<PagedResult<FaceEnrollmentDeviceResponse>>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    [EndpointSummary("List an employee's eligible registered devices for face enrollment")]
    [EndpointDescription("attendance.face.manage; current web MFA and explicit Employee/Site/All scope. The employee account must be active, approved and eligible. Returns only verified, nonrevoked devices belonging to that account, with verification no later than now. No device identifier hashes, secrets, biometric evidence or provider readiness are returned.")]
    public Task<PagedResult<FaceEnrollmentDeviceResponse>> Devices(Guid employeeId, CancellationToken ct,
        [FromQuery, Range(1, 1000000)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 25) =>
        service.DevicesAsync(employeeId, page, pageSize, Context(), ct);
    [HttpGet("current"), ProducesResponseType<CurrentFaceEnrollmentResponse>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    [EndpointSummary("Read the employee's current face enrollment for replacement")]
    [EndpointDescription("attendance.face.read; current web MFA and explicit Employee/Site/All scope, matching enrollment history. Returns enrollment metadata with its exact ID/version, or enrollment: null when none is active. No history paging, biometric evidence or provider readiness evaluation.")]
    public Task<CurrentFaceEnrollmentResponse> Current(Guid employeeId, CancellationToken ct) =>
        service.CurrentAsync(employeeId, Context(), ct);
    [HttpGet, ProducesResponseType<PagedResult<FaceEnrollmentResponse>>(200)]
    [EndpointSummary("Read scoped face enrollment metadata without template access")]
    [EndpointDescription("attendance.face.read; current web MFA and explicit Employee/Site/All scope. No templates, thresholds or capture images are returned.")]
    public Task<PagedResult<FaceEnrollmentResponse>> History(Guid employeeId, CancellationToken ct,
        [FromQuery, Range(1, 1000000)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 25) =>
        service.HistoryAsync(employeeId, page, pageSize, Context(), ct);
    [HttpGet("attempts"), ProducesResponseType<PagedResult<FaceAttemptResponse>>(200)]
    [EndpointSummary("Read scoped face verification attempt metadata")]
    public Task<PagedResult<FaceAttemptResponse>> Attempts(Guid employeeId, CancellationToken ct,
        [FromQuery, Range(1, 1000000)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 25) =>
        service.AttemptsAsync(employeeId, page, pageSize, Context(), ct);
    [HttpGet("enrollment-attempts"), ProducesResponseType<FaceEnrollmentAttemptPage>(200)]
    [EndpointSummary("Read scoped enrollment attempt outcomes, including failures")]
    [EndpointDescription("attendance.face.read; current web MFA plus current and originally recorded employee/site scope. UTC interval is inclusive FromUtc/exclusive ToUtc, maximum 366 days. At most 100 candidates are examined per page; no total count. Follow NextCursor with identical filters/page size, including after an empty page. Unfinished starts remain reconciliation_pending; age never implies success/failure. No templates, evidence, internal actor authority or private references are returned. This history covers durable journal attempts, not older generic enrollment receipts.")]
    public Task<FaceEnrollmentAttemptPage> EnrollmentAttempts(Guid employeeId, [FromQuery] FaceEnrollmentHistoryRequest request,
        CancellationToken ct) => historyService.HistoryAsync(employeeId, request, Context(), ct);
    [HttpPost, RequestSizeLimit(16384), ProducesResponseType<FaceEnrollmentResponse>(200)]
    [EndpointSummary("Enroll or replace an employee's authorized face template")]
    [EndpointDescription("attendance.face.manage, current web MFA, approved biometric authority/provider/retention and reason required. RegisteredDeviceId must already belong to this employee. ExpectedActiveEnrollmentId/version control replacement. The self-hosted provider requires a server-protected session/device-bound capture token and server liveness checks; no arbitrary template or client success assertion. Prior templates remain private pending approved retention processing.")]
    public Task<FaceEnrollmentResponse> Enroll(Guid employeeId, FaceEnrollmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.EnrollAsync(employeeId, request, key, Context(), ct);
    [HttpPost("captures"), RequestSizeLimit(710000), StrictFaceCaptureJson, ProducesResponseType<FaceCaptureResponse>(200)]
    [EndpointSummary("Upload scoped enrollment camera evidence for the self-hosted provider")]
    [EndpointDescription("attendance.face.manage and genuine web MFA/CSRF, existing employee device and approved biometric authority/storage required. Returns a protected session-bound token, never a verified enrollment. Use it with the existing enrollment endpoint; liveness and single-face checks occur on the server. No default authority or biometric retention policy is implied.")]
    public Task<FaceCaptureResponse> Capture(Guid employeeId, UploadEnrollmentFaceCaptureRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        captures.UploadEnrollmentAsync(employeeId, request, key, Context(), ct);
    [HttpPost("{id:guid}/revoke"), RequestSizeLimit(8192), ProducesResponseType<FaceEnrollmentResponse>(200)]
    [EndpointSummary("Revoke a face enrollment and invalidate its unused challenges")]
    [EndpointDescription("attendance.face.manage and approved authority. Reason, expected version and Idempotency-Key required. Revocation does not pretend to delete private templates before a retention policy is approved.")]
    public Task<FaceEnrollmentResponse> Revoke(Guid employeeId, Guid id, RevokeFaceEnrollmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) => service.RevokeAsync(employeeId, id, request, key, Context(), ct);
    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
