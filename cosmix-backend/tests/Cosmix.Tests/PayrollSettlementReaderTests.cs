using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Application.HR.Payslips;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.Authentication;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Cosmix.Application.Common.Security;
using Cosmix.Domain.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Attendance.Shifts;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollSettlementReaderTests
{
    [Fact]
    public async Task Batch_list_has_bounded_lookahead_and_rejects_invalid_pagination_before_repository()
    {
        var calls=0;var first=new PayrollSettlementBatchSummary(Guid.NewGuid(),"PAY-1",new(2026,9,1),new(2026,10,1),"PKR");
        var second=first with {BatchId=Guid.NewGuid(),BatchReference="PAY-2"};
        var accounting=Stub<IPayrollAccountingRepository>((method,args)=>{
            Assert.Equal(nameof(IPayrollAccountingRepository.ListSettlementBatchesAsync),method);calls++;
            Assert.Equal(2,(int)args![0]!);Assert.Equal(1,(int)args[1]!);
            return Task.FromResult<IReadOnlyList<PayrollSettlementBatchSummary>>([first,second]);});
        var reader=new PayrollSettlementReader(null!,null!,accounting,TimeProvider.System);
        var result=await reader.BatchPageInTransactionAsync(2,1,CancellationToken.None);
        Assert.Equal(first,Assert.Single(result.Items));Assert.True(result.HasMore);Assert.Equal(2,result.Page);
        await Assert.ThrowsAsync<AuthenticationFailure>(()=>reader.BatchPageInTransactionAsync(0,1,CancellationToken.None));
        await Assert.ThrowsAsync<AuthenticationFailure>(()=>reader.BatchPageInTransactionAsync(1,51,CancellationToken.None));
        Assert.Equal(1,calls);
    }

    [Fact]
    public async Task Report_only_actor_can_list_without_accrue_but_cannot_export()
    {
        var actor=new AuthenticatedAccount(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Web,true);
        var grant=new PermissionGrant("payroll.reports.read",true,Guid.NewGuid(),AccessScopeKind.All,null)
            {AuthorityTimingKnown=true,ValidFromUtc=DateTimeOffset.UtcNow.AddDays(-1)};
        var current=Stub<ICurrentAuthentication>((_,_)=>actor);
        var access=Stub<IAccountAccessReader>((_,_)=>Task.FromResult<IReadOnlyList<PermissionGrant>>([grant]));
        var authorization=Stub<IAccessManagementRepository>((method,args)=>method switch{
            "InTransactionAsync"=>((Delegate)args![0]!).DynamicInvoke(args[1]),
            "GetAssignmentRoleCodeAsync"=>Task.FromResult<string?>("ADMIN"),
            "IsCurrentAdministratorAsync"=>Task.FromResult(true),_=>throw new NotSupportedException(method)});
        var journal=Stub<ISchedulingJournalRepository>((method,_)=>method switch{
            "IsCurrentSessionAsync"=>Task.FromResult(true),"AddAudit"=>null,"SaveChangesAsync"=>Task.CompletedTask,
            _=>throw new NotSupportedException(method)});
        var row=new PayrollSettlementBatchSummary(Guid.NewGuid(),"PAY-READ-ONLY",new(2026,9,1),new(2026,10,1),"PKR");
        var accounting=Stub<IPayrollAccountingRepository>((method,_)=>method==nameof(IPayrollAccountingRepository.ListSettlementBatchesAsync)
            ?Task.FromResult<IReadOnlyList<PayrollSettlementBatchSummary>>([row]):throw new NotSupportedException(method));
        var reader=new PayrollSettlementReader(null!,null!,accounting,TimeProvider.System);
        var service=new PayrollSettlementReportService(new(current,access,authorization,journal,TimeProvider.System),reader);
        var page=await service.ListAsync(1,25,new("127.0.0.1","test"),CancellationToken.None);
        Assert.Equal(row,Assert.Single(page.Items));Assert.False(page.HasMore);
        await Assert.ThrowsAsync<AuthenticationFailure>(()=>service.ExportAsync(row.BatchId,"csv",new("127.0.0.1","test"),CancellationToken.None));
    }
    [Fact]
    public async Task Own_history_checks_membership_before_loading_any_frozen_source()
    {
        var frozenCalls=0;
        var frozen=Stub<IPayrollFrozenBatchReader>((_,_)=>{frozenCalls++;throw new InvalidOperationException("Protected source must not be read");});
        var identity=Stub<IPayslipRepository>((method,_)=>method==nameof(IPayslipRepository.ContainsEmployeeAsync)?Task.FromResult(false):throw new NotSupportedException());
        var accounting=Stub<IPayrollAccountingRepository>((_,_)=>throw new InvalidOperationException("No accounting read expected"));
        var reader=new PayrollSettlementReader(frozen,identity,accounting,TimeProvider.System);
        await Assert.ThrowsAsync<AuthenticationFailure>(()=>reader.OwnPeriodInTransactionAsync(Guid.NewGuid(),Guid.NewGuid(),1,20,CancellationToken.None));
        Assert.Equal(0,frozenCalls);
    }

    [Fact]
    public async Task Frozen_reader_keeps_other_employee_totals_out_of_own_history_and_report_has_full_population()
    {
        var own=Guid.NewGuid();var other=Guid.NewGuid();var batchId=Guid.NewGuid();var now=DateTimeOffset.UtcNow;
        PayrollHandoffLineResponse Line(Guid employee,decimal value)=>new(Guid.NewGuid(),employee,"BaseCompensation","attendance.payroll-salary.base",Guid.NewGuid(),1,Guid.NewGuid(),value,"PKR",null,null,"test");
        var batch=new PayrollFrozenBatch(batchId,"salary-v2",new(2026,9,1),new(2026,10,1),"PKR","fingerprint",
            [Line(own,100),Line(other,200)],[new(own,100,0,100),new(other,200,0,200)],"private-evidence");
        var frozen=Stub<IPayrollFrozenBatchReader>((method,_)=>method==nameof(IPayrollFrozenBatchReader.ReadAsync)?Task.FromResult(batch):throw new NotSupportedException());
        var identity=Stub<IPayslipRepository>((method,args)=>method switch{
            nameof(IPayslipRepository.ContainsEmployeeAsync)=>Task.FromResult((Guid)args![1]! ==own),
            nameof(IPayslipRepository.BatchAsync)=>Task.FromResult<PayslipBatchIdentity?>(new(batchId,"PAY-TEST","version",now)),
            nameof(IPayslipRepository.EmployeeAsync)=>Task.FromResult<PayslipEmployeeIdentity?>(new((Guid)args![0]!, (Guid)args[0]! ==own?1:2, (Guid)args[0]! ==own?"Own":"Other")),
            _=>throw new NotSupportedException()});
        var accounting=Stub<IPayrollAccountingRepository>((method,_)=>method==nameof(IPayrollAccountingRepository.GetObligationByBatchIdAsync)
            ?Task.FromResult<PayrollObligation?>(null):throw new InvalidOperationException("No payment reads before obligation"));
        var reader=new PayrollSettlementReader(frozen,identity,accounting,TimeProvider.System);
        var period=await reader.OwnPeriodInTransactionAsync(batchId,own,1,20,CancellationToken.None);
        Assert.Equal(100,period.NetEntitlement);Assert.Equal(100,period.OutstandingAmount);Assert.Empty(period.Payments);
        var serialized=System.Text.Json.JsonSerializer.Serialize(period);
        Assert.DoesNotContain(other.ToString(),serialized);Assert.DoesNotContain("private-evidence",serialized);
        var report=await reader.ReportInTransactionAsync(batchId,CancellationToken.None);
        Assert.Equal(2,report.Employees.Count);Assert.Equal(300,report.NetEntitlement);Assert.Contains("obligation_not_finalized",report.Issues);
        var changed=new PayrollObligation(batchId,300,300,"PKR","maker",now);
        changed.BindSource("DIFFERENT",batch.PeriodStart,batch.PeriodEndExclusive,"[]");changed.Finalize("reviewer",now);
        var changedAccounting=Stub<IPayrollAccountingRepository>((method,_)=>method==nameof(IPayrollAccountingRepository.GetObligationByBatchIdAsync)
            ?Task.FromResult<PayrollObligation?>(changed):throw new InvalidOperationException("Reject source before payments"));
        var changedReader=new PayrollSettlementReader(frozen,identity,changedAccounting,TimeProvider.System);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(()=>changedReader.ReportInTransactionAsync(batchId,CancellationToken.None));
    }

    [Theory]
    [InlineData(SessionClient.Mobile,true)]
    [InlineData(SessionClient.Web,false)]
    public async Task Report_and_export_reject_mobile_or_non_mfa_actor_before_reading(SessionClient client,bool mfa)
    {
        var actor=new AuthenticatedAccount(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,client,mfa);
        var current=Stub<ICurrentAuthentication>((_,_)=>actor);
        var operation=new SensitiveComplianceOperation(current,null!,null!,null!,TimeProvider.System);
        var service=new PayrollSettlementReportService(operation,null!);
        await Assert.ThrowsAsync<AuthenticationFailure>(async()=>await service.ReadAsync(Guid.NewGuid(),new("127.0.0.1","test"),CancellationToken.None));
        await Assert.ThrowsAsync<AuthenticationFailure>(async()=>await service.ExportAsync(Guid.NewGuid(),"csv",new("127.0.0.1","test"),CancellationToken.None));
    }

    [Fact]
    public void Finance_routes_declare_distinct_sensitive_read_and_export_permissions()
    {
        var type=typeof(Cosmix.Api.Controllers.HR.PayrollSettlementController);
        Assert.Contains(type.GetMethod("List")!.GetCustomAttributes<AuthorizeAttribute>(),x=>x.Policy=="payroll.reports.read");
        Assert.Contains(type.GetMethod("Read")!.GetCustomAttributes<AuthorizeAttribute>(),x=>x.Policy=="payroll.reports.read");
        Assert.Contains(type.GetMethod("Export")!.GetCustomAttributes<AuthorizeAttribute>(),x=>x.Policy=="payroll.reports.export");
    }

    private static T Stub<T>(Func<string,object?[]?,object?> handler) where T:class
    {
        var instance=DispatchProxy.Create<T,RepositoryProxy>();((RepositoryProxy)(object)instance).Handler=handler;return instance;
    }
    public class RepositoryProxy:DispatchProxy
    {
        public Func<string,object?[]?,object?> Handler {get;set;}=null!;
        protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!.Name,args);
    }
    [Fact]
    public void A_posted_payment_without_its_ledger_cannot_be_reported_as_valid()
    {
        var obligation = new PayrollObligation(Guid.NewGuid(), 100, 100, "PKR", "maker", DateTimeOffset.UtcNow);
        var payment = new SalaryPayment(obligation.Id, Guid.NewGuid(), "private-bank-ref", 20, "maker", DateTimeOffset.UtcNow, Guid.NewGuid());
        payment.RecordPaymentJournal(Guid.NewGuid());
        Assert.Throws<PayrollFinanceConflictException>(() => PayrollSettlementReader.ValidatePaymentRecords(obligation,
            [payment.EmployeeId!.Value], [payment], []));
    }

    [Fact]
    public void Another_employee_payment_is_rejected()
    {
        var obligation = new PayrollObligation(Guid.NewGuid(), 100, 100, "PKR", "maker", DateTimeOffset.UtcNow);
        var payment = new SalaryPayment(obligation.Id, Guid.NewGuid(), "private", 20, "maker", DateTimeOffset.UtcNow, Guid.NewGuid());
        Assert.Throws<PayrollFinanceConflictException>(() => PayrollSettlementReader.ValidatePaymentRecords(obligation,
            [Guid.NewGuid()], [payment], []));
    }

    [Fact]
    public void Valid_payment_and_exact_independent_reversal_reconcile()
    {
        var (obligation, payment, accrual, original) = Posted();
        PayrollSettlementReader.ValidatePaymentRecords(obligation, [payment.EmployeeId!.Value], [payment], [accrual, original]);
        var now = DateTimeOffset.UtcNow;
        payment.RequestReversal("requester", now, "failed transfer", "private-request", true);
        var reversal = new PayrollJournalEntry(obligation.Id, JournalEntryKind.SalaryPaymentReversal, "BANK", "PAYABLE", 20, "PKR", "reverse", "reviewer", now);
        reversal.BindPosting(new(2026,9,30),Guid.NewGuid(),null,$"PAYMENT-REVERSAL-{payment.Id:N}");
        payment.ApproveReversal("reviewer", now, reversal.Id, "private-approval");
        PayrollSettlementReader.ValidatePaymentRecords(obligation, [payment.EmployeeId!.Value], [payment], [accrual, original, reversal]);
        Assert.Equal(SalaryPaymentState.Failed,payment.State);
    }

    [Fact]
    public void Reversal_with_different_amount_is_rejected()
    {
        var (obligation,payment,accrual,original)=Posted();var now=DateTimeOffset.UtcNow;
        payment.RequestReversal("requester",now,"reverse","private-request",false);
        var reversal=new PayrollJournalEntry(obligation.Id,JournalEntryKind.SalaryPaymentReversal,"BANK","PAYABLE",19,"PKR","reverse","reviewer",now);
        reversal.BindPosting(new(2026,9,30),Guid.NewGuid(),null,$"PAYMENT-REVERSAL-{payment.Id:N}");
        payment.ApproveReversal("reviewer",now,reversal.Id,"private-approval");
        Assert.Throws<PayrollFinanceConflictException>(()=>PayrollSettlementReader.ValidatePaymentRecords(obligation,
            [payment.EmployeeId!.Value],[payment],[accrual,original,reversal]));
    }

    private static (PayrollObligation,SalaryPayment,PayrollJournalEntry,PayrollJournalEntry) Posted()
    {
        var now=DateTimeOffset.UtcNow;var obligation=new PayrollObligation(Guid.NewGuid(),100,100,"PKR","maker",now);
        var accrual=new PayrollJournalEntry(obligation.Id,JournalEntryKind.SalaryAccrual,"SALARY","PAYABLE",100,"PKR","accrual","maker",now);
        accrual.BindPosting(new(2026,9,30),Guid.NewGuid(),null,$"ACCRUAL-SALARY-{obligation.Id:N}");
        var payment=new SalaryPayment(obligation.Id,accrual.Id,"private-bank",20,"maker",now,Guid.NewGuid());
        var original=new PayrollJournalEntry(obligation.Id,JournalEntryKind.SalaryPayment,"PAYABLE","BANK",20,"PKR","payment","maker",now);
        original.BindPosting(new(2026,9,30),Guid.NewGuid(),null,$"PAYMENT-{obligation.Id:N}-{payment.Id:N}");
        payment.RecordPaymentJournal(original.Id);return(obligation,payment,accrual,original);
    }

    [Fact]
    public void Pagination_never_changes_the_total_over_all_payments()
    {
        var now = DateTimeOffset.UtcNow;
        EmployeePaymentItem[] payments = [new(Guid.NewGuid(), 20, "Confirmed", null, now, now, null),
            new(Guid.NewGuid(), 30, "Posted", null, now, null, null)];
        var period = PayrollSettlementReader.BuildEmployeePeriod(Guid.NewGuid(), "PAY-TEST", new(2026,9,1), new(2026,10,1), "PKR", 100, payments, 1, 1);
        Assert.Single(period.Payments); Assert.True(period.HasMorePayments);
        Assert.Equal(20, period.ConfirmedAmount); Assert.Equal(30, period.PostedAmount);
        Assert.Equal(80, period.OutstandingAmount); Assert.Equal(50, period.RemainingToInitiate);
    }

    [Fact]
    public void Csv_neutralizes_formula_names_and_contains_no_private_payment_reference()
    {
        var totals = new PayrollPaymentTotals(0,0,0,0,0,100,100,"Unpaid");
        var report = new PayrollSettlementReport(Guid.NewGuid(),"=BAD",new(2026,9,1),new(2026,10,1),"PKR","fingerprint",DateTimeOffset.UtcNow,
            [new(Guid.NewGuid(), 1, "=HYPERLINK(\"bad\")",100,0,100,totals)], [],100,0,100,0,0,100,0,0,[]);
        var text = System.Text.Encoding.UTF8.GetString(PayrollSettlementReportService.ExportCsv(report));
        Assert.Contains("'=HYPERLINK", text); Assert.Contains("'=BAD", text);
        Assert.DoesNotContain("PaymentReference",text); Assert.DoesNotContain("ProofReference",text);
    }
}
