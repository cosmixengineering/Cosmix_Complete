using System.Reflection;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Xunit;

namespace Cosmix.Tests;

public class DeviceRecoveryExpiryTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-recovery-expiry");

    public static IEnumerable<object[]> ExpiryCases() =>
        from authority in new[] { "session", "role", "grant", "target-employment", "target-access" }
        from stage in new[] { "device-read", "sessions-read", "first-save", "second-save" }
        select new object[] { authority, stage };

    [Theory, MemberData(nameof(ExpiryCases))]
    public async Task Expired_authority_cannot_commit_recovery(string authority, string stage)
    {
        var f = new Fixture(authority, stage);
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(f.Approve);
        Assert.Contains(failure.StatusCode, new[] { 403, 409 });
        Assert.True(f.Advanced);
        Assert.False(f.Committed);
        Assert.Equal(1, f.Target.Account.SecurityVersion);
        Assert.Null(f.Previous.RevokedAt);
        Assert.Null(f.TargetSession.RevokedAt);
        Assert.Null(f.Proof.RevokedAt);
        Assert.Single(f.Devices);
        Assert.Empty(f.Audits);
        if (stage is "device-read" or "sessions-read") Assert.Equal(0, f.SaveCalls);
        else Assert.True(f.SaveCalls >= (stage == "first-save" ? 1 : 2));
    }

    [Theory]
    [InlineData("session")]
    [InlineData("role")]
    [InlineData("grant")]
    [InlineData("target-employment")]
    [InlineData("target-access")]
    public async Task Review_rechecks_authority_after_device_read(string authority)
    {
        var f = new Fixture(authority, "review-read");
        await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Service.ReviewAsync(f.Target.Account.Id,
            new AccessListRequest(), Context, default));
        Assert.True(f.Advanced);
        Assert.False(f.Committed);
        Assert.Empty(f.Audits);
    }

    [Theory]
    [InlineData("session")]
    [InlineData("role")]
    [InlineData("grant")]
    public async Task Authority_reads_cannot_reuse_time_from_before_await(string authority)
    {
        var f = new Fixture(authority, "authority-session-read");
        await Assert.ThrowsAsync<AuthenticationFailure>(f.Approve);
        Assert.True(f.Advanced);
        Assert.False(f.Committed);
        Assert.Equal(0, f.SaveCalls);
    }

    [Fact]
    public async Task Current_authority_replaces_device_and_preserves_fresh_otp_requirement()
    {
        var f = new Fixture();
        var result = await f.Approve();
        Assert.True(f.Committed);
        Assert.Equal(2, f.SaveCalls);
        Assert.Equal(2, f.Target.Account.SecurityVersion);
        Assert.NotNull(f.Previous.RevokedAt);
        Assert.NotNull(f.TargetSession.RevokedAt);
        Assert.NotNull(f.Proof.RevokedAt);
        var replacement = Assert.Single(f.Devices, d => d.RevokedAt is null);
        Assert.Equal(result.DeviceId, replacement.Id);
        Assert.Equal(f.Proof.DeviceHash, replacement.DeviceIdentifierHash);
        Assert.Null(replacement.VerifiedAt);
        Assert.Equal("request_fresh_sms_otp", result.NextStep);
        Assert.Single(f.Audits);
    }

    [Fact]
    public async Task Current_authority_can_review_without_changing_device_state()
    {
        var f = new Fixture();
        var result = await f.Service.ReviewAsync(f.Target.Account.Id, new AccessListRequest(), Context, default);
        Assert.Single(result.Devices);
        Assert.True(f.Committed);
        Assert.Equal(1, f.Target.Account.SecurityVersion);
        Assert.Null(f.Previous.RevokedAt);
    }

    private sealed class Fixture
    {
        private readonly string? authority;
        private readonly string? stage;
        private readonly MutableClock clock = new();
        private bool inTransaction;
        private readonly AuthenticationAccount approver;
        private readonly UserSession actorSession;
        public AuthenticationAccount Target { get; }
        public RegisteredDevice Previous { get; }
        public MobileOtpChallenge Proof { get; }
        public UserSession TargetSession { get; }
        public List<RegisteredDevice> Devices { get; } = [];
        public List<AuditEvent> Audits { get; } = [];
        public DeviceRecoveryService Service { get; }
        public bool Advanced { get; private set; }
        public bool Committed { get; private set; }
        public int SaveCalls { get; private set; }

        public Fixture(string? authority = null, string? stage = null)
        {
            this.authority = authority; this.stage = stage;
            approver = Account(true, authority == "role" ? Start.AddMinutes(1) : null);
            Target = Account(false, authority == "target-access" ? Start.AddMinutes(1) : null);
            var actor = new AuthenticatedAccount(approver.Account.Id, approver.Account.EmployeeId,
                Guid.NewGuid(), 1, SessionClient.Web, true);
            actorSession = Session(actor.AccountId, actor.SessionId, SessionClient.Web,
                authority == "session" ? Start.AddMinutes(1) : Start.AddHours(2));
            Previous = new(Target.Account.Id, new string('a', 64), "android", "Synthetic previous", Start.AddHours(-1));
            Previous.MarkVerified(Start.AddHours(-1));
            Set(Previous, nameof(RegisteredDevice.RowVersion), new byte[8]);
            Devices.Add(Previous);
            TargetSession = Session(Target.Account.Id, Guid.NewGuid(), SessionClient.Mobile, Start.AddHours(2));
            Proof = new(Guid.NewGuid(), Target.Phone!, 1, new string('b', 64), "android", "Synthetic replacement",
                new string('c', 64), Start.AddMinutes(-1), Start.AddMinutes(10), 5);
            Proof.MarkReplacementProof(Start.AddSeconds(-1));

            var current = Proxy<ICurrentAuthentication>((_, _) => actor);
            var authentication = Proxy<IAuthenticationRepository>((method, args) =>
            {
                Assert.True(inTransaction);
                switch (method.Name)
                {
                    case "GetAccountAsync":
                        var at = (DateTimeOffset)args[1]!;
                        if ((Guid)args[0]! == actor.AccountId) return Task.FromResult<AuthenticationAccount?>(approver);
                        return Task.FromResult<AuthenticationAccount?>(Target with
                        { EffectiveEmploymentStatus = authority == "target-employment" && at >= Start.AddMinutes(1)
                            ? EmployeeStatus.Suspended : EmployeeStatus.Active });
                    case "GetSessionAsync": Advance("authority-session-read"); return Task.FromResult<UserSession?>(actorSession);
                    case "GetDeviceAsync": Advance("device-read"); return Task.FromResult<RegisteredDevice?>(Previous);
                    case "GetNonRevokedDevicesAsync": return Task.FromResult<IReadOnlyList<RegisteredDevice>>(Devices.Where(d => d.RevokedAt is null).ToArray());
                    case "FindDeviceAsync": return Task.FromResult(Devices.FirstOrDefault(d => d.DeviceIdentifierHash == (string)args[1]!));
                    case "GetChallengeAsync": return Task.FromResult<MobileOtpChallenge?>(Proof);
                    case "GetOpenChallengesAsync": return Task.FromResult<IReadOnlyList<MobileOtpChallenge>>([]);
                    case "GetSessionsAsync": Advance("sessions-read"); return Task.FromResult<IReadOnlyList<UserSession>>([TargetSession]);
                    case "GetDevicesAsync": Advance("review-read"); return Task.FromResult<IReadOnlyList<RegisteredDevice>>(Devices.ToArray());
                    case "AddDevice": Devices.Add((RegisteredDevice)args[0]!); return null;
                    default: throw new InvalidOperationException(method.Name);
                }
            });
            var administrators = Proxy<IAccessManagementRepository>((method, args) =>
            {
                if (method.Name == "InTransactionAsync") return typeof(Fixture).GetMethod(nameof(Transaction),
                    BindingFlags.Instance | BindingFlags.NonPublic)!.MakeGenericMethod(method.GetGenericArguments()).Invoke(this, args);
                Assert.True(inTransaction);
                switch (method.Name)
                {
                    case "IsCurrentAdministratorAsync":
                        return Task.FromResult(actorSession.IsActiveAt((DateTimeOffset)args[1]!));
                    case "HasSystemRoleAsync": return Task.FromResult(false);
                    case "GetPotentialAccountPermissionsAsync": return Task.FromResult<IReadOnlyList<RolePermission>>([]);
                    case "ExpectAccountVersion": Assert.Equal((byte[])args[1]!, ((UserAccount)args[0]!).RowVersion); return null;
                    case "AddAudit": Audits.Add((AuditEvent)args[0]!); return null;
                    case "SaveChangesAsync": SaveCalls++; Advance(SaveCalls == 1 ? "first-save" : "second-save"); return Task.CompletedTask;
                    default: throw new InvalidOperationException(method.Name);
                }
            });
            var permissions = Proxy<IAccountAccessReader>((_, args) =>
            {
                var forActor = (Guid)args[0]! == actor.AccountId;
                var assignment = (forActor ? approver : Target).Access[0].Assignment;
                var until = (forActor && authority == "grant") || (!forActor && authority == "target-access")
                    ? Start.AddMinutes(1) : Start.AddHours(2);
                var codes = forActor ? new[] { AccessPermissionCatalog.AccountsMobileManage, AccessPermissionCatalog.RolePermissionsManage }
                    : new[] { AccessPermissionCatalog.SelfRead };
                IReadOnlyList<PermissionGrant> grants = codes.Select(code => new PermissionGrant(code, forActor,
                    assignment.Id, AccessScopeKind.All, null)
                { AuthorityTimingKnown = true, ValidFromUtc = Start.AddHours(-1), ValidUntilUtc = until })
                    .Where(g => g.IsEffectiveAt((DateTimeOffset)args[1]!)).ToArray();
                return Task.FromResult(grants);
            });
            var options = new MobileAuthenticationOptions
            {
                Enabled = true, BusinessTimeZoneId = "UTC", OtpLifetimeSeconds = 300, ResendCooldownSeconds = 30,
                MaximumOtpAttempts = 5, RateWindowSeconds = 300, RequestsPerPhone = 5, RequestsPerIp = 10,
                RequestsGlobal = 20, VerificationAttemptsPerPhone = 5, VerificationAttemptsPerIp = 10,
                VerificationAttemptsGlobal = 20, AccessTokenLifetimeSeconds = 300, RefreshTokenLifetimeSeconds = 600,
                SessionLifetimeSeconds = 900, MinimumRequestDurationMilliseconds = 100
            };
            var mobile = new MobileAuthenticationService(authentication, permissions, null!, null!, null!, null!, options, clock);
            Service = new(authentication, administrators, permissions, current, mobile, options, clock);
        }

        public Task<DeviceRecoveryApprovalResponse> Approve() => Service.ApproveAsync(Target.Account.Id, Previous.Id,
            new("Synthetic independent approval", Convert.ToBase64String(Target.Account.RowVersion),
                Convert.ToBase64String(Previous.RowVersion), Proof.Id), Context, default);

        private void Advance(string boundary)
        {
            if (stage != boundary || Advanced) return;
            Advanced = true; clock.Now = Start.AddMinutes(2);
        }

        private async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
        {
            inTransaction = true;
            try { var result = await action(ct); Committed = true; return result; }
            catch
            {
                // Model the repository's outer SQL rollback, including both staged saves.
                Set(Target.Account, nameof(UserAccount.SecurityVersion), 1);
                Set(Previous, nameof(RegisteredDevice.RevokedAt), null);
                Set(Previous, nameof(RegisteredDevice.RevocationReason), null);
                Set(TargetSession, nameof(UserSession.RevokedAt), null);
                Set(TargetSession, nameof(UserSession.RevocationReason), null);
                Set(Proof, nameof(MobileOtpChallenge.RevokedAt), null);
                Devices.RemoveAll(d => d.Id != Previous.Id); Audits.Clear(); throw;
            }
            finally { inTransaction = false; }
        }
    }

    private static AuthenticationAccount Account(bool administrator, DateTimeOffset? roleEnd)
    {
        var employee = Empty<Employee>();
        Set(employee, nameof(Employee.Id), Guid.NewGuid());
        Set(employee, nameof(Employee.Status), EmployeeStatus.Active);
        Set(employee, nameof(Employee.OnboardingStatus), OnboardingStatus.Approved);
        Set(employee, nameof(Employee.JoinedOn), new DateOnly(2026, 1, 1));
        var submission = Empty<EmployeeSubmission>();
        Set(submission, nameof(EmployeeSubmission.Id), Guid.NewGuid());
        Set(submission, nameof(EmployeeSubmission.MobileAccessRequested), true);
        Set(submission, nameof(EmployeeSubmission.ApprovedLoginPhoneNumber), "+12025550199");
        var approval = Empty<EmployeeOnboardingDecision>();
        Set(approval, nameof(EmployeeOnboardingDecision.Id), Guid.NewGuid());
        Set(approval, nameof(EmployeeOnboardingDecision.EmployeeId), employee.Id);
        Set(approval, nameof(EmployeeOnboardingDecision.Decision), OnboardingDecision.Approve);
        Set(approval, nameof(EmployeeOnboardingDecision.Submission), submission);
        Set(employee, nameof(Employee.ApprovedOnboardingDecisionId), approval.Id);
        var account = new UserAccount(employee, approval, "synthetic", Start.AddHours(-1));
        Set(account, nameof(UserAccount.Status), AccountStatus.Active);
        Set(account, nameof(UserAccount.MobileAccessEnabled), true);
        Set(account, nameof(UserAccount.RowVersion), new byte[8]);
        var phone = Empty<LoginPhoneBinding>();
        Set(phone, nameof(LoginPhoneBinding.Id), Guid.NewGuid());
        Set(phone, nameof(LoginPhoneBinding.UserAccountId), account.Id);
        Set(phone, nameof(LoginPhoneBinding.ApprovedSubmissionId), submission.Id);
        Set(phone, nameof(LoginPhoneBinding.PhoneNumber), submission.ApprovedLoginPhoneNumber);
        Set(phone, nameof(LoginPhoneBinding.CreatedAt), Start.AddHours(-1));
        Set(phone, nameof(LoginPhoneBinding.VerifiedAt), Start.AddHours(-1));
        var role = administrator ? Role.CreateSystem(SystemRoleKind.Admin, Guid.NewGuid(), Start.AddHours(-1))
            : new Role("SYNTHETIC", "Synthetic employee", Guid.NewGuid(), Start.AddHours(-1));
        var assignment = new UserRoleAssignment(account, role, Guid.NewGuid(), Start.AddHours(-1), roleEnd);
        return new(account, phone, [new(assignment, new UserAccessScope(assignment, AccessScopeKind.All))], EmployeeStatus.Active, false);
    }

    private static UserSession Session(Guid accountId, Guid id, SessionClient client, DateTimeOffset expires)
    {
        var session = Empty<UserSession>();
        Set(session, nameof(UserSession.Id), id); Set(session, nameof(UserSession.UserAccountId), accountId);
        Set(session, nameof(UserSession.Client), client); Set(session, nameof(UserSession.SecurityVersion), 1);
        Set(session, nameof(UserSession.CreatedAt), Start.AddHours(-1)); Set(session, nameof(UserSession.ExpiresAt), expires);
        return session;
    }

    private static T Empty<T>() => (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
    private static void Set<T>(T target, string property, object? value) => typeof(T).GetProperty(property)!.SetValue(target, value);
    private sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now = Start;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private static T Proxy<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = DispatchProxy.Create<T, TestProxy>(); ((TestProxy)(object)value).Handler = handler; return value; }
    public class TestProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Handler = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    }
}
