using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.VehicleFuel;

namespace Cosmix.Application.HR.VehicleFuel;

public static class VehicleFuelPermissions
{
    public const string Self = "attendance.vehicle.self";
    public const string Manage = "attendance.vehicle.manage";
    public const string Custody = "attendance.vehicle.custody.manage";
    public const string Trips = "attendance.vehicle.trips.manage";
    public const string Fuel = "attendance.vehicle.fuel.manage";
    public const string Maintenance = "attendance.vehicle.maintenance.manage";
    public const string Incident = "attendance.vehicle.incident.manage";
    public const string Commute = "attendance.vehicle.commute.manage";

    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Self, "Read own vehicle custody, trips, fuel claims, and maintenance records", false, false, true),
        new(Manage, "Manage company vehicle assets and directory", false, true, false),
        new(Custody, "Issue vehicle custody, record returns, and inspect vehicle handover condition", false, true, false),
        new(Trips, "Submit, review, and approve authorized vehicle work trips", false, true, false),
        new(Fuel, "Submit, calculate, review, and settle fuel claims", true, true, false),
        new(Maintenance, "Manage motorcycle maintenance eligibility milestones", true, true, false),
        new(Incident, "Record vehicle incidents, damage evidence, third-party claims, and insurance", true, true, false),
        new(Commute, "Configure employee baseline home-to-office commute distance", false, true, false)
    ];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateVehicleRequest(
    [Required, StringLength(40)] string RegistrationNumber,
    [Required, StringLength(60)] string VehicleType,
    [Required, StringLength(150)] string MakeModel,
    [StringLength(100)] string? AssetReference);

public sealed record VehicleResponse(
    Guid Id,
    string RegistrationNumber,
    string VehicleType,
    string MakeModel,
    string? AssetReference,
    bool IsActive);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IssueCustodyRequest(
    Guid VehicleId,
    Guid EmployeeId,
    DateTimeOffset IssuedAt,
    [Range(0, 1_000_000)] decimal IssueOdometerKm,
    [Required, StringLength(500)] string IssueConditionReference);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReturnCustodyRequest(
    DateTimeOffset ReturnedAt,
    [Range(0, 1_000_000)] decimal ReturnOdometerKm,
    [Required, StringLength(500)] string ReturnConditionReference);

public sealed record VehicleCustodyResponse(
    Guid Id,
    Guid VehicleId,
    string RegistrationNumber,
    Guid EmployeeId,
    string EmployeeName,
    DateTimeOffset IssuedAt,
    decimal IssueOdometerKm,
    string IssueConditionReference,
    DateTimeOffset? ReturnedAt,
    decimal? ReturnOdometerKm,
    string? ReturnConditionReference,
    bool IsActive);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetCommuteDistanceRequest(
    Guid EmployeeId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    [Range(0, 500)] decimal OneWayKm,
    [Range(0, 1000)] decimal DailyRoundTripKm,
    [Required, StringLength(500)] string RouteReference);

public sealed record EmployeeCommuteDistanceResponse(
    Guid Id,
    Guid EmployeeId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal OneWayKm,
    decimal DailyRoundTripKm,
    string RouteReference);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitVehicleTripRequest(
    Guid VehicleId,
    VehicleJourneyPurpose Purpose,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    [Required, StringLength(200)] string OriginLabel,
    [Required, StringLength(200)] string DestinationLabel,
    [Range(0, 1_000_000)] decimal OdometerStartKm,
    [Range(0, 1_000_000)] decimal OdometerEndKm,
    [Range(0, 50_000)] decimal MapDistanceKm,
    [Required, StringLength(500)] string RouteEvidenceReference,
    [Required, StringLength(150)] string WorkReference);

public sealed record VehicleTripResponse(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    Guid VehicleId,
    string RegistrationNumber,
    VehicleJourneyPurpose Purpose,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    string OriginLabel,
    string DestinationLabel,
    decimal OdometerStartKm,
    decimal OdometerEndKm,
    decimal TripDistanceKm,
    decimal MapDistanceKm,
    string RouteEvidenceReference,
    string WorkReference,
    DateTimeOffset CreatedAt,
    string ApprovalState,
    Guid? ApprovalRequestId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitFuelClaimRequest(
    Guid VehicleTripId,
    [Required, StringLength(10)] string Currency,
    [StringLength(500)] string? ReceiptReference);

public sealed record FuelClaimResponse(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    Guid VehicleTripId,
    decimal EligibleDistanceKm,
    decimal ExcludedCommuteKm,
    decimal FuelLiters,
    decimal ClaimedAmount,
    string Currency,
    string CalculationReference,
    string? ReceiptReference,
    DateTimeOffset CreatedAt,
    string ApprovalState,
    Guid? ApprovalRequestId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitServiceRecordRequest(
    Guid VehicleId,
    VehicleServiceKind ServiceKind,
    DateOnly ServiceDate,
    [Range(0, 1_000_000)] decimal OdometerKm,
    [Range(0, 50_000)] decimal ApprovedOutstationKm,
    [Required, StringLength(200)] string AuthorizedWorkshopReference,
    [Required, StringLength(500)] string EvidenceReference,
    [Range(0, 10_000_000)] decimal ClaimedAmount,
    [Required, StringLength(10)] string Currency);

public sealed record VehicleServiceRecordResponse(
    Guid Id,
    Guid VehicleId,
    string RegistrationNumber,
    Guid EmployeeId,
    string EmployeeName,
    VehicleServiceKind ServiceKind,
    DateOnly ServiceDate,
    decimal OdometerKm,
    decimal ApprovedOutstationKm,
    string AuthorizedWorkshopReference,
    string EvidenceReference,
    decimal ClaimedAmount,
    string Currency,
    DateTimeOffset CreatedAt,
    string ApprovalState,
    Guid? ApprovalRequestId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReportVehicleIncidentRequest(
    Guid VehicleCustodyId,
    DateTimeOffset OccurredAt,
    [Required, StringLength(500)] string IncidentEvidenceReference,
    [StringLength(500)] string? ThirdPartyDetailsReference,
    [StringLength(200)] string? InsuranceClaimReference,
    [Range(0, 10_000_000)] decimal? ProposedEmployeeLiability,
    string? Currency);

public sealed record VehicleIncidentResponse(
    Guid Id,
    Guid VehicleCustodyId,
    DateTimeOffset OccurredAt,
    string IncidentEvidenceReference,
    string? ThirdPartyDetailsReference,
    string? InsuranceClaimReference,
    decimal? ProposedEmployeeLiability,
    string? Currency,
    DateTimeOffset CreatedAt,
    string ApprovalState,
    Guid? ApprovalRequestId);

public sealed record VehicleFuelPage<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    bool HasMore);
