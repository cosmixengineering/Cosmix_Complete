using Cosmix.Domain.Common;

namespace Cosmix.Domain.Authentication;

/// <summary>Immutable independent identity-review evidence; resetting a factor never starts a session.</summary>
public sealed class MfaResetApproval : IAppendOnlyEntity
{
    private MfaResetApproval() { }
    public MfaResetApproval(Guid id, UserAccount target, Guid reviewerAccountId, Guid reviewerEmployeeId,
        string proofReference, DateTimeOffset at)
    {
        if (!target.AuthenticatorRequired || target.Id == reviewerAccountId || target.EmployeeId == reviewerEmployeeId ||
            target.Status != AccountStatus.Active || !target.WebAccessEnabled || at < target.CreatedAt)
            throw new InvalidOperationException("Authenticator reset requires an independent approved identity review.");
        Id = ModelGuard.Id(id, nameof(id)); UserAccountId = target.Id; EmployeeId = target.EmployeeId;
        SecurityVersionBefore = target.SecurityVersion; SecurityVersionAfter = checked(target.SecurityVersion + 1);
        ReviewerAccountId = ModelGuard.Id(reviewerAccountId, nameof(reviewerAccountId));
        ReviewerEmployeeId = ModelGuard.Id(reviewerEmployeeId, nameof(reviewerEmployeeId));
        ProofReference = ModelGuard.Required(proofReference, 2000, nameof(proofReference)); ApprovedAt = ModelGuard.Utc(at);
    }
    public Guid Id { get; private set; }
    public Guid UserAccountId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public int SecurityVersionBefore { get; private set; }
    public int SecurityVersionAfter { get; private set; }
    public Guid ReviewerAccountId { get; private set; }
    public Guid ReviewerEmployeeId { get; private set; }
    public string ProofReference { get; private set; } = "";
    public DateTimeOffset ApprovedAt { get; private set; }
}
