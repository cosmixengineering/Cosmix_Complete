using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.VehicleFuel;

namespace Cosmix.Application.HR.VehicleFuel;

public sealed partial class VehicleFuelService
{
    private static VehicleWorkflowPolicy RequireWorkflow(VehicleFuelPolicyValues policy) =>
        policy.Workflow ?? throw AttendanceOperation.Conflict("vehicle_workflow_policy_not_configured");

    private static string ServicePurpose(Guid vehicleId) => $"vehicle:service:{vehicleId:N}";
    private static string IncidentPurpose(Guid incidentId) => $"vehicle:incident:review:{incidentId:N}";

    private async Task<VehicleServiceEligibilitySnapshot> ServiceEligibilityAsync(Guid employeeId,
        SubmitServiceRecordRequest request, string vehicleType, CancellationToken ct, Guid? retainedVersion = null,
        Guid? reviewedServiceId = null, Guid? pinnedPreviousId = null,
        ValidatedMaintenancePredecessor? validatedPredecessor = null)
    {
        var (version, policy) = await LoadPolicyAsync(request.ServiceDate, ct, retainedVersion);
        var workflow = RequireWorkflow(policy);
        var staff = workflow.RequireStaff(employeeId, request.ServiceDate, request.ServiceDate);
        var rule = workflow.RequireMaintenance(request.ServiceKind, vehicleType, request.ServiceDate,
            request.AuthorizedWorkshopReference, request.ClaimedAmount, request.Currency);
        var requiredKm = request.ServiceKind == VehicleServiceKind.Tuning ? policy.TuningKm : policy.OverhaulKm;
        if (requiredKm is null || requiredKm > 1_000_000 || decimal.Round(requiredKm.Value, 3) != requiredKm)
            throw AttendanceOperation.Conflict("vehicle_fuel_policy_not_configured");
        var mileage = await repository.ListApprovedMileageAsync(request.VehicleId, request.ServiceDate, ct);
        if (mileage.Count > 10000) throw AttendanceOperation.Conflict("vehicle_mileage_source_limit");
        if (mileage.Any(x => x.VehicleId != request.VehicleId || x.BusinessDate > request.ServiceDate ||
                x.ApprovedBusinessKm != x.OutstationKm + x.ExcludedCommuteKm || x.MonthlyAdjustmentKm != 0) ||
            mileage.Select(x => x.VehicleTripId).Distinct().Count() != mileage.Count)
            throw AttendanceOperation.Conflict("vehicle_mileage_source_invalid");
        var total = mileage.Sum(x => x.OutstationKm);
        var datedSources = new List<object>();
        var previous = validatedPredecessor?.Record ??
            await repository.FindLatestApprovedServiceAsync(request.VehicleId, request.ServiceKind, ct);
        if (reviewedServiceId is not null)
        {
            if (pinnedPreviousId is null || previous is null ||
                previous.Id != pinnedPreviousId && previous.Id != reviewedServiceId)
                throw AttendanceOperation.Conflict("vehicle_service_eligibility_changed");
            previous = await repository.FindServiceRecordAsync(pinnedPreviousId.Value, ct)
                ?? throw AttendanceOperation.Conflict("vehicle_service_cycle_evidence_unavailable");
        }
        var requiresEquivalence = false;
        var appliedCorrections = new List<Guid>();
        foreach (var entry in mileage)
        {
            var headers = await repository.ListVehicleAdjustmentRequestsAsync(entry.VehicleTripId, TripAttributionRequestType, ct);
            var correction = (await CorrectionRows(entry.VehicleTripId, ct)).SingleOrDefault(r => r.Header.State == ApprovalState.Approved);
            if (!headers.Any(h => h.State == ApprovalState.Approved) && correction is null) continue;
            var trip = await AttributionTrip(entry.VehicleTripId, ct);
            var segments = await ApprovedTripSegments(entry, trip, ct) ?? throw AttendanceOperation.Conflict("vehicle_attribution_evidence_invalid");
            if (correction is not null)
            {
                if (previous is not null && entry.BusinessDate <= previous.ServiceDate &&
                    (await ReadServiceEligibilityAsync(previous, ct)).AppliedTripCorrectionIds?.Contains(correction.Header.Id) != true)
                    requiresEquivalence = true;
                appliedCorrections.Add(correction.Header.Id);
            }
            total -= entry.OutstationKm;
            total += segments.Where(s => s.Source.BusinessDate <= request.ServiceDate).Sum(s => s.EligibleKm);
            datedSources.Add(new { entry.Id, Segments = segments.Where(s => s.Source.BusinessDate <= request.ServiceDate).ToArray() });
        }
        var adjustments = await MaintenanceMonthlyAdjustments(request.VehicleId, request.ServiceDate, ct);
        total += adjustments.Delta;
        if (total < 0) throw AttendanceOperation.Conflict("vehicle_maintenance_adjusted_mileage_negative");
        if (total > 999_999_999.999m) throw AttendanceOperation.Conflict("vehicle_mileage_source_invalid");
        var fingerprint = ServiceMileageFingerprint(mileage);
        // Only a nonempty approved adjustment set changes the legacy mileage fingerprint.
        if (datedSources.Count > 0) fingerprint = AdjustmentHash(new { Mileage = fingerprint, DatedSources = datedSources });
        if (adjustments.Fingerprint != AdjustmentHash(Array.Empty<object>()))
            fingerprint = AdjustmentHash(new { Mileage = fingerprint, adjustments.Fingerprint });
        var consumed = 0m;
        ConsumedMaintenanceEquivalence? equivalence = null;
        if (previous is not null)
        {
            if (previous.VehicleId != request.VehicleId || previous.ServiceKind != request.ServiceKind ||
                previous.ServiceDate > request.ServiceDate)
                throw AttendanceOperation.Conflict("vehicle_service_cycle_chronology_conflict");
            if (rule.CycleMode == "Once") throw AttendanceOperation.Conflict("vehicle_service_once_already_consumed");
            var prior = await ReadServiceEligibilityAsync(previous, ct);
            if (prior.AppliedTripCorrectionIds?.Except(appliedCorrections).Any() == true)
                throw AttendanceOperation.Conflict("vehicle_maintenance_attribution_reconciliation_required");
            if (validatedPredecessor is not null)
                equivalence = validatedPredecessor.Equivalence;
            else if (prior.ConsumedCorrectionEquivalence is not null || requiresEquivalence)
                equivalence = await ProveConsumedMaintenanceChain(previous, prior, request.ServiceDate,
                    reviewedServiceId, ct);
            var previousAdjustments = await MaintenanceMonthlyAdjustments(request.VehicleId, previous.ServiceDate, ct);
            if ((previousAdjustments.HasDailyAggregation || prior.MonthlyAggregationFingerprint is not null) &&
                previousAdjustments.Fingerprint != prior.MonthlyAggregationFingerprint)
                throw AttendanceOperation.Conflict("vehicle_maintenance_monthly_reconciliation_required");
            if (prior.RequiredKm != requiredKm.Value || prior.Rule.CycleMode != rule.CycleMode)
            {
                var transitions = AdjustmentPolicy(version)?.MaintenanceTransitions.Where(t => t.Kind == request.ServiceKind &&
                    t.PreviousPolicyVersionId == previous.PolicyVersionId).ToArray();
                if (transitions is not { Length: 1 } || transitions[0].Mode != "CarryConsumedKilometers")
                    throw AttendanceOperation.Conflict("vehicle_service_cycle_policy_transition_unconfigured");
                fingerprint = AdjustmentHash(new { Mileage = fingerprint, Transition = transitions[0], PreviousServiceId = previous.Id });
            }
            consumed = prior.ConsumedThroughKm;
        }
        var through = consumed + requiredKm.Value;
        if (total < through)
            throw AttendanceOperation.Conflict($"maintenance_milestone_not_reached:{Kilometers(total)}km_of_{Kilometers(through)}km");
        return new(2, request.VehicleId, employeeId, request.ServiceDate, request.ServiceKind, request.OdometerKm,
            version.Id, total, request.ApprovedOutstationKm, requiredKm.Value, request.AuthorizedWorkshopReference,
            request.EvidenceReference, request.ClaimedAmount, request.Currency.ToUpperInvariant(), vehicleType,
            fingerprint, previous?.Id, consumed, through, rule, staff)
            { AppliedTripCorrectionIds = appliedCorrections.Count > 0 ? appliedCorrections.Order().ToArray() : null,
                MonthlyAggregationFingerprint = adjustments.HasDailyAggregation ? adjustments.Fingerprint : null,
                ConsumedCorrectionEquivalence = equivalence };
    }

