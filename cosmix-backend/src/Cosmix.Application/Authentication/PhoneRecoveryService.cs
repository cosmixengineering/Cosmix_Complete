using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.Authentication;

public sealed record ApprovePhoneRecoveryRequest(
    [Required, StringLength(40)] string NewPhoneNumber,
    [Required, StringLength(1000)] string Reason,
    [Required, StringLength(500)] string ApprovalReference,
    DateTimeOffset ExpiresAt,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedAccountVersion);
public sealed record PhoneRecoveryApprovalResponse(Guid RecoveryId, DateTimeOffset ExpiresAt,
    string AccountVersion, string NextStep = "verify_new_phone");
public sealed record PhoneRecoveryRequest(string PhoneNumber, string ContextSecret, string Platform);
public sealed record PhoneRecoveryVerifyRequest(Guid ChallengeId, string Code, string ContextSecret, string Platform);

public interface IPhoneRecoveryRepository
{
    void AddChange(LoginPhoneChange change);
    Task<MobileOtpChallenge?> GetLatestChallengeAsync(Guid accountId, CancellationToken ct);
    /// <summary>Only within the caller's transaction, before inserting the replacement unique current binding.</summary>
    Task SavePredecessorAsync(CancellationToken ct);
}

/// <summary>Identity review is administrative; possession of the newly approved SIM completes recovery, never login.</summary>
public sealed class PhoneRecoveryService(IAuthenticationRepository authentication, IAccessManagementRepository administrators,
    IPhoneRecoveryRepository recoveries, IEmployeeMobileProvisioningRepository identities, IAccountAccessReader permissions,
    ICurrentAuthentication current, IOtpProtector secrets, IOtpSmsSender sender, IPhoneNumberNormalizer phones,
    MobileAuthenticationOptions options, TimeProvider clock)
{
    public Task<PhoneRecoveryApprovalResponse> ApproveAsync(Guid accountId, ApprovePhoneRecoveryRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        var actor = current.Require();
        if (actor.Client != SessionClient.Web || !actor.MfaSatisfied || actor.AccountId == accountId)
            throw Error("access_denied", 403);
        var phone = NormalizeOrNull(request.NewPhoneNumber);
        if (phone is null || string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.ApprovalReference))
            throw Error("invalid_phone_recovery_request", 400);
        byte[] version;
        try { version = Convert.FromBase64String(request.ExpectedAccountVersion); }
        catch (Exception e) when (e is FormatException or ArgumentNullException) { throw Error("invalid_expected_version", 400); }
        if (version.Length != 8) throw Error("invalid_expected_version", 400);
        return administrators.InTransactionAsync(async token =>
        {
            var now = clock.GetUtcNow();
            if (!await administrators.IsCurrentAdministratorAsync(actor, now, token)) throw Error("access_denied", 403);
            var approver = await authentication.GetAccountAsync(actor.AccountId, now, token);
            var target = await authentication.GetAccountAsync(accountId, now, token) ?? throw Error("access_resource_not_found", 404);
            if (!TargetEligible(target, now) || target.Phone is null) throw Error("account_ineligible", 409);
            administrators.ExpectAccountVersion(target.Account, version);
            var assignment = approver?.Access.Where(p => p.Assignment.IsEffectiveAt(now) && p.Assignment.Role.IsActive &&
                p.Assignment.Role.IsSystemRole && p.Scope.Kind == AccessScopeKind.All)
                .OrderByDescending(p => p.Assignment.Role.SystemRole).FirstOrDefault()?.Assignment;
            if (assignment is null || !await HasAuthorityAsync(approver!, target, assignment.Id, now, token))
                throw Error("access_denied", 403);
            if (request.ExpiresAt <= now || request.ExpiresAt > now.AddHours(24)) throw Error("invalid_recovery_expiry", 400);
            if (phone != target.Phone.PhoneNumber && await identities.IsPhoneBoundAsync(phone, token))
                throw Error("login_phone_conflict", 409);
            if (phone == target.Phone.PhoneNumber && target.Phone.IsApprovedFor(target.Account, now))
                throw Error("login_phone_unchanged", 409);

            var change = new LoginPhoneChange(target.Account, target.Phone, assignment, phone, request.Reason,
                request.ApprovalReference, now, request.ExpiresAt);
            if (target.Phone.LoginPhoneChange is { CompletedAt: null, SupersededAt: null } previous) previous.Supersede(now);
            target.Phone.Revoke(now); target.Account.InvalidateSecurityVersion();
            await RevokeAccessAsync(target.Account.Id, now, token);
            // Flush the old active unique key before adding its replacement; the outer transaction still owns every write.
            await recoveries.SavePredecessorAsync(token);
            recoveries.AddChange(change);
            identities.AddBinding(LoginPhoneBinding.CreateRecoveryBinding(target.Account, change, now));
            authentication.AddAudit(new AuditEvent("account.phone.recovery.approved", actor.AccountId.ToString(),
                assignment.Role.Code, "phone-recovery", change.Id.ToString(), now, context.CorrelationId,
                employeeScopeId: target.Account.EmployeeId, protectedChangesReference: request.ApprovalReference,
                ipAddress: context.IpAddress, deviceReference: $"session:{actor.SessionId}"));
            await administrators.SaveChangesAsync(token);
            return new PhoneRecoveryApprovalResponse(change.Id, change.ExpiresAt, Convert.ToBase64String(target.Account.RowVersion));
        }, ct);
    }

    public async Task<OtpRequestResponse> RequestAsync(PhoneRecoveryRequest request, AuthRequestContext context,
        CancellationToken ct, Guid? previousChallengeId = null)
    {
        EnsureConfigured();
        var started = Stopwatch.GetTimestamp();
        var now = clock.GetUtcNow();
        var response = new OtpRequestResponse(Guid.NewGuid(), now.AddSeconds(options.OtpLifetimeSeconds), now.AddSeconds(options.ResendCooldownSeconds));
        try
        {
            await authentication.AtomicAsync(async token =>
            {
                now = clock.GetUtcNow();
                var phone = NormalizeOrNull(request.PhoneNumber);
                if (!await EdgeAsync("request", options.RequestsGlobal, options.RequestsPerIp, context, now, token) ||
                    !await BudgetAsync("request-phone:" + secrets.Digest(phone ?? "invalid"), options.RequestsPerPhone, now, token) ||
                    phone is null || !ValidContext(request.ContextSecret, request.Platform)) return false;
                var state = await authentication.FindByPhoneAsync(phone, now, token);
                if (state is null || !await EligibleAsync(state, now, token) ||
                    !await BudgetAsync("phone-recovery-request:" + state.Account.Id, options.RequestsPerPhone, now, token)) return false;
                var change = state.Phone!.LoginPhoneChange!;
                // Use the same public lifetime for all requests; a nearly expired approval needs a new administrative review.
                if (response.ExpiresAt > change.ExpiresAt || now >= response.ExpiresAt) return false;
                var hash = ContextHash(request.ContextSecret, request.Platform);
                var latest = await recoveries.GetLatestChallengeAsync(state.Account.Id, token);
                if (previousChallengeId.HasValue && (latest is null || latest.Id != previousChallengeId || latest.ConsumedAt.HasValue ||
                    latest.DeviceHash != hash || !Matches(latest, state))) return false;
                if (latest is not null && latest.IssuedAt.AddSeconds(options.ResendCooldownSeconds) > now) return false;
                foreach (var old in await authentication.GetOpenChallengesAsync(state.Account.Id, token))
                    if (old.Purpose == "PhoneRecovery") old.Revoke(now);
                var code = secrets.GenerateCode();
                var challenge = MobileOtpChallenge.CreatePhoneRecovery(response.ChallengeId, state.Phone, state.Account.SecurityVersion,
                    hash, request.Platform, secrets.ComputeVerifier(response.ChallengeId, code), now, response.ExpiresAt, options.MaximumOtpAttempts);
                authentication.AddChallenge(challenge, new OtpDelivery(challenge, secrets.ProtectDelivery(phone, code, response.ExpiresAt), now));
                Audit("account.phone.recovery.otp.requested", "anonymous", change.Id, now, context);
                return true;
            }, ct);
            return response;
        }
        finally
        {
            var remaining = TimeSpan.FromMilliseconds(options.MinimumRequestDurationMilliseconds) - Stopwatch.GetElapsedTime(started);
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining, ct);
        }
    }

    public async Task VerifyAsync(PhoneRecoveryVerifyRequest request, AuthRequestContext context, CancellationToken ct)
    {
        EnsureConfigured();
        var result = await authentication.AtomicAsync(async token =>
        {
            var now = clock.GetUtcNow();
            if (!await EdgeAsync("verify", options.VerificationAttemptsGlobal, options.VerificationAttemptsPerIp, context, now, token))
                return new AuthenticationResult<bool>(false, "temporarily_limited", 429);
            var proof = await authentication.GetChallengeAsync(request.ChallengeId, token);
            if (proof is null || proof.Purpose != "PhoneRecovery" || proof.Platform != request.Platform) return Rejected();
            var state = await authentication.GetAccountAsync(proof.UserAccountId, now, token);
            var allowed = await BudgetAsync("phone-recovery-verify:" + proof.UserAccountId, options.VerificationAttemptsPerPhone, now, token);
            allowed &= await BudgetAsync("verify-account:" + proof.UserAccountId, options.VerificationAttemptsPerPhone, now, token);
            allowed &= await BudgetAsync("verify-phone:" + secrets.Digest(proof.PhoneBinding.PhoneNumber), options.VerificationAttemptsPerPhone, now, token);
            var code = request.Code is { Length: 6 } && request.Code.All(char.IsAsciiDigit) ? request.Code : string.Empty;
            if (!allowed || !proof.IsUsableAt(now) || !ValidContext(request.ContextSecret, request.Platform) ||
                proof.DeviceHash != ContextHash(request.ContextSecret, request.Platform) || !secrets.Verify(proof.Id, code, proof.Verifier))
            {
                if (proof.IsUsableAt(now)) proof.RecordFailure(now);
                Audit("account.phone.recovery.otp.rejected", "anonymous", proof.Id, now, context);
                return Rejected();
            }
            if (state is null || !Matches(proof, state) || !await EligibleAsync(state, now, token))
            { proof.Revoke(now); return Rejected(); }
            var change = state.Phone!.LoginPhoneChange!;
            change.Complete(state.Account, state.Phone, proof, now);
            await RevokeAccessAsync(state.Account.Id, now, token);
            Audit("account.phone.recovery.completed", state.Account.Id.ToString(), change.Id, now, context);
            return new AuthenticationResult<bool>(true);
        }, ct);
        _ = result.RequireValue();
    }

    public async Task<bool> EligibleAsync(AuthenticationAccount state, DateTimeOffset now, CancellationToken ct)
    {
        if (!options.Enabled || !options.IsValid() || !TargetEligible(state, now) ||
            state.Phone is not { RevokedAt: null, VerifiedAt: null, LoginPhoneChange: { } change } ||
            change.UserAccountId != state.Account.Id || change.Id != state.Phone.LoginPhoneChangeId ||
            !change.IsPendingAt(now) || change.SecurityVersion != state.Account.SecurityVersion ||
            change.NewPhoneNumber != state.Phone.PhoneNumber || state.Phone.CreatedAt < change.ApprovedAt || state.Phone.CreatedAt > now)
            return false;
        var approver = await authentication.GetAccountAsync(change.ApprovedByUserId, now, ct);
        return approver is not null && await HasAuthorityAsync(approver, state, change.ApprovedByAssignmentId, now, ct);
    }

    private async Task<bool> HasAuthorityAsync(AuthenticationAccount approver, AuthenticationAccount target,
        Guid assignmentId, DateTimeOffset now, CancellationToken ct)
    {
        if (approver.Account.Id == target.Account.Id || !TargetEligible(approver, now) ||
            approver.Account.Status != AccountStatus.Active || !approver.Account.WebAccessEnabled ||
            approver.Phone is not { VerifiedAt: not null } || !approver.Phone.IsApprovedFor(approver.Account, now)) return false;
        var pair = approver.Access.FirstOrDefault(p => p.Assignment.Id == assignmentId && p.Assignment.IsEffectiveAt(now) &&
            p.Assignment.UserAccountId == approver.Account.Id && p.Assignment.Role.IsActive && p.Assignment.Role.IsSystemRole &&
            p.Scope.UserRoleAssignmentId == p.Assignment.Id && p.Scope.Kind == AccessScopeKind.All);
        if (pair is null || (await administrators.HasSystemRoleAsync(target.Account.Id, now, true, ct) &&
            pair.Assignment.Role.SystemRole != SystemRoleKind.SuperAdmin)) return false;
        var actor = new AuthenticatedAccount(approver.Account.Id, approver.Account.EmployeeId, Guid.Empty,
            approver.Account.SecurityVersion, SessionClient.Web, true);
        var grants = await permissions.GetGrantsAsync(actor.AccountId, now, ct);
        if (!new[] { AccessPermissionCatalog.AccountsMobileManage, AccessPermissionCatalog.AccountsWebManage,
            AccessPermissionCatalog.RolePermissionsManage }.All(p => AccessPermissionEvaluator.HasAllScope(actor, grants, p))) return false;
        return (await administrators.GetPotentialAccountPermissionsAsync(target.Account.Id, now, ct))
            .All(p => AccessPermissionEvaluator.HasAllScope(actor, grants, p.Permission.Code));
    }

    private bool TargetEligible(AuthenticationAccount state, DateTimeOffset now)
    {
        var a = state.Account; var e = a.Employee; var approval = a.ApprovalDecision;
        if (!options.IsValid() || string.IsNullOrWhiteSpace(options.BusinessTimeZoneId)) return false;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(options.BusinessTimeZoneId)).DateTime);
        return a.Status is AccountStatus.Active or AccountStatus.ReadyForInvitation && a.CreatedAt <= now &&
            a.CurrentSetupInvitationId is null && a.EmployeeId == e.Id && e.Status == EmployeeStatus.Active &&
            state.EffectiveEmploymentStatus == EmployeeStatus.Active && !state.HasConflictingStatus &&
            e.JoinedOn.HasValue && e.JoinedOn <= today &&
            e.OnboardingStatus == Cosmix.Domain.HR.Onboarding.OnboardingStatus.Approved && e.ApprovedOnboardingDecisionId == approval.Id &&
            a.ApprovalDecisionId == approval.Id && approval.EmployeeId == e.Id && approval.DecidedAt <= now &&
            approval.Decision == Cosmix.Domain.HR.Onboarding.OnboardingDecision.Approve;
    }

    private async Task RevokeAccessAsync(Guid accountId, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var challenge in await authentication.GetOpenChallengesAsync(accountId, ct)) challenge.Revoke(now);
        foreach (var session in await authentication.GetSessionsAsync(accountId, ct))
            if (!session.RevokedAt.HasValue) session.Revoke("phone-recovery", now);
    }
    private async Task<bool> EdgeAsync(string key, int global, int ip, AuthRequestContext context, DateTimeOffset now, CancellationToken ct) =>
        await BudgetAsync(key + "-global", global, now, ct) && await BudgetAsync(key + "-ip:" + secrets.Digest(context.IpAddress), ip, now, ct);
    private async Task<bool> BudgetAsync(string key, int limit, DateTimeOffset now, CancellationToken ct)
    {
        var bucket = await authentication.GetRateBucketAsync(key, ct);
        if (bucket is null) { bucket = new AuthRateBucket(key, now, now.AddSeconds(options.RateWindowSeconds)); authentication.AddRateBucket(bucket); }
        return bucket.TryConsume(limit, now);
    }
    private string ContextHash(string value, string platform) => secrets.Digest("phone-recovery:" + platform + ":" + value);
    private string? NormalizeOrNull(string value)
    {
        try { return phones.Normalize(value); }
        catch (ArgumentException) { return null; }
    }
    private static bool ValidContext(string value, string platform) => !string.IsNullOrWhiteSpace(value) && value.Length is >= 32 and <= 256 && platform is "web" or "android" or "ios";
    private static bool Matches(MobileOtpChallenge proof, AuthenticationAccount state) => proof.PhoneBindingId == state.Phone?.Id && proof.SecurityVersion == state.Account.SecurityVersion;
    private static AuthenticationResult<bool> Rejected() => new(false, "invalid_authentication");
    private static AuthenticationFailure Error(string code, int status) => new(code, status);
    private void Audit(string name, string actor, Guid id, DateTimeOffset now, AuthRequestContext context) =>
        authentication.AddAudit(new AuditEvent(name, actor, "authentication", "phone-recovery", id.ToString(), now,
            context.CorrelationId, ipAddress: context.IpAddress));
    private void EnsureConfigured()
    {
        if (!options.Enabled || !options.IsValid() || !secrets.IsConfigured || !sender.IsConfigured)
            throw Error("authentication_unavailable", 503);
    }
}
