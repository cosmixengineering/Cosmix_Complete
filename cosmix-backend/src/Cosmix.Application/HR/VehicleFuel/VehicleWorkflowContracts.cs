using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Cosmix.Application.HR.VehicleFuel;

public enum VehicleIncidentReviewOutcome
{
    ReturnedForEvidence = 1,
    Rejected = 2,
    NoLiabilityProposed = 3,
    ProposedLiabilityRecorded = 4,
    InsuranceReviewRequired = 5
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviewVehicleIncidentRequest(
    Guid ExpectedApprovalRequestId,
    VehicleIncidentReviewOutcome Outcome,
    [Required, StringLength(500)] string EvidenceReference,
    [Required, StringLength(1000)] string Reason,
    decimal? ProposedEmployeeLiability,
    string? Currency,
    [StringLength(200)] string? InsuranceClaimReference);

public sealed record VehicleIncidentReviewResponse(Guid IncidentId, Guid ApprovalRequestId, Guid DecisionId,
    VehicleIncidentReviewOutcome Outcome, string ApprovalState, decimal? ProposedEmployeeLiability,
    string? Currency, string? InsuranceClaimReference, string EvidenceReference, string Reason,
    Guid PolicyVersionId, DateTimeOffset RecordedAt, bool EmployeeDeductionAuthorized, bool InsuranceSettled);
