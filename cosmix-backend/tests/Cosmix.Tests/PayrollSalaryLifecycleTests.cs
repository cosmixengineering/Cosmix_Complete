using System.Reflection;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.PayrollHandoff;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollSalaryLifecycleTests
{
    [Fact]
    public async Task Salary_beneficiary_cannot_review_lock_or_process_even_with_another_account()
    {
        var fixture = await Fixture.CreateAsync();
        var beneficiary = fixture.Batch.Lines.First().EmployeeId;
        fixture.Employee = beneficiary;
        Assert.Equal(403, (await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Review())).StatusCode);
        fixture.Actor = Guid.NewGuid();
        Assert.Equal(403, (await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Review())).StatusCode);
        fixture.Employee = Guid.NewGuid();
        await fixture.Review();
        fixture.Actor = Guid.NewGuid(); fixture.Employee = beneficiary;
        Assert.Equal(403, (await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Lock())).StatusCode);
        fixture.Employee = Guid.NewGuid(); await fixture.Lock();
        fixture.Actor = Guid.NewGuid(); fixture.Employee = beneficiary;
        Assert.Equal(403, (await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Process())).StatusCode);
        fixture.Employee = Guid.NewGuid();
        Assert.Equal("finance_handed_off", (await fixture.Process()).Batch.State);
    }

    [Fact]
    public async Task Approved_salary_can_be_returned_without_erasing_its_original_approval_or_lines()
    {
        var fixture = await Fixture.CreateAsync();
        await fixture.Review();
        var approval = fixture.Batch.HrApprovalRequestId;
        var fingerprint = fixture.Batch.FrozenFingerprint;
        var result = await fixture.Return();
        Assert.Equal("returned", result.Batch.State);
        Assert.Equal(approval, fixture.Batch.HrApprovalRequestId);
        Assert.Equal(fingerprint, fixture.Batch.FrozenFingerprint);
        Assert.Equal(100m, Assert.Single(result.Lines).Amount);
        fixture.Actor = Guid.NewGuid();
        Assert.Equal(409, (await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Lock())).StatusCode);
        await Assert.ThrowsAsync<AuthenticationFailure>(() =>
            new PayrollFrozenBatchReader(fixture.Repository, fixture.Files).ReadAsync(fixture.Batch.Id, default));
    }

    [Fact]
    public async Task Return_rejects_a_salary_that_has_not_been_hr_approved()
    {
        var fixture = await Fixture.CreateAsync();
        Assert.Equal(409, (await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Return())).StatusCode);
        Assert.Equal("prepared", fixture.Batch.ReviewState);
    }

    [Fact]
    public async Task Return_rejects_a_locked_salary_and_preserves_its_finance_authority()
    {
        var fixture = await Fixture.CreateAsync();
        await fixture.Review(); fixture.Actor = Guid.NewGuid(); await fixture.Lock();
        Assert.Equal(409, (await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Return())).StatusCode);
        Assert.Equal("locked", fixture.Batch.ReviewState);
        Assert.Equal(100m, Assert.Single((await new PayrollFrozenBatchReader(fixture.Repository, fixture.Files)
            .ReadAsync(fixture.Batch.Id, default)).Employees).NetPay);
    }

    [Fact]
    public async Task Return_requires_current_hr_authority_and_a_nonblank_reason()
    {
        var fixture = await Fixture.CreateAsync(); await fixture.Review();
        fixture.PermissionsEnabled = false;
        Assert.Equal(403, (await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Return())).StatusCode);
        fixture.PermissionsEnabled = true;
        Assert.Equal(400, (await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Return(reason: " "))).StatusCode);
        Assert.Equal("hr_approved", fixture.Batch.ReviewState);
    }

    [Fact]
    public async Task Preparer_cannot_return_own_batch_and_processed_batch_cannot_be_returned()
    {
        var fixture = await Fixture.CreateAsync(); await fixture.Review();
        // Preparer cannot return (maker-checker separation)
        fixture.Actor = Guid.Parse(fixture.Batch.PreparedByUserId);
        Assert.Equal(403, (await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Return())).StatusCode);

        // Batch with downstream processing records cannot be returned
        fixture.Actor = Guid.NewGuid();
        fixture.AddProcessingRecord(new PayrollHandoffProcessingRecord(fixture.Batch.Id, "FINANCE-EXT-1", "External Accounts handoff", "synthetic-user", DateTimeOffset.UtcNow));
        Assert.Equal(409, (await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Return())).StatusCode);
    }

    [Fact]
    public async Task Return_retries_preserve_one_return_and_reject_new_intent_after_return()
    {
        var fixture = await Fixture.CreateAsync(); await fixture.Review();
        var key = Guid.NewGuid().ToString();
        var first = await fixture.Return(key);
        Assert.Equal(first.Batch.State, (await fixture.Return(key)).Batch.State);
        Assert.Equal(409, (await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Return())).StatusCode);
    }

    [Fact]
    public async Task Service_requires_independent_hr_management_and_finance_and_retains_approved_proofs()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Actor = Guid.Parse(fixture.Batch.PreparedByUserId);
        var self = await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Review());
        Assert.Equal(403, self.StatusCode);
        fixture.Actor = Guid.NewGuid();
        Assert.Equal("hr_approved", (await fixture.Review()).Batch.State);
        var hrLock = await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Lock());
        Assert.Equal(403, hrLock.StatusCode);
        fixture.Actor = Guid.NewGuid();
        Assert.Equal("locked", (await fixture.Lock()).Batch.State);
        var managerFinance = await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Process());
        Assert.Equal(403, managerFinance.StatusCode);
        fixture.Actor = Guid.NewGuid();
        var result = await fixture.Process();
        Assert.Equal("finance_handed_off", result.Batch.State);
        Assert.Equal(100m, Assert.Single(result.Batch.EmployeeTotals).NetPay);
        Assert.Single(result.Processing);
        var frozen = await new PayrollFrozenBatchReader(fixture.Repository, fixture.Files).ReadAsync(fixture.Batch.Id, default);
        Assert.Equal(100m, Assert.Single(frozen.Employees).NetPay);
    }

    [Fact]
    public async Task Service_refuses_management_lock_before_hr_review()
    {
        var fixture = await Fixture.CreateAsync();
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Lock());
        Assert.Equal(409, failure.StatusCode);
        Assert.Null(fixture.Batch.LockedAt);
    }

    [Fact]
    public async Task Hr_rejection_preserves_salary_and_prevents_management_lock()
    {
        var fixture = await Fixture.CreateAsync();
        var result = await fixture.Review(ApprovalState.Rejected);
        Assert.Equal("rejected", result.Batch.State);
        Assert.Equal(100m, Assert.Single(result.Lines).Amount);
        fixture.Actor = Guid.NewGuid();
        await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Lock());
    }

    [Fact]
    public async Task Only_maker_can_cancel_an_unreviewed_salary()
    {
        var fixture = await Fixture.CreateAsync();
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Cancel());
        Assert.Equal(403, failure.StatusCode);
        fixture.Actor = Guid.Parse(fixture.Batch.PreparedByUserId);
        Assert.Equal("cancelled", (await fixture.Cancel()).Batch.State);
        Assert.Single(fixture.Batch.Lines);
    }

    [Fact]
    public async Task Missing_hr_permission_denies_review_without_state_change()
    {
        var fixture = await Fixture.CreateAsync(); fixture.PermissionsEnabled = false;
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(() => fixture.Review());
        Assert.Equal(403, failure.StatusCode);
        Assert.Equal("prepared", fixture.Batch.ReviewState);
    }

    private sealed class Fixture
    {
        public Guid Actor = Guid.NewGuid();
        public bool PermissionsEnabled = true;
        public Guid Employee = Guid.NewGuid();
        private readonly Guid session = Guid.NewGuid();
        private readonly List<ApprovalRequest> approvals = [];
        private readonly List<ApprovalDecision> decisions = [];
        private readonly List<PayrollHandoffProcessingRecord> processing = [];
        private readonly List<IdempotencyReceipt> receipts = [];
        private PayrollHandoffService service = null!;
        public PayrollHandoffBatch Batch = null!;
        public PayrollSalaryFrozenEvidenceTests.MemoryFiles Files = null!;
        public IPayrollHandoffRepository Repository = null!;
        private readonly AuthRequestContext context = new("127.0.0.1", "synthetic-salary-lifecycle");
        private const string Version = "AAAAAAAAAAA=";

        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture(); (f.Batch, f.Files) = await PayrollSalaryFrozenEvidenceTests.FixtureAsync();
            var current = Proxy<ICurrentAuthentication>((_, _) => new AuthenticatedAccount(f.Actor, f.Employee, f.session, 1, SessionClient.Web, true));
            var grants = new[] { PayrollHandoffPermissions.Prepare, PayrollHandoffPermissions.HrReview,
                PayrollHandoffPermissions.Lock, PayrollHandoffPermissions.Process }.Select(p => new PermissionGrant(p, true, Guid.NewGuid(), AccessScopeKind.All, null)
                { AuthorityTimingKnown = true, ValidFromUtc = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero) }).ToArray();
            var access = Proxy<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(f.PermissionsEnabled ? grants : []));
            var authorization = Proxy<IAccessManagementRepository>((method, args) => method.Name switch
            {
                "InTransactionAsync" => typeof(Fixture).GetMethod(nameof(Transaction), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(method.GetGenericArguments()).Invoke(null, args),
                "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SyntheticPayrollRole"),
                _ => throw new InvalidOperationException(method.Name)
            });
            var journal = Proxy<ISchedulingJournalRepository>((method, args) => method.Name switch
            {
                "IsCurrentSessionAsync" => Task.FromResult(true),
                "FindReceiptAsync" => Task.FromResult(f.receipts.SingleOrDefault(x => x.Key == (string)args[2]!)),
                "AddReceipt" => Add(f.receipts, (IdempotencyReceipt)args[0]!),
                "AddAudit" => null,
                "SaveChangesAsync" => Task.CompletedTask,
                _ => throw new InvalidOperationException(method.Name)
            });
            f.Repository = Proxy<IPayrollHandoffRepository>((method, args) => method.Name switch
            {
                "FindBatchAsync" => Task.FromResult<PayrollHandoffBatch?>((Guid)args[0]! == f.Batch.Id ? f.Batch : null),
                "ExpectVersion" => null,
                "SaveChangesAsync" => Task.CompletedTask,
                "ApprovalAsync" => Task.FromResult(f.approvals.SingleOrDefault(x => x.Id == (Guid)args[0]!)),
                "DecisionRecordsAsync" => Task.FromResult<IReadOnlyList<ApprovalDecision>>(f.decisions.Where(x => x.ApprovalRequestId == (Guid)args[0]!).ToArray()),
                "ProcessingAsync" => Task.FromResult<IReadOnlyList<PayrollHandoffProcessingRecord>>(f.processing.ToArray()),
                "ProcessingReferenceExistsAsync" => Task.FromResult(false),
                "Add" => f.AddRepository(args),
                _ => throw new InvalidOperationException(method.Name)
            });
            var operation = new AttendanceOperation(current, access, authorization, journal, null!, new Clock(), f.Files);
            f.service = new(operation, f.Repository, null!, null!, null!, null!, access, f.Files);
            return f;
        }

        public Task<PayrollHandoffBatchDetail> Review(ApprovalState decision = ApprovalState.Approved) =>
            service.HrReviewAsync(Batch.Id, new(Version, decision, "Synthetic HR review"), Guid.NewGuid().ToString(), context, default);
        public Task<PayrollHandoffBatchDetail> Lock() => service.LockAsync(Batch.Id, new(Version, "Synthetic Management lock"), Guid.NewGuid().ToString(), context, default);
        public Task<PayrollHandoffBatchDetail> Process() => service.ProcessingAsync(Batch.Id, new("SYNTHETIC-FINANCE", "Synthetic Finance handoff"), Guid.NewGuid().ToString(), context, default);
        public Task<PayrollHandoffBatchDetail> Cancel() => service.CancelAsync(Batch.Id, new(Version, "Synthetic cancellation"), Guid.NewGuid().ToString(), context, default);
        public Task<PayrollHandoffBatchDetail> Return(string? key = null, string reason = "Correct source before payroll lock")
            => service.ReturnForCorrectionAsync(Batch.Id, new(Version, reason), key ?? Guid.NewGuid().ToString(), context, default);
        public void AddProcessingRecord(PayrollHandoffProcessingRecord record) => processing.Add(record);
        private object? AddRepository(object?[] args)
        {
            if (args[0] is ApprovalRequest approval) { approvals.Add(approval); decisions.Add((ApprovalDecision)args[1]!); }
            else if (args[0] is PayrollHandoffProcessingRecord record) processing.Add(record);
            else throw new InvalidOperationException("Unexpected synthetic repository write");
            return null;
        }
        private static object? Add<T>(List<T> list, T row) { list.Add(row); return null; }
        private static Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
    }

    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero); }
    private static T Proxy<T>(Func<MethodInfo, object?[], object?> callback) where T : class
    {
        var value = DispatchProxy.Create<T, CallbackProxy>(); ((CallbackProxy)(object)value).Callback = callback; return value;
    }
    public class CallbackProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Callback = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Callback(method!, args ?? []);
    }
}
