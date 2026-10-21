using Cosmix.Domain.Common;

namespace Cosmix.Domain.Authentication.Mobile;

/// <summary>Administrator identity-review evidence, separate from the immutable HR submission.</summary>
public sealed class LoginPhoneChange
{
    private LoginPhoneChange() { }

    public LoginPhoneChange(UserAccount account, LoginPhoneBinding previous, UserRoleAssignment approver,
        string newPhoneNumber, string reason, string approvalReference, DateTimeOffset approvedAt, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(account); ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(approver);
        _ = account.RequireLoginPhoneApproval(approvedAt);
        if (account.Status is not (AccountStatus.ReadyForInvitation or AccountStatus.Active) ||
            previous.UserAccountId != account.Id || previous.RevokedAt.HasValue || previous.CreatedAt > approvedAt ||
            previous.VerifiedAt > approvedAt || approver.UserAccountId == account.Id ||
            !approver.IsEffectiveAt(approvedAt) || !approver.Role.IsActive || !approver.Role.IsSystemRole ||
            expiresAt <= approvedAt || expiresAt > approvedAt.AddHours(24))
            throw new InvalidOperationException("A current different administrator, current binding and bounded approval are required.");
        var phone = LoginPhoneNumber.RequireCanonical(newPhoneNumber, nameof(newPhoneNumber));
        if (phone == previous.PhoneNumber && previous.LoginPhoneChange?.CompletedAt is not null)
            throw new InvalidOperationException("The approved phone is unchanged.");
        if (phone == previous.PhoneNumber && previous.LoginPhoneChangeId is null)
            throw new InvalidOperationException("The approved phone is unchanged.");
        Id = Guid.NewGuid(); UserAccountId = account.Id; PreviousBindingId = previous.Id;
        PreviousBinding = previous; NewPhoneNumber = phone;
        ApprovedByUserId = approver.UserAccountId; ApprovedByAssignmentId = approver.Id;
        ApprovedByAssignment = approver; ApprovedBySystemRole = approver.Role.SystemRole;
        Reason = ModelGuard.Required(reason, 1000, nameof(reason));
        ApprovalReference = ModelGuard.Required(approvalReference, 500, nameof(approvalReference));
        ApprovedAt = ModelGuard.Utc(approvedAt); ExpiresAt = ModelGuard.Utc(expiresAt);
        SecurityVersion = checked(account.SecurityVersion + 1);
    }

    public Guid Id { get; private set; }
    public Guid UserAccountId { get; private set; }
    public Guid PreviousBindingId { get; private set; }
    public LoginPhoneBinding PreviousBinding { get; private set; } = null!;
    public string NewPhoneNumber { get; private set; } = string.Empty;
    public Guid ApprovedByUserId { get; private set; }
    public Guid ApprovedByAssignmentId { get; private set; }
    public UserRoleAssignment ApprovedByAssignment { get; private set; } = null!;
    public SystemRoleKind ApprovedBySystemRole { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string ApprovalReference { get; private set; } = string.Empty;
    public int SecurityVersion { get; private set; }
    public DateTimeOffset ApprovedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public Guid? CompletionChallengeId { get; private set; }
    public DateTimeOffset? SupersededAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsPendingAt(DateTimeOffset at) => CompletedAt is null && SupersededAt is null && ApprovedAt <= at && at < ExpiresAt;

    public void Supersede(DateTimeOffset at)
    {
        if (CompletedAt.HasValue || SupersededAt.HasValue || at < ApprovedAt)
            throw new InvalidOperationException("Only an unfinished phone recovery can be superseded.");
        SupersededAt = ModelGuard.Utc(at);
    }

    /// <summary>Call after constant-time OTP verification; save proof, binding, version and audit atomically.</summary>
    public void Complete(UserAccount account, LoginPhoneBinding binding, MobileOtpChallenge proof, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(account); ArgumentNullException.ThrowIfNull(binding); ArgumentNullException.ThrowIfNull(proof);
        _ = account.RequireLoginPhoneApproval(at);
        if (!IsPendingAt(at) || account.Id != UserAccountId || account.SecurityVersion != SecurityVersion ||
            account.Status is not (AccountStatus.ReadyForInvitation or AccountStatus.Active) ||
            binding.UserAccountId != account.Id || binding.LoginPhoneChangeId != Id || binding.PhoneNumber != NewPhoneNumber ||
            binding.RevokedAt.HasValue || binding.VerifiedAt.HasValue || binding.CreatedAt > at ||
            proof.UserAccountId != account.Id || proof.PhoneBindingId != binding.Id ||
            proof.SecurityVersion != SecurityVersion || proof.Purpose != "PhoneRecovery" ||
            proof.IssuedAt < ApprovedAt || !proof.IsUsableAt(at))
            throw new InvalidOperationException("Current approved recovery and matching unused proof are required.");
        _ = checked(account.SecurityVersion + 1);
        proof.Consume(at); CompletionChallengeId = proof.Id; CompletedAt = ModelGuard.Utc(at);
        binding.Verify(at); account.InvalidateSecurityVersion();
    }
}
