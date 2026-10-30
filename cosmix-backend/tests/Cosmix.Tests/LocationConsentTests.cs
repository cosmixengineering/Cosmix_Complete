using System.Reflection;
using System.Text.Json;
using Cosmix.Application.Attendance.LocationConsent;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Security;
using Cosmix.Domain.Attendance.LocationConsent;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cosmix.Tests;

public class LocationConsentTests
{
    private static readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-consent");

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Consent_requests_use_MVC_record_validation(bool publication, bool oversized)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var context = new Microsoft.AspNetCore.Mvc.ActionContext(
            new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = provider },
            new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        object request = publication
            ? new PublishLocationNoticePolicyRequest(oversized ? new string('x', 4001) : "Reviewed notice", 1)
            : new AcceptLocationConsentRequest(1, oversized ? new string('x', 501) : "Synthetic device");
        provider.GetRequiredService<Microsoft.AspNetCore.Mvc.ModelBinding.Validation.IObjectModelValidator>()
            .Validate(context, validationState: null, prefix: "", model: request);
        Assert.Equal(!oversized, context.ModelState.IsValid);
    }

    [Fact]
    public async Task Accept_RejectsNoticeVersionTheEmployeeDidNotReview()
    {
        var f = new Fixture();
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Service.AcceptAsync(new(1), Context, default));
        Assert.Equal("location_policy_changed", error.Code);
        Assert.Equal(409, error.StatusCode);
        Assert.Empty(f.Repository.Consents);
        Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task Accept_IsIdempotentAndUsesAuthenticatedEmployeeAndServerIp()
    {
        var f = new Fixture();
        var first = await f.Service.AcceptAsync(new(2, "test device"), Context, default);
        var second = await f.Service.AcceptAsync(new(2), Context, default);
        Assert.Equal(first, second);
        var consent = Assert.Single(f.Repository.Consents);
        Assert.Equal(f.Actor.EmployeeId, consent.EmployeeId);
        Assert.Equal(Context.IpAddress, consent.IpAddress);
        Assert.Equal(2, consent.NoticePolicyVersion);
        Assert.Equal(2, f.Audits.Count);
        Assert.All(f.Audits, audit => Assert.Equal(f.Actor.AccountId.ToString(), audit.ActorUserId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Accept_RequiresPositiveReviewedVersion(int version)
    {
        var f = new Fixture();
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Service.AcceptAsync(new(version), Context, default));
        Assert.Equal(400, error.StatusCode);
        Assert.Empty(f.Repository.Consents);
    }

    [Theory]
    [InlineData("ipAddress", "127.0.0.99")]
    [InlineData("employeeId", "00000000-0000-0000-0000-000000000001")]
    public void Accept_RejectsClientIdentityFields(string property, string value)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object> { ["expectedPolicyVersion"] = 2, [property] = value });
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AcceptLocationConsentRequest>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public async Task Status_DistinguishesKnownNoPolicyFromReadFailure()
    {
        var f = new Fixture();
        f.Repository.Policies.Clear();
        Assert.Equal(ConsentStatus.NotRequired, (await f.Service.GetStatusAsync(Context, default)).Status);
        f.Repository.FailRead = true;
        await Assert.ThrowsAsync<IOException>(() => f.Service.GetStatusAsync(Context, default));
        Assert.Single(f.Audits);
    }

    [Fact]
    public async Task Status_PendingThenConsentedAndNewNoticeRequiresReviewAgain()
    {
        var f = new Fixture();
        Assert.Equal(ConsentStatus.PendingConsent, (await f.Service.GetStatusAsync(Context, default)).Status);
        await f.Service.AcceptAsync(new(2), Context, default);
        Assert.Equal(ConsentStatus.Consented, (await f.Service.GetStatusAsync(Context, default)).Status);
        f.Repository.Policies.Single().Deactivate();
        f.Repository.Policies.Add(Repository.Policy(3));
        Assert.Equal(ConsentStatus.PendingConsent, (await f.Service.GetStatusAsync(Context, default)).Status);
    }

    [Fact]
    public async Task Self_RevokedSessionCannotReadOrCommitConsent()
    {
        var f = new Fixture { Session = false };
        await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Service.GetStatusAsync(Context, default));
        Assert.Equal(0, f.Repository.Reads);
        f.Session = true;
        f.Repository.AfterConsentAdded = () => f.Session = false;
        await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Service.AcceptAsync(new(2), Context, default));
        Assert.Empty(f.Repository.Consents);
        Assert.Empty(f.Audits);
        Assert.Equal(0, f.Saves);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Self_SessionRevokedDuringAuditSaveCannotExposeOrCommitConsent(bool accept)
    {
        var f = new Fixture();
        f.AfterSave = () => f.Session = false;
        await Assert.ThrowsAsync<AuthenticationFailure>(async () =>
        {
            if (accept) await f.Service.AcceptAsync(new(2), Context, default);
            else await f.Service.GetStatusAsync(Context, default);
        });
        Assert.Empty(f.Repository.Consents);
        Assert.Empty(f.Audits);
    }

    [Theory]
    [InlineData("mobile")]
    [InlineData("no-mfa")]
    [InlineData("no-grant")]
    [InlineData("employee-scope")]
    public async Task Administration_DeniesBeforePolicyAccess(string mode)
    {
        var f = new Fixture(mode);
        await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Service.GetPolicyAsync(Context, default));
        await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Service.PublishPolicyAsync(new("new notice", 2), Context, default));
        Assert.Equal(0, f.Repository.Reads);
        Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task Publication_DerivesActorAuditsAndRejectsStaleEditorVersion()
    {
        var f = new Fixture("admin");
        var result = await f.Service.PublishPolicyAsync(new("Revised approved notice", 2), Context, default);
        Assert.Equal(3, result.Version);
        Assert.Single(f.Repository.Policies, p => p.IsActive);
        Assert.Equal(f.Actor.AccountId.ToString(), f.Repository.Policies.Last().CreatedByUserId);
        Assert.Equal("location.policy.publish", Assert.Single(f.Audits).EventType);
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Service.PublishPolicyAsync(new("stale notice", 2), Context, default));
        Assert.Equal("location_policy_changed", error.Code);
        Assert.Equal(2, f.Repository.Policies.Count);
    }

    private sealed class Fixture
    {
        public AuthenticatedAccount Actor { get; }
        public Repository Repository { get; } = new();
        public LocationConsentService Service { get; }
        public List<AuditEvent> Audits { get; } = [];
        public bool Session = true;
        public int Saves;
        public Action? AfterSave;
        private bool inTransaction;

        public Fixture(string mode = "mobile")
        {
            Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
                mode == "mobile" ? SessionClient.Mobile : SessionClient.Web, mode != "no-mfa");
            var current = Proxy<ICurrentAuthentication>((_, _) => Actor);
            var access = Proxy<IAccountAccessReader>((_, args) =>
            {
                var now = (DateTimeOffset)args[1]!;
                IReadOnlyList<PermissionGrant> grants = mode == "no-grant" ? [] :
                    [new("location.consent.admin", true, Guid.NewGuid(), mode == "employee-scope" ? AccessScopeKind.Employee : AccessScopeKind.All,
                        mode == "employee-scope" ? Actor.EmployeeId : null)
                    { AuthorityTimingKnown = true, ValidFromUtc = now.AddHours(-1), ValidUntilUtc = now.AddHours(1) }];
                return Task.FromResult(grants);
            });
            var transactions = Proxy<IAccessManagementRepository>((method, args) => method.Name switch
            {
                "InTransactionAsync" => typeof(Fixture).GetMethod(nameof(Transaction), BindingFlags.Instance | BindingFlags.NonPublic)!
                    .MakeGenericMethod(method.GetGenericArguments()).Invoke(this, args),
                "IsCurrentAdministratorAsync" => Task.FromResult(true),
                "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("HR"),
                _ => throw new InvalidOperationException(method.Name)
            });
            var journal = Proxy<ISchedulingJournalRepository>((method, args) =>
            {
                Assert.True(inTransaction);
                switch (method.Name)
                {
                    case "IsCurrentSessionAsync": return Task.FromResult(Session);
                    case "AddAudit": Audits.Add((AuditEvent)args[0]!); return null;
                    case "SaveChangesAsync": Saves++; AfterSave?.Invoke(); return Task.CompletedTask;
                    default: throw new InvalidOperationException(method.Name);
                }
            });
            Repository.AssertTransaction = () => Assert.True(inTransaction);
            Service = new(Repository, TimeProvider.System, current, transactions, journal,
                new(current, access, transactions, journal, TimeProvider.System));
        }

        private async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
        {
            var before = Repository.Consents.ToArray();
            var auditCount = Audits.Count;
            inTransaction = true;
            try { return await action(ct); }
            catch
            {
                Repository.Consents.Clear(); Repository.Consents.AddRange(before);
                Audits.RemoveRange(auditCount, Audits.Count - auditCount);
                throw;
            }
            finally { inTransaction = false; }
        }
    }

    private sealed class Repository : ILocationConsentRepository
    {
        public List<EmployeeLocationConsent> Consents { get; } = [];
        public List<LocationNoticePolicy> Policies { get; } = [Policy(2)];
        public Action AssertTransaction = () => { };
        public Action? AfterConsentAdded;
        public bool FailRead;
        public int Reads;
        public static LocationNoticePolicy Policy(int version)
        {
            var value = new LocationNoticePolicy(version, "Notice requiring employee review", "HR", DateTimeOffset.UtcNow);
            value.Publish("HR", DateTimeOffset.UtcNow);
            return value;
        }
        public Task<LocationNoticePolicy?> GetActivePolicyAsync(CancellationToken ct)
        {
            AssertTransaction(); Reads++;
            if (FailRead) throw new IOException("repository unavailable");
            return Task.FromResult(Policies.SingleOrDefault(p => p.IsActive));
        }
        public Task<LocationNoticePolicy?> GetLatestPolicyAsync(CancellationToken ct)
        { AssertTransaction(); Reads++; return Task.FromResult(Policies.LastOrDefault()); }
        public Task<EmployeeLocationConsent?> GetConsentAsync(Guid employee, int version, CancellationToken ct)
        { AssertTransaction(); return Task.FromResult(Consents.SingleOrDefault(c => c.EmployeeId == employee && c.NoticePolicyVersion == version)); }
        public Task AddConsentAsync(EmployeeLocationConsent consent, CancellationToken ct)
        { AssertTransaction(); Consents.Add(consent); AfterConsentAdded?.Invoke(); return Task.CompletedTask; }
        public Task AddPolicyAsync(LocationNoticePolicy policy, CancellationToken ct)
        { AssertTransaction(); Policies.Add(policy); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct) => throw new InvalidOperationException("Commit through the audited operation only.");
    }

    private static T Proxy<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = DispatchProxy.Create<T,TestProxy>(); ((TestProxy)(object)value).Handler = handler; return value; }
    public class TestProxy : DispatchProxy
    {
        public Func<MethodInfo,object?[],object?> Handler = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    }
}
