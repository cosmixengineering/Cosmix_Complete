using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.VehicleFuel;

namespace Cosmix.Application.HR.VehicleFuel;

public sealed partial class VehicleFuelService
{
    public const string TripAttributionCorrectionRequestType = "vehicle.trip.attribution-correction";
    private static string CorrectionPurpose(Guid id) => $"vehicle:trip:correction:{id:N}";

    public Task<VehicleTripAttributionCorrectionContext> TripAttributionCorrectionContextAsync(Guid tripId,
        AuthRequestContext context, CancellationToken ct) => operation.ReadAsync(VehicleFuelPermissions.Trips,
        "vehicle.trip.correction.context", null, context, async (actor, token) =>
        {
            var trip = await AttributionTrip(tripId, token);
            var original = await OriginalCorrectionSource(trip, token);
            var rows = await CorrectionRows(tripId, token);
            await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Trips, actor, token);
            return new VehicleTripAttributionCorrectionContext(tripId, trip.EmployeeId, trip.ApprovalRequest!.Id,
                Convert.ToBase64String(trip.ApprovalRequest.RowVersion), original.Mileage.Id, original.Fingerprint,
                rows.LastOrDefault()?.Header.Id);
        }, ct);

    public Task<VehicleTripAttributionCorrectionView> SubmitTripAttributionCorrectionAsync(Guid tripId,
        SubmitVehicleTripAttributionCorrectionRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        request is null ? throw AttendanceOperation.Invalid("vehicle_attribution_request_required") :
        operation.WriteAsync(VehicleFuelPermissions.Trips, "vehicle.trip.correction.submit", null, key, new { tripId, request },
            request.Reason, context, async (actor, token) =>
            {
                AdjustmentEvidence(request.EvidenceReference);
                var trip = await AttributionTrip(tripId, token);
                if (!trip.ApprovalRequest!.RowVersion.SequenceEqual(SchedulingOperation.Version(request.ExpectedTripVersion)))
                    throw AttendanceOperation.Conflict("vehicle_attribution_trip_version_conflict");
                var original = await OriginalCorrectionSource(trip, token);
                if (original.Fingerprint != request.ExpectedSourceFingerprint)
                    throw AttendanceOperation.Conflict("vehicle_correction_sources_changed");
                var rows = await CorrectionRows(tripId, token);
                if (rows.LastOrDefault()?.Header.Id != request.ExpectedLatestRequestId)
                    throw AttendanceOperation.Conflict("vehicle_attribution_revision_changed");
                if (rows.Any(r => r.Header.State is ApprovalState.Pending or ApprovalState.Approved))
                    throw AttendanceOperation.Conflict("vehicle_correction_already_pending_or_approved");
                var segments = await CalculateTripSegments(trip, request.Segments, actor.Now, token);
                var (authority, _) = await LoadPolicyAsync(RosterSchedule.Today(clock.GetUtcNow()), token);
                var rule = AdjustmentPolicy(authority)?.TripAttribution ?? throw AttendanceOperation.Conflict("vehicle_attribution_not_configured");
                var snapshot = new TripCorrectionSnapshot(1, NextAdjustmentRevision(rows.Select(r => r.Header)), tripId,
                    trip.EmployeeId, original.Mileage.Id, request.ExpectedTripVersion, original.Fingerprint, segments,
                    authority.Id, rule, request.EvidenceReference, request.Reason, actor.Identity.AccountId, actor.Now);
                var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot), CorrectionPurpose(tripId), token));
                await ValidateCorrectionSources(trip, snapshot, token, requireCurrentSelection: true);
                await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Trips, actor, token);
                var header = new ApprovalRequest(TripAttributionCorrectionRequestType, tripId, actor.Identity.AccountId.ToString(), reference, snapshot.Revision, actor.Now);
                repository.AddVehicleAdjustmentRequest(header); await repository.SaveChangesAsync(token);
                return new AttendanceChange<VehicleTripAttributionCorrectionView>(header.Id, null, CorrectionView(header, snapshot, null));
            }, ct, beforeReplay: (actor, token) => RequireCurrentVehicleAuthority(VehicleFuelPermissions.Trips, actor, token),
            beforeCommit: async (actor, token) =>
            {
                var trip = await AttributionTrip(tripId, token);
                var row = (await CorrectionRows(tripId, token)).Last();
                await ValidateCorrectionSources(trip, row.Snapshot, token, requireCurrentSelection: true);
                await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Trips, actor, token);
            });

    public Task<VehicleTripAttributionCorrectionView> ReviewTripAttributionCorrectionAsync(Guid tripId, Guid requestId,
        ReviewVehicleAdjustmentRequest request, string key, AuthRequestContext context, CancellationToken ct) =>
        request is null ? throw AttendanceOperation.Invalid("vehicle_attribution_request_required") :
        operation.WriteAsync(VehicleFuelPermissions.Trips, "vehicle.trip.correction.review", null, key, new { tripId, requestId, request },
            request.Reason, context, async (actor, token) =>
            {
                var trip = await AttributionTrip(tripId, token);
                var header = await AdjustmentRequest(tripId, requestId, TripAttributionCorrectionRequestType, token);
                var snapshot = await CorrectionSnapshot(header, token);
                ExpectAdjustment(header, request);
                if ((await CorrectionRows(tripId, token)).LastOrDefault()?.Header.Id != requestId)
                    throw AttendanceOperation.Conflict("vehicle_attribution_revision_changed");
                if (request.Decision == ApprovalState.Approved) await ValidateCorrectionSources(trip, snapshot, token, requireCurrentSelection: true);
                var before = CorrectionView(header, snapshot, null);
                var decision = await DecideAdjustment(header, request, snapshot, VehicleFuelPermissions.Trips, actor, token);
                // The original trip approval and append-only mileage never change here.
                await RequireCorrectionReviewer(tripId, requestId, actor, token, requireCurrentSelection: true);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<VehicleTripAttributionCorrectionView>(header.Id, before, CorrectionView(header, snapshot, decision));
            }, ct, beforeReplay: (actor, token) => RequireCorrectionReviewer(tripId, requestId, actor, token),
            beforeCommit: (actor, token) => RequireCorrectionReviewer(tripId, requestId, actor, token, requireCurrentSelection: true));

    public Task<IReadOnlyList<VehicleTripAttributionCorrectionView>> TripAttributionCorrectionHistoryAsync(Guid tripId,
        AuthRequestContext context, CancellationToken ct) => operation.ReadAsync<IReadOnlyList<VehicleTripAttributionCorrectionView>>(
        VehicleFuelPermissions.Trips, "vehicle.trip.correction.history", null, context, async (actor, token) =>
        {
            await AttributionTrip(tripId, token);
            var rows = await CorrectionRows(tripId, token);
            await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Trips, actor, token);
            return rows.Select(r => CorrectionView(r.Header, r.Snapshot, r.Decision)).ToArray();
        }, ct);

    private async Task RequireCorrectionReviewer(Guid tripId, Guid requestId, AttendanceActor actor, CancellationToken ct, bool requireCurrentSelection = false)
    {
        var trip = await AttributionTrip(tripId, ct);
        var header = await AdjustmentRequest(tripId, requestId, TripAttributionCorrectionRequestType, ct);
        var snapshot = await CorrectionSnapshot(header, ct);
        RequireIndependentReviewer(trip.EmployeeId, trip.RequestedByUserId, actor);
        RequireIndependentReviewer(snapshot.EmployeeId, header.RequesterUserId, actor);
        RequireAdjustmentRole(snapshot.Authority.ReviewerRoleCodes, actor);
        foreach (var segment in snapshot.Segments) RequireAdjustmentRole(segment.Rule.ReviewerRoleCodes, actor);
        if (header.State == ApprovalState.Approved) await ValidateCorrectionSources(trip, snapshot, ct, requireCurrentSelection);
        var (currentPolicy, _) = await LoadPolicyAsync(RosterSchedule.Today(clock.GetUtcNow()), ct);
        var currentRule = AdjustmentPolicy(currentPolicy)?.TripAttribution ?? throw AttendanceOperation.Conflict("vehicle_attribution_not_configured");
        RequireAdjustmentRole(currentRule.ReviewerRoleCodes, actor);
        await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Trips, actor, ct);
    }

    private async Task ValidateCorrectionSources(VehicleTrip trip, TripCorrectionSnapshot snapshot, CancellationToken ct, bool requireCurrentSelection = false)
    {
        await RequireUniquePhysicalTrip(trip, ct);
        var original = await OriginalCorrectionSource(trip, ct);
        if (snapshot.EmployeeId != trip.EmployeeId || snapshot.MileageEntryId != original.Mileage.Id ||
            snapshot.OriginalSourceFingerprint != original.Fingerprint ||
            snapshot.TripVersion != Convert.ToBase64String(trip.ApprovalRequest!.RowVersion))
            throw AttendanceOperation.Conflict("vehicle_correction_sources_changed");
        var segments = await CalculateTripSegments(trip, snapshot.Segments.Select(s => s.Source).ToArray(), clock.GetUtcNow(), ct, requireCurrentSelection);
        if (AdjustmentHash(segments) != AdjustmentHash(snapshot.Segments))
            throw AttendanceOperation.Conflict("vehicle_correction_sources_changed");
    }

    private async Task<(VehicleMileageEntry Mileage, string Fingerprint)> OriginalCorrectionSource(VehicleTrip trip, CancellationToken ct)
    {
        if (trip.ApprovalRequest?.State != ApprovalState.Approved)
            throw AttendanceOperation.Conflict("vehicle_correction_approved_trip_required");
        if ((await AttributionRows(trip, ct)).Any(r => r.Header.State is ApprovalState.Pending or ApprovalState.Approved))
            throw AttendanceOperation.Conflict("vehicle_correction_legacy_trip_required");
        var mileage = await repository.FindApprovedMileageForTripAsync(trip.Id, ct)
            ?? throw AttendanceOperation.Conflict("approved_trip_mileage_unavailable");
        var original = await operation.ReadPrivateAsync<VehicleMileageSnapshot>(mileage.CalculationPrivateReference, MileagePurpose(trip.Id), ct);
        var decision = await repository.FindVehicleAdjustmentDecisionAsync(trip.ApprovalRequest.Id, ct);
        if (original.SchemaVersion != 3 || original.TripId != trip.Id || original.EmployeeId != trip.EmployeeId || original.VehicleId != trip.VehicleId ||
            original.ApprovalRequestId != trip.ApprovalRequest.Id || mileage.ApprovalRequestId != trip.ApprovalRequest.Id ||
            mileage.VehicleTripId != trip.Id || mileage.EmployeeId != trip.EmployeeId || mileage.VehicleId != trip.VehicleId ||
            original.PolicyVersionId != mileage.PolicyVersionId || original.BusinessDate != mileage.BusinessDate ||
            mileage.BusinessDate != RosterSchedule.Today(trip.StartedAt) || original.Purpose != trip.Purpose ||
            original.ApprovedBusinessKm != mileage.ApprovedBusinessKm || original.ExcludedCommuteKm != mileage.ExcludedCommuteKm ||
            original.OutstationKm != mileage.OutstationKm || mileage.ApprovedBusinessKm != mileage.OutstationKm + mileage.ExcludedCommuteKm ||
            mileage.MonthlyAdjustmentKm != 0 || original.OdometerStartKm != trip.OdometerStartKm || original.OdometerEndKm != trip.OdometerEndKm ||
            original.MapDistanceKm != trip.MapDistanceKm || original.RouteEvidenceReference != trip.RouteEvidencePrivateReference ||
            original.ActorAccountId.ToString() != mileage.RecordedByUserId || original.RecordedAt != mileage.RecordedAt ||
            decision?.Decision != ApprovalState.Approved || decision.ApprovalRequestId != trip.ApprovalRequest.Id ||
            decision.ProtectedChangeReference != mileage.CalculationPrivateReference || decision.ActorUserId != mileage.RecordedByUserId ||
            decision.DecidedAt != mileage.RecordedAt || trip.ApprovalRequest.ClosedAt != decision.DecidedAt)
            throw AttendanceOperation.Conflict("vehicle_correction_original_evidence_invalid");
        return (mileage, AdjustmentHash(new { Trip = TripFingerprint(trip), TripVersion = Convert.ToBase64String(trip.ApprovalRequest.RowVersion),
            trip.RequestedByUserId, trip.ApprovalRequest.RequesterUserId, trip.ApprovalRequest.RequestedAt, trip.ApprovalRequest.SnapshotPrivateReference,
            original, MileageEntryId = mileage.Id, mileage.CalculationPrivateReference, mileage.IdempotencyKey,
            DecisionId = decision.Id, decision.ActorRoleCode, decision.Reason }));
    }

    private async Task<IReadOnlyList<TripCorrectionRow>> CorrectionRows(Guid tripId, CancellationToken ct)
    {
        var headers = await repository.ListVehicleAdjustmentRequestsAsync(tripId, TripAttributionCorrectionRequestType, ct);
        ValidateAdjustmentHeaders(headers, tripId, TripAttributionCorrectionRequestType);
        var ordered = headers.OrderBy(h => h.Stage).ToArray();
        if (ordered.Count(h => h.State == ApprovalState.Approved) > 1 ||
            ordered.Take(Math.Max(0, ordered.Length - 1)).Any(h => h.State is ApprovalState.Pending or ApprovalState.Approved))
            throw AttendanceOperation.Conflict("vehicle_correction_history_invalid");
        var rows = new List<TripCorrectionRow>();
        foreach (var header in ordered)
        {
            var snapshot = await CorrectionSnapshot(header, ct);
            rows.Add(new(header, snapshot, await AdjustmentDecision(header, snapshot, ct)));
        }
        return rows;
    }

    private async Task<TripCorrectionSnapshot> CorrectionSnapshot(ApprovalRequest header, CancellationToken ct)
    {
        var s = await operation.ReadPrivateAsync<TripCorrectionSnapshot>(header.SnapshotPrivateReference, CorrectionPurpose(header.SubjectId), ct);
        if (s.SchemaVersion != 1 || s.TripId != header.SubjectId || s.Revision != header.Stage || s.EmployeeId == Guid.Empty || s.MileageEntryId == Guid.Empty ||
            s.RequestedByAccountId.ToString() != header.RequesterUserId || s.RequestedAt != header.RequestedAt ||
            s.OriginalSourceFingerprint is not { Length: 64 } || s.AuthorityPolicyVersionId == Guid.Empty || s.Authority is null ||
            s.Segments is not { Count: > 0 and <= 366 } || s.Segments.Any(x => x is null || x.Source is null || x.Rule is null))
            throw AttendanceOperation.Conflict("vehicle_correction_evidence_invalid");
        return s;
    }

    private async Task<IReadOnlyList<VehicleTripSegment>?> ApprovedCorrectionSegments(VehicleMileageEntry mileage, VehicleTrip trip, CancellationToken ct)
    {
        var row = (await CorrectionRows(trip.Id, ct)).SingleOrDefault(r => r.Header.State == ApprovalState.Approved);
        if (row is null) return null;
        await ValidateCorrectionSources(trip, row.Snapshot, ct);
        var original = await OriginalCorrectionSource(trip, ct);
        var retained = original.Mileage;
        if (mileage.Id != retained.Id || mileage.VehicleTripId != retained.VehicleTripId || mileage.VehicleId != retained.VehicleId ||
            mileage.EmployeeId != retained.EmployeeId || mileage.BusinessDate != retained.BusinessDate ||
            mileage.ApprovalRequestId != retained.ApprovalRequestId || mileage.PolicyVersionId != retained.PolicyVersionId ||
            mileage.CalculationPrivateReference != retained.CalculationPrivateReference || mileage.ApprovedBusinessKm != retained.ApprovedBusinessKm ||
            mileage.OutstationKm != retained.OutstationKm || mileage.ExcludedCommuteKm != retained.ExcludedCommuteKm ||
            mileage.MonthlyAdjustmentKm != retained.MonthlyAdjustmentKm || mileage.IdempotencyKey != retained.IdempotencyKey ||
            mileage.RecordedByUserId != retained.RecordedByUserId || mileage.RecordedAt != retained.RecordedAt)
            throw AttendanceOperation.Conflict("vehicle_correction_sources_changed");
        return row.Snapshot.Segments;
    }

    private async Task RequireUncorrectedFuelSource(Guid tripId, CancellationToken ct)
    {
        if ((await CorrectionRows(tripId, ct)).Any(r => r.Header.State == ApprovalState.Approved))
            throw AttendanceOperation.Conflict("vehicle_fuel_attribution_reconciliation_required");
    }

    private static VehicleTripAttributionCorrectionView CorrectionView(ApprovalRequest h, TripCorrectionSnapshot s, ApprovalDecision? d) =>
        new(h.Id, h.Stage, Convert.ToBase64String(h.RowVersion), h.State.ToString(), s.TripId, s.MileageEntryId,
            s.OriginalSourceFingerprint, s.Segments, s.AuthorityPolicyVersionId, s.EvidenceReference, s.Reason, s.RequestedByAccountId, s.RequestedAt, d?.Id);
    private sealed record TripCorrectionSnapshot(int SchemaVersion, int Revision, Guid TripId, Guid EmployeeId, Guid MileageEntryId,
        string TripVersion, string OriginalSourceFingerprint, IReadOnlyList<VehicleTripSegment> Segments, Guid AuthorityPolicyVersionId,
        VehicleTripAttributionRule Authority, string EvidenceReference, string Reason, Guid RequestedByAccountId, DateTimeOffset RequestedAt);
    private sealed record TripCorrectionRow(ApprovalRequest Header, TripCorrectionSnapshot Snapshot, ApprovalDecision? Decision);
}
