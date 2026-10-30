using System.Reflection;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Xunit;

namespace Cosmix.Tests;

public class EmployeeAuthorityExpiryTests
{
    public static IEnumerable<object[]> ExpiryCases() =>
        from kind in new[] { "read", "write", "replay", "try-replay" }
        from authority in new[] { "session", "grant" }
        from stage in new[] { "role-read", "audit-save" }
        select new object[] { kind, authority, stage };

    [Theory, MemberData(nameof(ExpiryCases))]
    public async Task Expiring_authority_cannot_expose_or_commit_employee_operations(string kind, string authority, string stage)
    {
        var f = new Fixture(authority, stage);
        if (kind.Contains("replay")) await f.PrimeReplay();
        await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Execute(kind));
        Assert.True(f.Advanced);
        Assert.False(f.Committed);
        Assert.Equal(kind.Contains("replay") ? 1 : 0, f.BusinessRows);
        Assert.Equal(kind.Contains("replay") ? 1 : 0, f.Receipts.Count);
        Assert.Equal(kind.Contains("replay") ? 1 : 0, f.Audits.Count);
        if (stage == "role-read") Assert.Equal(kind.Contains("replay") ? 1 : 0, f.Actions);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    public async Task Untimed_grants_cannot_authorize_employee_access(string kind)
    {
        var f = new Fixture("unknown-timing");
        await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Execute(kind));
        Assert.Equal(0, f.Actions);
        Assert.Empty(f.Audits);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData("replay")]
    [InlineData("try-replay")]
    public async Task Valid_authority_preserves_audited_reads_writes_and_exact_retries(string kind)
    {
        var f = new Fixture();
        if (kind.Contains("replay")) await f.PrimeReplay();
        Assert.Equal(f.EmployeeId, (await f.Execute(kind))!.EmployeeId);
        Assert.True(f.Committed);
        Assert.Equal(kind == "read" ? 0 : 1, f.BusinessRows);
        Assert.Equal(kind == "read" ? 0 : 1, f.Receipts.Count);
        Assert.Equal(kind.Contains("replay") ? 2 : 1, f.Audits.Count);
    }

    [Theory]
    [InlineData("read", false)]
    [InlineData("write", false)]
    [InlineData("read", true)]
    [InlineData("write", true)]
    public async Task Fixed_review_authority_is_required_on_the_permission_assignment_until_completion(string kind, bool valid)
    {
        var f = new Fixture(valid ? "fixed-valid" : "fixed-role", valid ? null : "audit-save");
        if (valid)
        {
            Assert.NotNull(await f.Execute(kind));
            Assert.True(f.Committed);
        }
        else
        {
            await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Execute(kind));
            Assert.False(f.Committed);
            Assert.Empty(f.Receipts);
            Assert.Empty(f.Audits);
            Assert.Equal(0, f.BusinessRows);
        }
    }

    private sealed class Fixture
    {
        private string Permission => authority is "fixed-role" or "fixed-valid" ? EmployeePermissions.Review : EmployeePermissions.Read;
        private string? ReviewPermission => authority is "fixed-role" or "fixed-valid" ? Permission : null;
        private static readonly DateTimeOffset Start = new(2026, 10, 30, 10, 0, 0, TimeSpan.Zero);
        private readonly Clock clock = new(Start);
        private readonly string? authority;
        private readonly string? stage;
        private bool enabled = true;
        private bool inTransaction;
        private readonly EmployeeOperation operation;
        private readonly AuthRequestContext context = new("127.0.0.1", "synthetic-employee-expiry");
        public Guid EmployeeId { get; } = Guid.NewGuid();
        public bool Advanced { get; private set; }
        public bool Committed { get; private set; }
        public int BusinessRows { get; private set; }
        public int Actions { get; private set; }
        public List<IdempotencyReceipt> Receipts { get; } = [];
        public List<AuditEvent> Audits { get; } = [];

        public Fixture(string? authority = null, string? stage = null)
        {
            this.authority = authority; this.stage = stage;
            var actor = new AuthenticatedAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
            var current = Proxy<ICurrentAuthentication>((_, _) => actor);
            PermissionGrant grant = new(Permission, false, Guid.NewGuid(), AccessScopeKind.All, null)
            {
                AuthorityTimingKnown = authority != "unknown-timing", ValidFromUtc = Start.AddHours(-1),
                ValidUntilUtc = authority == "grant" ? Start.AddMinutes(1) : Start.AddHours(1)
            };
            var access = Proxy<IAccountAccessReader>((_, args) =>
            {
                Assert.True(inTransaction);
                return Task.FromResult<IReadOnlyList<PermissionGrant>>(!grant.AuthorityTimingKnown || grant.IsEffectiveAt((DateTimeOffset)args[1]!) ? [grant] : []);
            });
            var authorization = Proxy<IAccessManagementRepository>((method, args) => method.Name switch
            {
                "InTransactionAsync" or "InReadTransactionAsync" => typeof(Fixture)
                    .GetMethod(nameof(Transaction), BindingFlags.Instance | BindingFlags.NonPublic)!
                    .MakeGenericMethod(method.GetGenericArguments()).Invoke(this, args.Take(2).ToArray()),
                "IsCurrentAdministratorAsync" => Task.FromResult(authority != "session" || (DateTimeOffset)args[1]! < Start.AddMinutes(1)),
                "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult((Guid)args[1]! == grant.RoleAssignmentId &&
                    (authority != "fixed-role" || (DateTimeOffset)args[2]! < Start.AddMinutes(1))),
                "GetAssignmentRoleCodeAsync" => Role(),
                _ => throw new InvalidOperationException(method.Name)
            });
            var repository = Proxy<IEmployeeHiringRepository>((method, args) =>
            {
                Assert.True(inTransaction);
                switch (method.Name)
                {
                    case "FindDraftAsync": return Task.FromResult<EmployeeHiringDraft?>(null);
                    case "FindReceiptAsync": return Task.FromResult(Receipts.SingleOrDefault(x => x.Key == (string)args[2]!));
                    case "AddReceipt": Receipts.Add((IdempotencyReceipt)args[0]!); return null;
                    case "AddAudit": Audits.Add((AuditEvent)args[0]!); return null;
                    case "SaveChangesAsync": Advance("audit-save"); return Task.CompletedTask;
                    default: throw new InvalidOperationException(method.Name);
                }
            });
            var files = Proxy<IPrivateFileStore>((method, args) => method.Name == "WriteAsync"
                ? Task.FromResult(new PrivateFileReference(Guid.NewGuid().ToString("N"), "1", new string('A', 64), "synthetic", ((byte[])args[0]!).Length))
                : throw new InvalidOperationException(method.Name));
            operation = new(current, access, authorization, repository, clock, files);
        }

        private Task<string?> Role() { Advance("role-read"); return Task.FromResult<string?>(ReviewPermission is null ? "HR" : "ADMIN"); }
        private EmployeeWriteResult Result() => new(EmployeeId, "synthetic-version", OnboardingStatus.Approved);
        public Task<EmployeeWriteResult?> Execute(string kind)
        {
            if (kind == "try-replay") return operation.TryReplayAsync([Permission], "synthetic.write", EmployeeId,
                "synthetic-employee-retry", new { value = 42 }, context, default);
            return AsNullable(kind == "read"
                ? operation.ReadAsync([Permission], "synthetic.read", EmployeeId, context, (_, _) =>
                    { Actions++; return Task.FromResult(Result()); }, default, fixedAdminPermission: ReviewPermission)
                : operation.WriteAsync([Permission], "synthetic.write", EmployeeId, "synthetic-employee-retry", new { value = 42 },
                    context, (_, _) => { Actions++; BusinessRows++; return Task.FromResult(Result()); }, default, fixedAdminPermission: ReviewPermission));
        }
        private static async Task<EmployeeWriteResult?> AsNullable(Task<EmployeeWriteResult> task) => await task;
        public async Task PrimeReplay() { enabled = false; await Execute("write"); enabled = true; Committed = false; }
        private void Advance(string point)
        { if (enabled && !Advanced && stage == point) { Advanced = true; clock.Now = Start.AddMinutes(1); } }
        private async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
        {
            Assert.False(inTransaction);
            var rows = BusinessRows; var audits = Audits.Count; var receipts = Receipts.Count;
            inTransaction = true;
            try { var result = await action(ct); Committed = true; return result; }
            catch
            {
                BusinessRows = rows;
                Audits.RemoveRange(audits, Audits.Count - audits);
                Receipts.RemoveRange(receipts, Receipts.Count - receipts);
                throw;
            }
            finally { inTransaction = false; }
        }
    }
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    { public DateTimeOffset Now { get; set; } = now; public override DateTimeOffset GetUtcNow() => Now; }
    private static T Proxy<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = DispatchProxy.Create<T, TestProxy>(); ((TestProxy)(object)value).Handler = handler; return value; }
    public class TestProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Handler = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    }
}
