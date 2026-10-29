using System.Reflection;
using System.Text.Json;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Security;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Application.HR.StatutoryContributions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Domain.HR.StatutoryContributions;
using Xunit;

namespace Cosmix.Tests;

public class PermissionsTests
{
    private static readonly AuthRequestContext Context = new("127.0.0.1", "compliance-check");
    [Theory]
    [InlineData("mobile")]
    [InlineData("no-mfa")]
    [InlineData("no-grant")]
    [InlineData("employee-scope")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("unknown-timing")]
    [InlineData("stale-session")]
    [InlineData("stale-mfa")]
    public async Task SensitiveCompliance_AllServiceEntrypointsDenyBeforeRepositoryAccess(string mode)
    {
        var fixture = new Fixture(mode);
        var writes = new PayrollFinanceOperation(fixture.Operation, null!, null!, TimeProvider.System);
        var payroll = new PayrollAccountingService(null!, TimeProvider.System, fixture.Operation, writes: writes);
        var statutory = new StatutoryContributionService(null!, TimeProvider.System, fixture.Operation, writes: writes);
        Func<Task>[] calls = [
            () => payroll.ListObligationsAsync(1, 25, Context, default),
            () => payroll.GetObligationByBatchIdAsync(Guid.NewGuid(), Context, default),
            () => payroll.FinalizeObligationAsync(new(Guid.NewGuid(), 100, 90, "PKR"), Context, default),
            () => payroll.PostAccrualJournalAsync(new(Guid.NewGuid(), "d", "c", "test"), Context, default),
            () => payroll.RecordSalaryPaymentAsync(new(Guid.NewGuid(), Guid.NewGuid(), "ref", "d", "c"), Context, default),
            () => payroll.ConfirmPaymentAsync(Guid.NewGuid(), Context, default),
            () => payroll.GetPaymentsForObligationAsync(Guid.NewGuid(), Context, default),
            () => payroll.GetJournalEntriesForObligationAsync(Guid.NewGuid(), Context, default),
            () => statutory.CreateAsync(new(StatutoryScheme.EOBI, new(2026,10,1),100,500,"PKR","p",null,null),Context,default),
            () => statutory.ApproveAsync(Guid.NewGuid(),Context,default),
            () => statutory.PostAsync(Guid.NewGuid(),Context,default),
            () => statutory.GetAsync(Guid.NewGuid(),Context,default),
            () => statutory.ListAsync(null,null,1,25,Context,default),
            () => statutory.ExportScheduleCsvAsync(Guid.NewGuid(),Context,default),
            () => statutory.AddRegistrationAsync(Guid.NewGuid(),new(StatutoryScheme.EOBI,"r",new(2026,10,1)),Context,default),
            () => statutory.GetRegistrationsAsync(Guid.NewGuid(),Context,default),
            () => payroll.FinalizeObligationAsync(new(Guid.NewGuid(), Reason: "Synthetic denial check"), "synthetic-denied-key", Context, default),
            () => payroll.PostAccrualJournalAsync(new(Guid.NewGuid(), Reason: "Synthetic denial check"), "synthetic-denied-key", Context, default),
            () => payroll.PostStatutoryJournalAsync(new(Guid.NewGuid(), Reason: "Synthetic denial check"), "synthetic-denied-key", Context, default),
            () => payroll.RecordSalaryPaymentAsync(new(Guid.NewGuid(), Guid.NewGuid(), "ref", "d", "c", Reason: "Synthetic denial check"), "synthetic-denied-key", Context, default),
            () => payroll.ConfirmPaymentAsync(Guid.NewGuid(), new ConfirmSalaryPaymentRequest("Synthetic denial check"), "synthetic-denied-key", Context, default),
            () => statutory.CreateAsync(new(StatutoryScheme.EOBI, new(2026,10,1), Reason: "Synthetic denial check"), "synthetic-denied-key", Context, default),
            () => statutory.ApproveAsync(Guid.NewGuid(), new FinancialDecisionRequest("AAAAAAAAAAA=", "Synthetic denial check"), "synthetic-denied-key", Context, default),
            () => statutory.PostAsync(Guid.NewGuid(), new PostContributionRequest(Guid.NewGuid(), Guid.NewGuid(), new(2026,10,1), "AAAAAAAAAAA=", "Synthetic denial check"), "synthetic-denied-key", Context, default),
            () => statutory.GetCoverageAsync(Guid.NewGuid(), Context, default),
            () => statutory.CreateCoverageAsync(Guid.NewGuid(), new StatutoryCoverageRequest(new(2026,10,1), null, [], "synthetic", new string('A',64), "Synthetic denial check"), "synthetic-denied-key", Context, default),
            () => statutory.ApproveCoverageAsync(Guid.NewGuid(), Guid.NewGuid(), new FinancialDecisionRequest("AAAAAAAAAAA=", "Synthetic denial check"), "synthetic-denied-key", Context, default),
            () => statutory.AddRegistrationAsync(Guid.NewGuid(), new(StatutoryScheme.EOBI,"synthetic",new(2026,10,1), "Synthetic denial check"), "synthetic-denied-key", Context, default)
        ];
        foreach(var call in calls)
        {
            var failure = await Assert.ThrowsAsync<AuthenticationFailure>(call);
            Assert.Equal(403, failure.StatusCode);
        }
        Assert.Empty(fixture.Audits);
        Assert.Equal(0, fixture.Saves);
    }

