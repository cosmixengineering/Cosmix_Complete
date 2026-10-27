using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Xunit;

namespace Cosmix.Tests;

public class PayrollActivationAuthorityExpiryTests
{
    public static IEnumerable<object[]> ExpiryCases() =>
        from permission in new[] { AttendancePayrollActivationPermissions.Request, AttendancePayrollActivationPermissions.Approve }
        from replay in new[] { false, true }
        from stage in new[] { "final-grant-read", "final-session-read" }
        select new object[] { permission, replay, stage };

    [Theory, MemberData(nameof(ExpiryCases))]
    public async Task All_scoped_activation_read_must_survive_final_await_and_retry(string permission, bool replay, string stage)
    {
        var f = new Fixture(permission, stage);
        if (replay) await f.PrimeReplay();
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(f.Execute);
        Assert.Equal(403, failure.StatusCode);
        Assert.True(f.Advanced);
        Assert.False(f.Committed);
        Assert.Equal(replay ? 1 : 0, f.BusinessRows);
        Assert.Equal(replay ? 1 : 0, f.Receipts.Count);
        Assert.Equal(replay ? 1 : 0, f.Audits.Count);
    }

    [Theory]
    [InlineData(AttendancePayrollActivationPermissions.Request, false)]
    [InlineData(AttendancePayrollActivationPermissions.Request, true)]
    [InlineData(AttendancePayrollActivationPermissions.Approve, false)]
    [InlineData(AttendancePayrollActivationPermissions.Approve, true)]
    public async Task Valid_independent_permissions_preserve_operation_and_exact_retry(string permission, bool replay)
    {
        var f = new Fixture(permission);
        if (replay) await f.PrimeReplay();
        Assert.Equal(42, await f.Execute());
        Assert.True(f.Committed);
        Assert.Equal(1, f.BusinessRows);
        Assert.Single(f.Receipts);
        Assert.Equal(replay ? 2 : 1, f.Audits.Count);
    }

    [Theory]
    [InlineData(AttendancePayrollActivationPermissions.Request)]
    [InlineData(AttendancePayrollActivationPermissions.Approve)]
    public async Task Employee_scoped_activation_read_cannot_replace_required_All_authority(string permission)
    {
        var f = new Fixture(permission, allRead: false);
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(f.Execute);
        Assert.Equal(403, failure.StatusCode);
        Assert.Equal(0, f.BusinessRows);
        Assert.Empty(f.Receipts);
        Assert.Empty(f.Audits);
    }

    private sealed class Fixture
    {
        private static readonly DateTimeOffset Start = new(2030, 3, 1, 12, 0, 0, TimeSpan.Zero);
        private readonly Clock clock = new(Start);
        private readonly string permission;
        private readonly string? stage;
        private readonly Guid employee = Guid.NewGuid();
        private readonly Guid resource = Guid.NewGuid();
        private bool enabled = true;
        private bool inTransaction;
        private bool finalCheck;
        private readonly AttendanceOperation operation;
        private readonly AttendancePayrollActivationService activation;
        private readonly object bound;
        private readonly Dictionary<string, (string Purpose, byte[] Bytes)> files = [];
        public int BusinessRows { get; private set; }
        public bool Committed { get; private set; }
        public bool Advanced { get; private set; }
        public List<AuditEvent> Audits { get; } = [];
        public List<IdempotencyReceipt> Receipts { get; } = [];

