using System.Globalization;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.VehicleFuel;

namespace Cosmix.Application.HR.VehicleFuel;

public sealed partial class VehicleFuelService(
    AttendanceOperation operation,
    IVehicleFuelRepository repository,
    IPrivateFileStore files,
    ICurrentAuthentication current,
    IAccountAccessReader access,
    IAccessManagementRepository authorization,
    ISchedulingJournalRepository journal,
    TimeProvider clock)
{
    public const string TripRequestType = "vehicle.trip";
    public const string FuelRequestType = "vehicle.fuel";
    public const string ServiceRequestType = "vehicle.service";
    public const string IncidentRequestType = "vehicle.incident";

    #region Vehicles

    public Task<VehicleResponse> CreateVehicleAsync(
        CreateVehicleRequest request,
        string key,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.WriteAsync(
            VehicleFuelPermissions.Manage,
            "vehicle.create",
            null,
            key,
            request,
            $"Create vehicle {request.RegistrationNumber}",
            context,
            async (_, token) =>
            {
                var existing = await repository.FindVehicleByRegistrationAsync(request.RegistrationNumber, token);
                if (existing is not null)
                    throw AttendanceOperation.Conflict("vehicle_registration_already_exists");

                var vehicle = new Vehicle(
                    request.RegistrationNumber,
                    request.VehicleType,
                    request.MakeModel,
                    request.AssetReference);

                repository.AddVehicle(vehicle);
                await repository.SaveChangesAsync(token);

                var response = new VehicleResponse(
                    vehicle.Id,
                    vehicle.RegistrationNumber,
                    vehicle.VehicleType,
                    vehicle.MakeModel,
                    vehicle.AssetReference,
                    vehicle.IsActive);

                return new AttendanceChange<VehicleResponse>(vehicle.Id, null, response);
            },
            ct,
            self: false);

    public Task<IReadOnlyList<VehicleResponse>> ListVehiclesAsync(
        bool activeOnly,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.ReadAsync<IReadOnlyList<VehicleResponse>>(
            VehicleFuelPermissions.Manage,
            "vehicle.list",
            null,
            context,
            async (_, token) =>
            {
                var items = await repository.ListVehiclesAsync(activeOnly, token);
                return items.Select(v => new VehicleResponse(
                    v.Id,
                    v.RegistrationNumber,
                    v.VehicleType,
                    v.MakeModel,
                    v.AssetReference,
                    v.IsActive)).ToArray();
            },
            ct,
            self: false);

    #endregion

    #region Custody

    public Task<VehicleCustodyResponse> IssueCustodyAsync(
        IssueCustodyRequest request,
        string key,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.WriteAsync(
            VehicleFuelPermissions.Custody,
            "vehicle.custody.issue",
            request.EmployeeId,
            key,
            request,
            $"Issue custody of vehicle {request.VehicleId} to employee {request.EmployeeId}",
            context,
            async (actor, token) =>
            {
                var vehicle = await repository.FindVehicleAsync(request.VehicleId, token)
                    ?? throw AttendanceOperation.Missing("vehicle_not_found");

                if (!vehicle.IsActive)
                    throw AttendanceOperation.Conflict("vehicle_inactive");

                var existingVehicleCustody = await repository.FindActiveCustodyForVehicleAsync(request.VehicleId, token);
                if (existingVehicleCustody is not null)
                    throw AttendanceOperation.Conflict("vehicle_already_in_custody");

                var existingEmployeeCustody = await repository.FindActiveCustodyForEmployeeAsync(request.EmployeeId, token);
                if (existingEmployeeCustody is not null)
                    throw AttendanceOperation.Conflict("employee_already_has_active_custody");
                if (await repository.HasCustodyPeriodOverlapAsync(request.VehicleId, request.EmployeeId, request.IssuedAt, token))
                    throw AttendanceOperation.Conflict("vehicle_custody_period_overlap");

                var custody = new VehicleCustody(
                    request.VehicleId,
                    request.EmployeeId,
                    request.IssuedAt,
                    request.IssueOdometerKm,
                    request.IssueConditionReference,
                    actor.Identity.AccountId.ToString());

                repository.AddCustody(custody);
                await repository.SaveChangesAsync(token);

                var response = new VehicleCustodyResponse(
                    custody.Id,
                    custody.VehicleId,
                    vehicle.RegistrationNumber,
                    custody.EmployeeId,
                    custody.Employee?.FullLegalName ?? string.Empty,
                    custody.IssuedAt,
                    custody.IssueOdometerKm,
                    custody.IssueConditionPrivateReference,
                    custody.ReturnedAt,
                    custody.ReturnOdometerKm,
                    custody.ReturnConditionPrivateReference,
                    custody.ReturnedAt == null);

                return new AttendanceChange<VehicleCustodyResponse>(custody.Id, null, response);
            },
            ct,
            self: false);

    public Task<VehicleCustodyResponse> ReturnCustodyAsync(
        Guid custodyId,
        ReturnCustodyRequest request,
        string key,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.WriteAsync(
            VehicleFuelPermissions.Custody,
            "vehicle.custody.return",
            null,
            key,
            request,
            $"Return custody {custodyId}",
            context,
            async (actor, token) =>
            {
                var custody = await repository.FindCustodyAsync(custodyId, token)
                    ?? throw AttendanceOperation.Missing("custody_not_found");

                if (custody.ReturnedAt.HasValue)
                    throw AttendanceOperation.Conflict("custody_already_returned");
                if (await repository.HasCustodyReturnConflictAsync(custodyId, request.ReturnedAt, request.ReturnOdometerKm, token))
                    throw AttendanceOperation.Conflict("vehicle_custody_return_conflicts_with_evidence");

                custody.RecordReturn(
                    request.ReturnedAt,
                    request.ReturnOdometerKm,
                    request.ReturnConditionReference,
                    actor.Identity.AccountId.ToString());

                await repository.SaveChangesAsync(token);

                var response = new VehicleCustodyResponse(
                    custody.Id,
                    custody.VehicleId,
                    custody.Vehicle?.RegistrationNumber ?? string.Empty,
                    custody.EmployeeId,
                    custody.Employee?.FullLegalName ?? string.Empty,
                    custody.IssuedAt,
                    custody.IssueOdometerKm,
                    custody.IssueConditionPrivateReference,
                    custody.ReturnedAt,
                    custody.ReturnOdometerKm,
                    custody.ReturnConditionPrivateReference,
                    false);

                return new AttendanceChange<VehicleCustodyResponse>(custody.Id, null, response);
            },
            ct,
            self: false);

    public Task<IReadOnlyList<VehicleCustodyResponse>> ListCustodyHistoryAsync(
        Guid? vehicleId,
        Guid? employeeId,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.ReadAsync<IReadOnlyList<VehicleCustodyResponse>>(
            VehicleFuelPermissions.Custody,
            "vehicle.custody.history",
            employeeId,
            context,
            async (_, token) =>
            {
                var items = await repository.ListCustodyHistoryAsync(vehicleId, employeeId, token);
                return items.Select(MapCustody).ToArray();
            },
            ct,
            self: false);

    public Task<IReadOnlyList<VehicleCustodyResponse>> MyCustodyHistoryAsync(
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.ReadAsync<IReadOnlyList<VehicleCustodyResponse>>(
            VehicleFuelPermissions.Self,
            "vehicle.custody.history.self",
            null,
            context,
            async (actor, token) =>
            {
                var items = await repository.ListCustodyHistoryAsync(null, actor.Identity.EmployeeId, token);
                return items.Select(MapCustody).ToArray();
            },
            ct,
            self: true);

    private static VehicleCustodyResponse MapCustody(VehicleCustody c) =>
        new(
            c.Id,
            c.VehicleId,
            c.Vehicle?.RegistrationNumber ?? string.Empty,
            c.EmployeeId,
            c.Employee?.FullLegalName ?? string.Empty,
            c.IssuedAt,
            c.IssueOdometerKm,
            c.IssueConditionPrivateReference,
            c.ReturnedAt,
            c.ReturnOdometerKm,
            c.ReturnConditionPrivateReference,
            c.ReturnedAt == null);

    #endregion

    #region Commute Distance Baseline

    public Task<EmployeeCommuteDistanceResponse> SetCommuteDistanceAsync(
        SetCommuteDistanceRequest request,
        string key,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.WriteAsync(
            VehicleFuelPermissions.Commute,
            "vehicle.commute.set",
            request.EmployeeId,
            key,
            request,
            $"Set baseline commute distance for {request.EmployeeId}",
            context,
            async (actor, token) =>
            {
                if (await repository.HasCommutePeriodOverlapAsync(request.EmployeeId, request.EffectiveFrom, request.EffectiveTo, token))
                    throw AttendanceOperation.Conflict("vehicle_commute_period_overlap");
                var commute = new EmployeeCommuteDistance(
                    request.EmployeeId,
                    request.DailyRoundTripKm,
                    request.RouteReference,
                    request.EffectiveFrom,
                    request.EffectiveTo,
                    actor.Identity.AccountId.ToString(),
                    actor.Now);

                repository.AddCommuteDistance(commute);
                await repository.SaveChangesAsync(token);

                var response = new EmployeeCommuteDistanceResponse(
                    commute.Id,
                    commute.EmployeeId,
                    commute.EffectiveFrom,
                    commute.EffectiveTo,
                    commute.UsualHomeOfficeKm / 2.0m,
                    commute.UsualHomeOfficeKm,
                    commute.RoutePrivateReference);

                return new AttendanceChange<EmployeeCommuteDistanceResponse>(commute.Id, null, response);
            },
            ct,
            self: false);

    #endregion

    #region Trips

    public Task<VehicleTripResponse> SubmitTripAsync(
        SubmitVehicleTripRequest request,
        string key,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.WriteAsync(
            VehicleFuelPermissions.Self,
            "vehicle.trip.submit",
            null,
            key,
            request,
            request.WorkReference,
            context,
            async (actor, token) =>
            {
                var employeeId = actor.Identity.EmployeeId;
                if (request.EndedAt <= request.StartedAt)
                    throw AttendanceOperation.Invalid("trip_end_must_follow_start");
                if (request.OdometerEndKm < request.OdometerStartKm)
                    throw AttendanceOperation.Invalid("odometer_end_must_be_greater_than_start");
                if (request.EndedAt > actor.Now)
                    throw AttendanceOperation.Invalid("vehicle_trip_in_future");
                if (await repository.HasTripOverlapAsync(request.VehicleId, request.StartedAt, request.EndedAt,
                    request.OdometerStartKm, request.OdometerEndKm, null, token))
                    throw AttendanceOperation.Conflict("vehicle_trip_overlap");

                await RequireTripCustodyAsync(request.VehicleId, employeeId, request.StartedAt, request.EndedAt,
                    request.OdometerStartKm, request.OdometerEndKm, token);

                var vehicle = await repository.FindVehicleAsync(request.VehicleId, token)
                    ?? throw AttendanceOperation.Missing("vehicle_not_found");

                if (request.EndedAt - request.StartedAt > TimeSpan.FromDays(366)) throw AttendanceOperation.Invalid("vehicle_trip_duration_limit");
                var firstDay = RosterSchedule.Today(request.StartedAt); var lastDay = RosterSchedule.Today(request.EndedAt.AddTicks(-1));
                for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
                {
                    var (datedVersion, tripPolicy) = await LoadPolicyAsync(day, token);
                    RequireWorkflow(tripPolicy).RequireStaff(employeeId, day, day);
                    if (firstDay != lastDay && AdjustmentPolicy(datedVersion)?.TripAttribution is null)
                        throw AttendanceOperation.Conflict("vehicle_attribution_not_configured");
                }

                var trip = new VehicleTrip(
                    employeeId,
                    request.VehicleId,
                    request.Purpose,
                    request.StartedAt,
                    request.EndedAt,
                    request.OriginLabel,
                    request.DestinationLabel,
                    request.OdometerStartKm,
                    request.OdometerEndKm,
                    request.MapDistanceKm,
                    request.RouteEvidenceReference,
                    request.WorkReference,
                    actor.Identity.AccountId.ToString(),
                    actor.Now);

                var approval = new ApprovalRequest(
                    TripRequestType,
                    trip.Id,
                    actor.Identity.AccountId.ToString(),
                    $"vehicle:trip:{employeeId:N}:{trip.Id:N}",
                    1,
                    actor.Now);

                trip.Submit(approval);
                repository.AddTrip(trip, approval);
                await repository.SaveChangesAsync(token);

                var distance = request.OdometerEndKm - request.OdometerStartKm;
                var response = new VehicleTripResponse(
                    trip.Id,
                    employeeId,
                    trip.Employee?.FullLegalName ?? string.Empty,
                    trip.VehicleId,
                    vehicle.RegistrationNumber,
                    trip.Purpose,
                    trip.StartedAt,
                    trip.EndedAt,
                    trip.OriginLabel,
                    trip.DestinationLabel,
                    trip.OdometerStartKm,
                    trip.OdometerEndKm,
                    distance,
                    trip.MapDistanceKm,
                    trip.RouteEvidencePrivateReference,
                    trip.WorkReference,
                    trip.CreatedAt,
                    approval.State.ToString(),
                    approval.Id);

                return new AttendanceChange<VehicleTripResponse>(trip.Id, null, response);
            },
            ct,
            self: true);

    public Task<VehicleTripResponse> ApproveTripAsync(
        Guid tripId,
        string key,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.WriteAsync(
            VehicleFuelPermissions.Trips,
            "vehicle.trip.approve",
            null,
            key,
            new { TripId = tripId },
            $"Approve vehicle trip {tripId}",
            context,
            async (actor, token) =>
            {
                var trip = await repository.FindTripAsync(tripId, token)
                    ?? throw AttendanceOperation.Missing("trip_not_found");

                if (trip.ApprovalRequest is null || trip.ApprovalRequest.State != ApprovalState.Pending ||
                    trip.ApprovalRequestId is not Guid approvalRequestId)
                    throw AttendanceOperation.Conflict("trip_not_pending");
                if (await repository.FindApprovedMileageForTripAsync(tripId, token) is not null)
                    throw AttendanceOperation.Conflict("vehicle_attribution_mileage_exists");

                RequireIndependentReviewer(trip.EmployeeId, trip.RequestedByUserId, actor);
                await RequireUniquePhysicalTrip(trip, token);
                if (RosterSchedule.Today(trip.StartedAt) != RosterSchedule.Today(trip.EndedAt.AddTicks(-1)) ||
                    (await repository.ListVehicleAdjustmentRequestsAsync(trip.Id, TripAttributionRequestType, token)).Any(r => r.State == ApprovalState.Pending))
                    throw AttendanceOperation.Conflict("vehicle_trip_attribution_required");
                if (trip.EndedAt > actor.Now) throw AttendanceOperation.Invalid("vehicle_trip_in_future");
                await RequireTripCustodyAsync(trip.VehicleId, trip.EmployeeId, trip.StartedAt, trip.EndedAt,
                    trip.OdometerStartKm, trip.OdometerEndKm, token);

                var businessDate = RosterSchedule.Today(trip.StartedAt);
                var (policyVersion, policy) = await LoadPolicyAsync(businessDate, token);
                var workflow = RequireWorkflow(policy);
                var staff = workflow.RequireStaff(trip.EmployeeId, businessDate, RosterSchedule.Today(trip.EndedAt.AddTicks(-1)));
                if (trip.Purpose != VehicleJourneyPurpose.Commute && trip.MapDistanceKm <= 0)
                    throw AttendanceOperation.Conflict("vehicle_map_distance_required");
                var distanceBasis = trip.Purpose switch
                {
                    VehicleJourneyPurpose.SiteToSite => "ApprovedMapDistance",
                    VehicleJourneyPurpose.Commute => "Odometer",
                    _ => workflow.OtherWorkDistanceBasis
                };
                var approvedBusinessKm = distanceBasis switch
                {
                    "ApprovedMapDistance" => trip.MapDistanceKm,
                    "Odometer" => trip.OdometerEndKm - trip.OdometerStartKm,
                    _ => throw AttendanceOperation.Conflict("vehicle_distance_basis_not_configured")
                };
                var commute = await repository.FindActiveCommuteDistanceAsync(trip.EmployeeId, businessDate, token);
                var excludedCommuteKm = 0m;
                if (trip.Purpose == VehicleJourneyPurpose.Commute)
                {
                    excludedCommuteKm = approvedBusinessKm;
                }
                else if (trip.Purpose != VehicleJourneyPurpose.SiteToSite)
                {
                    if (commute is null) throw AttendanceOperation.Conflict("vehicle_commute_distance_not_configured");
                    excludedCommuteKm = Math.Min(approvedBusinessKm, commute.UsualHomeOfficeKm);
                }
                var outstationKm = approvedBusinessKm - excludedCommuteKm;

                var accumulatedBefore = await repository.GetAccumulatedOutstationMileageAsync(trip.VehicleId, businessDate, token);
                var calculationReference = AttendanceOperation.Reference(await files.WriteAsync(
                    JsonSerializer.SerializeToUtf8Bytes(new VehicleMileageSnapshot(3, trip.Id, trip.VehicleId,
                        trip.EmployeeId, trip.Purpose, businessDate, trip.OdometerStartKm, trip.OdometerEndKm,
                        approvedBusinessKm, excludedCommuteKm, outstationKm, accumulatedBefore, policyVersion.Id,
                        approvalRequestId, actor.Identity.AccountId, actor.Now, trip.MapDistanceKm,
                        trip.RouteEvidencePrivateReference, commute?.Id, distanceBasis, staff)),
                    MileagePurpose(trip.Id), token));
                await RequireUniquePhysicalTrip(trip, token);

                var mileage = new VehicleMileageEntry(
                    trip.VehicleId,
                    trip.EmployeeId,
                    trip.Id,
                    businessDate,
                    approvedBusinessKm,
                    excludedCommuteKm,
                    outstationKm,
                    0m,
                    calculationReference,
                    policyVersion.Id,
                    approvalRequestId,
                    MileageIdempotencyKey(key),
                    actor.Identity.AccountId.ToString(),
                    actor.Now);

                if (repository is not IVehicleFuelMileageWriter mileageWriter)
                    throw new InvalidOperationException("The vehicle fuel repository cannot stage mileage entries.");
                var decision = trip.ApprovalRequest.Decide(ApprovalState.Approved, actor.Identity.AccountId.ToString(),
                    actor.RoleCode, "Trip approved", actor.Now, calculationReference);
                repository.AddDecision(decision);
                mileageWriter.AddMileageEntry(mileage);
                await repository.SaveChangesAsync(token);

                var distance = trip.OdometerEndKm - trip.OdometerStartKm;
                var response = new VehicleTripResponse(
                    trip.Id,
                    trip.EmployeeId,
                    trip.Employee?.FullLegalName ?? string.Empty,
                    trip.VehicleId,
                    trip.Vehicle?.RegistrationNumber ?? string.Empty,
                    trip.Purpose,
                    trip.StartedAt,
                    trip.EndedAt,
                    trip.OriginLabel,
                    trip.DestinationLabel,
                    trip.OdometerStartKm,
                    trip.OdometerEndKm,
                    distance,
                    trip.MapDistanceKm,
                    trip.RouteEvidencePrivateReference,
                    trip.WorkReference,
                    trip.CreatedAt,
                    ApprovalState.Approved.ToString(),
                    trip.ApprovalRequestId);

                return new AttendanceChange<VehicleTripResponse>(trip.Id, null, response);
            },
            ct,
            self: false,
            beforeReplay: (actor, token) => RequireTripReviewIntegrity(tripId, actor, token),
            beforeCommit: (actor, token) => RequireTripReviewIntegrity(tripId, actor, token));

    public Task<VehicleFuelPage<VehicleTripResponse>> PageTripsAsync(
        Guid? employeeId,
        Guid? vehicleId,
        ApprovalState? state,
        int page,
        int pageSize,
        AuthRequestContext context,
        CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        return operation.ReadAsync(
            VehicleFuelPermissions.Trips,
            "vehicle.trips.page",
            employeeId,
            context,
            async (_, token) =>
            {
                var p = page;
                var size = pageSize;
                var skip = (p - 1) * size;

                var (items, total) = await repository.PageTripsAsync(employeeId, vehicleId, state, skip, size, token);
                var dtos = items.Select(t => new VehicleTripResponse(
                    t.Id,
                    t.EmployeeId,
                    t.Employee?.FullLegalName ?? string.Empty,
                    t.VehicleId,
                    t.Vehicle?.RegistrationNumber ?? string.Empty,
                    t.Purpose,
                    t.StartedAt,
                    t.EndedAt,
                    t.OriginLabel,
                    t.DestinationLabel,
                    t.OdometerStartKm,
                    t.OdometerEndKm,
                    t.OdometerEndKm - t.OdometerStartKm,
                    t.MapDistanceKm,
                    t.RouteEvidencePrivateReference,
                    t.WorkReference,
                    t.CreatedAt,
                    t.ApprovalRequest?.State.ToString() ?? ApprovalState.Pending.ToString(),
                    t.ApprovalRequestId)).ToArray();

                return new VehicleFuelPage<VehicleTripResponse>(dtos, p, size, total, skip + size < total);
            },
            ct,
            self: false);
    }

    #endregion

    #region Fuel Claims

    public Task<FuelClaimResponse> SubmitFuelClaimAsync(
        SubmitFuelClaimRequest request,
        string key,
        AuthRequestContext context,
        CancellationToken ct)
    {
        Guid? createdClaimId = null;
        return operation.WriteAsync(
            VehicleFuelPermissions.Self,
            "vehicle.fuel.claim.submit",
            null,
            key,
            request,
            $"Fuel claim for trip {request.VehicleTripId}",
            context,
            async (actor, token) =>
            {
                var employeeId = actor.Identity.EmployeeId;
                var trip = await repository.FindTripAsync(request.VehicleTripId, token)
                    ?? throw AttendanceOperation.Missing("trip_not_found");

                if (trip.EmployeeId != employeeId)
                    throw AttendanceOperation.Denied();

                if (trip.ApprovalRequest?.State != ApprovalState.Approved)
                    throw AttendanceOperation.Conflict("trip_must_be_approved_before_fuel_claim");

                if (await repository.HasClaimForTripAsync(trip.Id, token))
                    throw AttendanceOperation.Conflict("fuel_claim_already_submitted_for_this_trip");

                var mileage = await repository.FindApprovedMileageForTripAsync(trip.Id, token)
                    ?? throw AttendanceOperation.Conflict("approved_trip_mileage_unavailable");
                var date = RosterSchedule.Today(trip.StartedAt);
                if (mileage.EmployeeId != employeeId || mileage.VehicleId != trip.VehicleId ||
                    mileage.VehicleTripId != trip.Id || mileage.ApprovalRequestId != trip.ApprovalRequestId ||
                    mileage.BusinessDate != date || mileage.ApprovedBusinessKm != mileage.OutstationKm + mileage.ExcludedCommuteKm)
                    throw AttendanceOperation.Conflict("approved_trip_mileage_unavailable");
                var excludedCommute = mileage.ExcludedCommuteKm;
                var segments = await ApprovedTripSegments(mileage, trip, token);
                var corrected = await CalculateCorrectedFuel(trip, mileage, request.Currency, token);
                var (policyVersion, policy) = await LoadPolicyAsync(date, token,
                    corrected?.Calculation.Lines[0].PolicyVersionId ?? mileage.PolicyVersionId);
                if (segments is null) RequireWorkflow(policy).RequireStaff(employeeId, date, RosterSchedule.Today(trip.EndedAt.AddTicks(-1)));
                RequireWorkflow(policy).RequireFuelCurrency(request.Currency);
                if (trip.Purpose == VehicleJourneyPurpose.SiteToSite &&
                    (mileage.ApprovedBusinessKm != trip.MapDistanceKm || mileage.ExcludedCommuteKm != 0))
                    throw AttendanceOperation.Conflict("approved_trip_map_distance_mismatch");
                if (policy.KmPerLiter is null || policy.FuelPricePerLiter is null)
                    throw AttendanceOperation.Conflict("vehicle_fuel_policy_not_configured");

                var eligibleDistance = mileage.OutstationKm;
                var fuelLiters = Math.Round(eligibleDistance / policy.KmPerLiter.Value, 2);
                var claimedAmount = Math.Round(fuelLiters * policy.FuelPricePerLiter.Value, 2);
                object fuelEvidence;
                if (corrected is not null)
                {
                    eligibleDistance = corrected.Calculation.Lines.Sum(l => l.EligibleKm);
                    excludedCommute = corrected.Calculation.Lines.Sum(l => l.ExcludedCommuteKm);
                    fuelLiters = corrected.Calculation.FuelLiters; claimedAmount = corrected.Calculation.ClaimedAmount;
                    fuelEvidence = corrected;
                }
                else if (segments is not null)
                {
                    var dated = await CalculateDatedFuel(trip, mileage, segments, request.Currency, token);
                    fuelLiters = dated.FuelLiters; claimedAmount = dated.ClaimedAmount; fuelEvidence = dated;
                }
                else fuelEvidence = new
                {
                    SchemaVersion = 1, TripId = trip.Id, MileageEntryId = mileage.Id, mileage.CalculationPrivateReference,
                    PolicyVersionId = policyVersion.Id, EligibleDistanceKm = eligibleDistance, ExcludedCommuteKm = excludedCommute,
                    policy.KmPerLiter, policy.FuelPricePerLiter, FuelLiters = fuelLiters, ClaimedAmount = claimedAmount,
                    Currency = request.Currency.ToUpperInvariant()
                };

                var claim = new FuelClaim(
                    employeeId,
                    trip.Id,
                    null,
                    eligibleDistance,
                    excludedCommute,
                    fuelLiters,
                    claimedAmount,
                    request.Currency.ToUpperInvariant(),
                    policyVersion.Id,
                    AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(fuelEvidence), $"vehicle:fuel:{trip.Id:N}", token)),
                    request.ReceiptReference,
                    actor.Identity.AccountId.ToString(),
                    actor.Now);

                var approval = new ApprovalRequest(
                    FuelRequestType,
                    claim.Id,
                    actor.Identity.AccountId.ToString(),
                    $"vehicle:fuel:{employeeId:N}:{claim.Id:N}",
                    1,
                    actor.Now);

                claim.Submit(approval);
                await ValidateCorrectedFuelClaim(claim, token, requireUncorrectedLegacy: true);
                repository.AddFuelClaim(claim, approval);
                await repository.SaveChangesAsync(token);
                createdClaimId = claim.Id;

                var response = new FuelClaimResponse(
                    claim.Id,
                    employeeId,
                    trip.Employee?.FullLegalName ?? string.Empty,
                    trip.Id,
                    claim.EligibleDistanceKm,
                    claim.ExcludedCommuteKm,
                    claim.FuelLiters,
                    claim.ClaimedAmount,
                    claim.Currency,
                    claim.CalculationPrivateReference,
                    claim.ReceiptPrivateReference,
                    claim.CreatedAt,
                    approval.State.ToString(),
                    approval.Id);

                return new AttendanceChange<FuelClaimResponse>(claim.Id, null, response);
            },
            ct,
            self: true,
            beforeReplay: (actor, token) => RequireFuelSubmissionReplay(request.VehicleTripId, key, actor, token),
            beforeCommit: async (actor, token) =>
            {
                var claim = createdClaimId is Guid id ? await repository.FindFuelClaimAsync(id, token) : null;
                if (claim is null) throw AttendanceOperation.Conflict("fuel_claim_not_found");
                if (claim.EmployeeId != actor.Identity.EmployeeId) throw AttendanceOperation.Denied();
                await ValidateCorrectedFuelClaim(claim, token, requireUncorrectedLegacy: true);
            });
    }

    public Task<FuelClaimResponse> ApproveFuelClaimAsync(
        Guid claimId,
        string key,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.WriteAsync(
            VehicleFuelPermissions.Fuel,
            "vehicle.fuel.claim.approve",
            null,
            key,
            new { ClaimId = claimId },
            $"Approve fuel claim {claimId}",
            context,
            async (actor, token) =>
            {
                var claim = await repository.FindFuelClaimAsync(claimId, token)
                    ?? throw AttendanceOperation.Missing("fuel_claim_not_found");

                if (claim.ApprovalRequest is null || claim.ApprovalRequest.State != ApprovalState.Pending)
                    throw AttendanceOperation.Conflict("claim_not_pending");

                RequireIndependentReviewer(claim.EmployeeId, claim.RequestedByUserId, actor);
                var corrected = await ValidateCorrectedFuelClaim(claim, token, requireUncorrectedLegacy: true);
                if (!corrected)
                {
                var trip = await repository.FindTripAsync(claim.VehicleTripId, token)
                    ?? throw AttendanceOperation.Conflict("approved_trip_mileage_unavailable");
                await RequireUncorrectedFuelSource(trip.Id, token);
                var mileage = await repository.FindApprovedMileageForTripAsync(trip.Id, token)
                    ?? throw AttendanceOperation.Conflict("approved_trip_mileage_unavailable");
                var date = RosterSchedule.Today(trip.StartedAt);
                var segments = await ApprovedTripSegments(mileage, trip, token);
                if (segments is not null)
                {
                    var dated = await CalculateDatedFuel(trip, mileage, segments, claim.Currency, token);
                    var retained = await operation.ReadPrivateAsync<SegmentFuelEvidence>(claim.CalculationPrivateReference, $"vehicle:fuel:{trip.Id:N}", token);
                    if (AdjustmentHash(dated) != AdjustmentHash(retained) || trip.EmployeeId != claim.EmployeeId ||
                        mileage.PolicyVersionId != claim.PolicyVersionId || mileage.OutstationKm != claim.EligibleDistanceKm ||
                        mileage.ExcludedCommuteKm != claim.ExcludedCommuteKm || dated.FuelLiters != claim.FuelLiters || dated.ClaimedAmount != claim.ClaimedAmount)
                        throw AttendanceOperation.Conflict("approved_trip_mileage_unavailable");
                }
                else
                {
                var (_, policy) = await LoadPolicyAsync(date, token, claim.PolicyVersionId);
                RequireWorkflow(policy).RequireStaff(claim.EmployeeId, date, RosterSchedule.Today(trip.EndedAt.AddTicks(-1)));
                RequireWorkflow(policy).RequireFuelCurrency(claim.Currency);
                if (trip.EmployeeId != claim.EmployeeId || trip.ApprovalRequest?.State != ApprovalState.Approved ||
                    mileage.PolicyVersionId != claim.PolicyVersionId || mileage.OutstationKm != claim.EligibleDistanceKm ||
                    mileage.ExcludedCommuteKm != claim.ExcludedCommuteKm ||
                    mileage.EmployeeId != claim.EmployeeId || mileage.VehicleId != trip.VehicleId ||
                    mileage.ApprovalRequestId != trip.ApprovalRequestId || mileage.BusinessDate != date ||
                    policy.KmPerLiter is null || policy.FuelPricePerLiter is null ||
                    Math.Round(claim.EligibleDistanceKm / policy.KmPerLiter.Value, 2) != claim.FuelLiters ||
                    Math.Round(claim.FuelLiters * policy.FuelPricePerLiter.Value, 2) != claim.ClaimedAmount ||
                    trip.Purpose == VehicleJourneyPurpose.SiteToSite &&
                        (mileage.ApprovedBusinessKm != trip.MapDistanceKm || mileage.ExcludedCommuteKm != 0))
                    throw AttendanceOperation.Conflict("approved_trip_mileage_unavailable");
                }

                }
                var decision = claim.ApprovalRequest.Decide(
                    ApprovalState.Approved,
                    actor.Identity.AccountId.ToString(),
                    actor.RoleCode,
                    "Fuel claim approved; settlement requires separate authorized handoff",
                    actor.Now, corrected ? claim.CalculationPrivateReference : null);

                repository.AddDecision(decision);
                await repository.SaveChangesAsync(token);

                var response = new FuelClaimResponse(
                    claim.Id,
                    claim.EmployeeId,
                    claim.Employee?.FullLegalName ?? string.Empty,
                    claim.VehicleTripId,
                    claim.EligibleDistanceKm,
                    claim.ExcludedCommuteKm,
                    claim.FuelLiters,
                    claim.ClaimedAmount,
                    claim.Currency,
                    claim.CalculationPrivateReference,
                    claim.ReceiptPrivateReference,
                    claim.CreatedAt,
                    ApprovalState.Approved.ToString(),
                    claim.ApprovalRequestId);

                return new AttendanceChange<FuelClaimResponse>(claim.Id, null, response);
            },
            ct,
            self: false,
            beforeReplay: (actor, token) => RequireFuelReviewIntegrity(claimId, actor, token, historicalReplay: true),
            beforeCommit: (actor, token) => RequireFuelReviewIntegrity(claimId, actor, token, historicalReplay: false));

    public Task<VehicleFuelPage<FuelClaimResponse>> PageFuelClaimsAsync(
        Guid? employeeId,
        ApprovalState? state,
        int page,
        int pageSize,
        AuthRequestContext context,
        CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        return operation.ReadAsync(
            VehicleFuelPermissions.Fuel,
            "vehicle.fuel.page",
            employeeId,
            context,
            async (_, token) =>
            {
                var p = page;
                var size = pageSize;
                var skip = (p - 1) * size;

                var (items, total) = await repository.PageFuelClaimsAsync(employeeId, state, skip, size, token);
                var dtos = items.Select(c => new FuelClaimResponse(
                    c.Id,
                    c.EmployeeId,
                    c.Employee?.FullLegalName ?? string.Empty,
                    c.VehicleTripId,
                    c.EligibleDistanceKm,
                    c.ExcludedCommuteKm,
                    c.FuelLiters,
                    c.ClaimedAmount,
                    c.Currency,
                    c.CalculationPrivateReference,
                    c.ReceiptPrivateReference,
                    c.CreatedAt,
                    c.ApprovalRequest?.State.ToString() ?? ApprovalState.Pending.ToString(),
                    c.ApprovalRequestId)).ToArray();

                return new VehicleFuelPage<FuelClaimResponse>(dtos, p, size, total, skip + size < total);
            },
            ct,
            self: false);
    }

    #endregion

    #region Maintenance & Workshop

    public Task<VehicleServiceRecordResponse> SubmitServiceRecordAsync(
        SubmitServiceRecordRequest request,
        string key,
        AuthRequestContext context,
        CancellationToken ct)
    {
        VehicleServiceRecord? submitted = null;
        return operation.WriteAsync(
            VehicleFuelPermissions.Self,
            "vehicle.service.submit",
            null,
            key,
            request,
            $"Motorcycle {request.ServiceKind} maintenance",
            context,
            async (actor, token) =>
            {
                var employeeId = actor.Identity.EmployeeId;
                var vehicle = await repository.FindVehicleAsync(request.VehicleId, token)
                    ?? throw AttendanceOperation.Missing("vehicle_not_found");

                await RequireServiceCustodyAsync(request.VehicleId, employeeId, request.ServiceDate, request.OdometerKm, token);
                if (request.ServiceDate > RosterSchedule.Today(actor.Now))
                    throw AttendanceOperation.Invalid("vehicle_service_in_future");
                if (await repository.HasServiceEvidenceAsync(request.VehicleId, request.EvidenceReference, token))
                    throw AttendanceOperation.Conflict("vehicle_service_evidence_already_used");
                var eligibility = await ServiceEligibilityAsync(employeeId, request, vehicle.VehicleType, token);

                var record = new VehicleServiceRecord(
                    request.VehicleId,
                    employeeId,
                    request.ServiceKind,
                    request.ServiceDate,
                    request.OdometerKm,
                    eligibility.ApprovedOutstationKm,
                    request.AuthorizedWorkshopReference,
                    request.EvidenceReference,
                    request.ClaimedAmount,
                    request.Currency.ToUpperInvariant(),
                    eligibility.PolicyVersionId,
                    AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(eligibility),
                        ServicePurpose(request.VehicleId), token)),
                    actor.Identity.AccountId.ToString(),
                    actor.Now);

                var approval = new ApprovalRequest(
                    ServiceRequestType,
                    record.Id,
                    actor.Identity.AccountId.ToString(),
                    $"vehicle:service:{employeeId:N}:{record.Id:N}",
                    1,
                    actor.Now);

                record.Submit(approval);
                submitted = record;
                repository.AddServiceRecord(record, approval);
                await repository.SaveChangesAsync(token);

                var response = new VehicleServiceRecordResponse(
                    record.Id,
                    record.VehicleId,
                    vehicle.RegistrationNumber,
                    employeeId,
                    record.Employee?.FullLegalName ?? string.Empty,
                    record.ServiceKind,
                    record.ServiceDate,
                    record.OdometerKm,
                    record.ApprovedOutstationKm,
                    record.AuthorizedWorkshopReference,
                    record.EvidencePrivateReference,
                    record.ClaimedAmount,
                    record.Currency,
                    record.CreatedAt,
                    approval.State.ToString(),
                    approval.Id);

                return new AttendanceChange<VehicleServiceRecordResponse>(record.Id, null, response);
            },
            ct,
            self: true,
            beforeReplay: (actor, token) => RequireServiceSubmissionReplay(request.VehicleId, key, actor, token),
            beforeCommit: async (_, token) =>
            {
                if (submitted is not null) await RequireConsumedMaintenanceIntegrity(submitted, token);
            });
    }

    public Task<VehicleServiceRecordResponse> ApproveServiceRecordAsync(
        Guid serviceRecordId,
        string key,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.WriteAsync(
            VehicleFuelPermissions.Maintenance,
            "vehicle.service.approve",
            null,
            key,
            new { ServiceRecordId = serviceRecordId },
            $"Approve vehicle service record {serviceRecordId}",
            context,
            async (actor, token) =>
            {
                var record = await repository.FindServiceRecordAsync(serviceRecordId, token)
                    ?? throw AttendanceOperation.Missing("service_record_not_found");

                if (record.ApprovalRequest is null || record.ApprovalRequest.State != ApprovalState.Pending)
                    throw AttendanceOperation.Conflict("service_record_not_pending");

                RequireIndependentReviewer(record.EmployeeId, record.RequestedByUserId, actor);
                await RequireServiceCustodyAsync(record.VehicleId, record.EmployeeId, record.ServiceDate, record.OdometerKm, token);
                if (record.ServiceDate > RosterSchedule.Today(actor.Now))
                    throw AttendanceOperation.Invalid("vehicle_service_in_future");
                var retained = await ReadServiceEligibilityAsync(record, token);
                var vehicle = await repository.FindVehicleAsync(record.VehicleId, token)
                    ?? throw AttendanceOperation.Missing("vehicle_not_found");
                var current = await ServiceEligibilityAsync(record.EmployeeId, new(record.VehicleId, record.ServiceKind,
                    record.ServiceDate, record.OdometerKm, retained.ClaimedOutstationKm, record.AuthorizedWorkshopReference,
                    record.EvidencePrivateReference, record.ClaimedAmount, record.Currency), vehicle.VehicleType, token, record.PolicyVersionId);
                if (JsonSerializer.Serialize(retained) != JsonSerializer.Serialize(current))
                    throw AttendanceOperation.Conflict("vehicle_service_eligibility_changed");

                var decision = record.ApprovalRequest.Decide(
                    ApprovalState.Approved,
                    actor.Identity.AccountId.ToString(),
                    actor.RoleCode,
                    "Service eligibility approved; no payment or settlement recorded",
                    actor.Now, record.EligibilityCalculationPrivateReference);

                repository.AddDecision(decision);
                await repository.SaveChangesAsync(token);

                var response = new VehicleServiceRecordResponse(
                    record.Id,
                    record.VehicleId,
                    record.Vehicle?.RegistrationNumber ?? string.Empty,
                    record.EmployeeId,
                    record.Employee?.FullLegalName ?? string.Empty,
                    record.ServiceKind,
                    record.ServiceDate,
                    record.OdometerKm,
                    record.ApprovedOutstationKm,
                    record.AuthorizedWorkshopReference,
                    record.EvidencePrivateReference,
                    record.ClaimedAmount,
                    record.Currency,
                    record.CreatedAt,
                    ApprovalState.Approved.ToString(),
                    record.ApprovalRequestId);

                return new AttendanceChange<VehicleServiceRecordResponse>(record.Id, null, response);
            },
            ct,
            self: false,
            beforeReplay: (actor, token) => RequireServiceReviewIntegrity(serviceRecordId, actor, token, historicalReplay: true),
            beforeCommit: (actor, token) => RequireServiceReviewIntegrity(serviceRecordId, actor, token));

    public Task<VehicleFuelPage<VehicleServiceRecordResponse>> PageServiceRecordsAsync(
        Guid? vehicleId,
        Guid? employeeId,
        ApprovalState? state,
        int page,
        int pageSize,
        AuthRequestContext context,
        CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        return operation.ReadAsync(
            VehicleFuelPermissions.Maintenance,
            "vehicle.service.page",
            employeeId,
            context,
            async (_, token) =>
            {
                var p = page;
                var size = pageSize;
                var skip = (p - 1) * size;

                var (items, total) = await repository.PageServiceRecordsAsync(vehicleId, employeeId, state, skip, size, token);
                var dtos = items.Select(r => new VehicleServiceRecordResponse(
                    r.Id,
                    r.VehicleId,
                    r.Vehicle?.RegistrationNumber ?? string.Empty,
                    r.EmployeeId,
                    r.Employee?.FullLegalName ?? string.Empty,
                    r.ServiceKind,
                    r.ServiceDate,
                    r.OdometerKm,
                    r.ApprovedOutstationKm,
                    r.AuthorizedWorkshopReference,
                    r.EvidencePrivateReference,
                    r.ClaimedAmount,
                    r.Currency,
                    r.CreatedAt,
                    r.ApprovalRequest?.State.ToString() ?? ApprovalState.Pending.ToString(),
                    r.ApprovalRequestId)).ToArray();

                return new VehicleFuelPage<VehicleServiceRecordResponse>(dtos, p, size, total, skip + size < total);
            },
            ct,
            self: false);
    }

    #endregion

    #region Incidents & Liability

    public Task<VehicleIncidentResponse> ReportIncidentAsync(
        ReportVehicleIncidentRequest request,
        string key,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.WriteAsync(
            VehicleFuelPermissions.Self,
            "vehicle.incident.report",
            null,
            key,
            request,
            $"Report vehicle incident on custody {request.VehicleCustodyId}",
            context,
            async (actor, token) =>
            {
                var custody = await repository.FindCustodyAsync(request.VehicleCustodyId, token)
                    ?? throw AttendanceOperation.Missing("custody_not_found");

                if (custody.EmployeeId != actor.Identity.EmployeeId)
                    throw AttendanceOperation.Denied();

                if (request.OccurredAt > actor.Now)
                    throw AttendanceOperation.Invalid("incident_in_future");
                if (request.ProposedEmployeeLiability.HasValue != !string.IsNullOrWhiteSpace(request.Currency))
                    throw AttendanceOperation.Invalid("incident_liability_currency_required");

                if (request.OccurredAt < custody.IssuedAt ||
                    (custody.ReturnedAt is not null && request.OccurredAt > custody.ReturnedAt))
                    throw AttendanceOperation.Invalid("incident_outside_custody_window");

                var incident = new VehicleIncident(
                    request.VehicleCustodyId,
                    request.OccurredAt,
                    request.IncidentEvidenceReference,
                    request.ThirdPartyDetailsReference,
                    request.InsuranceClaimReference,
                    request.ProposedEmployeeLiability,
                    request.Currency?.ToUpperInvariant(),
                    actor.Identity.AccountId.ToString(),
                    actor.Now);

                var approval = new ApprovalRequest(
                    IncidentRequestType,
                    incident.Id,
                    actor.Identity.AccountId.ToString(),
                    $"vehicle:incident:{custody.EmployeeId:N}:{incident.Id:N}",
                    1,
                    actor.Now);

                incident.Submit(approval);
                repository.AddIncident(incident, approval);
                await repository.SaveChangesAsync(token);

                var response = new VehicleIncidentResponse(
                    incident.Id,
                    incident.VehicleCustodyId,
                    incident.OccurredAt,
                    incident.IncidentEvidencePrivateReference,
                    incident.ThirdPartyDetailsEncryptedReference,
                    incident.InsuranceClaimReference,
                    incident.ProposedEmployeeLiability,
                    incident.Currency,
                    incident.CreatedAt,
                    approval.State.ToString(),
                    approval.Id);

                return new AttendanceChange<VehicleIncidentResponse>(incident.Id, null, response);
            },
            ct,
            self: true);

    public Task<VehicleFuelPage<VehicleIncidentResponse>> PageIncidentsAsync(
        Guid? custodyId,
        ApprovalState? state,
        int page,
        int pageSize,
        AuthRequestContext context,
        CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        if (state.HasValue && !Enum.IsDefined(state.Value)) throw AttendanceOperation.Invalid("vehicle_incident_state_invalid");
        return operation.ReadAsync(
            VehicleFuelPermissions.Incident,
            "vehicle.incidents.page",
            null,
            context,
            async (_, token) =>
            {
                // Effective outcomes live in protected amendment evidence, not the original
                // report's approval row. Project before filtering/counting; never truncate totals.
                var (items, sourceTotal) = await repository.PageIncidentsAsync(custodyId, null, 0, 1001, token);
                if (sourceTotal > 1000 || items.Count > 1000) throw AttendanceOperation.Conflict("vehicle_incident_projection_limit");
                if (sourceTotal != items.Count) throw AttendanceOperation.Conflict("vehicle_incident_projection_incomplete");
                var projected = new List<VehicleIncidentResponse>();
                foreach (var incident in items)
                {
                    var initialState = incident.ApprovalRequest?.State ?? ApprovalState.Pending;
                    var current = initialState is ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected
                        ? (await IncidentHistory(incident.Id, token)).Current : null;
                    var effectiveState = current?.ApprovalState ?? initialState.ToString();
                    if (state.HasValue && effectiveState != state.Value.ToString()) continue;
                    projected.Add(new(incident.Id, incident.VehicleCustodyId, incident.OccurredAt,
                        incident.IncidentEvidencePrivateReference, incident.ThirdPartyDetailsEncryptedReference,
                        current is null ? incident.InsuranceClaimReference : current.InsuranceClaimReference,
                        current is null ? incident.ProposedEmployeeLiability : current.ProposedEmployeeLiability,
                        current is null ? incident.Currency : current.Currency, incident.CreatedAt, effectiveState,
                        current?.ApprovalRequestId ?? incident.ApprovalRequestId));
                }
                var skip = (page - 1) * pageSize;
                var dtos = projected.OrderByDescending(i => i.OccurredAt).ThenByDescending(i => i.Id).Skip(skip).Take(pageSize).ToArray();
                return new VehicleFuelPage<VehicleIncidentResponse>(dtos, page, pageSize, projected.Count, skip + pageSize < projected.Count);
            },
            ct,
            self: false);
    }

    #endregion

    private static void ValidatePage(int page, int pageSize)
    {
        if (page is < 1 or > 4096 || pageSize is < 1 or > 100)
            throw AttendanceOperation.Invalid("invalid_page");
    }

    private static string MileagePurpose(Guid tripId) => $"vehicle:mileage:{tripId:N}";

    private static string MileageIdempotencyKey(string approveKey) =>
        (approveKey.Length <= 92 ? approveKey : approveKey[..92]) + ":mileage";

    private static string Kilometers(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static void RequireIndependentReviewer(Guid employeeId, string requester, AttendanceActor actor)
    {
        if (employeeId == actor.Identity.EmployeeId || requester == actor.Identity.AccountId.ToString())
            throw new AuthenticationFailure("vehicle_self_decision_forbidden", 403);
    }

    private async Task RequireTripCustodyAsync(Guid vehicleId, Guid employeeId, DateTimeOffset from,
        DateTimeOffset to, decimal odometerStart, decimal odometerEnd, CancellationToken token)
    {
        if (to <= from) throw AttendanceOperation.Invalid("trip_end_must_follow_start");
        var rows = await repository.FindCustodiesOverlappingAsync(vehicleId, from, to, token);
        if (rows.Count != 1 || rows[0].EmployeeId != employeeId || rows[0].VehicleId != vehicleId ||
            rows[0].IssuedAt > from || rows[0].ReturnedAt < to || odometerStart < rows[0].IssueOdometerKm ||
            rows[0].ReturnOdometerKm < odometerEnd)
            throw AttendanceOperation.Conflict("vehicle_custody_not_valid_for_date");
    }

    private async Task RequireServiceCustodyAsync(Guid vehicleId, Guid employeeId, DateOnly date, decimal odometerKm, CancellationToken token)
    {
        RosterSchedule.ValidateDate(date);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(RosterSchedule.TimeZoneId);
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var rows = await repository.FindCustodiesOverlappingAsync(vehicleId,
            new(start, zone.GetUtcOffset(start)), new(end, zone.GetUtcOffset(end)), token);
        // A date-only service claim cannot disambiguate a transfer on that same date.
        if (rows.Count != 1 || rows[0].VehicleId != vehicleId || rows[0].EmployeeId != employeeId ||
            odometerKm < rows[0].IssueOdometerKm || rows[0].ReturnOdometerKm < odometerKm)
            throw AttendanceOperation.Conflict("vehicle_custody_not_valid_for_date");
    }

    private sealed record VehicleFuelPolicyValues(
        decimal? KmPerLiter,
        decimal? TuningKm,
        decimal? OverhaulKm,
        decimal? FuelPricePerLiter,
        VehicleWorkflowPolicy? Workflow);

    private sealed record VehicleMileageSnapshot(int SchemaVersion, Guid TripId, Guid VehicleId, Guid EmployeeId,
        VehicleJourneyPurpose Purpose, DateOnly BusinessDate, decimal OdometerStartKm, decimal OdometerEndKm,
        decimal ApprovedBusinessKm, decimal ExcludedCommuteKm, decimal OutstationKm, decimal AccumulatedOutstationKmBefore,
        Guid PolicyVersionId, Guid ApprovalRequestId, Guid ActorAccountId, DateTimeOffset RecordedAt,
        decimal MapDistanceKm, string RouteEvidenceReference, Guid? CommuteDistanceId,
        string DistanceBasis, VehicleStaffEligibilityRule StaffEligibility);

    private async Task<(PolicyVersion Version, VehicleFuelPolicyValues Values)> LoadPolicyAsync(
        DateOnly date, CancellationToken token, Guid? retainedVersionId = null)
    {
        var version = (retainedVersionId is Guid id
            ? await repository.FindVehicleFuelPolicyVersionAsync(id, token)
            : await repository.GetActiveVehicleFuelPolicyVersionAsync(date, token))
            ?? throw AttendanceOperation.Conflict("vehicle_fuel_policy_not_configured");
        if (version.EffectiveFrom > date || version.EffectiveTo <= date ||
            retainedVersionId.HasValue && version.Id != retainedVersionId)
            throw AttendanceOperation.Conflict("vehicle_fuel_policy_not_configured");
        return (version, ParsePolicyValues(version));
    }

    private static VehicleFuelPolicyValues ParsePolicyValues(PolicyVersion version)
    {
        if (string.IsNullOrWhiteSpace(version.RulesJson))
            throw AttendanceOperation.Conflict("vehicle_fuel_policy_not_configured");

        try
        {
            using var doc = JsonDocument.Parse(version.RulesJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw AttendanceOperation.Conflict("vehicle_fuel_policy_not_configured");
            return new VehicleFuelPolicyValues(
                TryGetPositiveDecimal(doc.RootElement, "StandardFuelKmPerLiter"),
                TryGetPositiveDecimal(doc.RootElement, "TuningKm"),
                TryGetPositiveDecimal(doc.RootElement, "OverhaulKm"),
                TryGetPositiveDecimal(doc.RootElement, "ClaimFuelPricePerLiter"),
                VehicleWorkflowPolicy.Parse(doc.RootElement));
        }
        catch (JsonException)
        {
            throw AttendanceOperation.Conflict("vehicle_fuel_policy_not_configured");
        }
    }

    private static decimal? TryGetPositiveDecimal(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDecimal(out var number) &&
        number > 0
            ? number
            : null;
}

public interface IVehicleFuelMileageWriter
{
    void AddMileageEntry(VehicleMileageEntry entry);
}
