using System.Reflection;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Security;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Xunit;

namespace Cosmix.Tests;

public class ComplianceAuthorityExpiryTests
{
    public static IEnumerable<object[]> Expiries() =>
        from authority in new[] { "session", "administrator", "grant" }
        from stage in new[] { "session-read", "grant-read", "role-read", "audit-save" }
        select new object[] { authority, stage };

    [Theory, MemberData(nameof(Expiries))]
    public async Task Expiry_during_final_reads_or_save_rolls_back_business_and_audit(string authority, string stage)
    {
        var f = new Fixture(authority, stage);
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(f.Execute);
        Assert.Equal("access_denied", failure.Code);
        Assert.Equal(403, failure.StatusCode);
        Assert.True(f.Expired);
        Assert.False(f.Committed);
        Assert.Equal(0, f.BusinessRows);
        Assert.Empty(f.Audits);
    }

    [Fact]
    public async Task Valid_authority_commits_business_and_attributed_audit_once()
    {
        var f = new Fixture();
        Assert.Equal(42, await f.Execute());
        Assert.True(f.Committed);
        Assert.Equal(1, f.BusinessRows);
        Assert.Equal(1, f.SaveCount);
        var audit = Assert.Single(f.Audits);
        Assert.Equal(f.Actor.AccountId.ToString(), audit.ActorUserId);
        Assert.Equal("ConfiguredFinance", audit.ActorRoleCode);
    }

    private sealed class Fixture
    {
        private static readonly DateTimeOffset Start = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        private readonly MutableClock clock = new(Start);
        private readonly string? authority;
        private readonly string? stage;
        private bool afterAction;
        private bool inTransaction;
        public bool Expired { get; private set; }
        public bool Committed { get; private set; }
        public int BusinessRows { get; private set; }
        public int SaveCount { get; private set; }
        public List<AuditEvent> Audits { get; } = [];
        public AuthenticatedAccount Actor { get; } = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
        private readonly SensitiveComplianceOperation operation;

        public Fixture(string? authority = null, string? stage = null)
        {
            this.authority = authority;
            this.stage = stage;
            var assignmentId = Guid.NewGuid();
            var current = Proxy<ICurrentAuthentication>((_, _) => Actor);
            var access = Proxy<IAccountAccessReader>((method, args) =>
            {
                Assert.True(inTransaction);
                Assert.Equal("GetGrantsAsync", method.Name);
                IReadOnlyList<PermissionGrant> grants = [new("payroll.accounting.accrue", true,
                    assignmentId, AccessScopeKind.All, null)
                {
                    AuthorityTimingKnown = true, ValidFromUtc = Start.AddHours(-1),
                    ValidUntilUtc = authority == "grant" ? Start.AddMinutes(1) : Start.AddHours(1)
                }];
                Advance("grant-read");
                return Task.FromResult(grants);
            });
            var authorization = Proxy<IAccessManagementRepository>((method, args) =>
            {
                switch (method.Name)
                {
                    case "InTransactionAsync":
                        return typeof(Fixture).GetMethod(nameof(Transaction), BindingFlags.Instance | BindingFlags.NonPublic)!
                            .MakeGenericMethod(method.GetGenericArguments()).Invoke(this, args)!;
                    case "IsCurrentAdministratorAsync":
                        Assert.True(inTransaction);
                        return Task.FromResult(authority != "administrator" || (DateTimeOffset)args[1]! < Start.AddMinutes(1));
                    case "GetAssignmentRoleCodeAsync":
                        Assert.True(inTransaction);
                        Advance("role-read");
                        return Task.FromResult<string?>("ConfiguredFinance");
                    default: throw new InvalidOperationException(method.Name);
                }
            });
            var journal = Proxy<ISchedulingJournalRepository>((method, args) =>
            {
                Assert.True(inTransaction);
                switch (method.Name)
                {
                    case "IsCurrentSessionAsync":
                        var currentSession = authority != "session" || clock.GetUtcNow() < Start.AddMinutes(1);
                        Advance("session-read");
                        return Task.FromResult(currentSession);
                    case "AddAudit": Audits.Add((AuditEvent)args[0]!); return null;
                    case "SaveChangesAsync": SaveCount++; Advance("audit-save"); return Task.CompletedTask;
                    default: throw new InvalidOperationException(method.Name);
                }
            });
            operation = new(current, access, authorization, journal, clock);
        }

        public Task<int> Execute() => operation.ExecuteAsync("payroll.accounting.accrue", "SyntheticWrite", "PayrollAccounting",
            "synthetic", new("127.0.0.1", "synthetic-authority-expiry"), (_, _) =>
            {
                Assert.True(inTransaction);
                BusinessRows++;
                afterAction = true;
                return Task.FromResult(42);
            }, default);

        private void Advance(string at)
        {
            if (!Expired && afterAction && stage == at)
            {
                Expired = true;
                clock.Now = Start.AddMinutes(1);
            }
        }

        private async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
        {
            Assert.False(inTransaction);
            inTransaction = true;
            try
            {
                var result = await action(ct);
                Committed = true;
                return result;
            }
            catch
            {
                BusinessRows = 0;
                Audits.Clear();
                throw;
            }
            finally { inTransaction = false; }
        }
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static T Proxy<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, TestProxy>();
        ((TestProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    public class TestProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Handler = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args ?? []);
    }
}
