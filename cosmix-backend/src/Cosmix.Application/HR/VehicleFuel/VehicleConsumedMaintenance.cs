using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.VehicleFuel;

namespace Cosmix.Application.HR.VehicleFuel;

public sealed partial class VehicleFuelService
{
    private async Task<ConsumedMaintenanceEquivalence> ProveConsumedMaintenanceChain(
        VehicleServiceRecord previous, VehicleServiceEligibilitySnapshot prior, DateOnly nextDate,
        Guid? reviewedServiceId, CancellationToken ct, Guid? requiredServiceId = null)
    {
        // Walk first, then reconstruct from the original consumed service outward. No recursive evidence traversal.
        var chain = new List<(VehicleServiceRecord Record, VehicleServiceEligibilitySnapshot Snapshot, ApprovalDecision Decision)>();
        var seen = new HashSet<Guid>();
        var record = previous;
        var snapshot = prior;
        while (true)
        {
            if (chain.Count >= 64 || !seen.Add(record.Id) || record.Id == reviewedServiceId ||
                record.VehicleId != previous.VehicleId || record.ServiceKind != previous.ServiceKind ||
                snapshot.MonthlyAggregationFingerprint is not null)
                throw AttendanceOperation.Conflict("vehicle_service_cycle_evidence_unavailable");
            chain.Add((record, snapshot, await RequireServiceApprovalEvidence(record, ct)));
            if (snapshot.PreviousServiceRecordId is not Guid predecessorId)
            {
                if (snapshot.ConsumedCorrectionEquivalence is not null || snapshot.ConsumedBeforeKm != 0)
                    throw AttendanceOperation.Conflict("vehicle_service_cycle_evidence_unavailable");
                break;
            }
            if (snapshot.ConsumedCorrectionEquivalence is { } retainedProof &&
                predecessorId != retainedProof.ServiceRecordId)
                throw AttendanceOperation.Conflict("vehicle_service_cycle_evidence_unavailable");
            var predecessor = await repository.FindServiceRecordAsync(predecessorId, ct)
                ?? throw AttendanceOperation.Conflict("vehicle_service_cycle_evidence_unavailable");
            var predecessorSnapshot = await ReadServiceEligibilityAsync(predecessor, ct);
            if (predecessor.ServiceDate > record.ServiceDate ||
                predecessor.ApprovalRequest?.ClosedAt is not DateTimeOffset predecessorApprovedAt ||
                predecessorApprovedAt > record.CreatedAt ||
                predecessorSnapshot.ConsumedThroughKm != snapshot.ConsumedBeforeKm)
                throw AttendanceOperation.Conflict("vehicle_service_cycle_evidence_unavailable");
            record = predecessor;
            snapshot = predecessorSnapshot;
        }
        if (requiredServiceId is Guid required && !seen.Contains(required))
            throw AttendanceOperation.Conflict("vehicle_service_eligibility_changed");
        // A latest-row lookup alone cannot rule out an older competing approved branch.
        var approved = await repository.PageServiceRecordsAsync(previous.VehicleId, null, ApprovalState.Approved, 0, 10001, ct);
        if (approved.TotalCount > 10000 || approved.Items.Count != approved.TotalCount ||
            approved.Items.Select(r => r.Id).Distinct().Count() != approved.Items.Count ||
            approved.Items.Any(r => r.VehicleId != previous.VehicleId || r.ApprovalRequest?.State != ApprovalState.Approved))
            throw AttendanceOperation.Conflict("vehicle_service_cycle_evidence_unavailable");
        var sameKind = approved.Items.Where(r => r.ServiceKind == previous.ServiceKind && r.Id != reviewedServiceId)
            .Select(r => r.Id).ToHashSet();
        if (!sameKind.SetEquals(seen))
            throw AttendanceOperation.Conflict("vehicle_service_eligibility_changed");

        var proof = await ProveConsumedMaintenanceEquivalence(record, snapshot, nextDate, ct);
        for (var index = chain.Count - 2; index >= 0; index--)
        {
            var node = chain[index];
            if (node.Snapshot.ConsumedCorrectionEquivalence is null)
            {
                // Original approvals retain their original mileage fingerprint, not a retroactive equivalence proof.
                var predecessor = chain[index + 1];
                proof = await ProveConsumedMaintenanceEquivalence(node.Record, node.Snapshot, nextDate,
                    ct, (predecessor.Record, predecessor.Snapshot));
                continue;
            }
            if (JsonSerializer.Serialize(node.Snapshot.ConsumedCorrectionEquivalence) != JsonSerializer.Serialize(proof))
                throw AttendanceOperation.Conflict("vehicle_service_cycle_evidence_unavailable");
            var current = await ServiceEligibilityAsync(node.Record.EmployeeId,
                new(node.Record.VehicleId, node.Record.ServiceKind, node.Record.ServiceDate, node.Record.OdometerKm,
                    node.Snapshot.ClaimedOutstationKm, node.Record.AuthorizedWorkshopReference,
                    node.Record.EvidencePrivateReference, node.Record.ClaimedAmount, node.Record.Currency),
                node.Snapshot.VehicleType, ct, node.Record.PolicyVersionId,
                validatedPredecessor: new(chain[index + 1].Record, proof));
            if (JsonSerializer.Serialize(node.Snapshot) != JsonSerializer.Serialize(current))
                throw AttendanceOperation.Conflict("vehicle_service_cycle_evidence_unavailable");
            proof = MaintenanceEquivalenceProof(node.Record, node.Snapshot, node.Decision, proof.Members);
        }
        return proof;
    }

