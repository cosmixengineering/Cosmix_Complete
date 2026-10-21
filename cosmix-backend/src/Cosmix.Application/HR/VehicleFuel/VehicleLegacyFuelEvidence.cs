using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.VehicleFuel;

namespace Cosmix.Application.HR.VehicleFuel;

public sealed partial class VehicleFuelService
{
    private async Task ValidateLegacyFuelClaim(FuelClaim claim, int schema, CancellationToken ct)
    {
        var trip = await repository.FindTripAsync(claim.VehicleTripId, ct)
            ?? throw AttendanceOperation.Conflict("approved_trip_mileage_unavailable");
        var mileage = await repository.FindApprovedMileageForTripAsync(trip.Id, ct)
            ?? throw AttendanceOperation.Conflict("approved_trip_mileage_unavailable");
        var header = claim.ApprovalRequest;
        if (trip.ApprovalRequest?.State != ApprovalState.Approved || trip.EmployeeId != claim.EmployeeId ||
            mileage.VehicleTripId != trip.Id || mileage.VehicleId != trip.VehicleId || mileage.EmployeeId != trip.EmployeeId ||
            mileage.ApprovalRequestId != trip.ApprovalRequestId || mileage.PolicyVersionId != claim.PolicyVersionId ||
            mileage.BusinessDate != RosterSchedule.Today(trip.StartedAt) ||
            mileage.OutstationKm != claim.EligibleDistanceKm || mileage.ExcludedCommuteKm != claim.ExcludedCommuteKm ||
            mileage.ApprovedBusinessKm != mileage.OutstationKm + mileage.ExcludedCommuteKm ||
            header is null || header.Id != claim.ApprovalRequestId || header.SubjectId != claim.Id ||
            header.RequestType != FuelRequestType || header.RequesterUserId != claim.RequestedByUserId)
            throw AttendanceOperation.Conflict("vehicle_legacy_fuel_sources_changed");

        var purpose = $"vehicle:fuel:{trip.Id:N}";
        if (schema == 1)
        {
            var retained = await operation.ReadPrivateAsync<LegacyFuelCalculation>(claim.CalculationPrivateReference, purpose, ct);
            var (_, policy) = await LoadPolicyAsync(mileage.BusinessDate, ct, claim.PolicyVersionId);
            RequireWorkflow(policy).RequireFuelCurrency(claim.Currency);
            if (retained.SchemaVersion != 1 || retained.TripId != trip.Id || retained.MileageEntryId != mileage.Id ||
                retained.CalculationPrivateReference != mileage.CalculationPrivateReference ||
                retained.PolicyVersionId != claim.PolicyVersionId || retained.EligibleDistanceKm != claim.EligibleDistanceKm ||
                retained.ExcludedCommuteKm != claim.ExcludedCommuteKm || retained.FuelLiters != claim.FuelLiters ||
                retained.ClaimedAmount != claim.ClaimedAmount || retained.Currency != claim.Currency ||
                retained.KmPerLiter <= 0 || retained.FuelPricePerLiter <= 0 ||
                retained.KmPerLiter != policy.KmPerLiter || retained.FuelPricePerLiter != policy.FuelPricePerLiter ||
                Math.Round(retained.EligibleDistanceKm / retained.KmPerLiter, 2) != retained.FuelLiters ||
                Math.Round(retained.FuelLiters * retained.FuelPricePerLiter, 2) != retained.ClaimedAmount)
                throw AttendanceOperation.Conflict("vehicle_legacy_fuel_evidence_invalid");
        }
        else
        {
            var segments = await ApprovedTripSegments(mileage, trip, ct)
                ?? throw AttendanceOperation.Conflict("vehicle_legacy_fuel_evidence_invalid");
            var current = await CalculateDatedFuel(trip, mileage, segments, claim.Currency, ct);
            var retained = await operation.ReadPrivateAsync<SegmentFuelEvidence>(claim.CalculationPrivateReference, purpose, ct);
            if (AdjustmentHash(current) != AdjustmentHash(retained) ||
                current.FuelLiters != claim.FuelLiters || current.ClaimedAmount != claim.ClaimedAmount ||
                current.Currency != claim.Currency)
                throw AttendanceOperation.Conflict("vehicle_legacy_fuel_evidence_invalid");
        }
        if (header.State == ApprovalState.Approved)
        {
            var decision = await repository.FindVehicleAdjustmentDecisionAsync(header.Id, ct);
            if (decision?.Decision != ApprovalState.Approved || decision.ApprovalRequestId != header.Id ||
                decision.DecidedAt != header.ClosedAt || decision.ActorUserId == header.RequesterUserId ||
                string.IsNullOrWhiteSpace(decision.ActorRoleCode) ||
                decision.ProtectedChangeReference is not null && decision.ProtectedChangeReference != claim.CalculationPrivateReference)
                throw AttendanceOperation.Conflict("vehicle_legacy_fuel_evidence_invalid");
        }
    }
}
