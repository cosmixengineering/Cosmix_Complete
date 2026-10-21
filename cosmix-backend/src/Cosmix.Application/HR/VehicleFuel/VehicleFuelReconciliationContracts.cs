using System.Text.Json.Serialization;

namespace Cosmix.Application.HR.VehicleFuel;

public sealed record FuelReconciliationLine(DateOnly BusinessDate, Guid PolicyVersionId, decimal EligibleKm,
    decimal ExcludedCommuteKm, decimal KmPerLiter, decimal PricePerLiter, decimal FuelLiters, decimal Amount);
public sealed record FuelReconciliationContext(Guid ResourceId, Guid EmployeeId, Guid TripId,
    Guid OriginalDecisionId, Guid CorrectionRequestId, Guid? LatestRequestId, string SourceFingerprint,
    decimal OriginalAmount, decimal CorrectedAmount, decimal DifferenceAmount, string Currency,
    IReadOnlyList<FuelReconciliationLine> Lines);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitFuelReconciliationRequest([property: JsonRequired] Guid? ExpectedLatestRequestId,
    [property: JsonRequired] string ExpectedSourceFingerprint, [property: JsonRequired] string EvidenceReference,
    [property: JsonRequired] string Reason);
public sealed record FuelReconciliationView(Guid RequestId, int Revision, string Version, string State,
    FuelReconciliationContext Calculation, string EvidenceReference, string Reason, Guid RequestedByAccountId,
    DateTimeOffset RequestedAt, Guid? DecisionId, string? ReviewReason, string SettlementState);