    private async Task<ConsumedMaintenanceEquivalence> ProveConsumedMaintenanceEquivalence(
        VehicleServiceRecord previous, VehicleServiceEligibilitySnapshot prior, DateOnly nextDate,
        CancellationToken ct,
        (VehicleServiceRecord Record, VehicleServiceEligibilitySnapshot Snapshot)? validatedPredecessor = null)
    {
        if (prior.PreviousServiceRecordId != validatedPredecessor?.Record.Id ||
            prior.ConsumedBeforeKm != (validatedPredecessor?.Snapshot.ConsumedThroughKm ?? 0) ||
            prior.AppliedTripCorrectionIds is { Count: > 0 } || prior.ConsumedCorrectionEquivalence is not null ||
            prior.MonthlyAggregationFingerprint is not null)
            throw AttendanceOperation.Conflict("vehicle_maintenance_attribution_reconciliation_required");
        await RequireNoEffectMonthlyHistory(previous.VehicleId, nextDate, ct);
        var decision = await RequireServiceApprovalEvidence(previous, ct);
        var (version, policy) = await LoadPolicyAsync(previous.ServiceDate, ct, previous.PolicyVersionId);
        var oldThreshold = previous.ServiceKind == VehicleServiceKind.Tuning ? policy.TuningKm : policy.OverhaulKm;
        var oldWorkflow = RequireWorkflow(policy);
        var oldRule = oldWorkflow.RequireMaintenance(previous.ServiceKind, prior.VehicleType, previous.ServiceDate,
            previous.AuthorizedWorkshopReference, previous.ClaimedAmount, previous.Currency);
        var oldStaff = oldWorkflow.RequireStaff(previous.EmployeeId, previous.ServiceDate, previous.ServiceDate);
        if (oldThreshold != prior.RequiredKm || AdjustmentHash(oldRule) != AdjustmentHash(prior.Rule) ||
            AdjustmentHash(oldStaff) != AdjustmentHash(prior.StaffEligibility))
            throw AttendanceOperation.Conflict("vehicle_maintenance_equivalence_policy_required");

        var mileage = await repository.ListApprovedMileageAsync(previous.VehicleId, previous.ServiceDate, ct);
        if (mileage.Count > 10000 || mileage.Select(m => m.VehicleTripId).Distinct().Count() != mileage.Count ||
            mileage.Any(m => m.VehicleId != previous.VehicleId || m.BusinessDate > previous.ServiceDate ||
                m.ApprovedBusinessKm != m.ExcludedCommuteKm + m.OutstationKm || m.MonthlyAdjustmentKm != 0))
            throw AttendanceOperation.Conflict("vehicle_maintenance_equivalence_sources_changed");
        var originalDated = new List<object>();
        foreach (var entry in mileage)
        {
            var headers = await repository.ListVehicleAdjustmentRequestsAsync(entry.VehicleTripId, TripAttributionRequestType, ct);
            if (!headers.Any(h => h.State == ApprovalState.Approved)) continue;
            var trip = await AttributionTrip(entry.VehicleTripId, ct);
            var segments = await ApprovedTripSegments(entry, trip, ct)
                ?? throw AttendanceOperation.Conflict("vehicle_attribution_evidence_invalid");
            originalDated.Add(new { entry.Id, Segments = segments.Where(s => s.Source.BusinessDate <= previous.ServiceDate).ToArray() });
        }
        // Reconstruct exact retained membership before accepting individual source proofs or totals.
        var fingerprint = ServiceMileageFingerprint(mileage);
        if (originalDated.Count > 0)
            fingerprint = AdjustmentHash(new { Mileage = fingerprint, DatedSources = originalDated });
        if (validatedPredecessor is { } predecessor)
        {
            if (oldRule.CycleMode == "Once")
                throw AttendanceOperation.Conflict("vehicle_service_once_already_consumed");
            if (predecessor.Snapshot.RequiredKm != prior.RequiredKm ||
                predecessor.Snapshot.Rule.CycleMode != prior.Rule.CycleMode)
            {
                var transitions = AdjustmentPolicy(version)?.MaintenanceTransitions.Where(t => t.Kind == previous.ServiceKind &&
                    t.PreviousPolicyVersionId == predecessor.Record.PolicyVersionId).ToArray();
                if (transitions is not { Length: 1 } || transitions[0].Mode != "CarryConsumedKilometers")
                    throw AttendanceOperation.Conflict("vehicle_service_cycle_policy_transition_unconfigured");
                // Reproduce the original workflow's exact transition-bearing fingerprint, without rebasing consumption.
                fingerprint = AdjustmentHash(new { Mileage = fingerprint, Transition = transitions[0], PreviousServiceId = predecessor.Record.Id });
            }
        }
        if (fingerprint != prior.MileageFingerprint)
            throw AttendanceOperation.Conflict("vehicle_maintenance_equivalence_sources_changed");
        var members = new List<ConsumedMaintenanceMember>();
        var originalTotal = 0m;
        var quantitiesMatch = true;
        foreach (var entry in mileage)
        {
            var trip = await AttributionTrip(entry.VehicleTripId, ct);
            var correction = (await CorrectionRows(trip.Id, ct)).SingleOrDefault(r => r.Header.State == ApprovalState.Approved);
            var segments = await ApprovedTripSegments(entry, trip, ct);
            var oldKm = entry.OutstationKm;
            string originalFingerprint;
            if (correction is not null || segments is null)
            {
                var original = await OriginalCorrectionSource(trip, ct);
                if (ServiceMileageFingerprint([original.Mileage]) != ServiceMileageFingerprint([entry]))
                    throw AttendanceOperation.Conflict("vehicle_maintenance_equivalence_sources_changed");
                originalFingerprint = original.Fingerprint;
            }
            else
            {
                var through = segments.Where(s => s.Source.BusinessDate <= previous.ServiceDate).ToArray();
                oldKm = through.Sum(s => s.EligibleKm);
                originalFingerprint = AdjustmentHash(new { entry.Id, entry.CalculationPrivateReference, Segments = segments });
            }
            var correctedKm = segments?.Where(s => s.Source.BusinessDate <= previous.ServiceDate).Sum(s => s.EligibleKm) ?? oldKm;
            quantitiesMatch &= correctedKm == oldKm;
            originalTotal += oldKm;
            members.Add(new(entry.Id, trip.Id, entry.CalculationPrivateReference, originalFingerprint, oldKm, correctedKm,
                correction?.Header.Id, correction?.Header.SnapshotPrivateReference, correction?.Decision?.Id,
                correction?.Decision?.ProtectedChangeReference,
                correction is null ? null : AdjustmentHash(new { correction.Snapshot,
                    Version = Convert.ToBase64String(correction.Header.RowVersion),
                    Decision = ReconciliationDecisionSource(correction.Decision!) })));
        }
        if (originalTotal != prior.ApprovedOutstationKm)
            throw AttendanceOperation.Conflict("vehicle_maintenance_equivalence_sources_changed");
        if (!quantitiesMatch)
            throw AttendanceOperation.Conflict("vehicle_maintenance_equivalence_policy_required");
        return MaintenanceEquivalenceProof(previous, prior, decision, members.OrderBy(m => m.MileageEntryId).ToArray());
    }

