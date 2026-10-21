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
    public const string TripAttributionRequestType = "vehicle.trip.attribution";
    private static string AttributionPurpose(Guid tripId) => $"vehicle:trip:attribution:{tripId:N}";

    public Task<VehicleTripAttributionContext> TripAttributionContextAsync(Guid tripId, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(VehicleFuelPermissions.Trips, "vehicle.trip.attribution.context", null, context, async (actor, token) =>
        {
            var trip = await AttributionTrip(tripId, token);
            var rows = await AttributionRows(trip, token);
            await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Trips, actor, token);
            return new VehicleTripAttributionContext(tripId, trip.EmployeeId, trip.ApprovalRequest!.Id,
                trip.ApprovalRequest.State.ToString(), Convert.ToBase64String(trip.ApprovalRequest.RowVersion), rows.LastOrDefault()?.Header.Id);
        }, ct);

    public Task<VehicleTripAttributionView> SubmitTripAttributionAsync(Guid tripId, SubmitVehicleTripAttributionRequest request,
        string key, AuthRequestContext context, CancellationToken ct) => request is null
        ? throw AttendanceOperation.Invalid("vehicle_attribution_request_required")
        : operation.WriteAsync(VehicleFuelPermissions.Trips, "vehicle.trip.attribution.submit", null, key,
            new { tripId, request }, request.Reason, context, async (actor, token) =>
            {
                AdjustmentEvidence(request.EvidenceReference);
                var trip = await AttributionTrip(tripId, token);
                if (trip.ApprovalRequest!.State != ApprovalState.Pending ||
                    !trip.ApprovalRequest.RowVersion.SequenceEqual(SchedulingOperation.Version(request.ExpectedTripVersion)))
                    throw AttendanceOperation.Conflict("vehicle_attribution_trip_version_conflict");
                var rows = await AttributionRows(trip, token);
                if (rows.LastOrDefault()?.Header.Id != request.ExpectedLatestRequestId)
                    throw AttendanceOperation.Conflict("vehicle_attribution_revision_changed");
                if (rows.Any(x => x.Header.State is ApprovalState.Pending or ApprovalState.Approved))
                    throw AttendanceOperation.Conflict("vehicle_attribution_already_pending_or_approved");
                var segments = await CalculateTripSegments(trip, request.Segments, actor.Now, token);
                var snapshot = new TripAttributionSnapshot(1, NextAdjustmentRevision(rows.Select(x => x.Header)), trip.Id,
                    trip.EmployeeId, trip.ApprovalRequest.Id, request.ExpectedTripVersion, TripFingerprint(trip), segments,
                    request.EvidenceReference, request.Reason, actor.Identity.AccountId, actor.Now);
                var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot), AttributionPurpose(trip.Id), token));
                await ValidateAttributionSources(trip, snapshot, token, requireCurrentSelection: true);
                await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Trips, actor, token);
                var approval = new ApprovalRequest(TripAttributionRequestType, trip.Id, actor.Identity.AccountId.ToString(), reference, snapshot.Revision, actor.Now);
                repository.AddVehicleAdjustmentRequest(approval); await repository.SaveChangesAsync(token);
                return new AttendanceChange<VehicleTripAttributionView>(approval.Id, null, AttributionView(approval, snapshot, null));
            }, ct, beforeReplay: (actor, token) => RequireCurrentVehicleAuthority(VehicleFuelPermissions.Trips, actor, token),
            beforeCommit: async (actor, token) =>
            {
                var trip = await AttributionTrip(tripId, token);
                var rows = await AttributionRows(trip, token);
                var latest = rows.Last();
                if (latest.Header.State != ApprovalState.Pending || rows.SkipLast(1).LastOrDefault()?.Header.Id != request.ExpectedLatestRequestId ||
                    trip.ApprovalRequest!.State != ApprovalState.Pending ||
                    Convert.ToBase64String(trip.ApprovalRequest.RowVersion) != request.ExpectedTripVersion)
                    throw AttendanceOperation.Conflict("vehicle_attribution_revision_changed");
                await ValidateAttributionSources(trip, latest.Snapshot, token, requireCurrentSelection: true);
                await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Trips, actor, token);
            });

    public Task<VehicleTripAttributionView> ReviewTripAttributionAsync(Guid tripId, Guid requestId, ReviewVehicleAdjustmentRequest request,
        string key, AuthRequestContext context, CancellationToken ct) => request is null
        ? throw AttendanceOperation.Invalid("vehicle_attribution_request_required")
        : operation.WriteAsync(VehicleFuelPermissions.Trips, "vehicle.trip.attribution.review", null, key,
            new { tripId, requestId, request }, request.Reason, context, async (actor, token) =>
            {
                var trip = await AttributionTrip(tripId, token);
                var header = await AdjustmentRequest(tripId, requestId, TripAttributionRequestType, token);
                var snapshot = await AttributionSnapshot(header, token);
                ExpectAdjustment(header, request);
                if ((await AttributionRows(trip, token)).LastOrDefault()?.Header.Id != requestId ||
                    trip.ApprovalRequest!.State != ApprovalState.Pending ||
                    !trip.ApprovalRequest.RowVersion.SequenceEqual(SchedulingOperation.Version(snapshot.TripVersion)))
                    throw AttendanceOperation.Conflict("vehicle_attribution_trip_version_conflict");
                var before = AttributionView(header, snapshot, null);
                if (request.Decision == ApprovalState.Approved)
                {
                    var current = await CalculateTripSegments(trip, snapshot.Segments.Select(s => s.Source).ToArray(), actor.Now, token);
                    if (snapshot.TripFingerprint != TripFingerprint(trip) || AdjustmentHash(current) != AdjustmentHash(snapshot.Segments))
                        throw AttendanceOperation.Conflict("vehicle_attribution_sources_changed");
                    if (await repository.FindApprovedMileageForTripAsync(tripId, token) is not null)
                        throw AttendanceOperation.Conflict("vehicle_attribution_mileage_exists");
                }
                var decision = await DecideAdjustment(header, request, snapshot, VehicleFuelPermissions.Trips, actor, token);
                if (request.Decision == ApprovalState.Approved)
                {
                    await ValidateAttributionSources(trip, snapshot, token, requireCurrentSelection: true);
                    repository.ExpectVehicleAdjustmentVersion(trip.ApprovalRequest, SchedulingOperation.Version(snapshot.TripVersion));
                    repository.AddDecision(trip.ApprovalRequest.Decide(ApprovalState.Approved, actor.Identity.AccountId.ToString(),
                        actor.RoleCode, request.Reason, actor.Now, header.SnapshotPrivateReference));
                    if (repository is not IVehicleFuelMileageWriter writer) throw new InvalidOperationException("Vehicle mileage writer is required.");
                    writer.AddMileageEntry(new(trip.VehicleId, trip.EmployeeId, trip.Id, snapshot.Segments[0].Source.BusinessDate,
                        snapshot.Segments.Sum(s => s.ApprovedBusinessKm), snapshot.Segments.Sum(s => s.ExcludedCommuteKm),
                        snapshot.Segments.Sum(s => s.EligibleKm), 0, header.SnapshotPrivateReference,
                        snapshot.Segments[0].Source.PolicyVersionId, trip.ApprovalRequest.Id, $"attribution:{requestId:N}",
                        actor.Identity.AccountId.ToString(), actor.Now));
                }
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<VehicleTripAttributionView>(header.Id, before, AttributionView(header, snapshot, decision));
            }, ct, beforeReplay: (actor, token) => RequireAttributionReviewer(tripId, requestId, actor, token),
            beforeCommit: (actor, token) => RequireAttributionReviewer(tripId, requestId, actor, token, requireCurrentSelection: true));

    public Task<IReadOnlyList<VehicleTripAttributionView>> TripAttributionHistoryAsync(Guid tripId, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync<IReadOnlyList<VehicleTripAttributionView>>(VehicleFuelPermissions.Trips, "vehicle.trip.attribution.history", null,
            context, async (actor, token) =>
            {
                var rows = await AttributionRows(await AttributionTrip(tripId, token), token);
                await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Trips, actor, token);
                return rows.Select(r => AttributionView(r.Header, r.Snapshot, r.Decision)).ToArray();
            }, ct);

    private async Task RequireAttributionReviewer(Guid tripId, Guid requestId, AttendanceActor actor, CancellationToken ct, bool requireCurrentSelection = false)
    {
        var trip = await AttributionTrip(tripId, ct);
        var header = await AdjustmentRequest(tripId, requestId, TripAttributionRequestType, ct);
        var snapshot = await AttributionSnapshot(header, ct);
        RequireIndependentReviewer(trip.EmployeeId, trip.RequestedByUserId, actor);
        RequireIndependentReviewer(trip.EmployeeId, header.RequesterUserId, actor);
        foreach (var segment in snapshot.Segments) RequireAdjustmentRole(segment.Rule.ReviewerRoleCodes, actor);
        if (header.State == ApprovalState.Approved)
        {
            var mileage = await repository.FindApprovedMileageForTripAsync(tripId, ct)
                ?? throw AttendanceOperation.Conflict("approved_trip_mileage_unavailable");
            await ApprovedTripSegments(mileage, trip, ct);
            if (requireCurrentSelection) await ValidateAttributionSources(trip, snapshot, ct, requireCurrentSelection: true);
        }
        await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Trips, actor, ct);
    }

    private async Task ValidateAttributionSources(VehicleTrip trip, TripAttributionSnapshot snapshot, CancellationToken ct, bool requireCurrentSelection = false)
    {
        await RequireUniquePhysicalTrip(trip, ct);
        var current = await CalculateTripSegments(trip, snapshot.Segments.Select(s => s.Source).ToArray(), clock.GetUtcNow(), ct, requireCurrentSelection);
        if (snapshot.TripFingerprint != TripFingerprint(trip) || AdjustmentHash(current) != AdjustmentHash(snapshot.Segments))
            throw AttendanceOperation.Conflict("vehicle_attribution_sources_changed");
    }

    private async Task<VehicleTrip> AttributionTrip(Guid id, CancellationToken ct)
    {
        var trip = await repository.FindTripAsync(id, ct) ?? throw AttendanceOperation.Missing("trip_not_found");
        if (trip.ApprovalRequest is null || trip.ApprovalRequestId != trip.ApprovalRequest.Id ||
            trip.ApprovalRequest.SubjectId != id || trip.ApprovalRequest.RequestType != TripRequestType)
            throw AttendanceOperation.Conflict("vehicle_trip_approval_unavailable");
        return trip;
    }

    private async Task RequireUniquePhysicalTrip(VehicleTrip trip, CancellationToken ct)
    {
        if (await repository.HasTripOverlapAsync(trip.VehicleId, trip.StartedAt, trip.EndedAt,
            trip.OdometerStartKm, trip.OdometerEndKm, trip.Id, ct))
            throw AttendanceOperation.Conflict("vehicle_trip_overlap");
    }

    private async Task RequireTripReviewIntegrity(Guid tripId, AttendanceActor actor, CancellationToken ct)
    {
        var trip = await AttributionTrip(tripId, ct);
        RequireIndependentReviewer(trip.EmployeeId, trip.RequestedByUserId, actor);
        await RequireUniquePhysicalTrip(trip, ct);
        await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Trips, actor, ct);
    }

    private async Task<IReadOnlyList<VehicleTripSegment>> CalculateTripSegments(VehicleTrip trip,
        IReadOnlyList<VehicleTripSegmentRequest>? sources, DateTimeOffset now, CancellationToken ct, bool requireCurrentSelection = true)
    {
        if (sources is not { Count: > 0 and <= 366 } || trip.EndedAt <= trip.StartedAt || trip.EndedAt > now ||
            trip.EndedAt - trip.StartedAt > TimeSpan.FromDays(366)) throw AttendanceOperation.Invalid("vehicle_attribution_segments_invalid");
        await RequireTripCustodyAsync(trip.VehicleId, trip.EmployeeId, trip.StartedAt, trip.EndedAt, trip.OdometerStartKm, trip.OdometerEndKm, ct);
        var result = new List<VehicleTripSegment>();
        var cursor = trip.StartedAt; var odometer = trip.OdometerStartKm; DateOnly? priorDate = null;
        foreach (var source in sources)
        {
            if (source is null || source.BusinessDate == default || source.BusinessDate.Year == 9999 || source.StartedAt.Offset != TimeSpan.Zero ||
                source.EndedAt.Offset != TimeSpan.Zero || source.StartedAt != cursor || source.EndedAt <= source.StartedAt || source.EndedAt > trip.EndedAt ||
                RosterSchedule.Today(source.StartedAt) != source.BusinessDate || RosterSchedule.Today(source.EndedAt.AddTicks(-1)) != source.BusinessDate ||
                priorDate >= source.BusinessDate || source.OdometerStartKm != odometer || source.OdometerEndKm < odometer ||
                !SegmentDistance(source.OdometerStartKm) || !SegmentDistance(source.OdometerEndKm) || !SegmentDistance(source.MapDistanceKm) ||
                !VehicleAdjustmentPolicy.Evidence(source.EvidenceReference) || source.PolicyVersionId == Guid.Empty)
                throw AttendanceOperation.Invalid("vehicle_attribution_segments_invalid");
            var (version, policy) = await LoadPolicyAsync(source.BusinessDate, ct, source.PolicyVersionId);
            if (requireCurrentSelection && (await repository.GetActiveVehicleFuelPolicyVersionAsync(source.BusinessDate, ct))?.Id != version.Id)
                throw AttendanceOperation.Conflict("vehicle_attribution_policy_mismatch");
            var rule = AdjustmentPolicy(version)?.TripAttribution ?? throw AttendanceOperation.Conflict("vehicle_attribution_not_configured");
            var workflow = RequireWorkflow(policy); var staff = workflow.RequireStaff(trip.EmployeeId, source.BusinessDate, source.BusinessDate);
            var basis = trip.Purpose == VehicleJourneyPurpose.SiteToSite ? "ApprovedMapDistance" : trip.Purpose == VehicleJourneyPurpose.Commute ? "Odometer" : workflow.OtherWorkDistanceBasis;
            var business = basis switch { "ApprovedMapDistance" => source.MapDistanceKm, "Odometer" => source.OdometerEndKm - source.OdometerStartKm,
                _ => throw AttendanceOperation.Conflict("vehicle_distance_basis_not_configured") };
            EmployeeCommuteDistance? commute = null;
            var excluded = trip.Purpose == VehicleJourneyPurpose.Commute ? business : 0m;
            if (trip.Purpose is not (VehicleJourneyPurpose.SiteToSite or VehicleJourneyPurpose.Commute))
            {
                commute = source.CommuteDistanceId is Guid id ? await repository.FindCommuteDistanceByIdAsync(id, ct) : null;
                if (commute is null || commute.EmployeeId != trip.EmployeeId || commute.EffectiveFrom > source.BusinessDate || commute.EffectiveTo <= source.BusinessDate)
                    throw AttendanceOperation.Conflict("vehicle_attribution_commute_invalid");
                excluded = Math.Min(business, commute.UsualHomeOfficeKm);
            }
            else if (source.CommuteDistanceId is not null) throw AttendanceOperation.Invalid("vehicle_attribution_commute_not_applicable");
            var fingerprint = AdjustmentHash(new { source, basis, staff, Policy = version.RulesJson,
                Commute = commute is null ? null : new { commute.Id, commute.Revision, commute.UsualHomeOfficeKm, commute.RoutePrivateReference, commute.EffectiveFrom, commute.EffectiveTo } });
            result.Add(new(source, business, excluded, business - excluded, commute?.UsualHomeOfficeKm, rule, fingerprint));
            cursor = source.EndedAt; odometer = source.OdometerEndKm; priorDate = source.BusinessDate;
        }
        if (cursor != trip.EndedAt || odometer != trip.OdometerEndKm || sources.Sum(s => s.MapDistanceKm) != trip.MapDistanceKm)
            throw AttendanceOperation.Invalid("vehicle_attribution_totals_mismatch");
        return result;
    }

    private async Task<IReadOnlyList<AttributionRow>> AttributionRows(VehicleTrip trip, CancellationToken ct)
    {
        var headers = await repository.ListVehicleAdjustmentRequestsAsync(trip.Id, TripAttributionRequestType, ct);
        ValidateAdjustmentHeaders(headers, trip.Id, TripAttributionRequestType);
        if (headers.OrderBy(h => h.Stage).SkipLast(1).Any(h => h.State is ApprovalState.Pending or ApprovalState.Approved))
            throw AttendanceOperation.Conflict("vehicle_attribution_history_invalid");
        var rows = new List<AttributionRow>();
        foreach (var header in headers.OrderBy(h => h.Stage))
        {
            var snapshot = await AttributionSnapshot(header, ct);
            if (snapshot.TripApprovalRequestId != trip.ApprovalRequestId || snapshot.EmployeeId != trip.EmployeeId)
                throw AttendanceOperation.Conflict("vehicle_attribution_evidence_invalid");
            rows.Add(new(header, snapshot, await AdjustmentDecision(header, snapshot, ct)));
        }
        return rows;
    }

    private async Task<IReadOnlyList<VehicleTripSegment>?> ApprovedTripSegments(VehicleMileageEntry mileage, VehicleTrip trip, CancellationToken ct)
    {
        var rows = await AttributionRows(trip, ct);
        var approved = rows.SingleOrDefault(r => r.Header.State == ApprovalState.Approved);
        if (approved is null) return await ApprovedCorrectionSegments(mileage, trip, ct);
        var snapshot = approved.Snapshot;
        var decision = await repository.FindVehicleAdjustmentDecisionAsync(snapshot.TripApprovalRequestId, ct);
        if (trip.ApprovalRequest?.State != ApprovalState.Approved || snapshot.TripFingerprint != TripFingerprint(trip) ||
            mileage.VehicleTripId != trip.Id || mileage.EmployeeId != trip.EmployeeId || mileage.VehicleId != trip.VehicleId ||
            mileage.ApprovalRequestId != trip.ApprovalRequestId || mileage.CalculationPrivateReference != approved.Header.SnapshotPrivateReference ||
            mileage.BusinessDate != snapshot.Segments[0].Source.BusinessDate || mileage.PolicyVersionId != snapshot.Segments[0].Source.PolicyVersionId ||
            mileage.ApprovedBusinessKm != snapshot.Segments.Sum(s => s.ApprovedBusinessKm) || mileage.ExcludedCommuteKm != snapshot.Segments.Sum(s => s.ExcludedCommuteKm) ||
            mileage.OutstationKm != snapshot.Segments.Sum(s => s.EligibleKm) || mileage.MonthlyAdjustmentKm != 0 ||
            decision?.Decision != ApprovalState.Approved || decision.ProtectedChangeReference != approved.Header.SnapshotPrivateReference ||
            decision.ApprovalRequestId != trip.ApprovalRequestId || decision.ActorUserId != mileage.RecordedByUserId ||
            decision.DecidedAt != mileage.RecordedAt || trip.ApprovalRequest.ClosedAt != decision.DecidedAt ||
            decision.ActorUserId != approved.Decision!.ActorUserId || decision.DecidedAt != approved.Decision.DecidedAt)
            throw AttendanceOperation.Conflict("vehicle_attribution_evidence_invalid");
        await ValidateAttributionSources(trip, snapshot, ct);
        return snapshot.Segments;
    }

    private async Task<TripAttributionSnapshot> AttributionSnapshot(ApprovalRequest header, CancellationToken ct)
    {
        var snapshot = await operation.ReadPrivateAsync<TripAttributionSnapshot>(header.SnapshotPrivateReference, AttributionPurpose(header.SubjectId), ct);
        if (snapshot.SchemaVersion != 1 || snapshot.Revision != header.Stage || snapshot.TripId != header.SubjectId || snapshot.EmployeeId == Guid.Empty ||
            snapshot.RequestedByAccountId.ToString() != header.RequesterUserId || snapshot.RequestedAt != header.RequestedAt ||
            snapshot.Segments is not { Count: > 0 and <= 366 } || snapshot.Segments.Any(s => s is null || s.Source is null || s.Rule is null ||
                s.ApprovedBusinessKm != s.ExcludedCommuteKm + s.EligibleKm || s.EligibleKm < 0 || s.ExcludedCommuteKm < 0))
            throw AttendanceOperation.Conflict("vehicle_attribution_evidence_invalid");
        return snapshot;
    }

    private static bool SegmentDistance(decimal value) => value is >= 0 and <= 999_999_999.999m && decimal.Round(value, 3) == value;
    private static string TripFingerprint(VehicleTrip trip) => AdjustmentHash(new { trip.Id, trip.EmployeeId, trip.VehicleId, trip.ApprovalRequestId,
        trip.Purpose, trip.StartedAt, trip.EndedAt, trip.OdometerStartKm, trip.OdometerEndKm, trip.MapDistanceKm, trip.RouteEvidencePrivateReference, trip.WorkReference });
    private static VehicleTripAttributionView AttributionView(ApprovalRequest row, TripAttributionSnapshot s, ApprovalDecision? d) =>
        new(row.Id, row.Stage, Convert.ToBase64String(row.RowVersion), row.State.ToString(), s.TripId, s.TripApprovalRequestId,
            s.Segments, s.EvidenceReference, s.Reason, s.RequestedByAccountId, s.RequestedAt, d?.Id);
    private sealed record TripAttributionSnapshot(int SchemaVersion, int Revision, Guid TripId, Guid EmployeeId, Guid TripApprovalRequestId,
        string TripVersion, string TripFingerprint, IReadOnlyList<VehicleTripSegment> Segments, string EvidenceReference, string Reason,
        Guid RequestedByAccountId, DateTimeOffset RequestedAt);
    private sealed record AttributionRow(ApprovalRequest Header, TripAttributionSnapshot Snapshot, ApprovalDecision? Decision);
}