        public Fixture(string permission, string? stage = null, bool allRead = true)
        {
            this.permission = permission; this.stage = stage;
            var identity = new AuthenticatedAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
            var grants = new List<PermissionGrant>();
            foreach (var value in new[] { permission, AttendancePayrollInputPermissions.Read })
                grants.Add(Grant(value, AccessScopeKind.All, null, Start.AddHours(1)));
            if (allRead) grants.Add(Grant(AttendancePayrollActivationPermissions.Read, AccessScopeKind.All, null, Start.AddMinutes(1)));
            // This grant remains valid at expiry, but it must not downgrade the All requirement.
            grants.Add(Grant(AttendancePayrollActivationPermissions.Read, AccessScopeKind.Employee, employee, Start.AddHours(1)));
            var current = Proxy<ICurrentAuthentication>((_, _) => identity);
            var access = Proxy<IAccountAccessReader>((_, args) =>
            {
                Assert.True(inTransaction);
                IReadOnlyList<PermissionGrant> result = grants.Where(x => x.IsEffectiveAt((DateTimeOffset)args[1]!)).ToArray();
                Advance("final-grant-read");
                return Task.FromResult(result);
            });
            var authorization = Proxy<IAccessManagementRepository>((method, args) => method.Name switch
            {
                "InTransactionAsync" => typeof(Fixture).GetMethod(nameof(Transaction), BindingFlags.Instance | BindingFlags.NonPublic)!
                    .MakeGenericMethod(method.GetGenericArguments()).Invoke(this, args)!,
                "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("ConfiguredFinance"),
                _ => throw new InvalidOperationException(method.Name)
            });
            var journal = Proxy<ISchedulingJournalRepository>((method, args) =>
            {
                Assert.True(inTransaction);
                switch (method.Name)
                {
                    case "IsCurrentSessionAsync": Advance("final-session-read"); return Task.FromResult(true);
                    case "FindReceiptAsync": return Task.FromResult(Receipts.SingleOrDefault(x => x.Key == (string)args[2]!));
                    case "AddReceipt": Receipts.Add((IdempotencyReceipt)args[0]!); return null;
                    case "AddAudit": Audits.Add((AuditEvent)args[0]!); return null;
                    case "SaveChangesAsync": return Task.CompletedTask;
                    default: throw new InvalidOperationException(method.Name);
                }
            });
            var storage = Proxy<IPrivateFileStore>((method, args) =>
            {
                if (method.Name == "WriteAsync")
                {
                    var key = Guid.NewGuid().ToString("N"); var bytes = ((byte[])args[0]!).ToArray();
                    files.Add(key, ((string)args[1]!, bytes));
                    return Task.FromResult(new PrivateFileReference(key, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length));
                }
                var value = files[((PrivateFileReference)args[0]!).StorageKey];
                Assert.Equal((string)args[1]!, value.Purpose); return Task.FromResult(value.Bytes.ToArray());
            });
            operation = new(current, access, authorization, journal, null!, clock, storage);
            IReadOnlyList<AttendanceResourceScope> scopes = [new([new(EmployeeId: employee)])];
            var producer = Proxy<IAttendancePayrollInputProducer>((method, _) => method.Name == "ResourceScopesAsync"
                ? Task.FromResult(scopes) : throw new InvalidOperationException(method.Name));
            activation = new(operation, null!, null!, producer, null!, storage, clock);
            var month = new DateOnly(2030, 2, 1);
            var row = new AttendancePayrollInputRevision(employee, month, 1, null, AttendancePayrollInputRules.Version,
                new('A', 64), "synthetic-unused-reference", identity.AccountId, Start);
            var evidence = new AttendancePayrollInputEvidence(1, employee, month, 1, new('A', 64), null!, [], scopes,
                identity.AccountId, "Authority boundary fixture", Start);
            // Isolate the real service's authorization guard; source-proof validation is tested separately.
            bound = Activator.CreateInstance(typeof(AttendancePayrollActivationService).GetNestedType("Bound", BindingFlags.NonPublic)!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [row, evidence, null, true], null)!;
        }

        public Task<int> Execute() => operation.WriteAsync(permission, "synthetic.activation", employee, "synthetic-activation-key",
            new { value = 42 }, "Synthetic activation authority check", new("127.0.0.1", "synthetic-activation-expiry"),
            (_, _) => { BusinessRows++; return Task.FromResult(new AttendanceChange<int>(resource, null, 42)); }, default,
            beforeReplay: RequireRead, beforeCommit: RequireRead, afterSave: async (actor, ct) =>
            { await RequireRead(actor, ct); finalCheck = true; });

        private Task RequireRead(AttendanceActor actor, CancellationToken ct) =>
            (Task)typeof(AttendancePayrollActivationService).GetMethod("RequireRead", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(activation, [bound, actor, ct])!;

        public async Task PrimeReplay()
        {
            enabled = false; await Execute(); enabled = true; finalCheck = false; Committed = false;
        }
        private void Advance(string point)
        {
            if (enabled && finalCheck && !Advanced && point == stage)
            { Advanced = true; clock.Now = Start.AddMinutes(1); }
        }
        private static PermissionGrant Grant(string permission, AccessScopeKind kind, Guid? target, DateTimeOffset until) =>
            new(permission, true, Guid.NewGuid(), kind, target) { AuthorityTimingKnown = true, ValidFromUtc = Start.AddHours(-1), ValidUntilUtc = until };
        private async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
        {
            Assert.False(inTransaction);
            var rows = BusinessRows; var audits = Audits.Count; var receipts = Receipts.Count; inTransaction = true;
            try { var result = await action(ct); Committed = true; return result; }
            catch
            {
                BusinessRows = rows; Audits.RemoveRange(audits, Audits.Count - audits);
                Receipts.RemoveRange(receipts, Receipts.Count - receipts); throw;
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
        var proxy = DispatchProxy.Create<T, TestProxy>(); ((TestProxy)(object)proxy).Handler = handler; return proxy;
    }
    public class TestProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Handler = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args ?? []);
    }
}

