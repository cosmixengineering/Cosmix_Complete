using System.Reflection;
using System.Text;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Security;
using Cosmix.Application.Finance.Reconciliation;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.Finance;
using Cosmix.Domain.HR.PayrollAccounting;
using Xunit;

namespace Cosmix.Tests;

public sealed class BankReconciliationWorkflowTests
{
    [Fact]
    public async Task Import_retry_is_stable_and_month_close_requires_another_employee()
    {
        var f = new Fixture(); var key = Guid.NewGuid().ToString();
        var imported = await f.Import(key);
        Assert.Equal(imported.Id, (await f.Import(key)).Id); Assert.Single(f.Statements);
        var view = await f.Read(imported.Id); var proposal = await f.Propose(view);
        Assert.Equal(403, (await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Close(imported.Id, proposal))).StatusCode);
        f.Actor = Guid.NewGuid();
        Assert.Equal(403, (await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Close(imported.Id, proposal))).StatusCode);
        f.Employee = Guid.NewGuid();
        Assert.True((await f.Close(imported.Id, proposal)).Closed);
        var final = await f.Read(imported.Id);
        Assert.True(final.Matching.CanClose); Assert.True(final.Statement.Closed); Assert.NotNull(final.ClosedBy);
        Assert.Equal(409, (await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Propose(final))).StatusCode);
        Assert.Equal(409, (await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Import(previous: imported.Id))).StatusCode);
    }
    [Fact]
    public async Task New_ledger_posting_or_unmatched_statement_blocks_closing_and_retains_evidence()
    {
        var f = new Fixture(); var imported = await f.Import(); var view = await f.Read(imported.Id);
        var proposal = await f.Propose(view); f.Actor = Guid.NewGuid(); f.Employee = Guid.NewGuid();
        f.Ledger.Add(new(Guid.NewGuid(), new(2026, 9, 3), "NEW-PAYMENT", -1m, "PKR"));
        Assert.Equal("bank_ledger_changed", (await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Close(imported.Id, proposal))).Message);
        Assert.Empty(f.Closes);
        view = await f.Read(imported.Id); Assert.False(view.Matching.CanClose);
        proposal = await f.Propose(view); f.Actor = Guid.NewGuid(); f.Employee = Guid.NewGuid();
        Assert.Equal("bank_reconciliation_unmatched", (await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Close(imported.Id, proposal))).Message);
        Assert.Equal(2, f.Proposals.Count);
    }
    [Fact]
    public async Task Duplicate_import_and_unauthorized_request_do_not_change_statement_history()
    {
        var f = new Fixture(); f.Allowed = false;
        Assert.Equal(403, (await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Import())).StatusCode);
        Assert.Empty(f.Statements); f.Allowed = true;
        var imported = await f.Import();
        Assert.Equal("bank_statement_duplicate_import", (await Assert.ThrowsAsync<AuthenticationFailure>(() => f.Import(previous: imported.Id))).Message);
        Assert.Single(f.Statements);
    }

    private sealed class Fixture
    {
        public Guid Actor = Guid.NewGuid(), Employee = Guid.NewGuid(); public bool Allowed = true;
        public List<BankStatementSnapshot> Statements = []; public List<BankReconciliationProposal> Proposals = []; public List<BankReconciliationClose> Closes = [];
        public List<BankLedgerMovement> Ledger = [new(Guid.NewGuid(), new(2026, 9, 2), "PAY-1", -100m, "PKR")];
        private readonly List<IdempotencyReceipt> receipts = [];
        private readonly BankReconciliationService service;
        private readonly Guid session = Guid.NewGuid();
        private static readonly AuthRequestContext Context = new("127.0.0.1", "synthetic-bank-review");
        public Fixture()
        {
            var clock = new FixedClock();
            var grants = new[] { BankReconciliationService.Read, BankReconciliationService.Manage, BankReconciliationService.Close }
                .Select(p => new PermissionGrant(p, true, Guid.NewGuid(), AccessScopeKind.All, null)
                { AuthorityTimingKnown = true, ValidFromUtc = clock.GetUtcNow().AddDays(-1) }).ToArray();
            var current = Proxy<ICurrentAuthentication>((_, _) => new AuthenticatedAccount(Actor, Employee, session, 1, SessionClient.Web, true));
            var access = Proxy<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Allowed ? grants : []));
            var authorization = Proxy<IAccessManagementRepository>((method, args) => method.Name switch {
                "InTransactionAsync" => typeof(Fixture).GetMethod(nameof(Transaction), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(method.GetGenericArguments()).Invoke(null, args),
                "IsCurrentAdministratorAsync" => Task.FromResult(true), "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SyntheticFinance"),
                _ => throw new InvalidOperationException(method.Name)
            });
            var journal = Proxy<ISchedulingJournalRepository>((method, args) => method.Name switch {
                "IsCurrentSessionAsync" => Task.FromResult(true),
                "FindReceiptAsync" => Task.FromResult(receipts.SingleOrDefault(x => x.Key == (string)args[2]!)),
                "AddReceipt" => Add(receipts, (IdempotencyReceipt)args[0]!), "AddAudit" => null, "SaveChangesAsync" => Task.CompletedTask,
                _ => throw new InvalidOperationException(method.Name)
            });
            var repository = Proxy<IBankReconciliationRepository>((method, args) => method.Name switch {
                "LatestAsync" => Task.FromResult(Statements.OrderByDescending(x => x.Revision).FirstOrDefault()),
                "StatementAsync" => Task.FromResult(Statements.SingleOrDefault(x => x.Id == (Guid)args[0]!)),
                "ClosedAsync" => Task.FromResult(Closes.SingleOrDefault()),
                "HashExistsAsync" => Task.FromResult(Statements.Any(x => x.FileSha256 == (string)args[3]!)),
                "LatestProposalAsync" => Task.FromResult(Proposals.Where(x => x.StatementId == (Guid)args[0]!).OrderByDescending(x => x.Revision).FirstOrDefault()),
                "LedgerAsync" => Task.FromResult<IReadOnlyList<BankLedgerMovement>>(Ledger.ToArray()),
                "Add" => args[0] switch { BankStatementSnapshot row => Add(Statements, row), BankReconciliationProposal row => Add(Proposals, row), BankReconciliationClose row => Add(Closes, row), _ => throw new InvalidOperationException() },
                "SaveChangesAsync" => Task.CompletedTask, _ => throw new InvalidOperationException(method.Name)
            });
            var finance = Proxy<IPayrollFinanceRepository>((_, _) => Task.FromResult<PayrollLedgerAccount?>(new("BANK-TEST", "Synthetic bank", PayrollAccountKind.Bank)));
            var files = new PayrollSalaryFrozenEvidenceTests.MemoryFiles();
            var operation = new SensitiveComplianceOperation(current, access, authorization, journal, clock);
            service = new(operation, new(operation, journal, files, clock), repository, finance, files, clock);
        }
        public Task<BankStatementSummary> Import(string? key = null, Guid? previous = null) => service.ImportAsync(
            new("BANK-TEST", "PKR", new(2026, 9, 1), 500m, 400m, "synthetic-bank.csv",
                Convert.ToBase64String(Encoding.UTF8.GetBytes("Date,Ref,Debit,Credit\n2026-09-02,PAY-1,100,\n")),
                new("CSV", "Date", "Ref", "Debit", "Credit", "yyyy-MM-dd"), previous, "Synthetic reconciliation import"),
            key ?? Guid.NewGuid().ToString(), Context, default);
        public Task<BankReconciliationWorkspace> Read(Guid id) => service.WorkspaceAsync(id, Context, default);
        public Task<BankProposalResponse> Propose(BankReconciliationWorkspace view) => service.ProposeAsync(view.Statement.Id,
            new(view.Proposal?.Id, view.LedgerFingerprint, [new(1, [Ledger[0].JournalId], null, null)], "Synthetic statement match review"), Guid.NewGuid().ToString(), Context, default);
        public Task<BankStatementSummary> Close(Guid id, BankProposalResponse proposal) => service.CloseAsync(id,
            new(proposal.Id, proposal.LedgerFingerprint, "Independent synthetic bank month review"), Guid.NewGuid().ToString(), Context, default);
        private static object? Add<T>(List<T> list, T row) { list.Add(row); return null; }
        private static Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
    }
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero); }
    private static T Proxy<T>(Func<MethodInfo, object?[], object?> callback) where T : class
    { var value = DispatchProxy.Create<T, Callback>(); ((Callback)(object)value).Run = callback; return value; }
    public class Callback : DispatchProxy { public Func<MethodInfo, object?[], object?> Run { get; set; } = null!; protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Run(targetMethod!, args ?? []); }
}
