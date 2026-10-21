using System.Text.Json.Serialization;

namespace Cosmix.Application.HR.VehicleFuel;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleTripSegmentRequest([property: JsonRequired] DateOnly BusinessDate,
    [property: JsonRequired] DateTimeOffset StartedAt, [property: JsonRequired] DateTimeOffset EndedAt,
    [property: JsonRequired] decimal OdometerStartKm, [property: JsonRequired] decimal OdometerEndKm,
    [property: JsonRequired] decimal MapDistanceKm, [property: JsonRequired] Guid PolicyVersionId,
    [property: JsonRequired] Guid? CommuteDistanceId, [property: JsonRequired] string EvidenceReference);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitVehicleTripAttributionRequest([property: JsonRequired] string ExpectedTripVersion,
    [property: JsonRequired] Guid? ExpectedLatestRequestId, [property: JsonRequired] IReadOnlyList<VehicleTripSegmentRequest> Segments,
    [property: JsonRequired] string EvidenceReference, [property: JsonRequired] string Reason);
public sealed record VehicleTripSegment(VehicleTripSegmentRequest Source, decimal ApprovedBusinessKm,
    decimal ExcludedCommuteKm, decimal EligibleKm, decimal? BaselineKm, VehicleTripAttributionRule Rule,
    string EvidenceFingerprint);
public sealed record VehicleTripAttributionView(Guid RequestId, int Revision, string Version, string State,
    Guid TripId, Guid TripApprovalRequestId, IReadOnlyList<VehicleTripSegment> Segments,
    string EvidenceReference, string Reason, Guid RequestedByAccountId, DateTimeOffset RequestedAt, Guid? DecisionId);
public sealed record VehicleTripAttributionContext(Guid TripId, Guid EmployeeId, Guid TripApprovalRequestId,
    string TripState, string TripVersion, Guid? LatestRequestId);
