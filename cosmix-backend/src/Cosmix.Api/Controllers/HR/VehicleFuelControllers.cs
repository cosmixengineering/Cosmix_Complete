using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.VehicleFuel;
using Cosmix.Domain.Common.Approvals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmix.Api.Controllers.HR;

[ApiController, Authorize, Route("api/v1/me/vehicles")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class MyVehiclesController(VehicleFuelService service) : ControllerBase
{
    [HttpGet("activity"), ProducesResponseType<MyVehicleActivityResponse>(200)]
    [EndpointSummary("Read own paged vehicle requests without protected evidence references")]
    public Task<MyVehicleActivityResponse> MyActivity([FromQuery, Range(1, 1000000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken ct = default) =>
        service.MyActivityAsync(page, pageSize, Context(), ct);

    [HttpGet("custody"), ProducesResponseType<IReadOnlyList<VehicleCustodyResponse>>(200)]
    [EndpointSummary("Read own vehicle custody history")]
    [EndpointDescription("Requires attendance.vehicle.self. Returns assigned vehicle custody and return history.")]
    public Task<IReadOnlyList<VehicleCustodyResponse>> MyCustody(CancellationToken ct) =>
        service.MyCustodyHistoryAsync(Context(), ct);

    [HttpPost("trips"), RequestSizeLimit(16384), ProducesResponseType<VehicleTripResponse>(200)]
    [EndpointSummary("Submit an official vehicle trip")]
    [EndpointDescription("Requires attendance.vehicle.self and Idempotency-Key. Submits trip with odometer readings and journey purpose.")]
    public Task<VehicleTripResponse> SubmitTrip(
        SubmitVehicleTripRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        service.SubmitTripAsync(request, key, Context(), ct);

    [HttpPost("fuel-claims"), RequestSizeLimit(16384), ProducesResponseType<FuelClaimResponse>(200)]
    [EndpointSummary("Submit a fuel reimbursement claim")]
    [EndpointDescription("Requires attendance.vehicle.self and Idempotency-Key. Uses retained approved trip mileage and its exact policy version. Creates an HR claim for review, not a payment.")]
    public Task<FuelClaimResponse> SubmitFuelClaim(
        SubmitFuelClaimRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        service.SubmitFuelClaimAsync(request, key, Context(), ct);

    [HttpPost("maintenance"), RequestSizeLimit(16384), ProducesResponseType<VehicleServiceRecordResponse>(200)]
    [EndpointSummary("Submit motorcycle maintenance milestone")]
    [EndpointDescription("Requires attendance.vehicle.self and Idempotency-Key. Milestone eligibility is read from the active fuel policy.")]
    public Task<VehicleServiceRecordResponse> SubmitMaintenance(
        SubmitServiceRecordRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        service.SubmitServiceRecordAsync(request, key, Context(), ct);

    [HttpPost("incidents"), RequestSizeLimit(32768), ProducesResponseType<VehicleIncidentResponse>(200)]
    [EndpointSummary("Report vehicle accident or damage incident")]
    [EndpointDescription("Requires attendance.vehicle.self and Idempotency-Key. Records evidence and insurance reference.")]
    public Task<VehicleIncidentResponse> ReportIncident(
        ReportVehicleIncidentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        service.ReportIncidentAsync(request, key, Context(), ct);

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}

[ApiController, Authorize, Route("api/v1/hr/vehicles")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class VehiclesAdminController(VehicleFuelService service) : ControllerBase
{
    [HttpPost("{vehicleId:guid}/monthly-assignment-context"), RequestSizeLimit(16384), ProducesResponseType<VehicleMonthlyAssignmentContext>(200)]
    [EndpointSummary("Read eligible equivalent same-day trip selection context")]
    [EndpointDescription("Requires attendance.vehicle.fuel.manage, current web MFA and All scope. ExplicitEquivalentTripPerDay must be configured. Candidates must share retained policy and commute evidence and have zero nearer deficit. Select exact mileage identity and context fingerprint in monthly preview/submission. Nonzero deficits require a company commute allocation rule.")]
    public Task<VehicleMonthlyAssignmentContext> MonthlyAssignmentContext(Guid vehicleId, PreviewVehicleMonthlyAdjustmentRequest request, CancellationToken ct) =>
        service.MonthlyAssignmentContextAsync(vehicleId, request, Context(), ct);

    [HttpGet("trips/{tripId:guid}/attribution-correction-context"), ProducesResponseType<VehicleTripAttributionCorrectionContext>(200)]
    [EndpointSummary("Read approved legacy trip correction context")]
    [EndpointDescription("Requires attendance.vehicle.trips.manage, current web MFA and All scope. Retains original approval and mileage identity; returns source fingerprint, trip version and latest correction request. Missing original evidence fails closed.")]
    public Task<VehicleTripAttributionCorrectionContext> TripAttributionCorrectionContext(Guid tripId, CancellationToken ct) =>
        service.TripAttributionCorrectionContextAsync(tripId, Context(), ct);

    [HttpPost("trips/{tripId:guid}/attribution-corrections"), RequestSizeLimit(524288), ProducesResponseType<VehicleTripAttributionCorrectionView>(200)]
    [EndpointSummary("Propose dated attribution correction for an approved legacy trip")]
    [EndpointDescription("Requires attendance.vehicle.trips.manage, current web MFA and All scope. Exact source fingerprint, trip version, latest request ID, daily evidence, reason and Idempotency-Key are required. At most one correction may be approved. Original trip, mileage, fuel claims and maintenance remain immutable.")]
    public Task<VehicleTripAttributionCorrectionView> SubmitTripAttributionCorrection(Guid tripId, SubmitVehicleTripAttributionCorrectionRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SubmitTripAttributionCorrectionAsync(tripId, request, key, Context(), ct);

    [HttpPost("trips/{tripId:guid}/attribution-corrections/{requestId:guid}/review"), RequestSizeLimit(16384), ProducesResponseType<VehicleTripAttributionCorrectionView>(200)]
    [EndpointSummary("Independently review a retained legacy trip correction")]
    [EndpointDescription("Requires attendance.vehicle.trips.manage, current web MFA, All scope, retained and current policy reviewer authority. Reviewer must differ from employee, trip requester and proposer. Approval retains corrected evidence without a new mileage entry or financial settlement. Changed monthly evidence needs explicit revision; fuel and consumed maintenance require reconciliation.")]
    public Task<VehicleTripAttributionCorrectionView> ReviewTripAttributionCorrection(Guid tripId, Guid requestId, ReviewVehicleAdjustmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.ReviewTripAttributionCorrectionAsync(tripId, requestId, request, key, Context(), ct);

    [HttpGet("trips/{tripId:guid}/attribution-corrections"), ProducesResponseType<IReadOnlyList<VehicleTripAttributionCorrectionView>>(200)]
    [EndpointSummary("Read retained legacy trip correction history")]
    [EndpointDescription("Requires attendance.vehicle.trips.manage, current web MFA and All scope. Audited history is bounded to 1000 requests and preserves returned, rejected and approved evidence.")]
    public Task<IReadOnlyList<VehicleTripAttributionCorrectionView>> TripAttributionCorrectionHistory(Guid tripId, CancellationToken ct) =>
        service.TripAttributionCorrectionHistoryAsync(tripId, Context(), ct);

    [HttpGet("trips/{tripId:guid}/attribution-context"), ProducesResponseType<VehicleTripAttributionContext>(200)]
    [EndpointSummary("Read trip attribution concurrency context")]
    [EndpointDescription("Requires attendance.vehicle.trips.manage, current web MFA and All scope. Returns the exact trip approval rowversion and latest proposal identity needed for an explicit dated attribution submission. Read is audited.")]
    public Task<VehicleTripAttributionContext> TripAttributionContext(Guid tripId, CancellationToken ct) =>
        service.TripAttributionContextAsync(tripId, Context(), ct);

    [HttpPost("trips/{tripId:guid}/attributions"), RequestSizeLimit(524288), ProducesResponseType<VehicleTripAttributionView>(200)]
    [EndpointSummary("Propose explicit dated trip mileage attribution")]
    [EndpointDescription("Requires attendance.vehicle.trips.manage, current web MFA and All scope. Idempotency-Key, trip approval rowversion, latest attribution ID, evidence and reason are required. Daily UTC intervals must exactly cover the trip and preserve odometer/map totals; each Pakistan-local date names its policy and commute evidence. No automatic mileage splitting.")]
    public Task<VehicleTripAttributionView> SubmitTripAttribution(Guid tripId, SubmitVehicleTripAttributionRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SubmitTripAttributionAsync(tripId, request, key, Context(), ct);

    [HttpPost("trips/{tripId:guid}/attributions/{requestId:guid}/review"), RequestSizeLimit(16384), ProducesResponseType<VehicleTripAttributionView>(200)]
    [EndpointSummary("Review dated mileage evidence and its pending trip")]
    [EndpointDescription("Requires attendance.vehicle.trips.manage, current web MFA, All scope and every retained segment's configured reviewer role. Reviewer must differ from employee, trip requester and attribution proposer. Exact version, evidence, reason and Idempotency-Key required. Approval retains one aggregate mileage row with immutable dated evidence; return/reject permits a new proposal and does not approve the trip. No settlement or payment.")]
    public Task<VehicleTripAttributionView> ReviewTripAttribution(Guid tripId, Guid requestId, ReviewVehicleAdjustmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.ReviewTripAttributionAsync(tripId, requestId, request, key, Context(), ct);

    [HttpGet("trips/{tripId:guid}/attributions"), ProducesResponseType<IReadOnlyList<VehicleTripAttributionView>>(200)]
    [EndpointSummary("Read retained dated trip attribution history")]
    [EndpointDescription("Requires attendance.vehicle.trips.manage, current web MFA and All scope. Audited, bounded history includes pending, returned, rejected and approved proposals; approved evidence remains immutable.")]
    public Task<IReadOnlyList<VehicleTripAttributionView>> TripAttributionHistory(Guid tripId, CancellationToken ct) =>
        service.TripAttributionHistoryAsync(tripId, Context(), ct);

    #region HR Policy Publication

    [HttpGet("policies"), ProducesResponseType<VehicleFuelPage<VehiclePolicyView>>(200)]
    [EndpointSummary("List HR vehicle policy definitions")]
    [EndpointDescription("Requires attendance.vehicle.policy.manage, current web MFA and fixed Admin/SuperAdmin with matching All scope. Reads are audited.")]
    public Task<VehicleFuelPage<VehiclePolicyView>> ListPolicies([FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, CancellationToken ct = default) => service.ListPoliciesAsync(page, pageSize, Context(), ct);

    [HttpPost("policies"), RequestSizeLimit(8192), ProducesResponseType<VehiclePolicyView>(200)]
    [EndpointSummary("Create an HR vehicle policy definition")]
    [EndpointDescription("Requires attendance.vehicle.policy.manage, current web MFA and fixed Admin/SuperAdmin with matching All scope. Idempotency-Key and reason are required. Does not create rates, eligibility or role grants.")]
    public Task<VehiclePolicyView> CreatePolicy(CreateVehiclePolicyRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.CreatePolicyAsync(request, key, Context(), ct);

    [HttpGet("policies/{id:guid}"), ProducesResponseType<VehiclePolicyView>(200)]
    [EndpointSummary("Read an HR vehicle policy and its concurrency version")]
    [EndpointDescription("Requires attendance.vehicle.policy.manage and fixed Admin/SuperAdmin with matching All scope and current web MFA.")]
    public Task<VehiclePolicyView> PolicyDetail(Guid id, CancellationToken ct) => service.PolicyDetailAsync(id, Context(), ct);

    [HttpPost("policies/validate"), RequestSizeLimit(131072), ProducesResponseType<VehiclePolicyValidation>(200)]
    [EndpointSummary("Validate explicit HR vehicle policy configuration")]
    [EndpointDescription("Requires attendance.vehicle.policy.manage and fixed Admin/SuperAdmin with matching All scope and current web MFA. Checks dated eligibility, workshop and incident authority references, rates, thresholds and competing policy periods. Null capability settings remain unconfigured; no amounts or grants are inferred. Does not publish or certify external authority.")]
    public Task<VehiclePolicyValidation> ValidatePolicy(ValidateVehiclePolicyRequest request, CancellationToken ct) =>
        service.ValidatePolicyAsync(request, Context(), ct);

    [HttpPost("policies/{id:guid}/publication-preview"), RequestSizeLimit(131072), ProducesResponseType<VehiclePolicyPreview>(200)]
    [EndpointSummary("Preview an immutable HR vehicle policy publication")]
    [EndpointDescription("Requires attendance.vehicle.policy.manage and fixed Admin/SuperAdmin with matching All scope and current web MFA. Binds the exact policy revision, rowversion, explicit configuration and future finite dates to a publication fingerprint.")]
    public Task<VehiclePolicyPreview> PreviewPolicy(Guid id, PreviewVehiclePolicyRequest request, CancellationToken ct) =>
        service.PreviewPolicyAsync(id, request, Context(), ct);

    [HttpPost("policies/{id:guid}/versions"), RequestSizeLimit(131072), ProducesResponseType<VehiclePolicyPublicationResult>(200)]
    [EndpointSummary("Publish a future HR vehicle policy version")]
    [EndpointDescription("Requires attendance.vehicle.policy.manage and fixed Admin/SuperAdmin with matching All scope and current web MFA. Idempotency-Key, reason, expected revision/rowversion and preview fingerprint are required. Revalidates under the authorization transaction. Dates are start-inclusive/end-exclusive. Schema 3 may explicitly succeed the current version of the same definition with predecessor ID and authority evidence; no branch, unrelated overlap or automatic predecessor mutation is allowed. Expiry never resurrects superseded rules. Existing claim values and policy versions remain immutable. Does not authorize deductions, settlement or payment.")]
    public Task<VehiclePolicyPublicationResult> PublishPolicy(Guid id, PublishVehiclePolicyRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.PublishPolicyAsync(id, request, key, Context(), ct);

    [HttpGet("policies/{id:guid}/versions"), ProducesResponseType<VehicleFuelPage<VehiclePolicyVersionView>>(200)]
    [EndpointSummary("List retained HR vehicle policy versions")]
    [EndpointDescription("Requires attendance.vehicle.policy.manage and fixed Admin/SuperAdmin with matching All scope and current web MFA. Includes exact historical rules and publication authority; no repricing occurs.")]
    public Task<VehicleFuelPage<VehiclePolicyVersionView>> PolicyVersions(Guid id, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, CancellationToken ct = default) => service.PolicyVersionsAsync(id, page, pageSize, Context(), ct);

    [HttpGet("policies/{id:guid}/versions/{versionId:guid}"), ProducesResponseType<VehiclePolicyVersionView>(200)]
    [EndpointSummary("Read an exact retained HR vehicle policy version")]
    [EndpointDescription("Requires attendance.vehicle.policy.manage and fixed Admin/SuperAdmin with matching All scope and current web MFA. Version identity must belong to the route policy.")]
    public Task<VehiclePolicyVersionView> PolicyVersionDetail(Guid id, Guid versionId, CancellationToken ct) =>
        service.PolicyVersionDetailAsync(id, versionId, Context(), ct);

    #endregion

    #region Vehicle Assets

    [HttpPost, RequestSizeLimit(16384), ProducesResponseType<VehicleResponse>(200)]
    [EndpointSummary("Register a company vehicle asset")]
    [EndpointDescription("Requires attendance.vehicle.manage.")]
    public Task<VehicleResponse> CreateVehicle(
        CreateVehicleRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        service.CreateVehicleAsync(request, key, Context(), ct);

    [HttpGet, ProducesResponseType<IReadOnlyList<VehicleResponse>>(200)]
    [EndpointSummary("List company vehicles")]
    [EndpointDescription("Requires attendance.vehicle.manage.")]
    public Task<IReadOnlyList<VehicleResponse>> ListVehicles(
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default) =>
        service.ListVehiclesAsync(activeOnly, Context(), ct);

    #endregion

    #region Custody

    [HttpPost("custody/issue"), RequestSizeLimit(16384), ProducesResponseType<VehicleCustodyResponse>(200)]
    [EndpointSummary("Issue vehicle custody to an employee")]
    [EndpointDescription("Requires attendance.vehicle.custody.manage.")]
    public Task<VehicleCustodyResponse> IssueCustody(
        IssueCustodyRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        service.IssueCustodyAsync(request, key, Context(), ct);

    [HttpPost("custody/{id:guid}/return"), RequestSizeLimit(16384), ProducesResponseType<VehicleCustodyResponse>(200)]
    [EndpointSummary("Record vehicle return and handover condition")]
    [EndpointDescription("Requires attendance.vehicle.custody.manage.")]
    public Task<VehicleCustodyResponse> ReturnCustody(
        Guid id,
        ReturnCustodyRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        service.ReturnCustodyAsync(id, request, key, Context(), ct);

    [HttpGet("custody/history"), ProducesResponseType<IReadOnlyList<VehicleCustodyResponse>>(200)]
    [EndpointSummary("List vehicle custody history")]
    [EndpointDescription("Requires attendance.vehicle.custody.manage.")]
    public Task<IReadOnlyList<VehicleCustodyResponse>> CustodyHistory(
        [FromQuery] Guid? vehicleId,
        [FromQuery] Guid? employeeId,
        CancellationToken ct) =>
        service.ListCustodyHistoryAsync(vehicleId, employeeId, Context(), ct);

    #endregion

    #region Baseline Commute Distance

    [HttpPost("commute"), RequestSizeLimit(16384), ProducesResponseType<EmployeeCommuteDistanceResponse>(200)]
    [EndpointSummary("Configure employee baseline home-office commute distance")]
    [EndpointDescription("Requires attendance.vehicle.commute.manage.")]
    public Task<EmployeeCommuteDistanceResponse> SetCommuteDistance(
        SetCommuteDistanceRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        service.SetCommuteDistanceAsync(request, key, Context(), ct);

    #endregion

    #region Trips & Approval

    [HttpGet("trips"), ProducesResponseType<VehicleFuelPage<VehicleTripResponse>>(200)]
    [EndpointSummary("List and page vehicle trips")]
    [EndpointDescription("Requires attendance.vehicle.trips.manage.")]
    public Task<VehicleFuelPage<VehicleTripResponse>> PageTrips(
        [FromQuery] Guid? employeeId,
        [FromQuery] Guid? vehicleId,
        [FromQuery] ApprovalState? state,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        service.PageTripsAsync(employeeId, vehicleId, state, page, pageSize, Context(), ct);

    [HttpPost("trips/{id:guid}/approve"), RequestSizeLimit(8192), ProducesResponseType<VehicleTripResponse>(200)]
    [EndpointSummary("Approve an authorized vehicle trip")]
    [EndpointDescription("Requires attendance.vehicle.trips.manage.")]
    public Task<VehicleTripResponse> ApproveTrip(
        Guid id,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        service.ApproveTripAsync(id, key, Context(), ct);

    #endregion

    #region Fuel Claim Review

    [HttpGet("fuel-claims"), ProducesResponseType<VehicleFuelPage<FuelClaimResponse>>(200)]
    [EndpointSummary("List and page fuel claims")]
    [EndpointDescription("Requires attendance.vehicle.fuel.manage.")]
    public Task<VehicleFuelPage<FuelClaimResponse>> PageFuelClaims(
        [FromQuery] Guid? employeeId,
        [FromQuery] ApprovalState? state,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        service.PageFuelClaimsAsync(employeeId, state, page, pageSize, Context(), ct);

    [HttpPost("fuel-claims/{id:guid}/approve"), RequestSizeLimit(8192), ProducesResponseType<FuelClaimResponse>(200)]
    [EndpointSummary("Record HR approval of a retained fuel claim")]
    [EndpointDescription("Requires attendance.vehicle.fuel.manage. Rechecks retained mileage, policy and claim values. This HR review does not post, settle or pay the claim.")]
    public Task<FuelClaimResponse> ApproveFuelClaim(
        Guid id,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        service.ApproveFuelClaimAsync(id, key, Context(), ct);

    [HttpGet("fuel-claims/{id:guid}/historical-closure-context"), ProducesResponseType<HistoricalFuelClosureContext>(200)]
    [EndpointSummary("Read retained evidence for closing a superseded pending fuel claim")]
    [EndpointDescription("Requires attendance.vehicle.fuel.manage and an independent current MFA reviewer. Only a retained legacy claim with an approved trip correction is eligible. Returns exact source fingerprint and version; no historical amount is changed.")]
    public Task<HistoricalFuelClosureContext> HistoricalFuelClosureContext(Guid id, CancellationToken ct) =>
        service.HistoricalFuelClosureContextAsync(id, Context(), ct);

    [HttpPost("fuel-claims/{id:guid}/historical-closure"), RequestSizeLimit(16384), ProducesResponseType<HistoricalFuelClosureView>(200)]
    [EndpointSummary("Close a superseded pending legacy fuel claim with retained evidence")]
    [EndpointDescription("Requires attendance.vehicle.fuel.manage, current MFA, independent review, CSRF and Idempotency-Key. Rejects the old pending claim against exact version, source fingerprint, evidence and reason. Preserves original calculations and approved history. This does not create a replacement claim, adjustment, posting, settlement or payment.")]
    public Task<HistoricalFuelClosureView> CloseHistoricalFuelClaim(Guid id, CloseHistoricalFuelClaimRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.CloseHistoricalFuelClaimAsync(id, request, key, Context(), ct);

    [HttpGet("fuel-claims/{id:guid}/reconciliation-context"), ProducesResponseType<FuelReconciliationContext>(200)]
    [EndpointSummary("Compare retained approved fuel against an approved dated correction")]
    [EndpointDescription("Requires attendance.vehicle.fuel.manage, All scope and current MFA. Uses immutable original claim/approval and exact corrected policy lines. DifferenceAmount is an HR calculation difference, not a refund, debt, posting or payment.")]
    public Task<FuelReconciliationContext> FuelReconciliationContext(Guid id, CancellationToken ct) =>
        service.FuelReconciliationContextAsync(id, Context(), ct);

    [HttpPost("fuel-claims/{id:guid}/reconciliations"), RequestSizeLimit(16384), ProducesResponseType<FuelReconciliationView>(200)]
    [EndpointSummary("Submit an immutable HR fuel calculation reconciliation")]
    [EndpointDescription("Requires attendance.vehicle.fuel.manage, current MFA, All scope, CSRF and Idempotency-Key. Requires exact source fingerprint, latest request ID, evidence and reason. No original claim is changed and no settlement is inferred.")]
    public Task<FuelReconciliationView> SubmitFuelReconciliation(Guid id, SubmitFuelReconciliationRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SubmitFuelReconciliationAsync(id, request, key, Context(), ct);

    [HttpPost("fuel-claims/{id:guid}/reconciliations/{requestId:guid}/review"), RequestSizeLimit(16384), ProducesResponseType<FuelReconciliationView>(200)]
    [EndpointSummary("Independently review the HR fuel calculation difference")]
    [EndpointDescription("Requires attendance.vehicle.fuel.manage, current MFA, All scope, CSRF and Idempotency-Key. Reviewer must differ from employee, original claimant and proposer. Requires expected version and current retained sources. Approval is HR review only; Accounts posting/settlement remains separate.")]
    public Task<FuelReconciliationView> ReviewFuelReconciliation(Guid id, Guid requestId, ReviewVehicleAdjustmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.ReviewFuelReconciliationAsync(id, requestId, request, key, Context(), ct);

    [HttpGet("fuel-claims/{id:guid}/reconciliations"), ProducesResponseType<IReadOnlyList<FuelReconciliationView>>(200)]
    [EndpointSummary("Read retained HR fuel reconciliation history")]
    [EndpointDescription("Requires attendance.vehicle.fuel.manage, All scope and current MFA. Retains original comparisons, proposals and independent decisions. SettlementState NotRecorded is not evidence that no payment ever occurred.")]
    public Task<IReadOnlyList<FuelReconciliationView>> FuelReconciliationHistory(Guid id, CancellationToken ct) =>
        service.FuelReconciliationHistoryAsync(id, Context(), ct);

    #endregion

    #region Monthly HR Distance Adjustments

    [HttpPost("{vehicleId:guid}/monthly-adjustments/preview"), RequestSizeLimit(8192), ProducesResponseType<VehicleMonthlyAdjustmentPreview>(200)]
    [EndpointSummary("Preview a completed month's HR distance adjustment")]
    [EndpointDescription("Requires attendance.vehicle.fuel.manage with All scope and current web MFA. Uses exact approved trip, commute and policy evidence. A whole calendar month must have one configured policy; ambiguous same-day assignments fail closed. Does not reprice fuel claims or authorize payment.")]
    public Task<VehicleMonthlyAdjustmentPreview> PreviewMonthlyAdjustment(Guid vehicleId, PreviewVehicleMonthlyAdjustmentRequest request, CancellationToken ct) =>
        service.PreviewMonthlyAdjustmentAsync(vehicleId, request, Context(), ct);

    [HttpPost("{vehicleId:guid}/monthly-adjustments"), RequestSizeLimit(16384), ProducesResponseType<VehicleMonthlyAdjustmentView>(200)]
    [EndpointSummary("Submit a retained monthly HR distance adjustment")]
    [EndpointDescription("Requires attendance.vehicle.fuel.manage with All scope, current web MFA and Idempotency-Key. Requires the preview fingerprint, explicit latest request identity, evidence and reason. Creates a pending immutable revision; no claim or mileage row is rewritten.")]
    public Task<VehicleMonthlyAdjustmentView> SubmitMonthlyAdjustment(Guid vehicleId, SubmitVehicleMonthlyAdjustmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.SubmitMonthlyAdjustmentAsync(vehicleId, request, key, Context(), ct);

    [HttpPost("{vehicleId:guid}/monthly-adjustments/{requestId:guid}/review"), RequestSizeLimit(16384), ProducesResponseType<VehicleMonthlyAdjustmentView>(200)]
    [EndpointSummary("Independently review a monthly HR distance adjustment")]
    [EndpointDescription("Requires attendance.vehicle.fuel.manage with All scope, current web MFA, the retained configured reviewer role and Idempotency-Key. Reviewer must differ from requester and beneficiary. Requires expected rowversion; approval rechecks source evidence. Approved, Returned or Rejected only. No Accounts action.")]
    public Task<VehicleMonthlyAdjustmentView> ReviewMonthlyAdjustment(Guid vehicleId, Guid requestId, ReviewVehicleAdjustmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.ReviewMonthlyAdjustmentAsync(vehicleId, requestId, request, key, Context(), ct);

    [HttpGet("{vehicleId:guid}/monthly-adjustments"), ProducesResponseType<IReadOnlyList<VehicleMonthlyAdjustmentView>>(200)]
    [EndpointSummary("Read retained monthly HR adjustment revisions")]
    [EndpointDescription("Requires attendance.vehicle.fuel.manage with All scope and current web MFA. Returns immutable pending, returned, rejected and approved history for the employee and month. Latest approved revision replaces, rather than adds to, its predecessor.")]
    public Task<IReadOnlyList<VehicleMonthlyAdjustmentView>> MonthlyAdjustmentHistory(Guid vehicleId, [FromQuery] Guid employeeId,
        [FromQuery] DateOnly monthStart, CancellationToken ct) => service.MonthlyAdjustmentHistoryAsync(vehicleId, employeeId, monthStart, Context(), ct);

    [HttpGet("{vehicleId:guid}/monthly-adjustments/entitlement"), ProducesResponseType<VehicleMonthlyEntitlement>(200)]
    [EndpointSummary("Read the currently usable approved monthly HR distance entitlement")]
    [EndpointDescription("Requires attendance.vehicle.fuel.manage with All scope and current web MFA. Only a source-current approved revision exposes AdjustedEligibleKm. ConfigurationPending, PendingReview, SourcesChanged and SourceUnavailable return null usable kilometres, never inferred zero. Prior approved evidence remains visible but is not repriced. This is an HR distance handoff, not a payable amount, posting or settlement.")]
    public Task<VehicleMonthlyEntitlement> MonthlyEntitlement(Guid vehicleId, [FromQuery] Guid employeeId,
        [FromQuery] DateOnly monthStart, CancellationToken ct) => service.MonthlyEntitlementAsync(vehicleId, employeeId, monthStart, Context(), ct);

    #endregion

    #region Maintenance & Workshop

    [HttpGet("maintenance"), ProducesResponseType<VehicleFuelPage<VehicleServiceRecordResponse>>(200)]
    [EndpointSummary("List and page vehicle maintenance records")]
    [EndpointDescription("Requires attendance.vehicle.maintenance.manage.")]
    public Task<VehicleFuelPage<VehicleServiceRecordResponse>> PageServiceRecords(
        [FromQuery] Guid? vehicleId,
        [FromQuery] Guid? employeeId,
        [FromQuery] ApprovalState? state,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        service.PageServiceRecordsAsync(vehicleId, employeeId, state, page, pageSize, Context(), ct);

    [HttpPost("maintenance/{id:guid}/approve"), RequestSizeLimit(8192), ProducesResponseType<VehicleServiceRecordResponse>(200)]
    [EndpointSummary("Approve vehicle maintenance reimbursement")]
    [EndpointDescription("Requires attendance.vehicle.maintenance.manage.")]
    public Task<VehicleServiceRecordResponse> ApproveMaintenance(
        Guid id,
        [FromHeader(Name = "Idempotency-Key"), Required] string key,
        CancellationToken ct) =>
        service.ApproveServiceRecordAsync(id, key, Context(), ct);

    #endregion

    #region Incidents & Liability

    [HttpPost("incidents/{id:guid}/review"), RequestSizeLimit(16384), ProducesResponseType<VehicleIncidentReviewResponse>(200)]
    [EndpointSummary("Record a configured vehicle incident review outcome")]
    [EndpointDescription("Requires attendance.vehicle.incident.manage and configured review authority. Records evidence and proposed liability only; does not authorize deductions or insurer settlement.")]
    public Task<VehicleIncidentReviewResponse> ReviewIncident(Guid id, ReviewVehicleIncidentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.ReviewIncidentAsync(id, request, key, Context(), ct);

    [HttpGet("incidents/{id:guid}/review"), ProducesResponseType<VehicleIncidentReviewResponse>(200)]
    [EndpointSummary("Read the retained vehicle incident review outcome")]
    public Task<VehicleIncidentReviewResponse> ReadIncidentReview(Guid id, CancellationToken ct) =>
        service.ReadIncidentReviewAsync(id, Context(), ct);

    [HttpPost("incidents/{id:guid}/amendments"), RequestSizeLimit(16384), ProducesResponseType<VehicleIncidentAmendmentView>(200)]
    [EndpointSummary("Propose an evidenced HR incident-review amendment")]
    [EndpointDescription("Requires attendance.vehicle.incident.manage with All scope, current web MFA and Idempotency-Key. Requires expected current decision identity and an explicitly configured allowed prior outcome. Original report and decisions remain immutable. Liability is proposed only; no deduction or settlement.")]
    public Task<VehicleIncidentAmendmentView> RequestIncidentAmendment(Guid id, ProposeVehicleIncidentAmendmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.RequestIncidentAmendmentAsync(id, request, key, Context(), ct);

    [HttpPost("incidents/{id:guid}/amendments/{requestId:guid}/review"), RequestSizeLimit(16384), ProducesResponseType<VehicleIncidentAmendmentView>(200)]
    [EndpointSummary("Independently review an HR incident amendment")]
    [EndpointDescription("Requires attendance.vehicle.incident.manage with All scope, current web MFA, configured incident and amendment reviewer authority, and Idempotency-Key. Checks expected rowversion, predecessor and independent reviewer. Approval changes the current HR interpretation while retaining prior decisions; no recovery or Accounts action.")]
    public Task<VehicleIncidentAmendmentView> ReviewIncidentAmendment(Guid id, Guid requestId, ReviewVehicleAdjustmentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string key, CancellationToken ct) =>
        service.ReviewIncidentAmendmentAsync(id, requestId, request, key, Context(), ct);

    [HttpGet("incidents/{id:guid}/review-history"), ProducesResponseType<VehicleIncidentReviewHistory>(200)]
    [EndpointSummary("Read original, current and amended HR incident review history")]
    [EndpointDescription("Requires attendance.vehicle.incident.manage with All scope and current web MFA. Reads retained protected evidence; no repricing, deduction or settlement is performed.")]
    public Task<VehicleIncidentReviewHistory> IncidentReviewHistory(Guid id, CancellationToken ct) =>
        service.IncidentReviewHistoryAsync(id, Context(), ct);

    [HttpGet("incidents"), ProducesResponseType<VehicleFuelPage<VehicleIncidentResponse>>(200)]
    [EndpointSummary("List and page vehicle incident reports")]
    [EndpointDescription("Requires attendance.vehicle.incident.manage. Filters, counts and liability fields use the verified effective HR review, including approved amendments; pending/rejected amendment proposals do not replace the current review. Original report evidence remains retained. Projection is bounded at 1000 incidents; narrow custodyId when the explicit projection-limit conflict is returned. No deduction or insurer settlement.")]
    public Task<VehicleFuelPage<VehicleIncidentResponse>> PageIncidents(
        [FromQuery] Guid? custodyId,
        [FromQuery] ApprovalState? state,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        service.PageIncidentsAsync(custodyId, state, page, pageSize, Context(), ct);

    #endregion

    private AuthRequestContext Context() => new(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", HttpContext.TraceIdentifier);
}