    private static string ServiceMileageFingerprint(IEnumerable<VehicleMileageEntry> mileage) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(mileage.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.VehicleTripId, x.VehicleId, x.EmployeeId, x.BusinessDate, x.ApprovalRequestId,
                x.PolicyVersionId, x.ApprovedBusinessKm, x.ExcludedCommuteKm, x.OutstationKm, x.MonthlyAdjustmentKm,
                x.CalculationPrivateReference }))));

    private async Task<VehicleServiceEligibilitySnapshot> ReadServiceEligibilityAsync(VehicleServiceRecord record, CancellationToken ct)
    {
        var snapshot = await operation.ReadPrivateAsync<VehicleServiceEligibilitySnapshot>(
            record.EligibilityCalculationPrivateReference, ServicePurpose(record.VehicleId), ct);
        if (snapshot.SchemaVersion != 2 || snapshot.VehicleId != record.VehicleId || snapshot.EmployeeId != record.EmployeeId ||
            snapshot.ServiceDate != record.ServiceDate || snapshot.ServiceKind != record.ServiceKind ||
            snapshot.OdometerKm != record.OdometerKm || snapshot.PolicyVersionId != record.PolicyVersionId ||
            snapshot.ApprovedOutstationKm != record.ApprovedOutstationKm ||
            snapshot.AuthorizedWorkshopReference != record.AuthorizedWorkshopReference ||
            snapshot.EvidenceReference != record.EvidencePrivateReference || snapshot.ClaimedAmount != record.ClaimedAmount ||
            snapshot.Currency != record.Currency || snapshot.RequiredKm <= 0 || snapshot.ConsumedBeforeKm < 0 ||
            snapshot.ConsumedThroughKm != snapshot.ConsumedBeforeKm + snapshot.RequiredKm ||
            snapshot.ConsumedThroughKm > snapshot.ApprovedOutstationKm || snapshot.MileageFingerprint is not { Length: 64 } ||
            snapshot.Rule is null || snapshot.StaffEligibility is null ||
            snapshot.AppliedTripCorrectionIds is { } corrections && (corrections.Any(id => id == Guid.Empty) || corrections.Distinct().Count() != corrections.Count) ||
            snapshot.MonthlyAggregationFingerprint is not null and not { Length: 64 })
            throw AttendanceOperation.Conflict("vehicle_service_cycle_evidence_unavailable");
        return snapshot;
    }

    public Task<VehicleIncidentReviewResponse> ReviewIncidentAsync(Guid incidentId, ReviewVehicleIncidentRequest request,
        string key, AuthRequestContext context, CancellationToken ct) => operation.WriteAsync(
        VehicleFuelPermissions.Incident, "vehicle.incident.review", null, key, new { IncidentId = incidentId, Request = request },
        request.Reason, context, async (actor, token) =>
        {
            if (!Enum.IsDefined(request.Outcome) || string.IsNullOrWhiteSpace(request.EvidenceReference) ||
                request.EvidenceReference.Length > 500 || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
                throw AttendanceOperation.Invalid("vehicle_incident_review_invalid");
            var incident = await repository.FindIncidentAsync(incidentId, token)
                ?? throw AttendanceOperation.Missing("incident_not_found");
            var custody = await repository.FindCustodyAsync(incident.VehicleCustodyId, token)
                ?? throw AttendanceOperation.Conflict("vehicle_incident_custody_unavailable");
            RequireIndependentReviewer(custody.EmployeeId, incident.RequestedByUserId, actor);
            if (incident.ApprovalRequest is not { State: ApprovalState.Pending } approval ||
                approval.Id != request.ExpectedApprovalRequestId || approval.SubjectId != incident.Id ||
                approval.RequestType != IncidentRequestType)
                throw AttendanceOperation.Conflict("vehicle_incident_not_pending");
            if (incident.OccurredAt > actor.Now || incident.OccurredAt < custody.IssuedAt || custody.ReturnedAt < incident.OccurredAt)
                throw AttendanceOperation.Conflict("vehicle_incident_custody_unavailable");
            var (version, policy) = await LoadPolicyAsync(RosterSchedule.Today(incident.OccurredAt), token);
            var authority = RequireWorkflow(policy).RequireIncidentReviewer(actor.RoleCode);
            if (request.Outcome == VehicleIncidentReviewOutcome.ProposedLiabilityRecorded)
            {
                if (request.ProposedEmployeeLiability is null or <= 0 or > 10_000_000 ||
                    decimal.Round(request.ProposedEmployeeLiability.Value, 2) != request.ProposedEmployeeLiability ||
                    request.Currency is not { Length: 3 } || request.Currency.Any(c => c is < 'A' or > 'Z'))
                    throw AttendanceOperation.Invalid("incident_liability_currency_required");
            }
            else if (request.ProposedEmployeeLiability.HasValue || request.Currency is not null)
                throw AttendanceOperation.Invalid("vehicle_incident_outcome_amount_not_allowed");
            if (request.InsuranceClaimReference?.Length > 200 ||
                request.Outcome == VehicleIncidentReviewOutcome.InsuranceReviewRequired && string.IsNullOrWhiteSpace(request.InsuranceClaimReference))
                throw AttendanceOperation.Invalid("vehicle_incident_insurance_reference_required");
            var state = IncidentDecisionState(request.Outcome);
            var snapshot = new VehicleIncidentReviewSnapshot(1, incident.Id, approval.Id, incident.VehicleCustodyId,
                custody.EmployeeId, version.Id, authority.AuthorityReference, request.Outcome,
                request.ProposedEmployeeLiability, request.Currency, request.InsuranceClaimReference,
                request.EvidenceReference, request.Reason, actor.Identity.AccountId, actor.RoleCode, actor.Now);
            var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot),
                IncidentPurpose(incident.Id), token));
            var decision = approval.Decide(state, actor.Identity.AccountId.ToString(), actor.RoleCode, request.Reason, actor.Now, reference);
            repository.AddDecision(decision);
            await repository.SaveChangesAsync(token);
            var response = IncidentReviewResponse(snapshot, decision.Id, state);
            return new AttendanceChange<VehicleIncidentReviewResponse>(incident.Id, null, response);
        }, ct, self: false, beforeReplay: async (actor, token) =>
        {
            var incident = await repository.FindIncidentAsync(incidentId, token)
                ?? throw AttendanceOperation.Missing("incident_not_found");
            var custody = await repository.FindCustodyAsync(incident.VehicleCustodyId, token)
                ?? throw AttendanceOperation.Conflict("vehicle_incident_custody_unavailable");
            RequireIndependentReviewer(custody.EmployeeId, incident.RequestedByUserId, actor);
            var (_, policy) = await LoadPolicyAsync(RosterSchedule.Today(incident.OccurredAt), token);
            RequireWorkflow(policy).RequireIncidentReviewer(actor.RoleCode);
        });

    public Task<VehicleIncidentReviewResponse> ReadIncidentReviewAsync(Guid incidentId, AuthRequestContext context,
        CancellationToken ct) => operation.ReadAsync(VehicleFuelPermissions.Incident, "vehicle.incident.review.read", null,
        context, async (_, token) => (await IncidentHistory(incidentId, token)).Current, ct, self: false);

    private async Task<VehicleIncidentReviewResponse> OriginalIncidentReview(VehicleIncident incident, CancellationToken token)
    {
            var decision = incident.ApprovalRequestId is Guid approvalId
                ? await repository.FindIncidentDecisionAsync(approvalId, token) : null;
            if (decision?.ProtectedChangeReference is null) throw AttendanceOperation.Conflict("vehicle_incident_review_unavailable");
            var snapshot = await operation.ReadPrivateAsync<VehicleIncidentReviewSnapshot>(decision.ProtectedChangeReference,
                IncidentPurpose(incident.Id), token);
            if (snapshot.SchemaVersion != 1 || snapshot.IncidentId != incident.Id ||
                snapshot.ApprovalRequestId != incident.ApprovalRequestId || snapshot.VehicleCustodyId != incident.VehicleCustodyId ||
                snapshot.ActorAccountId.ToString() != decision.ActorUserId || snapshot.ActorRoleCode != decision.ActorRoleCode ||
                snapshot.RecordedAt != decision.DecidedAt || incident.ApprovalRequest?.State != decision.Decision ||
                !Enum.IsDefined(snapshot.Outcome) || IncidentDecisionState(snapshot.Outcome) != decision.Decision)
                throw AttendanceOperation.Conflict("vehicle_incident_review_unavailable");
            return IncidentReviewResponse(snapshot, decision.Id, decision.Decision);
    }

    private static ApprovalState IncidentDecisionState(VehicleIncidentReviewOutcome outcome) => outcome switch
    {
        VehicleIncidentReviewOutcome.ReturnedForEvidence => ApprovalState.Returned,
        VehicleIncidentReviewOutcome.Rejected => ApprovalState.Rejected,
        _ => ApprovalState.Approved
    };

    private static VehicleIncidentReviewResponse IncidentReviewResponse(VehicleIncidentReviewSnapshot s, Guid decisionId,
        ApprovalState state) => new(s.IncidentId, s.ApprovalRequestId, decisionId, s.Outcome, state.ToString(),
            s.ProposedEmployeeLiability, s.Currency, s.InsuranceClaimReference, s.EvidenceReference, s.Reason,
            s.PolicyVersionId, s.RecordedAt, false, false);

    private sealed record VehicleServiceEligibilitySnapshot(int SchemaVersion, Guid VehicleId, Guid EmployeeId,
        DateOnly ServiceDate, VehicleServiceKind ServiceKind, decimal OdometerKm, Guid PolicyVersionId,
        decimal ApprovedOutstationKm, decimal ClaimedOutstationKm, decimal RequiredKm, string AuthorizedWorkshopReference,
        string EvidenceReference, decimal ClaimedAmount, string Currency, string VehicleType, string MileageFingerprint,
        Guid? PreviousServiceRecordId, decimal ConsumedBeforeKm, decimal ConsumedThroughKm,
        VehicleMaintenanceRule Rule, VehicleStaffEligibilityRule StaffEligibility)
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<Guid>? AppliedTripCorrectionIds { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MonthlyAggregationFingerprint { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ConsumedMaintenanceEquivalence? ConsumedCorrectionEquivalence { get; init; }
    }

    private sealed record VehicleIncidentReviewSnapshot(int SchemaVersion, Guid IncidentId, Guid ApprovalRequestId,
        Guid VehicleCustodyId, Guid EmployeeId, Guid PolicyVersionId, string AuthorityReference,
        VehicleIncidentReviewOutcome Outcome, decimal? ProposedEmployeeLiability, string? Currency,
        string? InsuranceClaimReference, string EvidenceReference, string Reason, Guid ActorAccountId,
        string ActorRoleCode, DateTimeOffset RecordedAt);
}
