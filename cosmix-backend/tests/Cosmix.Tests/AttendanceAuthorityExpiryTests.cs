using System.Reflection;
using System.Security.Cryptography;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Xunit;

namespace Cosmix.Tests;

public class AttendanceAuthorityExpiryTests
{
    public static IEnumerable<object[]> ExpiryCases() =>
        from kind in new[] { "read", "write", "replay" }
        from authority in new[] { "session", "grant" }
        from stage in new[] { "grant-read", "audit-save" }
        select new object[] { kind, authority, stage };

    [Theory, MemberData(nameof(ExpiryCases))]
    public async Task Current_authority_is_required_through_transaction_completion(string kind, string authority, string stage)
    {
        var f = new Fixture(authority, stage);
        if (kind == "replay") await f.PrimeReplay();
        await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Execute(kind));
        Assert.True(f.Advanced);
        Assert.False(f.Committed);
        Assert.Equal(kind == "replay" ? 1 : 0, f.BusinessRows);
        Assert.Equal(kind == "replay" ? 1 : 0, f.Receipts.Count);
        Assert.Equal(kind == "replay" ? 1 : 0, f.Audits.Count);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData("replay")]
    public async Task Supplementary_authority_is_checked_after_save_and_can_roll_back(string kind)
    {
        var f = new Fixture("supplementary", "audit-save");
        if (kind == "replay") await f.PrimeReplay();
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Execute(kind));
        Assert.Equal("supplementary_access_denied", failure.Code);
        Assert.True(f.Advanced);
        Assert.False(f.Committed);
        Assert.Equal(kind == "replay" ? 1 : 0, f.BusinessRows);
        Assert.Equal(kind == "replay" ? 1 : 0, f.Receipts.Count);
        Assert.Equal(kind == "replay" ? 1 : 0, f.Audits.Count);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    public async Task Untimed_grants_cannot_authorize_business_access(string kind)
    {
        var f = new Fixture("unknown-timing", null);
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Execute(kind));
        Assert.Equal(403, failure.StatusCode);
        Assert.Equal(0, f.Actions);
        Assert.Empty(f.Audits);
    }

    [Theory]
    [InlineData("session")]
    [InlineData("grant")]
    public async Task Authority_expiring_during_initial_role_read_cannot_start_action(string authority)
    {
        var f = new Fixture(authority, "initial-role-read");
        await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Execute("write"));
        Assert.True(f.Advanced);
        Assert.Equal(0, f.Actions);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData("replay")]
    public async Task Valid_authority_preserves_read_write_and_exact_retry(string kind)
    {
        var f = new Fixture();
        if (kind == "replay") await f.PrimeReplay();
        Assert.Equal(42, await f.Execute(kind));
        Assert.True(f.Committed);
        Assert.Equal(kind == "read" ? 0 : 1, f.BusinessRows);
        Assert.Equal(kind == "read" ? 0 : 1, f.Receipts.Count);
        Assert.Equal(kind == "replay" ? 2 : 1, f.Audits.Count);
    }

    private sealed class Fixture
    {
        private const string Permission = "attendance.payroll-handoff.prepare";
        private static readonly DateTimeOffset Start = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        private readonly Clock clock = new(Start);
        private readonly string? authority;
        private readonly string? stage;
        private bool enabled = true;
        private bool inTransaction;
        private bool afterAction;
        private readonly Guid resource = Guid.NewGuid();
        private readonly AttendanceOperation operation;
        private readonly Dictionary<string, (string Purpose, byte[] Content)> files = [];
        public bool Advanced { get; private set; }
        public bool Committed { get; private set; }
        public int BusinessRows { get; private set; }
        public int Actions { get; private set; }
        public List<IdempotencyReceipt> Receipts { get; } = [];
        public List<AuditEvent> Audits { get; } = [];
        private readonly AuthRequestContext context = new("127.0.0.1", "synthetic-attendance-expiry");

        public Fixture(string? authority = null, string? stage = null)
        {
            this.authority = authority; this.stage = stage;
            var actor = new AuthenticatedAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
            var current = Proxy<ICurrentAuthentication>((_, _) => actor);
            var grants = new PermissionGrant[] { new(Permission, true, Guid.NewGuid(), AccessScopeKind.All, null)
            {
                AuthorityTimingKnown = authority != "unknown-timing", ValidFromUtc = Start.AddHours(-1),
                ValidUntilUtc = authority == "grant" ? Start.AddMinutes(1) : Start.AddHours(1)
            } };
            var access = Proxy<IAccountAccessReader>((_, args) =>
            {
                Assert.True(inTransaction);
                IReadOnlyList<PermissionGrant> result = grants.Where(g => !g.AuthorityTimingKnown || g.IsEffectiveAt((DateTimeOffset)args[1]!)).ToArray();
                Advance("grant-read");
                return Task.FromResult(result);
            });
            var authorization = Proxy<IAccessManagementRepository>((method, args) =>
            {
                if (method.Name == "InTransactionAsync")
                    return typeof(Fixture).GetMethod(nameof(Transaction), BindingFlags.Instance | BindingFlags.NonPublic)!
                        .MakeGenericMethod(method.GetGenericArguments()).Invoke(this, args)!;
                if (method.Name == "GetAssignmentRoleCodeAsync")
                {
                    Advance("initial-role-read", initial: true);
                    return Task.FromResult<string?>("ConfiguredFinance");
                }
                throw new InvalidOperationException(method.Name);
            });
            var journal = Proxy<ISchedulingJournalRepository>((method, args) =>
            {
                Assert.True(inTransaction);
                switch (method.Name)
                {
                    case "IsCurrentSessionAsync": return Task.FromResult(authority != "session" || clock.Now < Start.AddMinutes(1));
                    case "FindReceiptAsync":
                        var receipt = Receipts.SingleOrDefault(x => x.Key == (string)args[2]!);
                        if (receipt is not null && enabled) afterAction = true;
                        return Task.FromResult(receipt);
                    case "AddReceipt": Receipts.Add((IdempotencyReceipt)args[0]!); return null;
                    case "AddAudit": Audits.Add((AuditEvent)args[0]!); return null;
                    case "SaveChangesAsync": Advance("audit-save"); return Task.CompletedTask;
                    default: throw new InvalidOperationException(method.Name);
                }
            });
            var storage = Proxy<IPrivateFileStore>((method, args) =>
            {
                if (method.Name == "WriteAsync")
                {
                    var bytes = ((byte[])args[0]!).ToArray();
                    var key = Guid.NewGuid().ToString("N");
                    files.Add(key, ((string)args[1]!, bytes));
                    return Task.FromResult(new PrivateFileReference(key, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length));
                }
                var reference = (PrivateFileReference)args[0]!;
                var stored = files[reference.StorageKey];
                Assert.Equal((string)args[1]!, stored.Purpose);
                return Task.FromResult(stored.Content.ToArray());
            });
            operation = new(current, access, authorization, journal, null!, clock, storage);
        }

        public Task<int> Execute(string kind) => kind == "read"
            ? operation.ReadAsync(Permission, "synthetic.read", null, context, (_, _) =>
            {
                Actions++; afterAction = true; return Task.FromResult(42);
            }, default, afterSave: SupplementaryAuthority)
            : operation.WriteAsync(Permission, "synthetic.write", null, "synthetic-exact-retry", new { value = 42 },
                "Synthetic authority regression", context, (_, _) =>
                {
                    Actions++; BusinessRows++; afterAction = true;
                    return Task.FromResult(new AttendanceChange<int>(resource, null, 42));
                }, default, afterSave: SupplementaryAuthority);

        private Task SupplementaryAuthority(AttendanceActor actor, CancellationToken ct)
        {
            Assert.True(inTransaction);
            Assert.NotEmpty(Audits);
            if (authority == "supplementary" && clock.Now >= Start.AddMinutes(1))
                throw new AuthenticationFailure("supplementary_access_denied", 403);
            return Task.CompletedTask;
        }

        public async Task PrimeReplay()
        {
            enabled = false;
            await Execute("write");
            enabled = true; afterAction = false; Committed = false;
        }

        private void Advance(string point, bool initial = false)
        {
            if (enabled && !Advanced && stage == point && (initial || afterAction))
            { Advanced = true; clock.Now = Start.AddMinutes(1); }
        }

        private async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
        {
            Assert.False(inTransaction);
            var rowsBefore = BusinessRows; var auditCount = Audits.Count; var receiptCount = Receipts.Count;
            inTransaction = true;
            try { var result = await action(ct); Committed = true; return result; }
            catch
            {
                BusinessRows = rowsBefore;
                Audits.RemoveRange(auditCount, Audits.Count - auditCount);
                Receipts.RemoveRange(receiptCount, Receipts.Count - receiptCount);
                throw;
            }
            finally { inTransaction = false; }
        }
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
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
