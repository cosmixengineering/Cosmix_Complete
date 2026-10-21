using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Onboarding;

namespace Cosmix.Domain.Authentication.Mobile;

/// <summary>Approved login credential history, independent of employee contact numbers.</summary>
public sealed class LoginPhoneBinding
{
    private LoginPhoneBinding() { }

    public LoginPhoneBinding(UserAccount account, string phoneNumber, Guid approvedSubmissionId, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(account);
        var phone = LoginPhoneNumber.RequireCanonical(phoneNumber, nameof(phoneNumber));
        var submissionId = ModelGuard.Id(approvedSubmissionId, nameof(approvedSubmissionId));
        var submission = account.RequireLoginPhoneApproval(createdAt);
        if (submission.Id != submissionId || submission.ApprovedLoginPhoneNumber != phone)
            throw new InvalidOperationException("Login phone must match this account's approved submission.");
        Id = Guid.NewGuid(); UserAccountId = account.Id; UserAccount = account;
        PhoneNumber = phone; ApprovedSubmissionId = submissionId; ApprovedSubmission = submission;
        CreatedAt = ModelGuard.Utc(createdAt);
    }

    public Guid Id { get; private set; }
    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = string.Empty;
    public Guid ApprovedSubmissionId { get; private set; }
    public EmployeeSubmission ApprovedSubmission { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? LoginPhoneChangeId { get; private set; }
    public LoginPhoneChange? LoginPhoneChange { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static LoginPhoneBinding CreateRecoveryBinding(UserAccount account, LoginPhoneChange change, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(account); ArgumentNullException.ThrowIfNull(change);
        var submission = account.RequireLoginPhoneApproval(at);
        if (change.UserAccountId != account.Id || !change.IsPendingAt(at) || account.SecurityVersion != change.SecurityVersion ||
            change.PreviousBinding.RevokedAt is null || change.PreviousBinding.RevokedAt > at)
            throw new InvalidOperationException("Revoke the previous credential and invalidate access before installing recovery.");
        return new LoginPhoneBinding { Id = Guid.NewGuid(), UserAccountId = account.Id, UserAccount = account,
            PhoneNumber = change.NewPhoneNumber, ApprovedSubmissionId = submission.Id, ApprovedSubmission = submission,
            LoginPhoneChangeId = change.Id, LoginPhoneChange = change, CreatedAt = ModelGuard.Utc(at) };
    }

    public bool IsApprovedFor(UserAccount account, DateTimeOffset at)
    {
        var submission = account.ApprovalDecision?.Submission;
        if (submission is null || UserAccountId != account.Id || ApprovedSubmissionId != submission.Id ||
            RevokedAt.HasValue || CreatedAt > at || VerifiedAt > at) return false;
        if (LoginPhoneChangeId is null) return PhoneNumber == submission.ApprovedLoginPhoneNumber;
        return LoginPhoneChange is { CompletedAt: not null, CompletionChallengeId: not null, SupersededAt: null } change &&
            change.Id == LoginPhoneChangeId && change.UserAccountId == account.Id && change.NewPhoneNumber == PhoneNumber &&
            change.ApprovedAt <= CreatedAt && change.CompletedAt <= at && change.CompletedAt == VerifiedAt;
    }

    /// <summary>Call only after trusted proof verification. This does not activate or enable an account.</summary>
    public void Verify(DateTimeOffset at)
    {
        if (RevokedAt.HasValue || at < CreatedAt || at < VerifiedAt ||
            (LoginPhoneChangeId.HasValue && (LoginPhoneChange?.CompletedAt is null || LoginPhoneChange.CompletedAt > at)))
            throw new InvalidOperationException("Phone binding cannot be verified.");
        if (VerifiedAt.HasValue) return;
        VerifiedAt = ModelGuard.Utc(at);
    }

    /// <summary>Provisioning must invalidate the account security version and affected proofs/sessions atomically.</summary>
    public void Revoke(DateTimeOffset at)
    {
        if (at < CreatedAt || at < VerifiedAt) throw new ArgumentException("Revocation precedes phone binding evidence.");
        if (RevokedAt.HasValue) return;
        RevokedAt = ModelGuard.Utc(at);
    }
}
