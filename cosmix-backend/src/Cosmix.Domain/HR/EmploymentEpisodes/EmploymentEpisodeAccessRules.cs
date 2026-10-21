using Cosmix.Domain.Authentication;

namespace Cosmix.Domain.HR.EmploymentEpisodes;

public enum EmploymentEpisodeResetAction
{
    InvalidateSecurityVersion = 1, RevokeSessionsAndRefreshCapability, RevokeRoleAssignments,
    RevokeDevices, RevokePhoneBindings, RevokeSetupInvitations, SupersedeUnfinishedPhoneRecovery,
    RevokeOpenOtp, RevokeMfaAndPasswordResetProofs, RevokeActiveFaceEnrollment,
    ClearCurrentActivation, ClearCurrentCredentials, ClearCurrentSetup, DisableMobileChannel, DisableWebChannel
}

public sealed record EmploymentEpisodeAccessState(Guid AccountId, Guid EmployeeId,
    Guid OriginalOnboardingDecisionId, Guid PreviousEpisodeId, AccountStatus Status,
    string? StatusReason, Guid? LifecycleBlockEpisodeId, int SecurityVersion, DateTimeOffset? LockedUntil,
    EmploymentBanEvidence CurrentBanEvidence, DateTimeOffset ObservedAt);

public sealed record EmploymentEpisodeAccessResetPlan(Guid AccountId, Guid EmployeeId,
    Guid PreviousEpisodeId, Guid NewEpisodeId, AccountStatus TargetStatus, bool PreserveIndependentBlock,
    bool RequiresManualReview, int SecurityVersion, DateTimeOffset? LockedUntil, bool EnableMobile,
    bool EnableWeb, IReadOnlyList<EmploymentEpisodeResetAction> Actions);

/// <summary>Pure required-action projection, not permission or execution of a security change.</summary>
public static class EmploymentEpisodeAccessRules
{
    public static EmploymentEpisodeAccessResetPlan BuildResetPlan(EmploymentEpisodeAccessState account,
        EmploymentEpisodeIdentity previous, EmployeeEmploymentEpisode next)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(next);
        if (account.ObservedAt == default || account.ObservedAt.Offset != TimeSpan.Zero || account.ObservedAt < next.Approval.DecidedAt)
            throw new ArgumentException("Account reset requires a current post-approval UTC observation.");
        var banFailure = EmploymentEpisodeEligibilityRules.BlockingCode(account.CurrentBanEvidence,
            account.EmployeeId, account.ObservedAt);
        if (banFailure is not null) throw new InvalidOperationException(banFailure);
        if (EmploymentEpisodeEligibilityRules.Fingerprint(account.CurrentBanEvidence) !=
            EmploymentEpisodeEligibilityRules.Fingerprint(next.BanEvidence))
            throw new InvalidOperationException("employee_rehire_blacklist_evidence_changed");
        if (account.AccountId == Guid.Empty || account.EmployeeId != previous.EmployeeId ||
            next.EmployeeId != previous.EmployeeId || account.PreviousEpisodeId != previous.PreviousEpisodeId ||
            next.PreviousEpisodeId != previous.PreviousEpisodeId ||
            account.OriginalOnboardingDecisionId != previous.OriginalOnboardingDecisionId ||
            next.OriginalOnboardingDecisionId != previous.OriginalOnboardingDecisionId ||
            !Enum.IsDefined(account.Status) || account.SecurityVersion < 1)
            throw new ArgumentException("The existing account and both employment episodes must share exact identity.");
        var lifecycleBlock = account.Status == AccountStatus.Disabled && account.StatusReason == "employee-lifecycle";
        var exactLifecycleBlock = lifecycleBlock && account.LifecycleBlockEpisodeId == previous.PreviousEpisodeId;
        var independentBlock = account.Status is AccountStatus.Disabled or AccountStatus.Suspended && !lifecycleBlock;
        var review = lifecycleBlock && !exactLifecycleBlock || !exactLifecycleBlock && !independentBlock;
        return new(account.AccountId, account.EmployeeId, previous.PreviousEpisodeId, next.Id,
            exactLifecycleBlock ? AccountStatus.PendingAccess : account.Status, independentBlock, review,
            checked(account.SecurityVersion + 1), account.LockedUntil, false, false,
            Array.AsReadOnly(Enum.GetValues<EmploymentEpisodeResetAction>()));
    }

    // Temporal predicate only. A caller must also verify current ban/approval/access evidence.
    public static bool IsWithinEpisode(EmployeeEmploymentEpisode episode, DateTimeOffset at, DateTimeOffset? endedAt)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return at >= episode.Terms.EffectiveAt &&
            (!endedAt.HasValue || endedAt > episode.Terms.EffectiveAt && at < endedAt);
    }
}
