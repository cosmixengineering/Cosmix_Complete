using System.Text.Json.Serialization;

namespace Cosmix.Application.HR.VehicleFuel;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CloseHistoricalFuelClaimRequest(
    [property: JsonRequired] string ExpectedVersion,
    [property: JsonRequired] string ExpectedSourceFingerprint,
    [property: JsonRequired] string EvidenceReference,
    [property: JsonRequired] string Reason);

public sealed record HistoricalFuelClosureContext(Guid ResourceId, string Version,
    string SourceFingerprint, IReadOnlyList<Guid> CorrectionRequestIds);

public sealed record HistoricalFuelClosureView(Guid ResourceId, Guid RequestId, string Version,
    string State, string SourceFingerprint, Guid? DecisionId);
