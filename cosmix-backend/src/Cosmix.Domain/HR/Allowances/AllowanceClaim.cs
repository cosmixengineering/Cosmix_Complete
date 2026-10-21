using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Domain.HR.Allowances;

/// <summary>EligibilityKey identifies the server-evaluated duty cycle/day. Exclusive meal/outstation eligibility still requires transactional application validation.</summary>
public sealed class AllowanceClaim
{
    private readonly List<AllowanceClaimSubmission> _submissions = [];
    private AllowanceClaim() { }

    public AllowanceClaim(Guid employeeId, AllowanceKind kind, Guid employeeAssignmentId, string deploymentCategoryCode, Guid? deploymentApprovalRequestId, Guid? workSessionId, DateTimeOffset eligibilityPeriodStart, DateTimeOffset eligibilityPeriodEnd, string eligibilityKey, decimal claimedAmount, string currency, DateOnly payableDate, DateOnly? payrollPeriodStart, Guid policyVersionId, string calculationPrivateReference, string? evidencePrivateReference, string requestedByUserId, DateTimeOffset createdAt, Guid? id = null)
    {
        if (eligibilityPeriodEnd <= eligibilityPeriodStart) throw new ArgumentException("Eligibility end must follow start.");
        if (kind == AllowanceKind.OutstationDaily && !deploymentApprovalRequestId.HasValue || (kind is AllowanceKind.OfficeMeal or AllowanceKind.SiteDinner or AllowanceKind.SiteBreakfast or AllowanceKind.SiteLunch) && !workSessionId.HasValue) throw new ArgumentException("Meal claims require a verified session; outstation claims require deployment approval.");
        Id = id.HasValue ? ModelGuard.Id(id.Value, nameof(id)) : Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        Kind = Enum.IsDefined(kind) ? kind : throw new ArgumentOutOfRangeException(nameof(kind));
        EmployeeAssignmentId = ModelGuard.Id(employeeAssignmentId, nameof(employeeAssignmentId));
        DeploymentCategoryCode = ModelGuard.Required(deploymentCategoryCode, 60, nameof(deploymentCategoryCode));
        DeploymentApprovalRequestId = deploymentApprovalRequestId.HasValue ? ModelGuard.Id(deploymentApprovalRequestId.Value, nameof(deploymentApprovalRequestId)) : null;
        WorkSessionId = workSessionId.HasValue ? ModelGuard.Id(workSessionId.Value, nameof(workSessionId)) : null;
        EligibilityPeriodStart = ModelGuard.Utc(eligibilityPeriodStart);
        EligibilityPeriodEnd = ModelGuard.Utc(eligibilityPeriodEnd);
        EligibilityKey = ModelGuard.Required(eligibilityKey, 120, nameof(eligibilityKey));
        ClaimedAmount = HrModelGuard.Money(claimedAmount, nameof(claimedAmount));
        Currency = HrModelGuard.Currency(currency);
        PayableDate = payableDate;
        PayrollPeriodStart = payrollPeriodStart;
        PolicyVersionId = ModelGuard.Id(policyVersionId, nameof(policyVersionId));
        CalculationPrivateReference = ModelGuard.Required(calculationPrivateReference, 500, nameof(calculationPrivateReference));
        EvidencePrivateReference = ModelGuard.Optional(evidencePrivateReference, 500, nameof(evidencePrivateReference));
        RequestedByUserId = ModelGuard.Required(requestedByUserId, 200, nameof(requestedByUserId));
        CreatedAt = ModelGuard.Utc(createdAt);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public AllowanceKind Kind { get; private set; }
    public Guid EmployeeAssignmentId { get; private set; }
    public string DeploymentCategoryCode { get; private set; } = string.Empty;
    public Guid? DeploymentApprovalRequestId { get; private set; }
    public Guid? WorkSessionId { get; private set; }
    public DateTimeOffset EligibilityPeriodStart { get; private set; }
    public DateTimeOffset EligibilityPeriodEnd { get; private set; }
    public string EligibilityKey { get; private set; } = string.Empty;
    public decimal ClaimedAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateOnly PayableDate { get; private set; }
    public DateOnly? PayrollPeriodStart { get; private set; }
    public Guid PolicyVersionId { get; private set; }
    public string CalculationPrivateReference { get; private set; } = string.Empty;
    public string? EvidencePrivateReference { get; private set; }
    public string RequestedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public PolicyVersion PolicyVersion { get; private set; } = null!;
    public EmployeeAssignment EmployeeAssignment { get; private set; } = null!;
    public ApprovalRequest? DeploymentApprovalRequest { get; private set; }
    public WorkSession? WorkSession { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public Guid? ApprovalRequestId { get; private set; }
    public Cosmix.Domain.Common.Approvals.ApprovalRequest? ApprovalRequest { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public int SubmissionVersion { get; private set; }
    public IReadOnlyCollection<AllowanceClaimSubmission> Submissions => _submissions.AsReadOnly();
    public ClaimSettlementState SettlementState { get; private set; }
    public DateTimeOffset? LastSettledAt { get; private set; }

    public AllowanceClaimSubmission Submit(ApprovalRequest approval)
    {
        ValidateSubmission(approval);
        if (SubmittedAt.HasValue || SubmissionVersion != 0)
            throw new InvalidOperationException("Submission needs a pending approval for this request and its requester.");
        return RecordSubmission(approval, ClaimedAmount, PolicyVersionId, CalculationPrivateReference,
            EvidencePrivateReference, PayableDate, PayrollPeriodStart, null, RequestedByUserId);
    }

    /// <summary>Correct and resubmit the same cycle atomically; save each submission and its approval together.</summary>
    public AllowanceClaimSubmission ResubmitReturned(ApprovalRequest returnedApproval, ApprovalRequest nextApproval,
        decimal claimedAmount, Guid policyVersionId, string calculationPrivateReference, string? evidencePrivateReference,
        DateOnly payableDate, DateOnly? payrollPeriodStart, string correctionReason, string correctedByUserId)
    {
        ArgumentNullException.ThrowIfNull(returnedApproval);
        ValidateSubmission(nextApproval);
        if (SubmissionVersion < 1 || !SubmittedAt.HasValue || ApprovalRequestId != returnedApproval.Id ||
            returnedApproval.SubjectId != Id || returnedApproval.RequesterUserId != RequestedByUserId ||
            returnedApproval.State != ApprovalState.Returned || !returnedApproval.ClosedAt.HasValue ||
            nextApproval.Id == returnedApproval.Id || nextApproval.RequestedAt < returnedApproval.ClosedAt.Value ||
            nextApproval.RequestType != returnedApproval.RequestType ||
            nextApproval.SnapshotPrivateReference == returnedApproval.SnapshotPrivateReference ||
            _submissions.Any(s => s.SnapshotPrivateReference == nextApproval.SnapshotPrivateReference) ||
            SettlementState != ClaimSettlementState.NotScheduled)
            throw new InvalidOperationException("Only this claim's returned, unsettled submission can be corrected with a fresh approval snapshot.");
        var amount = HrModelGuard.Money(claimedAmount, nameof(claimedAmount));
        var policy = ModelGuard.Id(policyVersionId, nameof(policyVersionId));
        var calculation = ModelGuard.Required(calculationPrivateReference, 500, nameof(calculationPrivateReference));
        var evidence = ModelGuard.Optional(evidencePrivateReference, 500, nameof(evidencePrivateReference));
        var reason = ModelGuard.Required(correctionReason, 2000, nameof(correctionReason));
        var actor = ModelGuard.Required(correctedByUserId, 200, nameof(correctedByUserId));
        return RecordSubmission(nextApproval, amount, policy, calculation, evidence, payableDate, payrollPeriodStart, reason, actor);
    }

    private void ValidateSubmission(ApprovalRequest approval)
    {
        ArgumentNullException.ThrowIfNull(approval);
        if (approval.SubjectId != Id || approval.RequesterUserId != RequestedByUserId ||
            approval.RequestedAt < CreatedAt || approval.State != ApprovalState.Pending)
            throw new InvalidOperationException("A pending approval for this claim and its requester is required.");
    }

    private AllowanceClaimSubmission RecordSubmission(ApprovalRequest approval, decimal amount, Guid policy,
        string calculation, string? evidence, DateOnly payableDate, DateOnly? payrollPeriodStart, string? reason, string actor)
    {
        var version = checked(SubmissionVersion + 1);
        var submission = new AllowanceClaimSubmission(this, approval, version, amount, policy, calculation, evidence,
            payableDate, payrollPeriodStart, reason, actor);
        _submissions.Add(submission);
        ClaimedAmount = amount; PolicyVersionId = policy; CalculationPrivateReference = calculation;
        EvidencePrivateReference = evidence; PayableDate = payableDate; PayrollPeriodStart = payrollPeriodStart;
        SubmissionVersion = version; ApprovalRequestId = approval.Id; ApprovalRequest = approval; SubmittedAt = approval.RequestedAt;
        return submission;
    }

    /// <summary>Records Accounts evidence only, never executes payment. Save the claim and record atomically.</summary>
    public AllowanceSettlementRecord RecordSettlement(ApprovalRequest approval, ClaimSettlementState state,
        Guid? payrollHandoffLineId, string? externalPaymentReference, string idempotencyKey,
        string recordedByUserId, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(approval);
        if (SubmissionVersion < 1 || approval.Id != ApprovalRequestId || approval.SubjectId != Id ||
            approval.State != ApprovalState.Approved || !approval.ClosedAt.HasValue || recordedAt < approval.ClosedAt.Value ||
            (LastSettledAt.HasValue && recordedAt < LastSettledAt.Value) ||
            state is not (ClaimSettlementState.InPayrollHandoff or ClaimSettlementState.RecordedAsPaid) ||
            SettlementState == ClaimSettlementState.RecordedAsPaid ||
            (SettlementState == ClaimSettlementState.InPayrollHandoff && state != ClaimSettlementState.RecordedAsPaid))
            throw new InvalidOperationException("Settlement requires the current approved submission and a forward, non-duplicate payment state.");
        var record = new AllowanceSettlementRecord(this, state, payrollHandoffLineId, externalPaymentReference,
            idempotencyKey, recordedByUserId, recordedAt);
        SettlementState = state; LastSettledAt = record.RecordedAt;
        return record;
    }
}
