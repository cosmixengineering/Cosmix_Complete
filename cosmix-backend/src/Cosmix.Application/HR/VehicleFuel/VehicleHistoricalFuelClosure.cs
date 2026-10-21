using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.VehicleFuel;

namespace Cosmix.Application.HR.VehicleFuel;

public sealed partial class VehicleFuelService
{
    public Task<HistoricalFuelClosureContext> HistoricalFuelClosureContextAsync(Guid claimId,
        AuthRequestContext context, CancellationToken ct) => operation.ReadAsync(VehicleFuelPermissions.Fuel,
        "vehicle.fuel.closure.context", null, context, async (actor, token) =>
        {
            var (claim, source) = await HistoricalFuelSource(claimId, token);
            RequireIndependentReviewer(claim.EmployeeId, claim.RequestedByUserId, actor);
            await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Fuel, actor, token);
            return source;
        }, ct);

    public Task<HistoricalFuelClosureView> CloseHistoricalFuelClaimAsync(Guid claimId,
        CloseHistoricalFuelClaimRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        request is null ? throw AttendanceOperation.Invalid("vehicle_reconciliation_request_required") :
        operation.WriteAsync(VehicleFuelPermissions.Fuel, "vehicle.fuel.historical.close", null, key,
            new { claimId, request }, request.Reason, context, async (actor, token) =>
            {
                var (claim, source) = await HistoricalFuelSource(claimId, token);
                RequireIndependentReviewer(claim.EmployeeId, claim.RequestedByUserId, actor);
                if (source.SourceFingerprint != request.ExpectedSourceFingerprint)
                    throw AttendanceOperation.Conflict("vehicle_reconciliation_sources_changed");
                var review = new ReviewVehicleAdjustmentRequest(request.ExpectedVersion, ApprovalState.Rejected,
                    request.EvidenceReference, request.Reason);
                ExpectAdjustment(claim.ApprovalRequest!, review);
                var decision = await DecideAdjustment(claim.ApprovalRequest!, review, source.SourceFingerprint,
                    VehicleFuelPermissions.Fuel, actor, token);
                var currentSource = await HistoricalFuelSource(claimId, token);
                if (currentSource.Context.SourceFingerprint != request.ExpectedSourceFingerprint)
                    throw AttendanceOperation.Conflict("vehicle_reconciliation_sources_changed");
                await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Fuel, actor, token);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<HistoricalFuelClosureView>(claim.Id, source,
                    new(claim.Id, claim.ApprovalRequest!.Id, Convert.ToBase64String(claim.ApprovalRequest.RowVersion),
                        claim.ApprovalRequest.State.ToString(), source.SourceFingerprint, decision.Id));
            }, ct, beforeReplay: (actor, token) => RequireHistoricalFuelClosure(claimId, request, key, actor, token),
            beforeCommit: (actor, token) => RequireHistoricalFuelClosure(claimId, request, key, actor, token, requireClosed: true));

    private async Task RequireHistoricalFuelClosure(Guid claimId, CloseHistoricalFuelClaimRequest request, string key,
        AttendanceActor actor, CancellationToken ct, bool requireClosed = false)
    {
        var (claim, source) = await HistoricalFuelSource(claimId, ct);
        RequireIndependentReviewer(claim.EmployeeId, claim.RequestedByUserId, actor);
        var receipt = await journal.FindReceiptAsync(actor.Identity.AccountId.ToString(), "vehicle.fuel.historical.close", key, ct);
        if ((requireClosed || receipt is not null) && claim.ApprovalRequest!.State != ApprovalState.Rejected ||
            source.SourceFingerprint != request.ExpectedSourceFingerprint)
            throw AttendanceOperation.Conflict("vehicle_reconciliation_sources_changed");
        await AdjustmentDecision(claim.ApprovalRequest!, source.SourceFingerprint, ct);
        await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Fuel, actor, ct);
    }

    private async Task<(FuelClaim Claim, HistoricalFuelClosureContext Context)> HistoricalFuelSource(Guid claimId, CancellationToken ct)
    {
        var claim = await repository.FindFuelClaimAsync(claimId, ct) ?? throw AttendanceOperation.Missing("fuel_claim_not_found");
        if (claim.ApprovalRequest is not { } header || header.Id != claim.ApprovalRequestId || header.SubjectId != claim.Id ||
            header.RequestType != FuelRequestType || header.RequesterUserId != claim.RequestedByUserId ||
            header.State is not (ApprovalState.Pending or ApprovalState.Rejected))
            throw AttendanceOperation.Conflict("vehicle_historical_fuel_not_closable");
        var trip = await AttributionTrip(claim.VehicleTripId, ct);
        var correction = (await CorrectionRows(trip.Id, ct)).SingleOrDefault(r => r.Header.State == ApprovalState.Approved)
            ?? throw AttendanceOperation.Conflict("vehicle_approved_correction_required");
        await ValidateCorrectionSources(trip, correction.Snapshot, ct);
        var original = await OriginalCorrectionSource(trip, ct);
        var retained = await operation.ReadPrivateAsync<LegacyFuelCalculation>(claim.CalculationPrivateReference, $"vehicle:fuel:{trip.Id:N}", ct);
        if (retained.SchemaVersion != 1 || retained.TripId != trip.Id || retained.MileageEntryId != original.Mileage.Id ||
            retained.CalculationPrivateReference != original.Mileage.CalculationPrivateReference || claim.EmployeeId != trip.EmployeeId ||
            retained.PolicyVersionId != claim.PolicyVersionId || claim.PolicyVersionId != original.Mileage.PolicyVersionId ||
            retained.EligibleDistanceKm != claim.EligibleDistanceKm || claim.EligibleDistanceKm != original.Mileage.OutstationKm ||
            retained.ExcludedCommuteKm != claim.ExcludedCommuteKm || claim.ExcludedCommuteKm != original.Mileage.ExcludedCommuteKm ||
            retained.FuelLiters != claim.FuelLiters || retained.ClaimedAmount != claim.ClaimedAmount || retained.Currency != claim.Currency ||
            retained.KmPerLiter <= 0 || retained.FuelPricePerLiter <= 0 ||
            Math.Round(retained.EligibleDistanceKm / retained.KmPerLiter, 2) != retained.FuelLiters ||
            Math.Round(retained.FuelLiters * retained.FuelPricePerLiter, 2) != retained.ClaimedAmount)
            throw AttendanceOperation.Conflict("vehicle_historical_fuel_evidence_invalid");
        var fingerprint = AdjustmentHash(new { claim.Id, claim.EmployeeId, claim.VehicleTripId, claim.EmployeeCommuteDistanceId,
            claim.PolicyVersionId, claim.CalculationPrivateReference, claim.ReceiptPrivateReference, claim.RequestedByUserId,
            claim.CreatedAt, claim.SubmittedAt, ApprovalRequestId = header.Id, header.RequestedAt, header.SnapshotPrivateReference,
            Original = original.Fingerprint, retained, CorrectionId = correction.Header.Id, correction.Snapshot,
            Decision = ReconciliationDecisionSource(correction.Decision!) });
        return (claim, new(claim.Id, Convert.ToBase64String(header.RowVersion), fingerprint, [correction.Header.Id]));
    }

    private sealed record LegacyFuelCalculation(int SchemaVersion, Guid TripId, Guid MileageEntryId, string CalculationPrivateReference,
        Guid PolicyVersionId, decimal EligibleDistanceKm, decimal ExcludedCommuteKm, decimal KmPerLiter,
        decimal FuelPricePerLiter, decimal FuelLiters, decimal ClaimedAmount, string Currency);

    private static object ReconciliationDecisionSource(ApprovalDecision decision) => new
    {
        decision.Id, decision.ApprovalRequestId, decision.Decision, decision.ActorUserId, decision.ActorRoleCode,
        decision.Reason, decision.DecidedAt, decision.ProtectedChangeReference
    };
}
