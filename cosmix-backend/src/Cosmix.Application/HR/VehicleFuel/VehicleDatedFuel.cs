using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.HR.VehicleFuel;

namespace Cosmix.Application.HR.VehicleFuel;

public sealed partial class VehicleFuelService
{
    private async Task<SegmentFuelEvidence> CalculateDatedFuel(VehicleTrip trip, VehicleMileageEntry mileage,
        IReadOnlyList<VehicleTripSegment> segments, string currency, CancellationToken ct)
    {
        var lines = new List<SegmentFuelLine>();
        foreach (var segment in segments)
        {
            var (version, policy) = await LoadPolicyAsync(segment.Source.BusinessDate, ct, segment.Source.PolicyVersionId);
            var workflow = RequireWorkflow(policy);
            workflow.RequireStaff(trip.EmployeeId, segment.Source.BusinessDate, segment.Source.BusinessDate);
            workflow.RequireFuelCurrency(currency);
            if (policy.KmPerLiter is null || policy.FuelPricePerLiter is null || segment.Rule.FuelRounding != "PerSegmentTwoDecimalsToEven")
                throw AttendanceOperation.Conflict("vehicle_fuel_policy_not_configured");
            var liters = Math.Round(segment.EligibleKm / policy.KmPerLiter.Value, 2, MidpointRounding.ToEven);
            var amount = Math.Round(liters * policy.FuelPricePerLiter.Value, 2, MidpointRounding.ToEven);
            lines.Add(new(segment.Source.BusinessDate, version.Id, segment.EligibleKm, segment.ExcludedCommuteKm,
                policy.KmPerLiter.Value, policy.FuelPricePerLiter.Value, liters, amount, segment.EvidenceFingerprint));
        }
        return new(2, trip.Id, mileage.Id, mileage.CalculationPrivateReference, lines, lines.Sum(l => l.FuelLiters),
            lines.Sum(l => l.ClaimedAmount), currency.ToUpperInvariant());
    }
    private sealed record SegmentFuelLine(DateOnly BusinessDate, Guid PolicyVersionId, decimal EligibleKm, decimal ExcludedCommuteKm,
        decimal KmPerLiter, decimal PricePerLiter, decimal FuelLiters, decimal ClaimedAmount, string EvidenceFingerprint);
    private sealed record SegmentFuelEvidence(int SchemaVersion, Guid TripId, Guid MileageEntryId, string CalculationPrivateReference,
        IReadOnlyList<SegmentFuelLine> Lines, decimal FuelLiters, decimal ClaimedAmount, string Currency);
}