    private async Task RequireNoEffectMonthlyHistory(Guid vehicleId, DateOnly through, CancellationToken ct)
    {
        // Validate complete history before selecting the same approved revisions as the maintenance consumer.
        var rows = (await MonthlyRows(vehicleId, ct))
            .Where(r => r.Request.State == ApprovalState.Approved && r.Snapshot.Calculation.MonthStart.AddMonths(1) <= through)
            .GroupBy(r => (r.Snapshot.Calculation.EmployeeId, r.Snapshot.Calculation.MonthStart))
            .Select(g => g.MaxBy(r => r.Request.Stage)!);
        foreach (var row in rows)
        {
            var calculation = row.Snapshot.Calculation;
            if (calculation.Rule.MaintenanceEffect != "None" ||
                calculation.Partitions is { } partitions &&
                (partitions.Count == 0 || partitions.Any(p => p?.Rule?.MaintenanceEffect != "None")))
                throw AttendanceOperation.Conflict("vehicle_maintenance_attribution_reconciliation_required");
        }
    }

    private static ConsumedMaintenanceEquivalence MaintenanceEquivalenceProof(VehicleServiceRecord previous,
        VehicleServiceEligibilitySnapshot prior, ApprovalDecision decision, IReadOnlyList<ConsumedMaintenanceMember> members) =>
        new(previous.Id, previous.EligibilityCalculationPrivateReference, decision.Id,
            decision.ProtectedChangeReference!, AdjustmentHash(new { prior, previous.RequestedByUserId, previous.CreatedAt,
                ApprovalVersion = Convert.ToBase64String(previous.ApprovalRequest!.RowVersion),
                Decision = ReconciliationDecisionSource(decision) }), members);

