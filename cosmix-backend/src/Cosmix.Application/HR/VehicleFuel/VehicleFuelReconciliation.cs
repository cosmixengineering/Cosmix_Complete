using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.HR.VehicleFuel;

public sealed partial class VehicleFuelService
{
    public const string FuelReconciliationRequestType = "vehicle.fuel.reconciliation";
    private static string FuelReconciliationPurpose(Guid id) => $"vehicle:fuel:reconciliation:{id:N}";

    public Task<FuelReconciliationContext> FuelReconciliationContextAsync(Guid claimId,
        AuthRequestContext context, CancellationToken ct) => operation.ReadAsync(VehicleFuelPermissions.Fuel,
        "vehicle.fuel.reconciliation.context", null, context, async (actor, token) =>
        {
            var source = await FuelReconciliationSource(claimId, token);
            var rows = await FuelReconciliationRows(claimId, token);
            await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Fuel, actor, token);
            return source with { LatestRequestId = rows.LastOrDefault()?.Header.Id };
        }, ct);

    public Task<FuelReconciliationView> SubmitFuelReconciliationAsync(Guid claimId,
        SubmitFuelReconciliationRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        request is null ? throw AttendanceOperation.Invalid("vehicle_reconciliation_request_required") :
        operation.WriteAsync(VehicleFuelPermissions.Fuel, "vehicle.fuel.reconciliation.submit", null, key,
            new { claimId, request }, request.Reason, context, async (actor, token) =>
            {
                AdjustmentEvidence(request.EvidenceReference);
                var source = await FuelReconciliationSource(claimId, token);
                var rows = await FuelReconciliationRows(claimId, token);
                if (rows.Any(r => r.Header.State is ApprovalState.Pending or ApprovalState.Approved))
                    throw AttendanceOperation.Conflict("vehicle_fuel_reconciliation_already_recorded");
                if (rows.LastOrDefault()?.Header.Id != request.ExpectedLatestRequestId ||
                    source.SourceFingerprint != request.ExpectedSourceFingerprint)
                    throw AttendanceOperation.Conflict("vehicle_reconciliation_sources_changed");
                var snapshot = new FuelReconciliationSnapshot(1, NextAdjustmentRevision(rows.Select(r => r.Header)),
                    source with { LatestRequestId = request.ExpectedLatestRequestId }, request.EvidenceReference,
                    request.Reason, actor.Identity.AccountId, actor.Now);
                var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot),
                    FuelReconciliationPurpose(claimId), token));
                await ValidateFuelReconciliationSource(snapshot, token);
                await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Fuel, actor, token);
                var header = new ApprovalRequest(FuelReconciliationRequestType, claimId, actor.Identity.AccountId.ToString(),
                    reference, snapshot.Revision, actor.Now);
                repository.AddVehicleAdjustmentRequest(header);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<FuelReconciliationView>(header.Id, null, FuelReconciliationViewOf(header, snapshot, null));
            }, ct, beforeReplay: (actor, token) => RequireFuelReconciliationSubmissionReplay(claimId, key, actor, token),
            beforeCommit: async (actor, token) =>
            {
                var rows = await FuelReconciliationRows(claimId, token);
                var latest = rows.LastOrDefault() ?? throw AttendanceOperation.Conflict("vehicle_reconciliation_sources_changed");
                if (latest.Header.State != ApprovalState.Pending ||
                    latest.Header.RequesterUserId != actor.Identity.AccountId.ToString() ||
                    latest.Snapshot.Calculation.SourceFingerprint != request.ExpectedSourceFingerprint ||
                    latest.Snapshot.Calculation.LatestRequestId != request.ExpectedLatestRequestId)
                    throw AttendanceOperation.Conflict("vehicle_reconciliation_sources_changed");
                await ValidateFuelReconciliationSource(latest.Snapshot, token);
                await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Fuel, actor, token);
            });

    public Task<FuelReconciliationView> ReviewFuelReconciliationAsync(Guid claimId, Guid requestId,
        ReviewVehicleAdjustmentRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        request is null ? throw AttendanceOperation.Invalid("vehicle_reconciliation_request_required") :
        operation.WriteAsync(VehicleFuelPermissions.Fuel, "vehicle.fuel.reconciliation.review", null, key,
            new { claimId, requestId, request }, request.Reason, context, async (actor, token) =>
            {
                var row = await RequireFuelReconciliationReviewer(claimId, requestId, actor, token);
                // History projections are untracked; mutate only the repository's tracked aggregate.
                var header = await AdjustmentRequest(claimId, requestId, FuelReconciliationRequestType, token);
                ExpectAdjustment(header, request);
                if ((await FuelReconciliationRows(claimId, token)).LastOrDefault()?.Header.Id != requestId)
                    throw AttendanceOperation.Conflict("vehicle_reconciliation_sources_changed");
                var before = FuelReconciliationViewOf(row.Header, row.Snapshot, row.Decision);
                var decision = await DecideAdjustment(header, request, row.Snapshot, VehicleFuelPermissions.Fuel, actor, token);
                await ValidateFuelReconciliationSource(row.Snapshot, token);
                await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Fuel, actor, token);
                await repository.SaveChangesAsync(token);
                await RequireFuelReconciliationReviewer(claimId, requestId, actor, token);
                return new AttendanceChange<FuelReconciliationView>(requestId, before,
                    FuelReconciliationViewOf(header, row.Snapshot, decision));
            }, ct, beforeReplay: async (actor, token) => { await RequireFuelReconciliationReviewer(claimId, requestId, actor, token); },
            beforeCommit: async (actor, token) => { await RequireFuelReconciliationReviewer(claimId, requestId, actor, token); });

    public Task<IReadOnlyList<FuelReconciliationView>> FuelReconciliationHistoryAsync(Guid claimId,
        AuthRequestContext context, CancellationToken ct) => operation.ReadAsync<IReadOnlyList<FuelReconciliationView>>(
        VehicleFuelPermissions.Fuel, "vehicle.fuel.reconciliation.history", null, context, async (actor, token) =>
        {
            if (await repository.FindFuelClaimAsync(claimId, token) is null) throw AttendanceOperation.Missing("fuel_claim_not_found");
            var rows = await FuelReconciliationRows(claimId, token);
            await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Fuel, actor, token);
            return rows.Select(r => FuelReconciliationViewOf(r.Header, r.Snapshot, r.Decision)).ToArray();
        }, ct);

    private async Task<FuelReconciliationContext> FuelReconciliationSource(Guid claimId, CancellationToken ct)
    {
        var claim = await repository.FindFuelClaimAsync(claimId, ct) ?? throw AttendanceOperation.Missing("fuel_claim_not_found");
        if (claim.ApprovalRequest?.State != ApprovalState.Approved)
            throw AttendanceOperation.Conflict("vehicle_fuel_reconciliation_approved_claim_required");
        // Only the retained legacy amount is compared; it is never treated as an actual payment.
        await ValidateLegacyFuelClaim(claim, 1, ct);
        var header = claim.ApprovalRequest;
        var decision = await repository.FindVehicleAdjustmentDecisionAsync(header.Id, ct)
            ?? throw AttendanceOperation.Conflict("vehicle_legacy_fuel_evidence_invalid");
        var trip = await AttributionTrip(claim.VehicleTripId, ct);
        var original = await OriginalCorrectionSource(trip, ct);
        var corrected = await CalculateCorrectedFuel(trip, original.Mileage, claim.Currency, ct)
            ?? throw AttendanceOperation.Conflict("vehicle_approved_correction_required");
        var retained = await operation.ReadPrivateAsync<LegacyFuelCalculation>(claim.CalculationPrivateReference,
            $"vehicle:fuel:{trip.Id:N}", ct);
        if (claim.ApprovalRequest != header || header.State != ApprovalState.Approved ||
            retained.ClaimedAmount != claim.ClaimedAmount || retained.FuelLiters != claim.FuelLiters ||
            retained.EligibleDistanceKm != claim.EligibleDistanceKm || retained.ExcludedCommuteKm != claim.ExcludedCommuteKm ||
            retained.Currency != claim.Currency || retained.PolicyVersionId != claim.PolicyVersionId)
            throw AttendanceOperation.Conflict("vehicle_reconciliation_sources_changed");
        var fingerprint = AdjustmentHash(new {
            claim.Id, claim.EmployeeId, claim.VehicleTripId, claim.EmployeeCommuteDistanceId, claim.PolicyVersionId,
            claim.EligibleDistanceKm, claim.ExcludedCommuteKm, claim.FuelLiters, claim.ClaimedAmount, claim.Currency,
            claim.CalculationPrivateReference, claim.ReceiptPrivateReference, claim.RequestedByUserId, claim.CreatedAt, claim.SubmittedAt,
            ApprovalRequestId = header.Id, Version = Convert.ToBase64String(header.RowVersion), header.RequestedAt, header.SnapshotPrivateReference,
            Original = original.Fingerprint, retained, Decision = ReconciliationDecisionSource(decision), corrected
        });
        return new(claim.Id, claim.EmployeeId, trip.Id, decision.Id, corrected.CorrectionRequestId, null, fingerprint,
            retained.ClaimedAmount, corrected.Calculation.ClaimedAmount, corrected.Calculation.ClaimedAmount - retained.ClaimedAmount,
            claim.Currency, corrected.Calculation.Lines.Select(l => new FuelReconciliationLine(l.BusinessDate, l.PolicyVersionId,
                l.EligibleKm, l.ExcludedCommuteKm, l.KmPerLiter, l.PricePerLiter, l.FuelLiters, l.ClaimedAmount)).ToArray());
    }

    private async Task ValidateFuelReconciliationSource(FuelReconciliationSnapshot retained, CancellationToken ct)
    {
        var current = await FuelReconciliationSource(retained.Calculation.ResourceId, ct);
        if (AdjustmentHash(current) != AdjustmentHash(retained.Calculation with { LatestRequestId = null }))
            throw AttendanceOperation.Conflict("vehicle_reconciliation_sources_changed");
    }

    private async Task<IReadOnlyList<FuelReconciliationRow>> FuelReconciliationRows(Guid claimId, CancellationToken ct)
    {
        var headers = await repository.ListVehicleAdjustmentRequestsAsync(claimId, FuelReconciliationRequestType, ct);
        ValidateAdjustmentHeaders(headers, claimId, FuelReconciliationRequestType);
        var ordered = headers.OrderBy(h => h.Stage).ToArray();
        if (ordered.Take(Math.Max(0, ordered.Length - 1)).Any(h => h.State is ApprovalState.Pending or ApprovalState.Approved))
            throw AttendanceOperation.Conflict("vehicle_fuel_reconciliation_history_invalid");
        var rows = new List<FuelReconciliationRow>();
        foreach (var header in ordered)
        {
            var snapshot = await operation.ReadPrivateAsync<FuelReconciliationSnapshot>(header.SnapshotPrivateReference,
                FuelReconciliationPurpose(claimId), ct);
            var calculation = snapshot.Calculation;
            if (snapshot.SchemaVersion != 1 || snapshot.Revision != header.Stage || calculation is null ||
                calculation.ResourceId != claimId || calculation.EmployeeId == Guid.Empty || calculation.TripId == Guid.Empty ||
                calculation.OriginalDecisionId == Guid.Empty || calculation.CorrectionRequestId == Guid.Empty ||
                calculation.SourceFingerprint is not { Length: 64 } || calculation.Lines is not { Count: > 0 and <= 366 } ||
                calculation.Lines.Any(l => l is null || l.PolicyVersionId == Guid.Empty || l.KmPerLiter <= 0 || l.PricePerLiter <= 0) ||
                calculation.CorrectedAmount != calculation.Lines.Sum(l => l.Amount) ||
                calculation.DifferenceAmount != calculation.CorrectedAmount - calculation.OriginalAmount ||
                calculation.LatestRequestId != rows.LastOrDefault()?.Header.Id ||
                snapshot.RequestedByAccountId.ToString() != header.RequesterUserId || snapshot.RequestedAt != header.RequestedAt ||
                !VehicleAdjustmentPolicy.Evidence(snapshot.EvidenceReference))
                throw AttendanceOperation.Conflict("vehicle_fuel_reconciliation_evidence_invalid");
            var decision = await AdjustmentDecision(header, snapshot, ct);
            if (decision is not null && (decision.ActorUserId == header.RequesterUserId ||
                decision.ActorUserId == snapshot.RequestedByAccountId.ToString()))
                throw AttendanceOperation.Conflict("vehicle_fuel_reconciliation_evidence_invalid");
            rows.Add(new(header, snapshot, decision));
        }
        return rows;
    }

    private async Task<FuelReconciliationRow> RequireFuelReconciliationReviewer(Guid claimId, Guid requestId,
        AttendanceActor actor, CancellationToken ct)
    {
        var row = (await FuelReconciliationRows(claimId, ct)).SingleOrDefault(r => r.Header.Id == requestId)
            ?? throw AttendanceOperation.Missing("vehicle_adjustment_not_found");
        var claim = await repository.FindFuelClaimAsync(claimId, ct) ?? throw AttendanceOperation.Missing("fuel_claim_not_found");
        RequireIndependentReviewer(claim.EmployeeId, claim.RequestedByUserId, actor);
        RequireIndependentReviewer(row.Snapshot.Calculation.EmployeeId, row.Header.RequesterUserId, actor);
        await ValidateFuelReconciliationSource(row.Snapshot, ct);
        await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Fuel, actor, ct);
        return row;
    }

    private async Task RequireFuelReconciliationSubmissionReplay(Guid claimId, string key, AttendanceActor actor, CancellationToken ct)
    {
        const string name = "vehicle.fuel.reconciliation.submit";
        var receipt = await journal.FindReceiptAsync(actor.Identity.AccountId.ToString(), name, key, ct);
        if (receipt?.ProtectedResultReference is not null)
        {
            var result = await operation.ReadPrivateAsync<FuelReconciliationReceipt>(receipt.ProtectedResultReference,
                $"attendance:operation:{actor.Identity.AccountId:N}:{name}:result", ct);
            var row = (await FuelReconciliationRows(claimId, ct)).SingleOrDefault(r => r.Header.Id == result.ResourceId)
                ?? throw AttendanceOperation.Conflict("idempotency_result_unavailable");
            if (row.Header.RequesterUserId != actor.Identity.AccountId.ToString()) throw AttendanceOperation.Denied();
            await ValidateFuelReconciliationSource(row.Snapshot, ct);
        }
        await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Fuel, actor, ct);
    }

    private static FuelReconciliationView FuelReconciliationViewOf(ApprovalRequest header, FuelReconciliationSnapshot snapshot,
        ApprovalDecision? decision) => new(header.Id, header.Stage, Convert.ToBase64String(header.RowVersion), header.State.ToString(),
            snapshot.Calculation, snapshot.EvidenceReference, snapshot.Reason, snapshot.RequestedByAccountId, snapshot.RequestedAt,
            decision?.Id, decision?.Reason, "NotRecorded");
    private sealed record FuelReconciliationSnapshot(int SchemaVersion, int Revision, FuelReconciliationContext Calculation,
        string EvidenceReference, string Reason, Guid RequestedByAccountId, DateTimeOffset RequestedAt);
    private sealed record FuelReconciliationRow(ApprovalRequest Header, FuelReconciliationSnapshot Snapshot, ApprovalDecision? Decision);
    private sealed record FuelReconciliationReceipt(Guid ResourceId);
}
