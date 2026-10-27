using System.Reflection;
using System.Security.Cryptography;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Approvals;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Xunit;

namespace Cosmix.Tests;

public class ApprovalRoutingPolicyTests
{
    [Theory]
    [InlineData("HR")]
    [InlineData("Admin")]
    public async Task Current_web_mfa_actor_with_explicit_manage_and_stage_can_publish(string role)
    {
        var f = new Fixture(role);
        var result = await f.Publish();
        Assert.Equal(1, result.Version);
        Assert.Single(f.Rules);
        Assert.Single(f.Receipts);
        var audit = Assert.Single(f.Audits);
        Assert.Equal(role, audit.ActorRoleCode);
        Assert.Equal(f.Actor.AccountId.ToString(), audit.ActorUserId);
        Assert.NotNull(audit.ProtectedChangesReference);
        Assert.Equal(2, f.Files.Count);
        Assert.Equal(0, f.SuperAdminChecks);
    }

    [Theory]
    [InlineData("missing-manage")]
    [InlineData("missing-stage")]
    [InlineData("manage-self")]
    [InlineData("stage-self")]
    [InlineData("different-stage")]
    [InlineData("no-mfa")]
    [InlineData("mobile")]
    [InlineData("stale-session")]
    [InlineData("expired-manage")]
    [InlineData("expired-stage")]
    public async Task Missing_or_ineligible_authority_cannot_publish(string scenario)
    {
        var f = new Fixture("HR", scenario);
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(f.Publish);
        Assert.Equal(403, error.StatusCode);
        Assert.Empty(f.Rules); Assert.Empty(f.Receipts); Assert.Empty(f.Audits); Assert.Empty(f.Files);
    }