    private async Task<ApprovalDecision> RequireServiceApprovalEvidence(VehicleServiceRecord record, CancellationToken ct)
    {
        var header = record.ApprovalRequest;
        var decision = header is null ? null : await repository.FindVehicleAdjustmentDecisionAsync(header.Id, ct);
        if (header is null || header.Id != record.ApprovalRequestId || header.SubjectId != record.Id ||
            header.RequestType != ServiceRequestType || header.RequesterUserId != record.RequestedByUserId ||
            header.RequestedAt != record.CreatedAt ||
            header.State != ApprovalState.Approved || decision?.Decision != ApprovalState.Approved ||
            decision.ApprovalRequestId != header.Id || decision.ProtectedChangeReference != record.EligibilityCalculationPrivateReference ||
            decision.DecidedAt != header.ClosedAt || decision.ActorUserId == header.RequesterUserId ||
            string.IsNullOrWhiteSpace(decision.ActorRoleCode))
            throw AttendanceOperation.Conflict("vehicle_service_cycle_evidence_unavailable");
        return decision;
    }

    private async Task RequireConsumedMaintenanceIntegrity(VehicleServiceRecord record, CancellationToken ct,
        bool historicalReplay = false)
    {
        var retained = await ReadServiceEligibilityAsync(record, ct);
        if (retained.ConsumedCorrectionEquivalence is null) return;
        if (record.ApprovalRequest is not { } header || header.Id != record.ApprovalRequestId ||
            header.SubjectId != record.Id || header.RequestType != ServiceRequestType ||
            header.RequesterUserId != record.RequestedByUserId ||
            header.State is not (ApprovalState.Pending or ApprovalState.Approved))
            throw AttendanceOperation.Conflict("vehicle_service_cycle_evidence_unavailable");
        if (header.State == ApprovalState.Approved) await RequireServiceApprovalEvidence(record, ct);
        await RequireServiceCustodyAsync(record.VehicleId, record.EmployeeId, record.ServiceDate, record.OdometerKm, ct);
        var vehicle = await repository.FindVehicleAsync(record.VehicleId, ct)
            ?? throw AttendanceOperation.Missing("vehicle_not_found");
        if (historicalReplay && header.State == ApprovalState.Approved)
        {
            var latest = await repository.FindLatestApprovedServiceAsync(record.VehicleId, record.ServiceKind, ct);
            if (latest is not null && latest.Id != record.Id)
            {
                if (latest.VehicleId != record.VehicleId || latest.ServiceKind != record.ServiceKind ||
                    retained.VehicleType != vehicle.VehicleType)
                    throw AttendanceOperation.Conflict("vehicle_service_eligibility_changed");
                // A valid descendant must not invalidate an old receipt. Prove the entire current
                // chain, including this target, without calculating another consumed milestone.
                await ProveConsumedMaintenanceChain(latest, await ReadServiceEligibilityAsync(latest, ct),
                    latest.ServiceDate, null, ct, requiredServiceId: record.Id);
                return;
            }
        }
        var current = await ServiceEligibilityAsync(record.EmployeeId, new(record.VehicleId, record.ServiceKind,
            record.ServiceDate, record.OdometerKm, retained.ClaimedOutstationKm, record.AuthorizedWorkshopReference,
            record.EvidencePrivateReference, record.ClaimedAmount, record.Currency), vehicle.VehicleType, ct,
            record.PolicyVersionId, record.Id, retained.PreviousServiceRecordId);
        if (JsonSerializer.Serialize(retained) != JsonSerializer.Serialize(current))
            throw AttendanceOperation.Conflict("vehicle_service_eligibility_changed");
    }

