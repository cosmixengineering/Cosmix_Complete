using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.VehicleFuel;

namespace Cosmix.Application.HR.VehicleFuel;

public sealed partial class VehicleFuelService
{
    public const string MonthlyAdjustmentRequestType = "vehicle.monthly-adjustment";
    private static string MonthlyPurpose(Guid vehicleId) => $"vehicle:monthly-adjustment:{vehicleId:N}";
    private static string AdjustmentDecisionPurpose(Guid requestId) => $"vehicle:adjustment:decision:{requestId:N}";

    public Task<VehicleMonthlyAdjustmentPreview> PreviewMonthlyAdjustmentAsync(Guid vehicleId, PreviewVehicleMonthlyAdjustmentRequest request,
        AuthRequestContext context, CancellationToken ct) => request is null ? throw AttendanceOperation.Invalid("vehicle_adjustment_request_required") :
        operation.ReadAsync(VehicleFuelPermissions.Fuel, "vehicle.monthly.preview", null, context,
            (actor, token) => MonthlyPreview(vehicleId, request.EmployeeId, request.MonthStart, actor.Now, token, request.AssignmentSelections), ct);

    public Task<VehicleMonthlyAssignmentContext> MonthlyAssignmentContextAsync(Guid vehicleId, PreviewVehicleMonthlyAdjustmentRequest request,
        AuthRequestContext context, CancellationToken ct) => request is null ? throw AttendanceOperation.Invalid("vehicle_adjustment_request_required") :
        operation.ReadAsync(VehicleFuelPermissions.Fuel, "vehicle.monthly.assignment-context", null, context, async (actor, token) =>
        {
            var calculation = await CalculateMonthly(vehicleId, request.EmployeeId, request.MonthStart, actor.Now, token, selectionContextOnly: true);
            return new VehicleMonthlyAssignmentContext(vehicleId, request.EmployeeId, request.MonthStart, AssignmentDays(calculation.Lines));
        }, ct);

