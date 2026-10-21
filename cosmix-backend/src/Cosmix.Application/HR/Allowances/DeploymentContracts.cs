using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Allowances;

namespace Cosmix.Application.HR.Allowances;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeploymentRequest(DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc,
    string Destination, string DeploymentCategory, string WrittenEvidence, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeploymentBreakRequest(string ExpectedDeploymentVersion, DateTimeOffset DepartedAtUtc,
    DateTimeOffset? ReturnedAtUtc, string WrittenEvidence, string Reason, Guid? SupersedesBreakId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeploymentDecisionRequest(string ExpectedDeploymentVersion, string ExpectedApprovalVersion,
    ApprovalState Decision, string WrittenEvidence, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeploymentClaimRequest(string ExpectedDeploymentVersion, string ExpectedFingerprint,
    DateOnly PayableDate, string Reason);

public sealed record DeploymentResponse(Guid Id, Guid EmployeeId, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc,
    string State, string Version, string ApprovalVersion, int Revision, Guid? FinalizedClaimId);
public sealed record DeploymentBreakResponse(Guid Id, DateTimeOffset DepartedAtUtc, DateTimeOffset? ReturnedAtUtc,
    string State, string ApprovalVersion);
public sealed record DeploymentClaimResponse(Guid Id, Guid DeploymentId, decimal Amount, string Currency,
    DateOnly PayableDate, Guid PolicyVersionId, string State, string ApprovalVersion);
public sealed record DeploymentDetail(DeploymentResponse Deployment, DeploymentRequest Proposal,
    IReadOnlyList<DeploymentBreakResponse> HomeBreaks, IReadOnlyList<DeploymentClaimResponse> Claims);
public sealed record DeploymentCalculation(string State, string Fingerprint, DeploymentValuationResult? Valuation,
    Guid? PolicyVersionId, IReadOnlyList<DeploymentPolicyDay> PolicyDays, IReadOnlyList<Guid> ApprovedBreakIds);
public sealed record DeploymentPolicyDay(DateOnly Date, Guid PolicyVersionId, IReadOnlyList<Guid> Assignments,
    string Fingerprint, string CanonicalRules);
public sealed record DeploymentEvidence(int SchemaVersion, Guid DeploymentId, Guid EmployeeId, Guid RequesterId,
    DeploymentRequest Proposal, OvertimeEmploymentSource Employment, IReadOnlyList<AttendanceResourceScope> OriginalScopes);
public sealed record DeploymentBreakEvidence(int SchemaVersion, Guid Id, Guid DeploymentId, Guid EmployeeId,
    Guid RequesterId, DeploymentBreakRequest Proposal);
public sealed record DeploymentClaimEvidence(int SchemaVersion, Guid Id, Guid DeploymentId, Guid EmployeeId,
    Guid RequesterId, DeploymentClaimRequest Proposal, DeploymentCalculation Calculation, DeploymentEvidence Deployment,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DeploymentClaimReturn? PreviousReturn = null);
public sealed record DeploymentClaimReturn(Guid ApprovalRequestId, string DecisionReference);
public sealed record DeploymentDecisionEvidence(int SchemaVersion, Guid SubjectId, Guid DeploymentId, Guid EmployeeId,
    Guid RequesterId, Guid ReviewerId, string ReviewerRole, ApprovalState Decision, string WrittenEvidence,
    string Reason, DateTimeOffset AtUtc, string SourceFingerprint);

public sealed record DeploymentRow(OutstationDeployment Deployment, ApprovalRequest Approval, ApprovalDecision? Decision);
public sealed record DeploymentBreakRow(DeploymentHomeBreakRequest Break, ApprovalRequest Approval, ApprovalDecision? Decision);
public sealed record DeploymentClaimRow(AllowanceClaim Claim, ApprovalRequest Approval, ApprovalDecision? Decision, Guid DeploymentId);

public interface IDeploymentRepository
{
    Task<DeploymentRow?> FindAsync(Guid employeeId, Guid deploymentId, CancellationToken ct);
    Task<DeploymentRow?> FindByApprovalAsync(Guid employeeId, Guid deploymentApprovalId, CancellationToken ct);
    Task<IReadOnlyList<DeploymentRow>> PageAsync(Guid employeeId, int skip, int take, CancellationToken ct);
    Task<IReadOnlyList<DeploymentBreakRow>> BreaksAsync(Guid deploymentId, CancellationToken ct);
    Task<IReadOnlyList<DeploymentClaimRow>> ClaimsAsync(Guid deploymentId, CancellationToken ct);
    Task<bool> OverlapAsync(Guid employeeId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<bool> MealOverlapAsync(Guid employeeId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    void Add(OutstationDeployment deployment, ApprovalRequest approval);
    void Add(DeploymentHomeBreakRequest homeBreak, ApprovalRequest approval);
    void Add(ApprovalRequest approval, AllowanceClaimSubmission submission);
    void Add(ApprovalDecision decision);
    void ExpectVersion(OutstationDeployment deployment, byte[] version);
    void ExpectVersion(ApprovalRequest approval, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}
