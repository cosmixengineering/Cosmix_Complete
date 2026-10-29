using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Application.Authentication.Web;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Infrastructure.Authentication.Providers;
using OtpNet;
using Xunit;

namespace Cosmix.Tests;

public sealed class WebAuthenticatorTests
{
    [Fact]
    public async Task Authenticator_login_and_refresh_work_without_sms_and_keep_browser_binding()
    {
        var f = await Fixture.Create();
        var challenge = await f.Login();
        Assert.Equal("enter_authenticator", challenge.NextStep);
        var result = await f.Verify(challenge.ChallengeId, f.Code);
        var session = result.RequireValue();
        Assert.NotNull(await f.Validate(session.SessionId));
        Assert.Null(await f.Validate(session.SessionId, new string('z', 40)));
        Assert.NotNull((await f.Service.RefreshResultAsync(new(session.RefreshToken, Fixture.Browser), Fixture.Context, default)).Value);
        Assert.Null((await f.Verify(challenge.ChallengeId, f.Code)).Value);
        f.State.Phone!.Revoke(f.Clock.Now);
        Assert.Null(await f.Validate(session.SessionId));
    }

    [Fact]
    public async Task Recovery_codes_are_single_use_and_sms_cannot_downgrade_enrolled_account()
    {
        var f = await Fixture.Create(smsAvailable: true);
        var recovery = f.Proofs.RecoveryCode(f.State.Account.Id);
        var credential = new MfaCredential(f.State.Account.Id, MfaMethod.RecoveryCode, null, recovery.Digest, f.Clock.Now);
        credential.Confirm(f.Clock.Now); f.Credentials.Add(credential);
        await f.Service.LoginAsync(f.Request, Fixture.Context, default);
        Assert.Empty(f.SmsChallenges);
        var challenge = await f.Login();
        var session = (await f.Verify(challenge.ChallengeId, recovery.Code, recovery: true)).RequireValue();
        Assert.NotNull(await f.Validate(session.SessionId));
        f.Clock.Now = f.Clock.Now.AddSeconds(31);
        var next = await f.Login();
        Assert.Null((await f.Verify(next.ChallengeId, recovery.Code, recovery: true)).Value);
        Assert.Single(f.Sessions);
        credential.Revoke(f.Clock.Now);
        Assert.Null(await f.Validate(session.SessionId));
    }

    [Theory]
    [InlineData("browser")]
    [InlineData("expired")]
    [InlineData("version")]
    [InlineData("password")]
    [InlineData("employee")]
    public async Task Changed_authority_or_context_never_issues_a_session(string change)
    {
        var f = await Fixture.Create(); var challenge = await f.Login();
        if (change == "expired") f.Clock.Now = f.Clock.Now.AddMinutes(6);
        if (change == "version") f.State.Account.InvalidateSecurityVersion();
        if (change == "password") Set(f.State.Account, nameof(UserAccount.PasswordChangedAt), f.Clock.Now.AddSeconds(1));
        if (change == "employee") Set(f.State.Account.Employee, nameof(Employee.Status), EmployeeStatus.Suspended);
        var result = await f.Verify(challenge.ChallengeId, f.Code, browser: change == "browser" ? new string('x', 40) : Fixture.Browser);
        Assert.Null(result.Value); Assert.Empty(f.Sessions);
    }

    [Fact]
    public async Task Wrong_password_is_opaque_and_wrong_codes_commit_attempt_limits()
    {
        var f = await Fixture.Create();
        var hidden = await f.Service.LoginAuthenticatorAsync(f.Request with { Password = "wrong" }, Fixture.Context, default);
        Assert.Equal("enter_authenticator", hidden.NextStep); Assert.Empty(f.Challenges);
        var challenge = await f.Login();
        for (var i = 0; i < 5; i++) Assert.Null((await f.Verify(challenge.ChallengeId, "invalid")).Value);
        Assert.Equal(5, f.Challenges.Single().FailedAttempts); Assert.NotNull(f.Challenges.Single().RevokedAt);
        Assert.Null((await f.Verify(challenge.ChallengeId, f.Code)).Value);
    }

