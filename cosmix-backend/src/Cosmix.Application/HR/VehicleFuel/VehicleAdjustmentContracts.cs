using System.Text.Json.Serialization;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.HR.VehicleFuel;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewVehicleMonthlyAdjustmentRequest([property: JsonRequired] Guid EmployeeId,
    [property: JsonRequired] DateOnly MonthStart)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<VehicleMonthlyAssignmentSelection>? AssignmentSelections { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitVehicleMonthlyAdjustmentRequest([property: JsonRequired] Guid EmployeeId,
    [property: JsonRequired] DateOnly MonthStart, [property: JsonRequired] Guid? ExpectedLatestRequestId,
    [property: JsonRequired] string ExpectedPreviewFingerprint, [property: JsonRequired] string EvidenceReference,
    [property: JsonRequired] string Reason)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<VehicleMonthlyAssignmentSelection>? AssignmentSelections { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleMonthlyAssignmentSelection([property: JsonRequired] DateOnly BusinessDate,
    [property: JsonRequired] Guid MileageEntryId, [property: JsonRequired] string ExpectedContextFingerprint);
public sealed record VehicleMonthlyAssignmentDay(DateOnly BusinessDate, IReadOnlyList<VehicleMonthlyDistanceLine> Candidates, string ContextFingerprint);
public sealed record VehicleMonthlyAssignmentContext(Guid VehicleId, Guid EmployeeId, DateOnly MonthStart, IReadOnlyList<VehicleMonthlyAssignmentDay> Days);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviewVehicleAdjustmentRequest([property: JsonRequired] string ExpectedVersion,
    [property: JsonRequired] ApprovalState Decision, [property: JsonRequired] string EvidenceReference,
    [property: JsonRequired] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProposeVehicleIncidentAmendmentRequest([property: JsonRequired] Guid ExpectedDecisionId,
    [property: JsonRequired] VehicleIncidentReviewOutcome Outcome, [property: JsonRequired] string EvidenceReference,
    [property: JsonRequired] string Reason, [property: JsonRequired] decimal? ProposedEmployeeLiability,
    [property: JsonRequired] string? Currency, [property: JsonRequired] string? InsuranceClaimReference);

public sealed record VehicleMonthlyDistanceLine(Guid MileageEntryId, Guid TripId, DateOnly BusinessDate,
    Guid PolicyVersionId, decimal ApprovedBusinessKm, decimal EligibleKm, Guid? CommuteDistanceId,
    decimal? BaselineKm, decimal NearerReductionKm, string EvidenceFingerprint);
public sealed record VehicleMonthlyDailyCommute(DateOnly BusinessDate, Guid PolicyVersionId, Guid CommuteDistanceId,
    IReadOnlyList<Guid> MileageEntryIds, decimal ApprovedBusinessKm, decimal BaselineKm, decimal RetainedEligibleKm,
    decimal CombinedEligibleKm, decimal EligibilityAdjustmentKm, decimal NearerReductionKm);
public sealed record VehicleMonthlyCalculation(Guid VehicleId, Guid EmployeeId, DateOnly MonthStart,
    Guid PolicyVersionId, decimal EligibleKm, decimal NearerReductionKm, decimal AdjustmentKm, decimal AdjustedEligibleKm,
    VehicleMonthlyAdjustmentRule Rule, IReadOnlyList<VehicleMonthlyDistanceLine> Lines, string SourceFingerprint)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<VehicleMonthlyPolicyPartition>? Partitions { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<VehicleMonthlyAssignmentSelection>? AssignmentSelections { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<VehicleMonthlyDailyCommute>? DailyCommuteBreakdown { get; init; }
}
public sealed record VehicleMonthlyPolicyPartition(Guid PolicyVersionId, VehicleMonthlyAdjustmentRule Rule,
    decimal EligibleKm, decimal NearerReductionKm, decimal AdjustmentKm, decimal AdjustedEligibleKm);
public sealed record VehicleMonthlyAdjustmentPreview(VehicleMonthlyCalculation Calculation, Guid? LatestRequestId,
    Guid? LatestApprovedRequestId, string PreviewFingerprint);
public sealed record VehicleMonthlyAdjustmentView(Guid RequestId, int Revision, string Version, string State,
    VehicleMonthlyCalculation Calculation, Guid? PreviousApprovedRequestId, string EvidenceReference, string Reason,
    Guid RequestedByAccountId, DateTimeOffset RequestedAt, Guid? DecisionId, string? ReviewReason);
public sealed record VehicleMonthlyEntitlement(Guid VehicleId, Guid EmployeeId, DateOnly MonthStart, string State,
    Guid? LatestRequestId, VehicleMonthlyAdjustmentView? RetainedApprovedRevision, decimal? AdjustedEligibleKm, string? BlockCode);
public sealed record VehicleIncidentAmendmentView(Guid RequestId, int Revision, string Version, string State,
    Guid IncidentId, Guid PreviousDecisionId, Guid PolicyVersionId, ProposeVehicleIncidentAmendmentRequest Proposal,
    Guid RequestedByAccountId, DateTimeOffset RequestedAt, Guid? DecisionId, string? ReviewReason);
public sealed record VehicleIncidentReviewHistory(VehicleIncidentReviewResponse Original, VehicleIncidentReviewResponse Current,
    IReadOnlyList<VehicleIncidentAmendmentView> Amendments);
