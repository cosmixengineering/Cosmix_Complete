using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;

namespace Cosmix.Domain.Authentication;

/// <summary>Shared web/mobile identity. Employment approval, access setup and login activation remain separate.</summary>
public sealed class UserAccount
{
    private UserAccount() { }
    public UserAccount(Employee employee, EmployeeOnboardingDecision approval, string loginName, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(employee); ArgumentNullException.ThrowIfNull(approval);
        if (employee.OnboardingStatus != OnboardingStatus.Approved || employee.ApprovedOnboardingDecisionId != approval.Id ||
            approval.EmployeeId != employee.Id || approval.Decision != OnboardingDecision.Approve)
            throw new InvalidOperationException("Account setup requires this employee's approved onboarding decision.");
        if (createdAt < approval.DecidedAt) throw new ArgumentException("Account creation cannot precede approval.");
        Id = Guid.NewGuid(); EmployeeId = employee.Id; ApprovalDecisionId = approval.Id;
        Employee = employee; ApprovalDecision = approval;
        LoginName = ModelGuard.Required(loginName, 320, nameof(loginName)); NormalizedLoginName = LoginName.ToUpperInvariant();
        CreatedAt = ModelGuard.Utc(createdAt); SecurityVersion = 1; Status = AccountStatus.PendingAccess;
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public Guid ApprovalDecisionId { get; private set; }
    public EmployeeOnboardingDecision ApprovalDecision { get; private set; } = null!;
    public string LoginName { get; private set; } = string.Empty;
    public string NormalizedLoginName { get; private set; } = string.Empty;
    public string? PasswordHash { get; private set; }
    public string? PasswordHashFormat { get; private set; }
    public DateTimeOffset? PasswordChangedAt { get; private set; }
    public AccountStatus Status { get; private set; }
    public Guid? InitialRoleAssignmentId { get; private set; }
    public Guid? InitialAccessScopeId { get; private set; }
    public Guid? CurrentSetupInvitationId { get; private set; }
    public bool MfaRequired { get; private set; }
    public bool MobileAccessEnabled { get; private set; }
    public bool WebAccessEnabled { get; private set; }
    public DateTimeOffset? PhoneVerifiedAt { get; private set; }
    public int SecurityVersion { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ActivatedAt { get; private set; }
    public DateTimeOffset? StatusChangedAt { get; private set; }
    public string? StatusReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void ConfigureInitialAccess(UserRoleAssignment assignment, UserAccessScope scope, DateTimeOffset at)
    {
        if (Status is not (AccountStatus.PendingAccess or AccountStatus.ReadyForInvitation))
            throw new InvalidOperationException("Initial access can only be configured before invitation.");
        ValidateAccess(assignment, scope, at);
        InitialRoleAssignmentId = assignment.Id; InitialAccessScopeId = scope.Id; Status = AccountStatus.ReadyForInvitation;
    }

    /// <summary>
    /// An authorized administrator can replace access before activation without repeating employee approval.
    /// Save the account, revoked invitation and replacement access atomically; stage audit in that transaction.
    /// </summary>
    public void ReplaceInitialAccess(AccountSetupInvitation currentInvitation, UserRoleAssignment assignment,
        UserAccessScope scope, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(currentInvitation);
        if (Status != AccountStatus.Invited || currentInvitation.UserAccountId != Id ||
            currentInvitation.ApprovalDecisionId != ApprovalDecisionId || CurrentSetupInvitationId != currentInvitation.Id ||
            currentInvitation.ConsumedAt.HasValue || at < currentInvitation.IssuedAt || at < currentInvitation.RevokedAt)
            throw new InvalidOperationException("Only this account's current unconsumed invitation can have its initial access replaced.");
        ValidateAccess(assignment, scope, at);
        var changedAt = ModelGuard.Utc(at);

        // All replacement checks precede revocation so an invalid request leaves both entities untouched.
        // An expired or already revoked invitation is recoverable; preserve any earlier revocation evidence.
        if (!currentInvitation.RevokedAt.HasValue) currentInvitation.Revoke(changedAt);
        InitialRoleAssignmentId = assignment.Id; InitialAccessScopeId = scope.Id;
        CurrentSetupInvitationId = null; Status = AccountStatus.ReadyForInvitation; StatusChangedAt = changedAt;
    }

    public AccountSetupInvitation IssueSetupInvitation(UserRoleAssignment assignment, UserAccessScope scope,
        string tokenHash, string channel, string protectedDestinationReference, Guid issuedByUserId,
        DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        if (Status is not (AccountStatus.ReadyForInvitation or AccountStatus.Invited))
            throw new InvalidOperationException("Approved access is required before inviting an employee.");
        ValidateAccess(assignment, scope, issuedAt);
        if (InitialRoleAssignmentId != assignment.Id || InitialAccessScopeId != scope.Id)
            throw new InvalidOperationException("Invitation must use the configured initial access.");
        var invitation = new AccountSetupInvitation(this, tokenHash, channel, protectedDestinationReference,
            issuedByUserId, issuedAt, expiresAt);
        CurrentSetupInvitationId = invitation.Id; Status = AccountStatus.Invited;
        return invitation;
    }

    /// <summary>Call only after trusted token verification and approved password hashing; save account and invitation atomically.</summary>
    public void Activate(AccountSetupInvitation invitation, UserRoleAssignment assignment, UserAccessScope scope,
        string passwordHash, string hashFormat, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        if (Status != AccountStatus.Invited || invitation.UserAccountId != Id ||
            invitation.ApprovalDecisionId != ApprovalDecisionId || CurrentSetupInvitationId != invitation.Id)
            throw new InvalidOperationException("Invitation does not match this pending account.");
        ValidateAccess(assignment, scope, at);
        if (assignment.Id != InitialRoleAssignmentId || scope.Id != InitialAccessScopeId)
            throw new InvalidOperationException("Initial access has changed.");
        var hash = ModelGuard.Required(passwordHash, 2000, nameof(passwordHash));
        var format = ModelGuard.Required(hashFormat, 100, nameof(hashFormat));
        var nextVersion = checked(SecurityVersion + 1);
        invitation.Consume(at);
        PasswordHash = hash; PasswordHashFormat = format; PasswordChangedAt = ModelGuard.Utc(at);
        Status = AccountStatus.Active; ActivatedAt = ModelGuard.Utc(at); WebAccessEnabled = true; SecurityVersion = nextVersion;
    }

    /// <summary>Authorized initial setup only. Save the hash, approved binding and administrator audit atomically; SMS proof is still required at login.</summary>
    public void ProvisionInitialWebCredentials(string email, string passwordHash, string hashFormat,
        LoginPhoneBinding binding, UserRoleAssignment assignment, UserAccessScope scope, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (Status is not (AccountStatus.ReadyForInvitation or AccountStatus.Active) ||
            CurrentSetupInvitationId.HasValue || PasswordHash is not null || PasswordHashFormat is not null ||
            PasswordChangedAt.HasValue || WebAccessEnabled || LockedUntil > at)
            throw new InvalidOperationException("Initial web setup cannot replace credentials, recover invitations or unblock accounts.");
        ValidateSecurityChangeTime(at);
        var submission = RequireLoginPhoneApproval(at);
        ValidateAccess(assignment, scope, at);
        if (!InitialRoleAssignmentId.HasValue || !InitialAccessScopeId.HasValue ||
            (Status != AccountStatus.Active && (assignment.Id != InitialRoleAssignmentId || scope.Id != InitialAccessScopeId)))
            throw new InvalidOperationException("Approved initial access must be configured before web credentials.");
        if (binding.UserAccountId != Id || binding.ApprovedSubmissionId != submission.Id ||
            !binding.IsApprovedFor(this, at) || binding.RevokedAt.HasValue ||
            at < binding.CreatedAt || at < binding.VerifiedAt)
            throw new InvalidOperationException("Web setup requires this account's approved current phone binding.");
        var login = ModelGuard.Required(email, 320, nameof(email));
        var hash = ModelGuard.Required(passwordHash, 2000, nameof(passwordHash));
        var format = ModelGuard.Required(hashFormat, 100, nameof(hashFormat));
        var changedAt = ModelGuard.Utc(at);
        var nextVersion = checked(SecurityVersion + 1);
        LoginName = login; NormalizedLoginName = login.ToUpperInvariant();
        PasswordHash = hash; PasswordHashFormat = format; PasswordChangedAt = changedAt;
        Status = AccountStatus.Active; ActivatedAt ??= changedAt; StatusChangedAt = changedAt;
        WebAccessEnabled = true; SecurityVersion = nextVersion;
    }

    /// <summary>Different administrator's reviewed first-password recovery only; pass complete persisted login history and save revocations/audit atomically.</summary>
    public void ResetInitialWebPassword(Guid reviewedByUserId, LoginPhoneBinding binding,
        UserRoleAssignment assignment, UserAccessScope scope, bool hasSessionHistory,
        string passwordHash, string hashFormat, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(binding);
        var reviewer = ModelGuard.Id(reviewedByUserId, nameof(reviewedByUserId));
        ValidateSecurityChangeTime(at);
        var submission = RequireLoginPhoneApproval(at);
        ValidateAccess(assignment, scope, at);
        if (reviewer == Id || hasSessionHistory || PhoneVerifiedAt.HasValue || binding.VerifiedAt.HasValue ||
            Status != AccountStatus.Active || !WebAccessEnabled || !ActivatedAt.HasValue ||
            CurrentSetupInvitationId.HasValue || LockedUntil > at ||
            string.IsNullOrWhiteSpace(PasswordHash) || PasswordHashFormat != hashFormat || !PasswordChangedAt.HasValue ||
            !InitialRoleAssignmentId.HasValue || !InitialAccessScopeId.HasValue ||
            binding.UserAccountId != Id || binding.ApprovedSubmissionId != submission.Id || !binding.IsApprovedFor(this, at))
            throw new InvalidOperationException("Reviewed initial password recovery requires a never-signed-in web account and its unverified approved phone.");
        var hash = ModelGuard.Required(passwordHash, 2000, nameof(passwordHash));
        var format = ModelGuard.Required(hashFormat, 100, nameof(hashFormat));
        var changedAt = ModelGuard.Utc(at);
        var nextVersion = checked(SecurityVersion + 1);
        PasswordHash = hash; PasswordHashFormat = format; PasswordChangedAt = changedAt; SecurityVersion = nextVersion;
        FailedLoginCount = 0; LockedUntil = null;
    }

    /// <summary>Authorized channel administration only; save invalidation and audit atomically. This does not verify or activate a credential.</summary>
    public void SetMobileAccess(bool enabled, DateTimeOffset at)
    {
        ValidateSecurityChangeTime(at);
        if (enabled && Status is AccountStatus.Suspended or AccountStatus.Disabled)
            throw new InvalidOperationException("A blocked account cannot enable mobile access.");
        if (enabled) _ = RequireMobileApproval(at);
        if (MobileAccessEnabled == enabled) return;
        var nextVersion = checked(SecurityVersion + 1);
        MobileAccessEnabled = enabled; SecurityVersion = nextVersion; StatusChangedAt = ModelGuard.Utc(at);
    }

    /// <summary>Authorized channel administration; this cannot create a web credential for a mobile-only account.</summary>
    public void SetWebAccess(bool enabled, DateTimeOffset at)
    {
        ValidateSecurityChangeTime(at);
        if (enabled && (Status != AccountStatus.Active || PasswordHash is null || PasswordHashFormat is null || !PasswordChangedAt.HasValue))
            throw new InvalidOperationException("Web access requires an active account with a provisioned web credential.");
        if (WebAccessEnabled == enabled) return;
        var nextVersion = checked(SecurityVersion + 1);
        WebAccessEnabled = enabled; SecurityVersion = nextVersion; StatusChangedAt = ModelGuard.Utc(at);
    }

    public void InvalidateSecurityVersion() => SecurityVersion = checked(SecurityVersion + 1);

    /// <summary>Approved employment suspension release only. Fresh grants and channel approval are still required.</summary>
    public void PrepareForEmploymentReactivation(DateTimeOffset at)
    {
        ValidateSecurityChangeTime(at);
        if (Status != AccountStatus.Suspended || StatusReason != "employee-lifecycle" || Employee.Status != EmployeeStatus.Active)
            throw new InvalidOperationException("Only a released lifecycle suspension can prepare access again.");
        var hasPassword = !string.IsNullOrWhiteSpace(PasswordHash) && !string.IsNullOrWhiteSpace(PasswordHashFormat) && PasswordChangedAt.HasValue;
        var noPassword = PasswordHash is null && PasswordHashFormat is null && !PasswordChangedAt.HasValue;
        var hasInitialAccess = InitialRoleAssignmentId.HasValue && InitialRoleAssignmentId != Guid.Empty &&
            InitialAccessScopeId.HasValue && InitialAccessScopeId != Guid.Empty;
        var noInitialAccess = !InitialRoleAssignmentId.HasValue && !InitialAccessScopeId.HasValue;
        var hasActivationEvidence = ActivatedAt.HasValue || !noPassword || PhoneVerifiedAt.HasValue;
        if ((!hasPassword && !noPassword) || (!hasInitialAccess && !noInitialAccess) ||
            (WebAccessEnabled && !hasPassword) ||
            (CurrentSetupInvitationId.HasValue && (!hasInitialAccess || CurrentSetupInvitationId == Guid.Empty)) ||
            (hasActivationEvidence && (!ActivatedAt.HasValue || ActivatedAt < CreatedAt ||
                !hasInitialAccess || (!hasPassword && !PhoneVerifiedAt.HasValue) ||
                PasswordChangedAt < CreatedAt || PhoneVerifiedAt < CreatedAt)))
            throw new InvalidOperationException("Inconsistent account activation evidence requires manual review.");
        var nextVersion = checked(SecurityVersion + 1);
        Status = hasActivationEvidence ? AccountStatus.Active : AccountStatus.PendingAccess;
        if (!hasActivationEvidence)
        {
            // The revoked objects remain as history; fresh explicit initial setup selects new access.
            InitialRoleAssignmentId = null; InitialAccessScopeId = null; CurrentSetupInvitationId = null;
        }
        MobileAccessEnabled = false; WebAccessEnabled = false;
        StatusReason = "employee-lifecycle-release-requires-access";
        StatusChangedAt = ModelGuard.Utc(at);
        SecurityVersion = nextVersion;
    }

    /// <summary>Trusted OTP completion only. Consume the challenge and save binding/account/session/audit in one transaction.</summary>
    public void ActivateMobile(LoginPhoneBinding binding, UserRoleAssignment assignment, UserAccessScope scope, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (Status is not (AccountStatus.ReadyForInvitation or AccountStatus.Active) || !MobileAccessEnabled || LockedUntil > at)
            throw new InvalidOperationException("Account is not eligible for mobile activation; recover any outstanding invitation first.");
        ValidateSecurityChangeTime(at);
        var submission = RequireMobileApproval(at);
        ValidateAccess(assignment, scope, at);
        // The initial pair is activation evidence; later authorized assignments do not rewrite that history.
        if (Status != AccountStatus.Active && (assignment.Id != InitialRoleAssignmentId || scope.Id != InitialAccessScopeId))
            throw new InvalidOperationException("Initial access has changed.");
        if (binding.UserAccountId != Id || binding.ApprovedSubmissionId != submission.Id ||
            !binding.IsApprovedFor(this, at) || binding.RevokedAt.HasValue ||
            !binding.VerifiedAt.HasValue || at < binding.VerifiedAt || at < binding.CreatedAt)
            throw new InvalidOperationException("A verified current binding for this account's approved login phone is required.");
        if (Status == AccountStatus.Active && PhoneVerifiedAt == binding.VerifiedAt) return;
        var nextVersion = checked(SecurityVersion + 1);
        PhoneVerifiedAt = binding.VerifiedAt; ActivatedAt ??= ModelGuard.Utc(at);
        Status = AccountStatus.Active; StatusChangedAt = ModelGuard.Utc(at); SecurityVersion = nextVersion;
    }

    internal EmployeeSubmission RequireMobileApproval(DateTimeOffset at)
    {
        var submission = RequireLoginPhoneApproval(at);
        if (!submission.MobileAccessRequested)
            throw new InvalidOperationException("Mobile access was not requested in the approved submission.");
        return submission;
    }

    internal EmployeeSubmission RequireLoginPhoneApproval(DateTimeOffset at)
    {
        var approval = ApprovalDecision;
        var submission = approval?.Submission;
        if (Status is AccountStatus.Suspended or AccountStatus.Disabled || at < CreatedAt ||
            Employee is null || Employee.Id != EmployeeId || Employee.Status != EmployeeStatus.Active ||
            Employee.OnboardingStatus != OnboardingStatus.Approved || Employee.ApprovedOnboardingDecisionId != ApprovalDecisionId ||
            approval is null || approval.Id != ApprovalDecisionId || approval.EmployeeId != EmployeeId ||
            approval.Decision != OnboardingDecision.Approve || at < approval.DecidedAt ||
            submission is null || submission.Id != approval.SubmissionId || submission.EmployeeId != EmployeeId ||
            submission.ApprovedLoginPhoneNumber is null)
            throw new InvalidOperationException("Load this employee's current approval and explicitly approved login phone.");
        return submission;
    }

    private void ValidateSecurityChangeTime(DateTimeOffset at)
    {
        if (at < CreatedAt || at < StatusChangedAt || at < ActivatedAt || at < PhoneVerifiedAt || at < PasswordChangedAt)
            throw new ArgumentException("Security change precedes account evidence.", nameof(at));
    }

    private void ValidateAccess(UserRoleAssignment assignment, UserAccessScope scope, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(assignment); ArgumentNullException.ThrowIfNull(scope);
        if (at < CreatedAt || at < StatusChangedAt || assignment.UserAccountId != Id || assignment.EmployeeId != EmployeeId || !assignment.IsEffectiveAt(at) ||
            assignment.Role is null || !assignment.Role.IsActive || assignment.Role.Id != assignment.RoleId ||
            scope.UserRoleAssignmentId != assignment.Id)
            throw new InvalidOperationException("A current role and its own resource scope are required.");
        // Load the current Role with the assignment. Application also checks current permissions,
        // employee eligibility and administrator delegation authority under the transaction.
    }

    public void RequireMfa() { MfaRequired = true; SecurityVersion = checked(SecurityVersion + 1); }

    public void RecordFailedLogin(int approvedFailureLimit, DateTimeOffset at, DateTimeOffset approvedLockoutUntil)
    {
        if (approvedFailureLimit < 1 || approvedLockoutUntil <= at) throw new ArgumentException("Valid approved lockout settings are required.");
        FailedLoginCount = checked(FailedLoginCount + 1);
        if (FailedLoginCount >= approvedFailureLimit) LockedUntil = ModelGuard.Utc(approvedLockoutUntil);
    }

    public void RecordSuccessfulLogin(DateTimeOffset at)
    {
        if (Status != AccountStatus.Active || LockedUntil > at) throw new InvalidOperationException("Account is not eligible to sign in.");
        FailedLoginCount = 0; LockedUntil = null;
    }

    public void Suspend(string reason, DateTimeOffset at) => Block(AccountStatus.Suspended, reason, at);
    public void Disable(string reason, DateTimeOffset at) => Block(AccountStatus.Disabled, reason, at);
    private void Block(AccountStatus status, string reason, DateTimeOffset at)
    {
        var validatedReason = ModelGuard.Required(reason, 1000, nameof(reason));
        var nextVersion = checked(SecurityVersion + 1);
        Status = status; StatusReason = validatedReason; StatusChangedAt = ModelGuard.Utc(at); SecurityVersion = nextVersion;
        // Session/token revocation and audit must be staged in the same application transaction.
    }

    /// <summary>Trusted current-password and browser-session verification only; save revocations and audit atomically.</summary>
    public void ChangeWebPassword(UserSession session, MobileOtpChallenge loginProof, LoginPhoneBinding binding,
        string passwordHash, string hashFormat, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(session); ArgumentNullException.ThrowIfNull(loginProof);
        ValidateWebPasswordReplacement(binding, at);
        if (session.UserAccountId != Id || session.SecurityVersion != SecurityVersion ||
            session.Client != SessionClient.Web || !session.IsActiveAt(at) ||
            session.RegisteredDeviceId.HasValue || session.MfaChallengeId.HasValue ||
            session.SmsOtpChallengeId != loginProof.Id || loginProof.Purpose != "WebLogin" ||
            !MatchesWebPasswordProof(loginProof, binding) || binding.VerifiedAt > session.CreatedAt || loginProof.ConsumedAt != session.CreatedAt ||
            loginProof.RevokedAt.HasValue || loginProof.IssuedAt > session.CreatedAt || session.CreatedAt >= loginProof.ExpiresAt ||
            loginProof.MaximumAttempts < 1 || loginProof.FailedAttempts < 0 || loginProof.FailedAttempts >= loginProof.MaximumAttempts)
            throw new InvalidOperationException("A current session with its consumed web SMS proof is required.");
        var hash = ModelGuard.Required(passwordHash, 2000, nameof(passwordHash));
        var format = ModelGuard.Required(hashFormat, 100, nameof(hashFormat));
        var nextVersion = checked(SecurityVersion + 1);
        PasswordHash = hash; PasswordHashFormat = format; PasswordChangedAt = ModelGuard.Utc(at); SecurityVersion = nextVersion;
        FailedLoginCount = 0; LockedUntil = null;
    }

    /// <summary>Trusted SMS and browser verification only. This command consumes the reset proof and never issues a session.</summary>
    public void ResetWebPassword(MobileOtpChallenge challenge, LoginPhoneBinding binding,
        string passwordHash, string hashFormat, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        ValidateWebPasswordReplacement(binding, at);
        if (challenge.Purpose != "WebPasswordReset" || !MatchesWebPasswordProof(challenge, binding) ||
            challenge.IssuedAt < binding.VerifiedAt || !challenge.IsUsableAt(at))
            throw new InvalidOperationException("A current purpose-bound password reset proof is required.");
        var hash = ModelGuard.Required(passwordHash, 2000, nameof(passwordHash));
        var format = ModelGuard.Required(hashFormat, 100, nameof(hashFormat));
        var nextVersion = checked(SecurityVersion + 1);
        challenge.Consume(at);
        PasswordHash = hash; PasswordHashFormat = format; PasswordChangedAt = ModelGuard.Utc(at); SecurityVersion = nextVersion;
        FailedLoginCount = 0; LockedUntil = null;
    }

    private void ValidateWebPasswordReplacement(LoginPhoneBinding binding, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ValidateSecurityChangeTime(at);
        _ = RequireLoginPhoneApproval(at);
        if (Status != AccountStatus.Active || !WebAccessEnabled || !ActivatedAt.HasValue || LockedUntil > at ||
            string.IsNullOrWhiteSpace(PasswordHash) || string.IsNullOrWhiteSpace(PasswordHashFormat) || !PasswordChangedAt.HasValue ||
            !binding.IsApprovedFor(this, at) || binding.RevokedAt.HasValue ||
            !binding.VerifiedAt.HasValue || binding.CreatedAt > at || binding.VerifiedAt > at)
            throw new InvalidOperationException("Password replacement requires an eligible web account and current verified phone.");
    }

    private bool MatchesWebPasswordProof(MobileOtpChallenge proof, LoginPhoneBinding binding) =>
        proof.Platform == "web" && proof.UserAccountId == Id && proof.SecurityVersion == SecurityVersion &&
        proof.PhoneBindingId == binding.Id && proof.PhoneBinding.UserAccountId == Id && proof.PhoneBinding.Id == binding.Id &&
        proof.DeviceHash is { Length: 64 } && proof.DeviceHash.All(char.IsAsciiHexDigit) &&
        proof.IssuedAt >= binding.CreatedAt && proof.IssuedAt >= PasswordChangedAt;

    public void ResetPassword(PasswordResetChallenge challenge, string passwordHash, string hashFormat, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        if (Status != AccountStatus.Active || !WebAccessEnabled || PasswordHash is null ||
            challenge.UserAccountId != Id || challenge.SecurityVersion != SecurityVersion)
            throw new InvalidOperationException("Reset challenge is not valid for this active account version.");
        var hash = ModelGuard.Required(passwordHash, 2000, nameof(passwordHash));
        var format = ModelGuard.Required(hashFormat, 100, nameof(hashFormat));
        var nextVersion = checked(SecurityVersion + 1);
        challenge.Consume(at);
        PasswordHash = hash; PasswordHashFormat = format; PasswordChangedAt = ModelGuard.Utc(at); SecurityVersion = nextVersion;
        FailedLoginCount = 0; LockedUntil = null;
    }
}