    [Fact]
    public async Task Enrollment_requires_current_password_and_confirmation_revokes_sessions_and_returns_codes_once()
    {
        var f = await Fixture.Create(); var (management, session) = f.SmsManagement();
        await Assert.ThrowsAsync<AuthenticationFailure>(() => management.BeginAsync(new("wrong", Fixture.Browser), Fixture.Context, default));
        Assert.Empty(f.Credentials);
        var setup = await management.BeginAsync(new(f.Request.Password, Fixture.Browser), Fixture.Context, default);
        Assert.False(f.State.Account.AuthenticatorRequired);
        var code = new Totp(Base32Encoding.ToBytes(setup.Base32Secret)).ComputeTotp(f.Clock.Now.UtcDateTime);
        var confirmed = await management.ConfirmAsync(new(setup.CredentialId, code, Fixture.Browser), Fixture.Context, default);
        Assert.True(f.State.Account.AuthenticatorRequired); Assert.Equal(10, confirmed.RecoveryCodes.Distinct().Count());
        Assert.NotNull(session.RevokedAt); Assert.Equal(2, f.State.Account.SecurityVersion);
        Assert.All(f.Credentials.Where(c => c.Method == MfaMethod.RecoveryCode), c =>
            Assert.DoesNotContain(c.RecoveryCodeHash, confirmed.RecoveryCodes));
        await Assert.ThrowsAsync<AuthenticationFailure>(() => management.ConfirmAsync(new(setup.CredentialId, code, Fixture.Browser), Fixture.Context, default));
    }

    [Fact]
    public async Task Enrollment_cannot_be_confirmed_from_another_browser_or_after_expiry()
    {
        var f = await Fixture.Create(); var (management, _) = f.SmsManagement();
        var setup = await management.BeginAsync(new(f.Request.Password, Fixture.Browser), Fixture.Context, default);
        var code = new Totp(Base32Encoding.ToBytes(setup.Base32Secret)).ComputeTotp(f.Clock.Now.UtcDateTime);
        await Assert.ThrowsAsync<AuthenticationFailure>(() => management.ConfirmAsync(new(setup.CredentialId, code, new string('z', 40)), Fixture.Context, default));
        f.Clock.Now = f.Clock.Now.AddMinutes(11);
        await Assert.ThrowsAsync<AuthenticationFailure>(() => management.ConfirmAsync(new(setup.CredentialId, code, Fixture.Browser), Fixture.Context, default));
        Assert.False(f.State.Account.AuthenticatorRequired);
    }

    [Fact]
    public async Task Replacing_recovery_codes_requires_fresh_totp_and_invalidates_old_recovery_sessions()
    {
        var f = await Fixture.Create(); var generated = f.Proofs.RecoveryCode(f.State.Account.Id);
        var recovery = new MfaCredential(f.State.Account.Id, MfaMethod.RecoveryCode, null, generated.Digest, f.Clock.Now);
        recovery.Confirm(f.Clock.Now); f.Credentials.Add(recovery);
        var challenge = await f.Login(); var session = (await f.Verify(challenge.ChallengeId, generated.Code, true)).RequireValue();
        var actor = new AuthenticatedAccount(f.State.Account.Id, f.State.Account.EmployeeId, session.SessionId, f.State.Account.SecurityVersion, SessionClient.Web, true);
        var service = new WebMfaManagementService(f.Authentication, f.Mfa, f.Service, f.Proofs, Proxy<ICurrentAuthentication>((_, _) => actor), f.Passwords, f.Protector, f.Options, f.Common, f.Clock);
        await Assert.ThrowsAsync<AuthenticationFailure>(() => service.RegenerateAsync(new(f.Request.Password, "wrong", Fixture.Browser), Fixture.Context, default));
        Assert.Null(recovery.RevokedAt);
        var result = await service.RegenerateAsync(new(f.Request.Password, f.Code, Fixture.Browser), Fixture.Context, default);
        Assert.Equal(10, result.RecoveryCodes.Count); Assert.NotNull(recovery.RevokedAt);
        Assert.Null(await f.Validate(session.SessionId));
    }