    [Fact]
    public async Task Stale_version_preserves_existing_route_and_protected_evidence()
    {
        var f = new Fixture();
        await f.Publish();
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Publish("another-key"));
        Assert.Equal(409, error.StatusCode);
        Assert.Single(f.Rules); Assert.Single(f.Receipts); Assert.Single(f.Audits); Assert.Equal(2, f.Files.Count);
    }

    [Fact]
    public async Task Retry_returns_original_result_but_rechecks_current_stage_authority()
    {
        var f = new Fixture();
        var first = await f.Publish();
        var retry = await f.Publish();
        Assert.Equal(first, retry);
        Assert.Single(f.Rules); Assert.Single(f.Receipts); Assert.Equal(2, f.Audits.Count);
        f.Grants.RemoveAll(g => g.Permission == ApprovalPermissions.StageReview(2));
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(f.Publish);
        Assert.Equal(403, error.StatusCode);
        Assert.Single(f.Rules); Assert.Equal(2, f.Audits.Count);
    }

    [Theory]
    [InlineData("leave")]
    [InlineData("attendance-correction")]
    [InlineData("overtime")]
    public async Task Route_publication_cannot_replace_fixed_consumer_decision_authority(string type)
    {
        var f = new Fixture();
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Service.PublishAsync(
            f.Input with { RequestType = type }, "fixed-route-test", f.Context, default));
        Assert.Equal(409, error.StatusCode); Assert.Empty(f.Rules);
    }

    private sealed class Fixture
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        public AuthenticatedAccount Actor { get; }
        public List<PermissionGrant> Grants { get; } = [];
        public List<ApprovalRouteRule> Rules { get; } = [];
        public List<IdempotencyReceipt> Receipts { get; } = [];
        public List<AuditEvent> Audits { get; } = [];
        public Dictionary<string, (string Purpose, byte[] Content)> Files { get; } = [];
        public int SuperAdminChecks { get; private set; }
        public AuthRequestContext Context { get; } = new("127.0.0.1", "approval-policy-test");
        public PublishApprovalRouteRequest Input { get; } = new("TEST-HR-STAGE2", 0, "allowance",
            ApprovalPermissions.StageReview(2), 2, new(2026, 10, 27), null, "Synthetic policy configuration");
        public ApprovalRoutingService Service { get; }

        public Fixture(string role = "HR", string? scenario = null)
        {
            Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
                scenario == "mobile" ? SessionClient.Mobile : SessionClient.Web, scenario != "no-mfa");
            var assignment = Guid.NewGuid();
            foreach (var permission in new[] { ApprovalPermissions.Manage, ApprovalPermissions.StageReview(2) })
            {
                var manage = permission == ApprovalPermissions.Manage;
                if (scenario == (manage ? "missing-manage" : "missing-stage")) continue;
                var self = scenario == (manage ? "manage-self" : "stage-self");
                Grants.Add(new(!manage && scenario == "different-stage" ? ApprovalPermissions.StageReview(1) : permission,
                    true, assignment, self ? AccessScopeKind.Self : AccessScopeKind.All, self ? Actor.EmployeeId : null)
                {
                    AuthorityTimingKnown = true, ValidFromUtc = Now.AddHours(-1),
                    ValidUntilUtc = scenario == (manage ? "expired-manage" : "expired-stage") ? Now : Now.AddHours(1)
                });
            }
            var current = Proxy<ICurrentAuthentication>((_, _) => Actor);
            var access = Proxy<IAccountAccessReader>((_, args) => Task.FromResult<IReadOnlyList<PermissionGrant>>(
                Grants.Where(g => g.IsEffectiveAt((DateTimeOffset)args[1]!)).ToArray()));
            var authorization = Proxy<IAccessManagementRepository>((method, args) =>
            {
                switch (method.Name)
                {
                    case "InTransactionAsync": return typeof(Fixture).GetMethod(nameof(Transaction), BindingFlags.Static | BindingFlags.NonPublic)!
                        .MakeGenericMethod(method.GetGenericArguments()).Invoke(null, args);
                    case "IsCurrentAdministratorAsync": return Task.FromResult(scenario != "stale-session" && Actor.Client == SessionClient.Web && Actor.MfaSatisfied);
                    case "IsCurrentSuperAdminAsync": SuperAdminChecks++; return Task.FromResult(false);
                    case "GetAssignmentRoleCodeAsync": return Task.FromResult<string?>(role);
                    default: throw new InvalidOperationException(method.Name);
                }
            });
            var repository = Proxy<IApprovalRoutingRepository>((method, args) =>
            {
                switch (method.Name)
                {
                    case "VersionsAsync": return Task.FromResult<IReadOnlyList<ApprovalRouteRule>>(Rules.Where(r => r.Code == (string)args[0]!).OrderBy(r => r.Version).ToArray());
                    case "ValidateTargetsAsync": case "SaveAsync": return Task.CompletedTask;
                    case "ReceiptAsync": return Task.FromResult(Receipts.SingleOrDefault(r => r.ActorUserId == (string)args[0]! && r.Operation == (string)args[1]! && r.Key == (string)args[2]!));
                    case "Add":
                        switch (args[0]) { case ApprovalRouteRule r: Rules.Add(r); break; case IdempotencyReceipt r: Receipts.Add(r); break; case AuditEvent a: Audits.Add(a); break; }
                        return null;
                    default: throw new InvalidOperationException(method.Name);
                }
            });
            var files = Proxy<IPrivateFileStore>((method, args) =>
            {
                if (method.Name == "WriteAsync")
                {
                    var content = (byte[])args[0]!; var id = Guid.NewGuid().ToString("N");
                    Files.Add(id, ((string)args[1]!, content));
                    return Task.FromResult(new PrivateFileReference(id, "1", Convert.ToHexString(SHA256.HashData(content)), "test-key", content.Length));
                }
                var row = Files[((PrivateFileReference)args[0]!).StorageKey]; Assert.Equal(row.Purpose, args[1]);
                return Task.FromResult(row.Content);
            });
            Service = new(current, access, authorization, repository, files, new FixedClock());
        }
        public Task<ApprovalRouteDto> Publish() => Publish("route-test-key");
        public Task<ApprovalRouteDto> Publish(string key) => Service.PublishAsync(Input, key, Context, default);
        private static Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
        private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    }

    private static T Proxy<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = DispatchProxy.Create<T, TestProxy>(); ((TestProxy)(object)proxy).Handler = handler; return proxy; }
    public class TestProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Handler = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    }
}