    [Fact]
    public async Task SensitiveCompliance_ReadReturnsDataAndAuditsWithinTransaction()
    {
        var f = new Fixture();
        var repository = Proxy<IPayrollAccountingRepository>((method,args) => {
            Assert.True(f.InTransaction);
            Assert.Equal("ListObligationsAsync",method.Name);
            return Task.FromResult<IReadOnlyList<PayrollObligation>>([]);
        });
        var service = new PayrollAccountingService(repository,TimeProvider.System,f.Operation);
        Assert.Empty(await service.ListObligationsAsync(1,25,Context,default));
        var audit = Assert.Single(f.Audits);
        Assert.Equal("ListObligations",audit.EventType);
        Assert.Equal(f.Actor.AccountId.ToString(),audit.ActorUserId);
        Assert.Equal("ConfiguredFinance",audit.ActorRoleCode);
        Assert.Equal(Context.CorrelationId,audit.CorrelationId);
        Assert.Equal(1,f.Saves);
    }

    [Fact]
    public async Task SensitiveCompliance_WriteDerivesActorAndCommitsAuditWithBusiness()
    {
        var f=new Fixture(); PayrollObligation? added=null;
        Task Add() { Assert.True(f.InTransaction); added=(PayrollObligation)f.LastArguments![0]!; return Task.CompletedTask; }
        var (batch, frozenReader) = await CreateApprovedPayrollAsync([(Guid.NewGuid(), 90m, 0m)], () => Assert.True(f.InTransaction));

        // Capture arguments without exposing any client-supplied actor input.
        var repository=Proxy<IPayrollAccountingRepository>((method,args)=> {
            f.LastArguments=args;
            return method.Name switch {
                "GetBatchByIdAsync"=>(object?)Task.FromResult<PayrollHandoffBatch?>(batch),
                "GetObligationByBatchIdAsync"=>(object?)Task.FromResult<PayrollObligation?>(null),
                "AddObligationAsync"=>(object?)Add(),
                "SaveChangesAsync"=>(object?)Task.CompletedTask,
                _=>throw new InvalidOperationException(method.Name)
            };
        });
        var setup = new PayrollFinanceSetupService(Proxy<IPayrollFinanceRepository>((method, _) => throw new InvalidOperationException($"Finalization must not query posting configuration: {method.Name}")));
        var service=new PayrollAccountingService(repository,TimeProvider.System,f.Operation, setup, frozenReader);
        var result=await service.FinalizeObligationAsync(new(batch.Id,90,90,"PKR", batch.FrozenFingerprint),Context,default);
        Assert.NotNull(added);
        Assert.Equal(f.Actor.AccountId.ToString(),result.FinalizedByUserId);
        Assert.Equal(batch.FrozenFingerprint, result.SourceFingerprint);
        Assert.Equal(90m, result.TotalNetAmount);
        Assert.Single(f.Audits); Assert.Equal(1,f.Saves);
    }