    [Theory]
    [InlineData("allowed", true)]
    [InlineData("own-account", false)]
    [InlineData("own-employee", false)]
    [InlineData("no-permission", false)]
    [InlineData("system-target", false)]
    [InlineData("stale-session", false)]
    [InlineData("stale-version", false)]
    public async Task Reset_requires_independent_current_authority_and_revokes_all_access(string scenario, bool allowed)
    {
        var f = await Fixture.Create(); var challenge = await f.Login();
        var session = (await f.Verify(challenge.ChallengeId, f.Code)).RequireValue();
        var actor = new AuthenticatedAccount(scenario == "own-account" ? f.State.Account.Id : Guid.NewGuid(),
            scenario == "own-employee" ? f.State.Account.EmployeeId : Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
        var admin = Proxy<IAccessManagementRepository>((m, a) => m.Name switch
        {
            "InTransactionAsync" => typeof(Fixture).GetMethod("Transaction", BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(m.GetGenericArguments()).Invoke(null, a),
            "IsCurrentAdministratorAsync" => Task.FromResult(scenario != "stale-session"),
            "IsCurrentSuperAdminAsync" => Task.FromResult(false),
            "HasSystemRoleAsync" => Task.FromResult(scenario == "system-target"),
            "GetPotentialAccountPermissionsAsync" => Task.FromResult<IReadOnlyList<RolePermission>>([]),
            "ExpectAccountVersion" => scenario == "stale-version" ? throw new AuthenticationFailure("access_version_conflict", 409) : null,
            _ => throw new InvalidOperationException(m.Name)
        });
        var permissions = Proxy<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(scenario == "no-permission" ? [] :
            new[] { AccessPermissionCatalog.AccountsWebManage, AccessPermissionCatalog.RolePermissionsManage }.Select(p => new PermissionGrant(p, true, Guid.NewGuid(), AccessScopeKind.All, null)).ToArray()));
        var service = new WebMfaResetService(f.Authentication, f.Mfa, admin, permissions, Proxy<ICurrentAuthentication>((_, _) => actor), f.Service, new CopyingFiles(), f.Clock);
        var action = () => service.ApproveAsync(f.State.Account.Id, new(Convert.ToBase64String(f.State.Account.RowVersion), "Synthetic independent identity review", "SYNTHETIC-REVIEW-1"), Fixture.Context, default);
        if (!allowed)
        {
            await Assert.ThrowsAsync<AuthenticationFailure>(action); Assert.True(f.State.Account.AuthenticatorRequired);
            Assert.Empty(f.Resets); Assert.Null(f.Sessions.Single().RevokedAt); return;
        }
        var result = await action(); Assert.Equal(f.Resets.Single().Id, result.ApprovalId);
        Assert.False(f.State.Account.AuthenticatorRequired); Assert.Equal(2, f.State.Account.SecurityVersion);
        Assert.All(f.Credentials, c => Assert.NotNull(c.RevokedAt)); Assert.NotNull(f.Sessions.Single().RevokedAt);
        Assert.Null(await f.Validate(session.SessionId));
    }

    internal sealed class Fixture
    {
        public const string Browser = "synthetic-browser-secret-01234567890123456789";
        public static readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-mfa-test");
        public readonly TestClock Clock = new();
        public AuthenticationAccount State = null!;
        public readonly List<MfaCredential> Credentials = [];
        public readonly List<MfaChallenge> Challenges = [];
        public readonly List<MobileOtpChallenge> SmsChallenges = [];
        public readonly List<UserSession> Sessions = [];
        public readonly List<RefreshToken> Refreshes = [];
        public readonly List<MfaResetApproval> Resets = [];
        public readonly Dictionary<string, AuthRateBucket> Buckets = [];
        public WebAuthenticationService Service = null!;
        public WebMfaProofs Proofs = null!;
        public IAuthenticationRepository Authentication = null!;
        public IWebMfaRepository Mfa = null!;
        public IOtpProtector Protector = null!;
        public IWebPasswordVerifier Passwords = null!;
        public readonly MobileAuthenticationOptions Common = new()
        {
            Enabled = true, BusinessTimeZoneId = "UTC", OtpLifetimeSeconds = 300, ResendCooldownSeconds = 30,
            MaximumOtpAttempts = 5, RateWindowSeconds = 300, RequestsPerPhone = 20, RequestsPerIp = 100,
            RequestsGlobal = 200, VerificationAttemptsPerPhone = 50, VerificationAttemptsPerIp = 100,
            VerificationAttemptsGlobal = 200, AccessTokenLifetimeSeconds = 300, RefreshTokenLifetimeSeconds = 600,
            SessionLifetimeSeconds = 900, MinimumRequestDurationMilliseconds = 100
        };
        public readonly WebAuthenticationOptions Options = new()
        {
            Enabled = true, MinimumPasswordLength = 12, MaximumPasswordFailures = 5, PasswordLockoutSeconds = 60,
            PasswordAttemptsPerAccount = 50, PasswordAttemptsPerEmail = 50, PasswordAttemptsPerIp = 100, PasswordAttemptsGlobal = 200
        };
        private byte[] secret = [];
        public string Code => new Totp(secret).ComputeTotp(Clock.Now.UtcDateTime);
        public WebLoginRequest Request => new(State.Account.LoginName, "Synthetic-password-123!", Browser);
        public Task<OtpRequestResponse> Login() => Service.LoginAuthenticatorAsync(Request, Context, default);
        public Task<AuthenticationResult<SessionResponse>> Verify(Guid challenge, string code, bool recovery = false, string browser = Browser) =>
            Service.VerifyAuthenticatorResultAsync(new(challenge, code, recovery, browser), Context, default);
        public Task<AuthenticatedAccount?> Validate(Guid session, string browser = Browser) =>
            Service.ValidateBrowserSessionAsync(State.Account.Id, session, State.Account.SecurityVersion, browser, default);

        public (WebMfaManagementService Management, UserSession Session) SmsManagement()
        {
            Set(State.Account, nameof(UserAccount.AuthenticatorRequired), false); Credentials.Clear();
            Set(State.Phone!, nameof(LoginPhoneBinding.UserAccount), State.Account);
            var proof = MobileOtpChallenge.CreateWebLogin(Guid.NewGuid(), State.Phone!, State.Account.SecurityVersion,
                Protector.Digest("browser:" + Browser), new string('A', 64), Clock.Now, Clock.Now.AddMinutes(5), 5);
            SmsChallenges.Add(proof);
            var session = new UserSession(State.Account, SessionClient.Web, null, null, Clock.Now, Clock.Now.AddMinutes(30), "synthetic", proof);
            Sessions.Add(session);
            var actor = new AuthenticatedAccount(State.Account.Id, State.Account.EmployeeId, session.Id, State.Account.SecurityVersion, SessionClient.Web, true);
            var current = Proxy<ICurrentAuthentication>((_, _) => actor);
            return (new(Authentication, Mfa, Service, Proofs, current, Passwords, Protector, Options, Common, Clock), session);
        }

        public static async Task<Fixture> Create(bool smsAvailable = false)
        {
            var f = new Fixture(); f.State = Account(f.Clock.Now);
            f.Protector = Proxy<IOtpProtector>((m, a) => m.Name switch
            {
                "get_IsConfigured" => true,
                "Digest" => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("synthetic-test-key-only"), Encoding.UTF8.GetBytes((string)a[0]!))),
                "NewRefreshToken" => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                _ => throw new InvalidOperationException(m.Name)
            });
            f.Passwords = Proxy<IWebPasswordVerifier>((m, a) => m.Name == "Verify" ? (string)a[1]! == "Synthetic-password-123!" : null);
            var sms = Proxy<IOtpSmsSender>((m, _) => m.Name == "get_IsConfigured" ? smsAvailable : throw new InvalidOperationException(m.Name));
            var issuer = Proxy<IAccessTokenIssuer>((m, _) => m.Name == "get_IsConfigured" ? true : "synthetic-token");
            f.Proofs = new(new TotpProvider(), f.Protector, new CopyingFiles());
            var setup = await f.Proofs.CreateAsync(f.State.Account.Id, "synthetic@example.invalid", default);
            f.secret = setup.Enrollment.Secret;
            var credential = new MfaCredential(f.State.Account.Id, MfaMethod.Totp, setup.ProtectedReference, null, f.Clock.Now.AddMinutes(-1));
            credential.Confirm(f.Clock.Now.AddMinutes(-1)); f.Credentials.Add(credential);
            f.Mfa = Proxy<IWebMfaRepository>((m, a) => m.Name switch
            {
                "CredentialsAsync" => Task.FromResult<IReadOnlyList<MfaCredential>>(f.Credentials.Where(c => c.RevokedAt is null).ToArray()),
                "ChallengeAsync" => Task.FromResult(f.Challenges.SingleOrDefault(c => c.Id == (Guid)a[0]!)),
                "LatestChallengeAsync" => Task.FromResult(f.Challenges.LastOrDefault()),
                "OpenChallengesAsync" => Task.FromResult<IReadOnlyList<MfaChallenge>>(f.Challenges.Where(c => c.RevokedAt is null && c.ConsumedAt is null).ToArray()),
                "Add" => f.AddMfa(a[0]!),
                _ => throw new InvalidOperationException(m.Name)
            });
            f.Authentication = Proxy<IAuthenticationRepository>((m, a) => m.Name switch
            {
                "AtomicAsync" => m.GetGenericArguments() is var types ? typeof(Fixture).GetMethod(nameof(Transaction), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(types).Invoke(null, a) : null,
                "GetAccountAsync" => Task.FromResult<AuthenticationAccount?>((Guid)a[0]! == f.State.Account.Id ? f.State : null),
                "GetRateBucketAsync" => Task.FromResult(f.Buckets.GetValueOrDefault((string)a[0]!)),
                "AddRateBucket" => f.AddBucket((AuthRateBucket)a[0]!),
                "GetSessionAsync" => Task.FromResult(f.Sessions.SingleOrDefault(s => s.Id == (Guid)a[0]!)),
                "GetChallengeAsync" => Task.FromResult(f.SmsChallenges.SingleOrDefault(s => s.Id == (Guid)a[0]!)),
                "GetSessionsAsync" => Task.FromResult<IReadOnlyList<UserSession>>(f.Sessions.ToArray()),
                "GetOpenChallengesAsync" => Task.FromResult<IReadOnlyList<MobileOtpChallenge>>(f.SmsChallenges.Where(c => c.RevokedAt is null && c.ConsumedAt is null).ToArray()),
                "FindRefreshTokenAsync" => Task.FromResult(f.Refreshes.SingleOrDefault(r => r.TokenHash == (string)a[0]!)),
                "AddRefreshToken" => f.AddRefresh((RefreshToken)a[0]!),
                "AddSession" => f.AddSession((UserSession)a[0]!, (RefreshToken)a[1]!),
                "AddAudit" => null,
                _ => throw new InvalidOperationException(m.Name)
            });
            var web = Proxy<IWebAuthenticationRepository>((m, _) => m.Name switch
            {
                "FindAccountIdByEmailAsync" => Task.FromResult<Guid?>(f.State.Account.Id),
                "GetLatestWebChallengeAsync" => Task.FromResult<MobileOtpChallenge?>(null),
                _ => throw new InvalidOperationException(m.Name)
            });
            f.Service = new(f.Authentication, web, f.Passwords, f.Protector, sms, issuer, f.Options, f.Common, f.Clock, f.Mfa, f.Proofs);
            return f;
        }
        private static Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
        private object? AddMfa(object value) { if (value is MfaChallenge c) Challenges.Add(c); else if (value is MfaCredential k) Credentials.Add(k); else if (value is MfaResetApproval r) Resets.Add(r); else throw new InvalidOperationException(); return null; }
        private object? AddBucket(AuthRateBucket bucket) { Buckets.Add(bucket.Key, bucket); return null; }
        private object? AddRefresh(RefreshToken refresh) { Set(refresh, nameof(RefreshToken.UserSession), Sessions.Single(s => s.Id == refresh.UserSessionId)); Refreshes.Add(refresh); return null; }
        private object? AddSession(UserSession session, RefreshToken refresh) { Sessions.Add(session); return AddRefresh(refresh); }
    }
    internal static AuthenticationAccount Account(DateTimeOffset now)
    {
        var before = now.AddHours(-1); var employee = Empty<Employee>();
        Set(employee, nameof(Employee.Id), Guid.NewGuid()); Set(employee, nameof(Employee.Status), EmployeeStatus.Active);
        Set(employee, nameof(Employee.OnboardingStatus), OnboardingStatus.Approved); Set(employee, nameof(Employee.JoinedOn), new DateOnly(2026, 1, 1));
        var submission = Empty<EmployeeSubmission>(); Set(submission, nameof(EmployeeSubmission.Id), Guid.NewGuid());
        Set(submission, nameof(EmployeeSubmission.EmployeeId), employee.Id); Set(submission, nameof(EmployeeSubmission.ApprovedLoginPhoneNumber), "+12025550199");
        var approval = Empty<EmployeeOnboardingDecision>(); Set(approval, nameof(EmployeeOnboardingDecision.Id), Guid.NewGuid());
        Set(approval, nameof(EmployeeOnboardingDecision.EmployeeId), employee.Id); Set(approval, nameof(EmployeeOnboardingDecision.SubmissionId), submission.Id);
        Set(approval, nameof(EmployeeOnboardingDecision.Submission), submission); Set(approval, nameof(EmployeeOnboardingDecision.Decision), OnboardingDecision.Approve);
        Set(approval, nameof(EmployeeOnboardingDecision.DecidedAt), before); Set(employee, nameof(Employee.ApprovedOnboardingDecisionId), approval.Id);
        var account = new UserAccount(employee, approval, "synthetic@example.invalid", before);
        Set(account, nameof(UserAccount.Status), AccountStatus.Active); Set(account, nameof(UserAccount.WebAccessEnabled), true);
        Set(account, nameof(UserAccount.AuthenticatorRequired), true); Set(account, nameof(UserAccount.ActivatedAt), before);
        Set(account, nameof(UserAccount.PasswordChangedAt), before); Set(account, nameof(UserAccount.PasswordHash), "synthetic");
        Set(account, nameof(UserAccount.PasswordHashFormat), WebPasswordFormats.IdentityV3); Set(account, nameof(UserAccount.RowVersion), new byte[8]);
        var phone = Empty<LoginPhoneBinding>(); Set(phone, nameof(LoginPhoneBinding.Id), Guid.NewGuid());
        Set(phone, nameof(LoginPhoneBinding.UserAccountId), account.Id); Set(phone, nameof(LoginPhoneBinding.ApprovedSubmissionId), submission.Id);
        Set(phone, nameof(LoginPhoneBinding.PhoneNumber), submission.ApprovedLoginPhoneNumber); Set(phone, nameof(LoginPhoneBinding.CreatedAt), before); Set(phone, nameof(LoginPhoneBinding.VerifiedAt), before);
        var role = new Role("SYNTHETIC", "Synthetic employee", Guid.NewGuid(), before);
        var assignment = new UserRoleAssignment(account, role, Guid.NewGuid(), before, null); var scope = new UserAccessScope(assignment, AccessScopeKind.All);
        Set(account, nameof(UserAccount.InitialRoleAssignmentId), assignment.Id); Set(account, nameof(UserAccount.InitialAccessScopeId), scope.Id);
        return new(account, phone, [new(assignment, scope)], EmployeeStatus.Active, false);
    }
    internal sealed class TestClock : TimeProvider
    { public DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
    internal sealed class CopyingFiles : IPrivateFileStore
    {
        private readonly Dictionary<string, (byte[] Bytes, string Purpose)> files = [];
        public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
        {
            var id = Guid.NewGuid().ToString("N"); files.Add(id, (content.ToArray(), purpose));
            return Task.FromResult(new PrivateFileReference(id, "1", Convert.ToHexString(SHA256.HashData(content)), "synthetic", content.Length));
        }
        public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
        { var file = files[reference.StorageKey]; Assert.Equal(file.Purpose, purpose); return Task.FromResult(file.Bytes.ToArray()); }
    }
    internal static T Empty<T>() => (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
    internal static void Set<T>(T target, string property, object? value) => typeof(T).GetProperty(property)!.SetValue(target, value);
    internal static T Proxy<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = DispatchProxy.Create<T, TestProxy>(); ((TestProxy)(object)value).Handler = handler; return value; }
    public class TestProxy : DispatchProxy
    { public Func<MethodInfo, object?[], object?> Handler = null!; protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []); }
}
