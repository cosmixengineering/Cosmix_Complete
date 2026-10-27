using System.Reflection;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Security;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.StatutoryContributions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
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
        var payroll = new PayrollAccountingService(null!, TimeProvider.System, fixture.Operation);
        var statutory = new StatutoryContributionService(null!, TimeProvider.System, fixture.Operation);
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
            () => statutory.GetRegistrationsAsync(Guid.NewGuid(),Context,default)
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
        var batch = new PayrollHandoffBatch("BATCH-PERM-TEST", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), "PKR", "hr-preparer", DateTimeOffset.UtcNow.AddDays(-2));
        var batchLines = new List<PayrollHandoffLine>
        {
            new(batch.Id, Guid.NewGuid(), PayrollComponentKind.BaseCompensation, "AttendanceSummary", Guid.NewGuid(), 1, Guid.NewGuid(), 90m, "PKR", null, null, "ref", "trace")
        };
        batch.Lines.Add(batchLines[0]);
        batch.SetSalaryEvidence("prev", "fp-perm-test", "calc-ref-perm");
        batch.ReviewSalary(Guid.NewGuid(), "hr-reviewer", DateTimeOffset.UtcNow.AddDays(-1.5), true);
        batch.Lock(Guid.NewGuid(), "mgmt-approver", DateTimeOffset.UtcNow.AddDays(-1));

        // Capture arguments without exposing any client-supplied actor input.
        var repository=Proxy<IPayrollAccountingRepository>((method,args)=> {
            f.LastArguments=args;
            return method.Name switch {
                "GetBatchByIdAsync"=>(object?)Task.FromResult<PayrollHandoffBatch?>(batch),
                "GetBatchLinesAsync"=>(object?)Task.FromResult<IReadOnlyList<PayrollHandoffLine>>(batchLines),
                "GetObligationByBatchIdAsync"=>(object?)Task.FromResult<PayrollObligation?>(null),
                "GetApprovedStatutoryContributionsForMonthAsync"=>(object?)Task.FromResult<IReadOnlyList<Cosmix.Domain.HR.StatutoryContributions.StatutoryContribution>>([]),
                "AddObligationAsync"=>(object?)Add(),
                "SaveChangesAsync"=>(object?)Task.CompletedTask,
                _=>throw new InvalidOperationException(method.Name)
            };
        });
        var service=new PayrollAccountingService(repository,TimeProvider.System,f.Operation);
        var result=await service.FinalizeObligationAsync(new(batch.Id,90,90,"PKR", "fp-perm-test"),Context,default);
        Assert.NotNull(added);
        Assert.Equal(f.Actor.AccountId.ToString(),result.FinalizedByUserId);
        Assert.Single(f.Audits); Assert.Equal(1,f.Saves);
    }

    [Fact]
    public async Task SensitiveCompliance_ExportIsAuditedAndRequiresManageRatherThanApprove()
    {
        var f=new Fixture();
        var item=new StatutoryContribution(StatutoryScheme.EOBI,new(2026,10,1),100,500,"PKR","creator",DateTimeOffset.UtcNow);
        var repository=Proxy<IStatutoryContributionRepository>((method,args)=>method.Name switch {
            "GetByIdAsync"=>Task.FromResult<StatutoryContribution?>(item),
            "GetAllRegistrationsForSchemeAsync"=>Task.FromResult<IReadOnlyList<EmployeeStatutoryRegistration>>([]),
            _=>throw new InvalidOperationException(method.Name)
        });
        var service=new StatutoryContributionService(repository,TimeProvider.System,f.Operation);
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