    private async Task RequireServiceReviewIntegrity(Guid id, AttendanceActor actor, CancellationToken ct,
        bool historicalReplay = false)
    {
        var record = await repository.FindServiceRecordAsync(id, ct)
            ?? throw AttendanceOperation.Missing("service_record_not_found");
        RequireIndependentReviewer(record.EmployeeId, record.RequestedByUserId, actor);
        await RequireConsumedMaintenanceIntegrity(record, ct, historicalReplay);
        await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Maintenance, actor, ct);
    }

    private async Task RequireServiceSubmissionReplay(Guid vehicleId, string key, AttendanceActor actor, CancellationToken ct)
    {
        const string operationName = "vehicle.service.submit";
        var receipt = await journal.FindReceiptAsync(actor.Identity.AccountId.ToString(), operationName, key, ct);
        if (receipt?.ProtectedResultReference is null) return;
        var result = await operation.ReadPrivateAsync<ServiceSubmissionReceipt>(receipt.ProtectedResultReference,
            $"attendance:operation:{actor.Identity.AccountId:N}:{operationName}:result", ct);
        var record = await repository.FindServiceRecordAsync(result.ResourceId, ct)
            ?? throw AttendanceOperation.Conflict("idempotency_result_unavailable");
        if (record.VehicleId != vehicleId) throw AttendanceOperation.Conflict("idempotency_key_reused");
        if (record.EmployeeId != actor.Identity.EmployeeId || record.RequestedByUserId != actor.Identity.AccountId.ToString())
            throw AttendanceOperation.Denied();
        await RequireConsumedMaintenanceIntegrity(record, ct, historicalReplay: true);
    }

    private sealed record ServiceSubmissionReceipt(Guid ResourceId);
    private sealed record ValidatedMaintenancePredecessor(VehicleServiceRecord Record, ConsumedMaintenanceEquivalence Equivalence);
    private sealed record ConsumedMaintenanceEquivalence(Guid ServiceRecordId, string CalculationReference,
        Guid DecisionId, string DecisionReference, string OriginalServiceFingerprint, IReadOnlyList<ConsumedMaintenanceMember> Members);
    private sealed record ConsumedMaintenanceMember(Guid MileageEntryId, Guid TripId, string CalculationReference,
        string OriginalSourceFingerprint, decimal OriginalThroughServiceKm, decimal CorrectedThroughServiceKm,
        Guid? CorrectionRequestId, string? CorrectionReference, Guid? CorrectionDecisionId,
        string? CorrectionDecisionReference, string? CorrectionFingerprint);
}
