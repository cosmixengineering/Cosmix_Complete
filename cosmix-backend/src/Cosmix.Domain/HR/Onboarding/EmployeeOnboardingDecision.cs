using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.Onboarding;

/// <summary>One final decision per submitted version. Authorization is checked server-side before this operation.</summary>
public sealed class EmployeeOnboardingDecision : IAppendOnlyEntity
{
    private EmployeeOnboardingDecision() { }
    internal EmployeeOnboardingDecision(EmployeeSubmission submission, OnboardingDecision decision,
        Guid actorUserId, Guid actorRoleId, string? reason, DateTimeOffset decidedAt)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if (submission.Provenance != OnboardingProvenance.HrWorkflow)
            throw new InvalidOperationException("Owner bootstrap cannot be represented as an HR actor decision.");
        if (!Enum.IsDefined(decision)) throw new ArgumentOutOfRangeException(nameof(decision));
        if (actorUserId == submission.SubmittedByUserId) throw new InvalidOperationException("A requester cannot decide their own submission.");
        if (decidedAt < submission.SubmittedAt) throw new ArgumentException("Decision cannot precede submission.", nameof(decidedAt));
        Id = Guid.NewGuid(); EmployeeId = submission.EmployeeId; SubmissionId = submission.Id;
        Submission = submission;
        Decision = decision; ActorUserId = ModelGuard.Id(actorUserId, nameof(actorUserId));
        ActorRoleId = ModelGuard.Id(actorRoleId, nameof(actorRoleId));
        Reason = decision == OnboardingDecision.Approve ? ModelGuard.Optional(reason, 2000, nameof(reason))
            : ModelGuard.Required(reason!, 2000, nameof(reason));
        DecidedAt = ModelGuard.Utc(decidedAt);
    }

    internal EmployeeOnboardingDecision(EmployeeSubmission submission, string externalApproverReference,
        string ownerApprovalReference, DateTimeOffset decidedAt)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if (submission.Provenance != OnboardingProvenance.OwnerBootstrap || submission.Version != 1 ||
            submission.MobileAccessRequested || submission.ApprovedLoginPhoneNumber is null || decidedAt < submission.SubmittedAt)
            throw new InvalidOperationException("Owner approval requires the first web-only bootstrap submission.");
        ExternalApproverReference = ModelGuard.Required(externalApproverReference, 200, nameof(externalApproverReference));
        OwnerApprovalReference = ModelGuard.Required(ownerApprovalReference, 500, nameof(ownerApprovalReference));
        Id = Guid.NewGuid(); EmployeeId = submission.EmployeeId; SubmissionId = submission.Id; Submission = submission;
        Provenance = OnboardingProvenance.OwnerBootstrap; Decision = OnboardingDecision.Approve;
        DecidedAt = ModelGuard.Utc(decidedAt);
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid SubmissionId { get; private set; }
    public EmployeeSubmission Submission { get; private set; } = null!;
    public OnboardingDecision Decision { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public Guid? ActorRoleId { get; private set; }
    public OnboardingProvenance Provenance { get; private set; }
    public string? ExternalApproverReference { get; private set; }
    public string? OwnerApprovalReference { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset DecidedAt { get; private set; }
}
