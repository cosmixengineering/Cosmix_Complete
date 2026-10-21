using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.VehicleFuel;

namespace Cosmix.Application.HR.VehicleFuel;

public sealed partial class VehicleFuelService
{
    private async Task<CorrectedFuelEvidence?> CalculateCorrectedFuel(VehicleTrip trip, VehicleMileageEntry mileage,
        string currency, CancellationToken ct)
    {
        var correction = (await CorrectionRows(trip.Id, ct)).SingleOrDefault(r => r.Header.State == ApprovalState.Approved);
        if (correction is null) return null;
        var segments = await ApprovedCorrectionSegments(mileage, trip, ct)
            ?? throw AttendanceOperation.Conflict("vehicle_correction_sources_changed");
        var calculation = await CalculateDatedFuel(trip, mileage, segments, currency, ct);
        var fingerprint = AdjustmentHash(new
        {
            correction.Header.Id, Version = Convert.ToBase64String(correction.Header.RowVersion),
            correction.Header.SnapshotPrivateReference, correction.Snapshot,
            Decision = new { correction.Decision!.Id, correction.Decision.ActorUserId, correction.Decision.ActorRoleCode,
                correction.Decision.DecidedAt, correction.Decision.ProtectedChangeReference }
        });
        return new(3, correction.Header.Id, fingerprint, calculation);
    }

    private async Task<bool> ValidateCorrectedFuelClaim(FuelClaim claim, CancellationToken ct, bool requireUncorrectedLegacy)
    {
        var purpose = $"vehicle:fuel:{claim.VehicleTripId:N}";
        var evidence = await operation.ReadPrivateAsync<JsonElement>(claim.CalculationPrivateReference, purpose, ct);
        if (evidence.ValueKind != JsonValueKind.Object || !evidence.TryGetProperty("SchemaVersion", out var schema) ||
            schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version))
            throw AttendanceOperation.Conflict("vehicle_corrected_fuel_evidence_invalid");
        if (version != 3)
        {
            if (version is not (1 or 2)) throw AttendanceOperation.Conflict("vehicle_corrected_fuel_evidence_invalid");
            await ValidateLegacyFuelClaim(claim, version, ct);
            if (requireUncorrectedLegacy) await RequireUncorrectedFuelSource(claim.VehicleTripId, ct);
            return false;
        }

        CorrectedFuelEvidence? retained;
        try { retained = evidence.Deserialize<CorrectedFuelEvidence>(); }
        catch (JsonException) { throw AttendanceOperation.Conflict("vehicle_corrected_fuel_evidence_invalid"); }
        if (retained is null || retained.CorrectionRequestId == Guid.Empty || retained.CorrectionFingerprint is not { Length: 64 } ||
            retained.Calculation is null || retained.Calculation.Lines is not { Count: > 0 })
            throw AttendanceOperation.Conflict("vehicle_corrected_fuel_evidence_invalid");
        var trip = await AttributionTrip(claim.VehicleTripId, ct);
        var mileage = await repository.FindApprovedMileageForTripAsync(trip.Id, ct)
            ?? throw AttendanceOperation.Conflict("approved_trip_mileage_unavailable");
        var currentEvidence = await CalculateCorrectedFuel(trip, mileage, claim.Currency, ct)
            ?? throw AttendanceOperation.Conflict("vehicle_correction_sources_changed");
        var calculation = currentEvidence.Calculation;
        if (AdjustmentHash(retained) != AdjustmentHash(currentEvidence) || claim.EmployeeId != trip.EmployeeId ||
            claim.PolicyVersionId != calculation.Lines[0].PolicyVersionId ||
            claim.EligibleDistanceKm != calculation.Lines.Sum(l => l.EligibleKm) ||
            claim.ExcludedCommuteKm != calculation.Lines.Sum(l => l.ExcludedCommuteKm) ||
            claim.FuelLiters != calculation.FuelLiters || claim.ClaimedAmount != calculation.ClaimedAmount ||
            claim.Currency != calculation.Currency || claim.ApprovalRequest is not { } approval ||
            approval.Id != claim.ApprovalRequestId || approval.SubjectId != claim.Id || approval.RequestType != FuelRequestType ||
            approval.RequesterUserId != claim.RequestedByUserId)
            throw AttendanceOperation.Conflict("vehicle_corrected_fuel_sources_changed");
        if (approval.State == ApprovalState.Approved)
        {
            var decision = await repository.FindVehicleAdjustmentDecisionAsync(approval.Id, ct);
            if (decision?.Decision != ApprovalState.Approved || decision.ApprovalRequestId != approval.Id ||
                decision.ProtectedChangeReference != claim.CalculationPrivateReference || decision.DecidedAt != approval.ClosedAt)
                throw AttendanceOperation.Conflict("vehicle_corrected_fuel_evidence_invalid");
        }
        return true;
    }

    private async Task RequireFuelReviewIntegrity(Guid claimId, AttendanceActor actor, CancellationToken ct, bool historicalReplay)
    {
        var claim = await repository.FindFuelClaimAsync(claimId, ct) ?? throw AttendanceOperation.Missing("fuel_claim_not_found");
        RequireIndependentReviewer(claim.EmployeeId, claim.RequestedByUserId, actor);
        await ValidateCorrectedFuelClaim(claim, ct, requireUncorrectedLegacy: !historicalReplay);
        await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Fuel, actor, ct);
    }

    private async Task RequireFuelSubmissionReplay(Guid tripId, string key, AttendanceActor actor, CancellationToken ct)
    {
        const string operationName = "vehicle.fuel.claim.submit";
        var receipt = await journal.FindReceiptAsync(actor.Identity.AccountId.ToString(), operationName, key, ct);
        if (receipt?.ProtectedResultReference is null) return;
        // Read only the retained resource identity; AttendanceOperation still owns digest, expiry and scope validation.
        var result = await operation.ReadPrivateAsync<FuelSubmissionReceipt>(receipt.ProtectedResultReference,
            $"attendance:operation:{actor.Identity.AccountId:N}:{operationName}:result", ct);
        var claim = await repository.FindFuelClaimAsync(result.ResourceId, ct)
            ?? throw AttendanceOperation.Conflict("idempotency_result_unavailable");
        if (claim.VehicleTripId != tripId) throw AttendanceOperation.Conflict("idempotency_key_reused");
        if (claim.EmployeeId != actor.Identity.EmployeeId || claim.RequestedByUserId != actor.Identity.AccountId.ToString())
            throw AttendanceOperation.Denied();
        await ValidateCorrectedFuelClaim(claim, ct, requireUncorrectedLegacy: false);
    }

    private sealed record CorrectedFuelEvidence(int SchemaVersion, Guid CorrectionRequestId, string CorrectionFingerprint,
        SegmentFuelEvidence Calculation);
    private sealed record FuelSubmissionReceipt(Guid ResourceId);
}
