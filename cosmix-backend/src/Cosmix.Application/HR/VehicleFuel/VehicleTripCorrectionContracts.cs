using System.Text.Json.Serialization;

namespace Cosmix.Application.HR.VehicleFuel;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitVehicleTripAttributionCorrectionRequest([property: JsonRequired] string ExpectedTripVersion,
    [property: JsonRequired] Guid? ExpectedLatestRequestId, [property: JsonRequired] string ExpectedSourceFingerprint,
    [property: JsonRequired] IReadOnlyList<VehicleTripSegmentRequest> Segments,
    [property: JsonRequired] string EvidenceReference, [property: JsonRequired] string Reason);
public sealed record VehicleTripAttributionCorrectionContext(Guid TripId, Guid EmployeeId, Guid TripApprovalRequestId,
    string TripVersion, Guid MileageEntryId, string SourceFingerprint, Guid? LatestRequestId);
public sealed record VehicleTripAttributionCorrectionView(Guid RequestId, int Revision, string Version, string State,
    Guid TripId, Guid MileageEntryId, string OriginalSourceFingerprint, IReadOnlyList<VehicleTripSegment> Segments,
    Guid AuthorityPolicyVersionId, string EvidenceReference, string Reason, Guid RequestedByAccountId,
    DateTimeOffset RequestedAt, Guid? DecisionId);
