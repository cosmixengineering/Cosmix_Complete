using System.Reflection;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Security;
using Cosmix.Application.Finance.Costing;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.PayrollAccounting;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollCostWorkflowTests
{
    [Fact]
    public async Task Month_report_exposes_unallocated_batches_and_counts_only_latest_approved_plan()
    {
        var f = await Fixture.CreateAsync();
        var initial = await f.Report();
        Assert.Single(initial.UnallocatedBatchIds);
        Assert.Empty(initial.Rows);
        var first = await f.Create();
        f.Actor = Guid.NewGuid(); f.Employee = Guid.NewGuid();
        await f.Decide(first);
        var next = await f.Create(previous: first.Id);
        Assert.Equal(1000m, Assert.Single((await f.Report()).Rows).Amount);
        f.Actor = Guid.NewGuid(); f.Employee = Guid.NewGuid();
        await f.Decide(next);
        var final = await f.Report();
        Assert.Empty(final.UnallocatedBatchIds);
        Assert.Equal(1000m, Assert.Single(final.Rows).Amount);
        Assert.Equal(1, final.ApprovedBatchCount);
    }

    [Fact]
    public async Task Cost_plan_requires_another_employee_and_retains_the_effective_approved_revision()
    {
        var f = await Fixture.CreateAsync(); var draft = await f.Create();
        Assert.Equal("Draft", draft.State);
        Assert.Equal(403, (await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Decide(draft))).StatusCode);
        f.Actor = Guid.NewGuid();
        Assert.Equal(403, (await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Decide(draft))).StatusCode);
        f.Employee = Guid.NewGuid();
        var approved = await f.Decide(draft);
        Assert.Equal("Approved", approved.State);
        Assert.Equal(1000m, Assert.Single(approved.Costs).Amount);
        Assert.Equal(approved.Id, (await f.Read()).EffectivePlanId);
        var replacement = await f.Create(previous: approved.Id);
        f.Actor = Guid.NewGuid(); f.Employee = Guid.NewGuid();
        Assert.Equal("Rejected", (await f.Decide(replacement, approve: false)).State);
        Assert.Equal(approved.Id, (await f.Read()).EffectivePlanId);
    }

    [Fact]
    public async Task Retry_returns_one_draft_but_new_or_changed_intent_does_not_create_duplicate_costs()
    {
        var f = await Fixture.CreateAsync(); var key = Guid.NewGuid().ToString();
        var first = await f.Create(key);
        Assert.Equal(first.Id, (await f.Create(key)).Id);
        Assert.Equal(409, (await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Create())).StatusCode);
        Assert.Equal(409, (await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Create(key, previous: Guid.NewGuid()))).StatusCode);
        Assert.Single(f.Plans);
    }

    [Fact]
    public async Task Closed_period_unauthorized_action_and_stale_review_do_not_change_allocations()
    {
        var f = await Fixture.CreateAsync();
        f.Allowed = false;
        Assert.Equal(403, (await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Create())).StatusCode);
        f.Allowed = true; var plan = await f.Create();
        f.Actor = Guid.NewGuid(); f.Employee = Guid.NewGuid();
        Assert.Equal(409, (await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Decide(plan with { Version = "AAAAAAAAAAA=" }))).StatusCode);
        Assert.Equal("Draft", Assert.Single(f.Plans).State);
        var closed = await Fixture.CreateAsync();
        closed.Salary.Period.Close("independent-finance", DateTimeOffset.UtcNow);
        await Assert.ThrowsAnyAsync<Exception>(() => closed.Create());
        Assert.Empty(closed.Plans);
    }

    private sealed class Fixture
    {
        public Guid Actor = Guid.NewGuid(), Employee = Guid.NewGuid();
        public bool Allowed = true;
        public readonly List<PayrollCostPlan> Plans = [];
        private readonly List<IdempotencyReceipt> receipts = [];
        public readonly PayrollFinanceSourceTests.Fixture Salary = new();
        private PayrollCostService service = null!;
        private PayrollCostSource source = null!;
        private static readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-project-cost");
        private readonly Guid session = Guid.NewGuid(); private long version;
        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture(); await f.Salary.Finalize(); await f.Salary.Accrue();
            f.source = await f.Salary.CostSource.ReadAsync(f.Salary.Source.BatchId, default);
            var grants = new[] { PayrollCostService.Read, PayrollCostService.Manage, PayrollCostService.Approve }
                .Select(p => new PermissionGrant(p, true, Guid.NewGuid(), AccessScopeKind.All, null)
                { AuthorityTimingKnown = true, ValidFromUtc = DateTimeOffset.UtcNow.AddDays(-1) }).ToArray();
            var current = Proxy<ICurrentAuthentication>((_, _) => new AuthenticatedAccount(f.Actor, f.Employee, f.session, 1, SessionClient.Web, true));
            var access = Proxy<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(f.Allowed ? grants : []));
            var authorization = Proxy<IAccessManagementRepository>((method, args) => method.Name switch {
                "InTransactionAsync" => typeof(Fixture).GetMethod(nameof(Transaction), BindingFlags.Static | BindingFlags.NonPublic)!
                    .MakeGenericMethod(method.GetGenericArguments()).Invoke(null, args),
                "IsCurrentAdministratorAsync" => Task.FromResult(true),
                "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SyntheticFinance"),
                _ => throw new InvalidOperationException(method.Name)
            });
            var journal = Proxy<ISchedulingJournalRepository>((method, args) => method.Name switch {
                "IsCurrentSessionAsync" => Task.FromResult(true),
                "FindReceiptAsync" => Task.FromResult(f.receipts.SingleOrDefault(x => x.Key == (string)args[2]!)),
                "AddReceipt" => Add(f.receipts, (IdempotencyReceipt)args[0]!),
                "AddAudit" => null,
                "SaveChangesAsync" => Task.CompletedTask,
                _ => throw new InvalidOperationException(method.Name)
            });
            var repository = Proxy<IPayrollCostRepository>((method, args) => method.Name switch {
                "PlansAsync" => Task.FromResult<IReadOnlyList<PayrollCostPlan>>(f.Plans.ToArray()),
                "MonthBatchIdsAsync" => Task.FromResult<IReadOnlyList<Guid>>([f.Salary.Source.BatchId]),
                "PlanAsync" => Task.FromResult(f.Plans.SingleOrDefault(x => x.Id == (Guid)args[0]!)),
                "TargetsAsync" => Task.FromResult(new PayrollCostTargets([], [])),
                "Add" => Add(f.Plans, (PayrollCostPlan)args[0]!),
                "SaveChangesAsync" => f.Save(),
                "ExpectVersion" => Expect((PayrollCostPlan)args[0]!, (byte[])args[1]!),
                _ => throw new InvalidOperationException(method.Name)
            });
            var files = new PayrollSalaryFrozenEvidenceTests.MemoryFiles();
            var operation = new SensitiveComplianceOperation(current, access, authorization, journal, TimeProvider.System);
            var writes = new PayrollFinanceOperation(operation, journal, files, TimeProvider.System);
            f.service = new(operation, writes, repository, f.Salary.CostSource, f.Salary.Setup, files, TimeProvider.System);
            return f;
        }
        public Task<PayrollCostPlanResponse> Create(string? key = null, Guid? previous = null) =>
            service.CreateAsync(source.BatchId, new(source.Fingerprint, previous,
                [new(Salary.Employee, null, null, 10000)], "Synthetic approved expense allocation"), key ?? Guid.NewGuid().ToString(), Context, default);
        public Task<PayrollCostPlanResponse> Decide(PayrollCostPlanResponse plan, bool approve = true) =>
            service.DecideAsync(plan.Id, new(plan.Version, approve, "Independent synthetic review"), Guid.NewGuid().ToString(), Context, default);
        public Task<PayrollCostWorkspace> Read() => service.WorkspaceAsync(source.BatchId, Context, default);
        public Task<PayrollCostMonthReport> Report() => service.MonthReportAsync(source.PeriodStart.Year, source.PeriodStart.Month, Context, default);
        private Task Save() { foreach (var plan in Plans) typeof(PayrollCostPlan).GetProperty(nameof(PayrollCostPlan.RowVersion))!.SetValue(plan, BitConverter.GetBytes(++version)); return Task.CompletedTask; }
        private static object? Expect(PayrollCostPlan plan, byte[] version) { if (!plan.RowVersion.SequenceEqual(version)) throw new AuthenticationFailure("access_version_conflict", 409); return null; }
        private static object? Add<T>(List<T> rows, T row) { rows.Add(row); return null; }
        private static Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
    }
    private static T Proxy<T>(Func<MethodInfo, object?[], object?> callback) where T : class
    { var value = DispatchProxy.Create<T, Callback>(); ((Callback)(object)value).Run = callback; return value; }
    public class Callback : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Run { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Run(targetMethod!, args ?? []);
    }
}