    internal static async Task<(PayrollHandoffBatch Batch, IPayrollFrozenBatchReader Reader)> CreateApprovedPayrollAsync(
        IReadOnlyList<(Guid EmployeeId, decimal Gross, decimal Deduction)> employees, Action? assertSourceTransaction = null)
    {
        var files = new PayrollSalaryFrozenEvidenceTests.MemoryFiles();
        var maker = Guid.NewGuid();
        var batch = new PayrollHandoffBatch("SYNTHETIC-APPROVED", new(2026, 10, 1), new(2026, 11, 1), "PKR", maker.ToString(), DateTimeOffset.UtcNow.AddDays(-2));
        foreach (var employee in employees)
        {
            foreach (var item in new[] { (Kind: PayrollComponentKind.BaseCompensation, Amount: employee.Gross), (Kind: PayrollComponentKind.TaxDeduction, Amount: -employee.Deduction) })
            {
                if (item.Kind != PayrollComponentKind.BaseCompensation && item.Amount == 0) continue;
                var source = Guid.NewGuid(); var locked = Guid.NewGuid();
                var type = item.Kind == PayrollComponentKind.BaseCompensation ? "attendance.payroll-salary.base" : "hr.payroll-salary.component";
                var proof = new PayrollFrozenLineEvidence(2, batch.Id, employee.EmployeeId, item.Kind.ToString(), type,
                    source, 1, locked, item.Amount, "PKR", null, null, "synthetic-source-evidence", maker, batch.PreparedAt);
                var reference = JsonSerializer.Serialize(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(proof), $"hr:payroll-handoff:{batch.Id:N}:line", default));
                batch.Lines.Add(new(batch.Id, employee.EmployeeId, item.Kind, type, source, 1, locked, item.Amount, "PKR", null, null, reference, Guid.NewGuid().ToString()));
            }
        }
        var lines = PayrollFrozenBatchReader.Lines(batch);
        var fingerprint = PayrollFrozenBatchReader.Fingerprint(batch, lines);
        var proofBatch = new PayrollFrozenEvidence(2, batch.Id, "salary-v2", batch.PeriodStart, batch.PeriodEnd, "PKR", fingerprint, lines,
            PayrollFrozenBatchReader.Totals(lines), batch.Lines.ToDictionary(x => x.Id, x => x.CalculationPrivateReference), batch.PreparedByUserId, batch.PreparedAt);
        var referenceBatch = JsonSerializer.Serialize(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(proofBatch), PayrollFrozenBatchReader.Purpose(batch.Id), default));
        batch.SetSalaryEvidence(new string('A', 64), fingerprint, referenceBatch);
        var approvals = new List<ApprovalRequest>(); var decisions = new List<ApprovalDecision>();
        foreach (var stage in new[] { (Type: PayrollHandoffService.HrReviewRequestType, Purpose: "hr-review"), (Type: PayrollHandoffService.LockRequestType, Purpose: "lock") })
        {
            var actor = Guid.NewGuid().ToString(); var at = batch.PreparedAt.AddHours(approvals.Count + 1);
            var reference = JsonSerializer.Serialize(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new { BatchId = batch.Id, Fingerprint = fingerprint }),
                $"hr:payroll-handoff:{batch.Id:N}:{stage.Purpose}", default));
            var approval = new ApprovalRequest(stage.Type, batch.Id, batch.PreparedByUserId, reference, 1, at);
            decisions.Add(approval.Decide(ApprovalState.Approved, actor, "SyntheticIndependentReviewer", "Synthetic approval", at)); approvals.Add(approval);
            if (stage.Purpose == "hr-review") batch.ReviewSalary(approval.Id, actor, at, true); else batch.Lock(approval.Id, actor, at);
        }
        batch.RecordFinanceHandoff(Guid.NewGuid().ToString());
        var repository = Proxy<IPayrollHandoffRepository>((method, args) =>
        {
            assertSourceTransaction?.Invoke();
            return method.Name switch
            {
                "FindBatchAsync" => Task.FromResult<PayrollHandoffBatch?>((Guid)args[0]! == batch.Id ? batch : null),
                "ApprovalAsync" => Task.FromResult(approvals.SingleOrDefault(x => x.Id == (Guid)args[0]!)),
                "DecisionRecordsAsync" => Task.FromResult<IReadOnlyList<ApprovalDecision>>(decisions.Where(x => x.ApprovalRequestId == (Guid)args[0]!).ToArray()),
                _ => throw new InvalidOperationException(method.Name)
            };
        });
        return (batch, new PayrollFrozenBatchReader(repository, files));
    }

    [Fact]
    public async Task SensitiveCompliance_ExportIsAuditedAndRequiresManageRatherThanApprove()
    {
        var f=new Fixture();
        var item=new StatutoryContribution(StatutoryScheme.EOBI,new(2026,10,1),100,500,"PKR","creator",DateTimeOffset.UtcNow,hasEmployeeEntries:true);
        var registration=new EmployeeStatutoryRegistration(Guid.NewGuid(),StatutoryScheme.EOBI,"SYNTHETIC-REG",new(2026,10,1),"maker",DateTimeOffset.UtcNow);
        item.Entries.Add(new(item.Id,registration.EmployeeId,registration.Id,registration.RegistrationNumber,100,500,
            "synthetic-source",new string('A',64),"replaced-by-retained-test-proof"));
        var retained=await StatutorySourceFixture.RetainAsync(item,registration);
        var service=retained.Service(f.Operation);
        var result=await service.ExportScheduleCsvAsync(item.Id,Context,default);
        Assert.NotEmpty(result.Bytes); Assert.Equal("ExportScheduleCsv",Assert.Single(f.Audits).EventType);
        f.Permission="statutory.contributions.approve";
        await Assert.ThrowsAsync<AuthenticationFailure>(()=>service.ExportScheduleCsvAsync(item.Id,Context,default));
    }

    [Theory]
    [InlineData("grant")]
    [InlineData("session")]
    public async Task SensitiveCompliance_RechecksAuthorityAfterActionBeforeAuditCommit(string revoked)
    {
        var f=new Fixture();
        await Assert.ThrowsAsync<AuthenticationFailure>(()=>f.Operation.ExecuteAsync("location.consent.admin","Publish","LocationPolicy","collection",Context,
            (actor,ct)=> { if(revoked=="grant") f.Permission="other"; else f.Session=false; return Task.FromResult(1); },default));
        Assert.Empty(f.Audits); Assert.Equal(0,f.Saves);
    }

    internal sealed class Fixture
    {
        public AuthenticatedAccount Actor {get;}
        public SensitiveComplianceOperation Operation {get;}
        public List<AuditEvent> Audits {get;}=[];
        public int Saves; public bool InTransaction; public bool Session=true;
        public string? Permission; public object?[]? LastArguments;
        public Fixture(string mode="allowed")
        {
            Actor=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,mode=="mobile"?SessionClient.Mobile:SessionClient.Web,mode!="no-mfa");
            Session=mode!="stale-session";
            var current=Proxy<ICurrentAuthentication>((m,a)=>Actor);
            var access=Proxy<IAccountAccessReader>((m,a)=> {
                var now=(DateTimeOffset)a[1]!;
                IReadOnlyList<PermissionGrant> grants=mode=="no-grant"?[]:new[]{"location.consent.admin","payroll.accounting.accrue","payroll.accounting.payment","payroll.accounting.payment.confirm","statutory.contributions.manage","statutory.contributions.approve"}
                    .Where(p=>Permission is null || p==Permission)
                    .Select(p=>new PermissionGrant(p,true,Guid.NewGuid(),mode=="employee-scope"?AccessScopeKind.Employee:AccessScopeKind.All,mode=="employee-scope"?Actor.EmployeeId:null) {
                        AuthorityTimingKnown=mode!="unknown-timing", ValidFromUtc=mode=="future"?now.AddHours(1):now.AddHours(-1),ValidUntilUtc=mode=="expired"?now:now.AddHours(1)
                    }).ToArray();
                return Task.FromResult(grants);
            });
            var authorization=Proxy<IAccessManagementRepository>((m,a)=>m.Name switch {
                "InTransactionAsync"=>Transaction(m,a),
                "IsCurrentAdministratorAsync"=>Task.FromResult(mode!="stale-mfa"),
                "GetAssignmentRoleCodeAsync"=>Task.FromResult<string?>("ConfiguredFinance"),
                _=>throw new InvalidOperationException(m.Name)
            });
            var journal=Proxy<ISchedulingJournalRepository>((m,a)=> {
                Assert.True(InTransaction);
                switch(m.Name) {
                    case "IsCurrentSessionAsync": return Task.FromResult(Session);
                    case "AddAudit": Audits.Add((AuditEvent)a[0]!); return null;
                    case "SaveChangesAsync": Saves++; return Task.CompletedTask;
                    default: throw new InvalidOperationException(m.Name);
                }
            });
            Operation=new(current,access,authorization,journal,TimeProvider.System);
        }
        private object Transaction(MethodInfo method,object?[] args)
        {
            return typeof(Fixture).GetMethod(nameof(TransactionCore),BindingFlags.Instance|BindingFlags.NonPublic)!
                .MakeGenericMethod(method.GetGenericArguments()).Invoke(this,args)!;
        }
        private async Task<T> TransactionCore<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct)
        { InTransaction=true; try{return await action(ct);} finally{InTransaction=false;} }
    }
    private static T Proxy<T>(Func<MethodInfo,object?[],object?> handler) where T:class
    { var proxy=DispatchProxy.Create<T,TestProxy>(); ((TestProxy)(object)proxy).Handler=handler; return proxy; }
    public class TestProxy:DispatchProxy
    {
        public Func<MethodInfo,object?[],object?> Handler=null!;
        protected override object? Invoke(MethodInfo? targetMethod,object?[]? args)=>Handler(targetMethod!,args??[]);
    }
}