    public Task<VehicleMonthlyAdjustmentView> SubmitMonthlyAdjustmentAsync(Guid vehicleId, SubmitVehicleMonthlyAdjustmentRequest request,
        string key, AuthRequestContext context, CancellationToken ct) => request is null ? throw AttendanceOperation.Invalid("vehicle_adjustment_request_required") :
        operation.WriteAsync(VehicleFuelPermissions.Fuel, "vehicle.monthly.submit", null, key, new { vehicleId, request }, request.Reason, context,
            async (actor, token) =>
            {
                AdjustmentEvidence(request.EvidenceReference);
                var preview = await MonthlyPreview(vehicleId, request.EmployeeId, request.MonthStart, actor.Now, token, request.AssignmentSelections);
                if (request.ExpectedLatestRequestId != preview.LatestRequestId || request.ExpectedPreviewFingerprint != preview.PreviewFingerprint)
                    throw AttendanceOperation.Conflict("vehicle_monthly_preview_changed");
                var rows = await MonthlyRows(vehicleId, token);
                var period = rows.Where(x => x.Snapshot.Calculation.EmployeeId == request.EmployeeId && x.Snapshot.Calculation.MonthStart == request.MonthStart).ToArray();
                if (period.Any(x => x.Request.State == ApprovalState.Pending)) throw AttendanceOperation.Conflict("vehicle_monthly_request_pending");
                var revision = NextAdjustmentRevision(rows.Select(x => x.Request));
                var snapshot = new VehicleMonthlySnapshot(1, revision, preview.Calculation, preview.LatestApprovedRequestId,
                    request.EvidenceReference, request.Reason, actor.Identity.AccountId, actor.Now);
                var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot), MonthlyPurpose(vehicleId), token));
                await ValidateMonthlySources(snapshot.Calculation, token);
                await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Fuel, actor, token);
                var approval = new ApprovalRequest(MonthlyAdjustmentRequestType, vehicleId, actor.Identity.AccountId.ToString(), reference, revision, actor.Now);
                repository.AddVehicleAdjustmentRequest(approval); await repository.SaveChangesAsync(token);
                return new AttendanceChange<VehicleMonthlyAdjustmentView>(approval.Id, null, MonthlyView(approval, snapshot, null));
            }, ct, beforeReplay: (actor, token) => RequireCurrentVehicleAuthority(VehicleFuelPermissions.Fuel, actor, token),
            beforeCommit: async (actor, token) =>
            {
                var period = (await MonthlyRows(vehicleId, token)).Where(x => x.Snapshot.Calculation.EmployeeId == request.EmployeeId &&
                    x.Snapshot.Calculation.MonthStart == request.MonthStart).ToArray();
                var latest = period.Last();
                if (latest.Request.State != ApprovalState.Pending || period.SkipLast(1).LastOrDefault()?.Request.Id != request.ExpectedLatestRequestId)
                    throw AttendanceOperation.Conflict("vehicle_monthly_revision_changed");
                await ValidateMonthlySources(latest.Snapshot.Calculation, token);
                await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Fuel, actor, token);
            });

    public Task<VehicleMonthlyAdjustmentView> ReviewMonthlyAdjustmentAsync(Guid vehicleId, Guid requestId, ReviewVehicleAdjustmentRequest request,
        string key, AuthRequestContext context, CancellationToken ct) => request is null ? throw AttendanceOperation.Invalid("vehicle_adjustment_request_required") :
        operation.WriteAsync(VehicleFuelPermissions.Fuel, "vehicle.monthly.review", null, key, new { vehicleId, requestId, request }, request.Reason,
            context, async (actor, token) =>
            {
                var approval = await AdjustmentRequest(vehicleId, requestId, MonthlyAdjustmentRequestType, token);
                var snapshot = await MonthlySnapshot(approval, token);
                ExpectAdjustment(approval, request);
                var rows = (await MonthlyRows(vehicleId, token)).Where(x => x.Snapshot.Calculation.EmployeeId == snapshot.Calculation.EmployeeId &&
                    x.Snapshot.Calculation.MonthStart == snapshot.Calculation.MonthStart).ToArray();
                if (rows.LastOrDefault()?.Request.Id != requestId || rows.LastOrDefault(x => x.Request.State == ApprovalState.Approved)?.Request.Id != snapshot.PreviousApprovedRequestId)
                    throw AttendanceOperation.Conflict("vehicle_monthly_revision_changed");
                if (request.Decision == ApprovalState.Approved)
                {
                    var current = await CalculateMonthly(vehicleId, snapshot.Calculation.EmployeeId, snapshot.Calculation.MonthStart,
                        actor.Now, token, snapshot.Calculation.PolicyVersionId, snapshot.Calculation.AssignmentSelections);
                    if (current.SourceFingerprint != snapshot.Calculation.SourceFingerprint)
                        throw AttendanceOperation.Conflict("vehicle_monthly_sources_changed");
                }
                var before = MonthlyView(approval, snapshot, null);
                var decision = await DecideAdjustment(approval, request, snapshot, VehicleFuelPermissions.Fuel, actor, token);
                await RequireMonthlyReviewAuthority(vehicleId, requestId, actor, token);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<VehicleMonthlyAdjustmentView>(approval.Id, before, MonthlyView(approval, snapshot, decision));
            }, ct, beforeReplay: (actor, token) => RequireMonthlyReviewAuthority(vehicleId, requestId, actor, token),
            beforeCommit: (actor, token) => RequireMonthlyReviewAuthority(vehicleId, requestId, actor, token));

    private async Task RequireMonthlyReviewAuthority(Guid vehicleId, Guid requestId, AttendanceActor actor, CancellationToken ct)
    {
        var approval = await AdjustmentRequest(vehicleId, requestId, MonthlyAdjustmentRequestType, ct);
        var snapshot = await MonthlySnapshot(approval, ct);
        RequireIndependentReviewer(snapshot.Calculation.EmployeeId, approval.RequesterUserId, actor);
        RequireAdjustmentRole(snapshot.Calculation.Rule.ReviewerRoleCodes, actor);
        foreach (var partition in snapshot.Calculation.Partitions ?? []) RequireAdjustmentRole(partition.Rule.ReviewerRoleCodes, actor);
        if (approval.State == ApprovalState.Approved)
        {
            await MonthlyRows(vehicleId, ct);
            await ValidateMonthlySources(snapshot.Calculation, ct);
        }
        await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Fuel, actor, ct);
    }

    private async Task ValidateMonthlySources(VehicleMonthlyCalculation retained, CancellationToken ct)
    {
        var current = await CalculateMonthly(retained.VehicleId, retained.EmployeeId, retained.MonthStart,
            clock.GetUtcNow(), ct, retained.PolicyVersionId, retained.AssignmentSelections);
        if (current.SourceFingerprint != retained.SourceFingerprint)
            throw AttendanceOperation.Conflict("vehicle_monthly_sources_changed");
    }

    public Task<IReadOnlyList<VehicleMonthlyAdjustmentView>> MonthlyAdjustmentHistoryAsync(Guid vehicleId, Guid employeeId, DateOnly monthStart,
        AuthRequestContext context, CancellationToken ct) => operation.ReadAsync<IReadOnlyList<VehicleMonthlyAdjustmentView>>(
            VehicleFuelPermissions.Fuel, "vehicle.monthly.history", null, context, async (_, token) =>
            {
                MonthDates(vehicleId, employeeId, monthStart);
                return (await MonthlyRows(vehicleId, token)).Where(x => x.Snapshot.Calculation.EmployeeId == employeeId &&
                    x.Snapshot.Calculation.MonthStart == monthStart).Select(x => MonthlyView(x.Request, x.Snapshot, x.Decision)).ToArray();
            }, ct);

    public Task<VehicleMonthlyEntitlement> MonthlyEntitlementAsync(Guid vehicleId, Guid employeeId, DateOnly monthStart,
        AuthRequestContext context, CancellationToken ct) => operation.ReadAsync(
            VehicleFuelPermissions.Fuel, "vehicle.monthly.entitlement", null, context, async (actor, token) =>
            {
                MonthDates(vehicleId, employeeId, monthStart);
                var rows = (await MonthlyRows(vehicleId, token)).Where(x => x.Snapshot.Calculation.EmployeeId == employeeId &&
                    x.Snapshot.Calculation.MonthStart == monthStart).ToArray();
                var approved = rows.LastOrDefault(x => x.Request.State == ApprovalState.Approved);
                var retained = approved is null ? null : MonthlyView(approved.Request, approved.Snapshot, approved.Decision);
                var latest = rows.LastOrDefault()?.Request.Id;
                try
                {
                    var current = await CalculateMonthly(vehicleId, employeeId, monthStart, actor.Now, token, retained?.Calculation.PolicyVersionId,
                        retained?.Calculation.AssignmentSelections);
                    if (retained is null) return new VehicleMonthlyEntitlement(vehicleId, employeeId, monthStart, "PendingReview", latest, null, null, null);
                    if (current.SourceFingerprint != retained.Calculation.SourceFingerprint)
                        return new(vehicleId, employeeId, monthStart, "SourcesChanged", latest, retained, null, "vehicle_monthly_sources_changed");
                    return new(vehicleId, employeeId, monthStart, "Approved", latest, retained, retained.Calculation.AdjustedEligibleKm, null);
                }
                catch (AuthenticationFailure failure) when (failure.StatusCode == 409)
                {
                    var state = retained is null && failure.Code is "vehicle_monthly_rules_not_configured" or "vehicle_fuel_policy_not_configured"
                        ? "ConfigurationPending" : "SourceUnavailable";
                    return new(vehicleId, employeeId, monthStart, state, latest, retained, null, failure.Code);
                }
            }, ct);

    private async Task<VehicleMonthlyAdjustmentPreview> MonthlyPreview(Guid vehicleId, Guid employeeId, DateOnly monthStart, DateTimeOffset now, CancellationToken ct,
        IReadOnlyList<VehicleMonthlyAssignmentSelection>? selections = null)
    {
        var calculation = await CalculateMonthly(vehicleId, employeeId, monthStart, now, ct, selections: selections);
        var rows = (await MonthlyRows(vehicleId, ct)).Where(x => x.Snapshot.Calculation.EmployeeId == employeeId && x.Snapshot.Calculation.MonthStart == monthStart).ToArray();
        var latest = rows.LastOrDefault()?.Request.Id; var approved = rows.LastOrDefault(x => x.Request.State == ApprovalState.Approved)?.Request.Id;
        return new(calculation, latest, approved, AdjustmentHash(new { calculation.SourceFingerprint, latest, approved }));
    }

    private async Task<VehicleMonthlyCalculation> CalculateMonthly(Guid vehicleId, Guid employeeId, DateOnly monthStart, DateTimeOffset now,
        CancellationToken ct, Guid? retainedPolicy = null, IReadOnlyList<VehicleMonthlyAssignmentSelection>? selections = null, bool selectionContextOnly = false)
    {
        var end = MonthDates(vehicleId, employeeId, monthStart);
        if (end > RosterSchedule.Today(now)) throw AttendanceOperation.Conflict("vehicle_month_not_complete");
        if (await repository.FindVehicleAsync(vehicleId, ct) is null) throw AttendanceOperation.Missing("vehicle_not_found");
        var source = await repository.ListApprovedMileageAsync(vehicleId, end.AddDays(-1), ct);
        if (source.Count > 10000) throw AttendanceOperation.Conflict("vehicle_mileage_source_limit");
        var selected = source.Where(x => x.EmployeeId == employeeId).OrderBy(x => x.BusinessDate).ThenBy(x => x.Id).ToArray();

        if (selected.Select(x => x.VehicleTripId).Distinct().Count() != selected.Length)
            throw AttendanceOperation.Conflict("vehicle_mileage_source_invalid");
        var lines = new List<VehicleMonthlyDistanceLine>();
        var rules = new Dictionary<Guid, VehicleMonthlyAdjustmentRule>(); var partitionAllowed = new HashSet<Guid>();
        foreach (var mileage in selected)
        {
            if (mileage.VehicleId != vehicleId || mileage.BusinessDate >= end || mileage.MonthlyAdjustmentKm != 0 ||
                mileage.ApprovedBusinessKm != mileage.OutstationKm + mileage.ExcludedCommuteKm)
                throw AttendanceOperation.Conflict("vehicle_mileage_source_invalid");
            var trip = await repository.FindTripAsync(mileage.VehicleTripId, ct) ?? throw AttendanceOperation.Conflict("approved_trip_mileage_unavailable");
            if (mileage.BusinessDate < monthStart && RosterSchedule.Today(trip.EndedAt.AddTicks(-1)) < monthStart) continue;
            var segments = await ApprovedTripSegments(mileage, trip, ct);
            if (segments is not null)
            {
                var rechecked = await CalculateTripSegments(trip, segments.Select(s => s.Source).ToArray(), now, ct, requireCurrentSelection: false);
                if (AdjustmentHash(rechecked) != AdjustmentHash(segments)) throw AttendanceOperation.Conflict("vehicle_attribution_sources_changed");
                foreach (var segment in segments.Where(s => s.Source.BusinessDate >= monthStart && s.Source.BusinessDate < end))
                {
                    var (segmentPolicy, _) = await LoadPolicyAsync(segment.Source.BusinessDate, ct, segment.Source.PolicyVersionId);
                    var segmentRules = AdjustmentPolicy(segmentPolicy);
                    var segmentRule = segmentRules?.Monthly ?? throw AttendanceOperation.Conflict("vehicle_monthly_rules_not_configured");
                    rules[segmentPolicy.Id] = segmentRule;
                    if (segmentRules!.MonthlyPeriods is not null) partitionAllowed.Add(segmentPolicy.Id);
                    var eligiblePurpose = segmentRule.EligiblePurposes.Contains(trip.Purpose);
                    if (eligiblePurpose && segment.BaselineKm is null) throw AttendanceOperation.Conflict("vehicle_monthly_commute_evidence_unavailable");
                    lines.Add(new(mileage.Id, trip.Id, segment.Source.BusinessDate, segment.Source.PolicyVersionId,
                        segment.ApprovedBusinessKm, segment.EligibleKm, eligiblePurpose ? segment.Source.CommuteDistanceId : null,
                        eligiblePurpose ? segment.BaselineKm : null, eligiblePurpose ? Math.Max(0, segment.BaselineKm!.Value - segment.ApprovedBusinessKm) : 0,
                        AdjustmentHash(new { segment, mileage.CalculationPrivateReference })));
                }
                continue;
            }
            if (mileage.BusinessDate < monthStart) throw AttendanceOperation.Conflict("vehicle_trip_attribution_required");
            var (version, values) = await LoadPolicyAsync(mileage.BusinessDate, ct, mileage.PolicyVersionId);
            var configuration = AdjustmentPolicy(version);
            var rule = configuration?.Monthly ?? throw AttendanceOperation.Conflict("vehicle_monthly_rules_not_configured");
            rules[version.Id] = rule;
            if (configuration!.MonthlyPeriods is not null) partitionAllowed.Add(version.Id);
            var snapshot = await operation.ReadPrivateAsync<VehicleMileageSnapshot>(mileage.CalculationPrivateReference, MileagePurpose(trip.Id), ct);
            if (snapshot.SchemaVersion != 3 || snapshot.TripId != trip.Id || snapshot.VehicleId != vehicleId || snapshot.EmployeeId != employeeId ||
                snapshot.PolicyVersionId != mileage.PolicyVersionId || snapshot.ApprovalRequestId != mileage.ApprovalRequestId ||
                snapshot.BusinessDate != mileage.BusinessDate || snapshot.ApprovedBusinessKm != mileage.ApprovedBusinessKm ||
                snapshot.OutstationKm != mileage.OutstationKm || snapshot.ExcludedCommuteKm != mileage.ExcludedCommuteKm ||
                trip.VehicleId != vehicleId || trip.EmployeeId != employeeId || trip.ApprovalRequestId != mileage.ApprovalRequestId ||
                trip.ApprovalRequest?.State != ApprovalState.Approved || trip.Purpose != snapshot.Purpose ||
                snapshot.RouteEvidenceReference != trip.RouteEvidencePrivateReference || snapshot.MapDistanceKm != trip.MapDistanceKm ||
                snapshot.OdometerStartKm != trip.OdometerStartKm || snapshot.OdometerEndKm != trip.OdometerEndKm ||
                RosterSchedule.Today(trip.StartedAt) != mileage.BusinessDate || RosterSchedule.Today(trip.EndedAt.AddTicks(-1)) != mileage.BusinessDate)
                throw AttendanceOperation.Conflict("vehicle_monthly_trip_evidence_invalid");
            RequireWorkflow(values).RequireStaff(employeeId, mileage.BusinessDate, mileage.BusinessDate);
            await RequireTripCustodyAsync(vehicleId, employeeId, trip.StartedAt, trip.EndedAt, trip.OdometerStartKm, trip.OdometerEndKm, ct);
            decimal? baseline = null; Guid? commuteId = null; var reduction = 0m; EmployeeCommuteDistance? commute = null;
            if (rule.EligiblePurposes.Contains(trip.Purpose))
            {
                commute = snapshot.CommuteDistanceId is Guid id ? await repository.FindCommuteDistanceByIdAsync(id, ct) : null;
                if (commute is null || commute.EmployeeId != employeeId || commute.EffectiveFrom > mileage.BusinessDate || commute.EffectiveTo <= mileage.BusinessDate ||
                    mileage.ExcludedCommuteKm != Math.Min(mileage.ApprovedBusinessKm, commute.UsualHomeOfficeKm))
                    throw AttendanceOperation.Conflict("vehicle_monthly_commute_evidence_unavailable");
                baseline = commute.UsualHomeOfficeKm; commuteId = commute.Id;
                reduction = Math.Max(0, baseline.Value - mileage.ApprovedBusinessKm);
            }
            lines.Add(new(mileage.Id, trip.Id, mileage.BusinessDate, mileage.PolicyVersionId, mileage.ApprovedBusinessKm,
                mileage.OutstationKm, commuteId, baseline, reduction, AdjustmentHash(new { snapshot, Commute = commute is null ? null : new {
                    commute.Id, commute.Revision, commute.UsualHomeOfficeKm, commute.RoutePrivateReference, commute.EffectiveFrom, commute.EffectiveTo } })));
        }
        if (lines.Count == 0)
        {
            var (emptyVersion, _) = await LoadPolicyAsync(monthStart, ct, retainedPolicy);
            if (AdjustmentPolicy(emptyVersion)?.Monthly is null) throw AttendanceOperation.Conflict("vehicle_monthly_rules_not_configured");
            throw AttendanceOperation.Conflict("vehicle_monthly_approved_mileage_required");
        }
        var ordered = lines.OrderBy(l => l.BusinessDate).ThenBy(l => l.MileageEntryId).ToArray();
        var days = AssignmentDays(ordered);
        if (selections is not null && (selections.Count > 31 || selections.Any(s => s is null) ||
            selections.Select(s => s.BusinessDate).Distinct().Count() != selections.Count || selections.Any(s => !days.Any(d => d.BusinessDate == s.BusinessDate))))
            throw AttendanceOperation.Conflict("vehicle_monthly_assignment_context_changed");
        foreach (var day in days)
        {
            if (day.Candidates.All(c => rules[c.PolicyVersionId].AssignmentSelection == "CombineDailyTravelOnce"))
            {
                if (day.Candidates.Select(c => (c.PolicyVersionId, c.CommuteDistanceId, c.BaselineKm)).Distinct().Count() != 1)
                    throw AttendanceOperation.Conflict("vehicle_monthly_commute_allocation_rule_required");
                if (selections?.Any(s => s.BusinessDate == day.BusinessDate) == true)
                    throw AttendanceOperation.Conflict("vehicle_monthly_assignment_context_changed");
                continue;
            }
            if (day.Candidates.Any(c => rules[c.PolicyVersionId].AssignmentSelection != "ExplicitEquivalentTripPerDay"))
                throw AttendanceOperation.Conflict("vehicle_monthly_assignment_precedence_unconfigured");
            // Identity selection cannot decide how a nonzero daily commute deficit is shared.
            if (day.Candidates.Any(c => c.NearerReductionKm != 0) ||
                day.Candidates.Select(c => (c.PolicyVersionId, c.CommuteDistanceId, c.BaselineKm)).Distinct().Count() != 1)
                throw AttendanceOperation.Conflict("vehicle_monthly_commute_allocation_rule_required");
            if (selectionContextOnly) continue;
            var selection = selections?.SingleOrDefault(s => s.BusinessDate == day.BusinessDate)
                ?? throw AttendanceOperation.Conflict("vehicle_monthly_assignment_selection_required");
            if (selection.ExpectedContextFingerprint != day.ContextFingerprint || !day.Candidates.Any(c => c.MileageEntryId == selection.MileageEntryId))
                throw AttendanceOperation.Conflict("vehicle_monthly_assignment_context_changed");
        }
        if (rules.Count > 1 && rules.Keys.Any(id => !partitionAllowed.Contains(id)))
            throw AttendanceOperation.Conflict("vehicle_monthly_policy_partition_not_configured");
        var combinedDays = ordered.Where(l => l.BaselineKm is not null && rules[l.PolicyVersionId].AssignmentSelection == "CombineDailyTravelOnce")
            .GroupBy(l => (l.BusinessDate, l.PolicyVersionId)).Select(g =>
            {
                var baseline = g.First().BaselineKm!.Value;
                var business = g.Sum(l => l.ApprovedBusinessKm);
                var retained = g.Sum(l => l.EligibleKm);
                var eligible = Math.Max(0, business - baseline);
                return new VehicleMonthlyDailyCommute(g.Key.BusinessDate, g.Key.PolicyVersionId, g.First().CommuteDistanceId!.Value,
                    g.Select(l => l.MileageEntryId).Order().ToArray(), business, baseline, retained, eligible,
                    eligible - retained, Math.Max(0, baseline - business));
            }).ToArray();
        var partitions = ordered.GroupBy(l => l.PolicyVersionId).Select(g =>
        {
            var rule = rules[g.Key];
            var combined = combinedDays.Where(d => d.PolicyVersionId == g.Key).ToArray();
            var eligible = g.Sum(l => l.EligibleKm) + combined.Sum(d => d.EligibilityAdjustmentKm);
            var nearer = rule.AssignmentSelection == "CombineDailyTravelOnce" ? combined.Sum(d => d.NearerReductionKm) : g.Sum(l => l.NearerReductionKm);
            var delta = -(rule.ExcessTreatment == "CapAtMonthlyEligible" ? Math.Min(eligible, nearer) : nearer);
            return new VehicleMonthlyPolicyPartition(g.Key, rule, eligible, nearer, delta, eligible + delta);
        }).ToArray();
        var first = partitions[0];
        var result = new VehicleMonthlyCalculation(vehicleId, employeeId, monthStart, first.PolicyVersionId,
            partitions.Sum(p => p.EligibleKm), partitions.Sum(p => p.NearerReductionKm), partitions.Sum(p => p.AdjustmentKm),
            partitions.Sum(p => p.AdjustedEligibleKm), first.Rule, ordered, "")
            { Partitions = partitions.Length > 1 ? partitions : null,
                AssignmentSelections = selections is { Count: > 0 } ? selections.OrderBy(s => s.BusinessDate).ToArray() : null,
                DailyCommuteBreakdown = combinedDays.Length > 0 ? combinedDays : null };
        return result with { SourceFingerprint = AdjustmentHash(result) };
    }

    private static IReadOnlyList<VehicleMonthlyAssignmentDay> AssignmentDays(IReadOnlyList<VehicleMonthlyDistanceLine> lines) =>
        lines.Where(l => l.BaselineKm is not null).GroupBy(l => l.BusinessDate).Where(g => g.Count() > 1).OrderBy(g => g.Key)
            .Select(g => new VehicleMonthlyAssignmentDay(g.Key, g.OrderBy(l => l.MileageEntryId).ToArray(),
                AdjustmentHash(g.OrderBy(l => l.MileageEntryId).ToArray()))).ToArray();

    private async Task<IReadOnlyList<MonthlyRow>> MonthlyRows(Guid vehicleId, CancellationToken ct)
    {
        var headers = await repository.ListVehicleAdjustmentRequestsAsync(vehicleId, MonthlyAdjustmentRequestType, ct);
        ValidateAdjustmentHeaders(headers, vehicleId, MonthlyAdjustmentRequestType);
        var rows = new List<MonthlyRow>();
        foreach (var header in headers.OrderBy(x => x.Stage))
        {
            var snapshot = await MonthlySnapshot(header, ct);
            var decision = await AdjustmentDecision(header, snapshot, ct);
            var prior = rows.LastOrDefault(x => x.Request.State == ApprovalState.Approved && x.Snapshot.Calculation.EmployeeId == snapshot.Calculation.EmployeeId &&
                x.Snapshot.Calculation.MonthStart == snapshot.Calculation.MonthStart)?.Request.Id;
            if (snapshot.PreviousApprovedRequestId != prior) throw AttendanceOperation.Conflict("vehicle_monthly_history_invalid");
            rows.Add(new(header, snapshot, decision));
        }
        return rows;
    }

    private async Task<VehicleMonthlySnapshot> MonthlySnapshot(ApprovalRequest header, CancellationToken ct)
    {
        var s = await operation.ReadPrivateAsync<VehicleMonthlySnapshot>(header.SnapshotPrivateReference, MonthlyPurpose(header.SubjectId), ct);
        if (s.SchemaVersion != 1 || s.Revision != header.Stage || s.Calculation is null || s.Calculation.Rule is null || s.Calculation.Lines is null ||
            s.Calculation.VehicleId != header.SubjectId || s.RequestedByAccountId.ToString() != header.RequesterUserId || s.RequestedAt != header.RequestedAt ||
            s.Calculation.SourceFingerprint != AdjustmentHash(s.Calculation with { SourceFingerprint = "" }))
            throw AttendanceOperation.Conflict("vehicle_monthly_evidence_invalid");
        return s;
    }

    private async Task<(decimal Delta, string Fingerprint, bool HasDailyAggregation)> MaintenanceMonthlyAdjustments(Guid vehicleId, DateOnly through, CancellationToken ct)
    {
        var rows = (await MonthlyRows(vehicleId, ct)).Where(x => x.Request.State == ApprovalState.Approved && x.Snapshot.Calculation.MonthStart.AddMonths(1) <= through)
            .GroupBy(x => (x.Snapshot.Calculation.EmployeeId, x.Snapshot.Calculation.MonthStart)).Select(g => g.MaxBy(x => x.Request.Stage)!).ToArray();
        var selected = rows.Where(x => x.Snapshot.Calculation.Partitions is { } partitions
            ? partitions.Any(p => p.Rule.MaintenanceEffect == "ReduceOutstation")
            : x.Snapshot.Calculation.Rule.MaintenanceEffect == "ReduceOutstation").OrderBy(x => x.Request.Id).ToArray();
        foreach (var row in selected)
        {
            var old = row.Snapshot.Calculation;
            var current = await CalculateMonthly(vehicleId, old.EmployeeId, old.MonthStart,
                new DateTimeOffset(through.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero), ct, old.PolicyVersionId, old.AssignmentSelections);
            if (current.SourceFingerprint != old.SourceFingerprint) throw AttendanceOperation.Conflict("vehicle_monthly_adjustment_stale");
        }
        return (selected.Sum(x =>
        {
            var calculation = x.Snapshot.Calculation;
            var partitions = calculation.Partitions ?? [new(calculation.PolicyVersionId, calculation.Rule, calculation.EligibleKm,
                calculation.NearerReductionKm, calculation.AdjustmentKm, calculation.AdjustedEligibleKm)];
            return partitions.Where(p => p.Rule.MaintenanceEffect == "ReduceOutstation").Sum(p => p.AdjustmentKm +
                (calculation.DailyCommuteBreakdown?.Where(d => d.PolicyVersionId == p.PolicyVersionId).Sum(d => d.EligibilityAdjustmentKm) ?? 0));
        }), AdjustmentHash(selected.Select(x => new {
            x.Request.Id, x.Request.Stage, x.Snapshot.Calculation.SourceFingerprint, x.Snapshot.Calculation.AdjustmentKm, DecisionId = x.Decision!.Id })),
            selected.Any(x => x.Snapshot.Calculation.DailyCommuteBreakdown is { Count: > 0 }));
    }

    private static VehicleAdjustmentPolicy? AdjustmentPolicy(PolicyVersion version)
    { using var json = JsonDocument.Parse(version.RulesJson); return VehicleAdjustmentPolicy.Parse(json.RootElement); }
    private static string AdjustmentHash(object value) => VehiclePolicyRulesCodec.Hash(JsonSerializer.Serialize(value));
    private static void AdjustmentEvidence(string? value)
    { if (!VehicleAdjustmentPolicy.Evidence(value)) throw AttendanceOperation.Invalid("vehicle_adjustment_evidence_required"); }
    private static void RequireAdjustmentRole(IReadOnlyList<string> roles, AttendanceActor actor)
    { if (!roles.Contains(actor.RoleCode, StringComparer.Ordinal)) throw AttendanceOperation.Denied(); }
    private static DateOnly MonthDates(Guid vehicleId, Guid employeeId, DateOnly month)
    {
        if (vehicleId == Guid.Empty || employeeId == Guid.Empty || month == default || month.Day != 1 || month.Year == 9999)
            throw AttendanceOperation.Invalid("vehicle_adjustment_month_invalid");
        return month.AddMonths(1);
    }
    private static int NextAdjustmentRevision(IEnumerable<ApprovalRequest> rows)
    {
        var latest = rows.Select(x => x.Stage).DefaultIfEmpty().Max();
        return latest < 1000 ? latest + 1 : throw AttendanceOperation.Conflict("vehicle_adjustment_history_limit");
    }
    private static void ValidateAdjustmentHeaders(IReadOnlyList<ApprovalRequest> rows, Guid subject, string type)
    {
        if (rows.Count > 1000) throw AttendanceOperation.Conflict("vehicle_adjustment_history_limit");
        if (rows.Any(r => r.SubjectId != subject || r.RequestType != type) || rows.Select(r => r.Stage).Distinct().Count() != rows.Count ||
            rows.OrderBy(r => r.Stage).Select((r, i) => r.Stage == i + 1).Any(valid => !valid))
            throw AttendanceOperation.Conflict("vehicle_adjustment_history_invalid");
    }
    private async Task<ApprovalRequest> AdjustmentRequest(Guid subject, Guid id, string type, CancellationToken ct)
    {
        var row = await repository.FindVehicleAdjustmentRequestAsync(id, type, ct);
        return row is not null && row.SubjectId == subject && row.RequestType == type ? row : throw AttendanceOperation.Missing("vehicle_adjustment_not_found");
    }
    private void ExpectAdjustment(ApprovalRequest approval, ReviewVehicleAdjustmentRequest request)
    {
        AdjustmentEvidence(request.EvidenceReference);
        if (request.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected)) throw AttendanceOperation.Invalid("vehicle_adjustment_decision_invalid");
        var version = SchedulingOperation.Version(request.ExpectedVersion);
        if (approval.State != ApprovalState.Pending || !approval.RowVersion.SequenceEqual(version)) throw AttendanceOperation.Conflict("vehicle_adjustment_version_conflict");
        repository.ExpectVehicleAdjustmentVersion(approval, version);
    }
    private async Task<ApprovalDecision> DecideAdjustment(ApprovalRequest approval, ReviewVehicleAdjustmentRequest request,
        object snapshot, string permission, AttendanceActor actor, CancellationToken ct)
    {
        var evidence = new VehicleAdjustmentDecisionEvidence(1, approval.Id, AdjustmentHash(snapshot), request.Decision,
            request.EvidenceReference, request.Reason, actor.Identity.AccountId, actor.RoleCode, actor.Now);
        var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(evidence), AdjustmentDecisionPurpose(approval.Id), ct));
        await RequireCurrentVehicleAuthority(permission, actor, ct);
        var decision = approval.Decide(request.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode, request.Reason, actor.Now, reference);
        repository.AddDecision(decision); return decision;
    }
    private async Task<ApprovalDecision?> AdjustmentDecision(ApprovalRequest header, object snapshot, CancellationToken ct)
    {
        var decision = await repository.FindVehicleAdjustmentDecisionAsync(header.Id, ct);
        if (header.State is ApprovalState.Pending or ApprovalState.Cancelled)
        { if (decision is not null) throw AttendanceOperation.Conflict("vehicle_adjustment_evidence_invalid"); return null; }
        if (decision?.ProtectedChangeReference is null) throw AttendanceOperation.Conflict("vehicle_adjustment_evidence_invalid");
        var evidence = await operation.ReadPrivateAsync<VehicleAdjustmentDecisionEvidence>(decision.ProtectedChangeReference, AdjustmentDecisionPurpose(header.Id), ct);
        if (evidence.SchemaVersion != 1 || evidence.RequestId != header.Id || evidence.SnapshotFingerprint != AdjustmentHash(snapshot) ||
            evidence.Decision != header.State || decision.Decision != header.State || decision.ApprovalRequestId != header.Id ||
            evidence.ActorAccountId.ToString() != decision.ActorUserId || evidence.ActorRole != decision.ActorRoleCode ||
            evidence.DecidedAt != decision.DecidedAt || header.ClosedAt != decision.DecidedAt || evidence.Reason != decision.Reason)
            throw AttendanceOperation.Conflict("vehicle_adjustment_evidence_invalid");
        return decision;
    }
    private static VehicleMonthlyAdjustmentView MonthlyView(ApprovalRequest r, VehicleMonthlySnapshot s, ApprovalDecision? d) =>
        new(r.Id, r.Stage, Convert.ToBase64String(r.RowVersion), r.State.ToString(), s.Calculation, s.PreviousApprovedRequestId,
            s.EvidenceReference, s.Reason, s.RequestedByAccountId, s.RequestedAt, d?.Id, d?.Reason);
    private sealed record VehicleMonthlySnapshot(int SchemaVersion, int Revision, VehicleMonthlyCalculation Calculation, Guid? PreviousApprovedRequestId,
        string EvidenceReference, string Reason, Guid RequestedByAccountId, DateTimeOffset RequestedAt);
    private sealed record MonthlyRow(ApprovalRequest Request, VehicleMonthlySnapshot Snapshot, ApprovalDecision? Decision);
    private sealed record VehicleAdjustmentDecisionEvidence(int SchemaVersion, Guid RequestId, string SnapshotFingerprint, ApprovalState Decision,
        string EvidenceReference, string Reason, Guid ActorAccountId, string ActorRole, DateTimeOffset DecidedAt);
}
