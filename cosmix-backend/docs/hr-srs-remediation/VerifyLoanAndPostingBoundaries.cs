using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.AdvancesLoans;
using Cosmix.Application.HR.Allowances;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.AdvancesLoans;
using Cosmix.Domain.HR.PayrollHandoff;

// Actual services and shared operation; synthetic IO, never SQL, payment or posting acceptance.
int checks=0;var failures=new List<string>();
void Check(bool value,string label){checks++;if(!value)failures.Add(label);}
async Task Denies(Func<Task> action,string code,string label){
    try{await action();Check(false,label);}catch(AuthenticationFailure e){Check(e.Code==code,label+": "+e.Code);}
}
var p=new FinanceFixture();
var processing=new ProcessPayrollHandoffRequest("SYNTHETIC-HANDOFF-ONLY","Record handoff processing, not posting");
await p.Handoff.ProcessingAsync(p.Batch.Id,processing,"processing-only-01",p.Context,default);
Check(p.Ledger.Count==0,"a processing reference must not change loan balance");
Check(p.Processed.Count==1,"processing remains a usable reviewable record");
await p.Handoff.ProcessingAsync(p.Batch.Id,processing,"processing-only-01",p.Context,default);
Check(p.Processed.Count==1 && p.Ledger.Count==0,"processing replay creates no loan deduction or duplicate receipt");
await Denies(()=>p.Handoff.ProcessingAsync(p.Batch.Id,processing with {ExternalProcessingReference="OTHER"},
    "processing-only-01",p.Context,default),"idempotency_key_reused","changed processing payload conflicts");
p.Grants.RemoveAll(x=>x.Permission==PayrollHandoffPermissions.Process);
await Denies(()=>p.Handoff.ProcessingAsync(p.Batch.Id,processing,"processing-only-01",p.Context,default),
    "access_denied","revoked processing permission blocks replay");

var f=new FinanceFixture();
var interestFree=new FinanceFixture();
var pureTerms=new AdvanceLoanApproveRequest(10000,"PKR",interestFree.Day,"Synthetic interest-free principal only",10,interestFree.Day);
await Denies(()=>interestFree.Loans.ApproveAsync(interestFree.Requests[0].Id,pureTerms with {OtherApprovedMonthlyAmount=100},
    "loan-fee-denied-001",interestFree.Context,default),"interest_free_loan_required","additional loan fees are prohibited");
Check(interestFree.Agreements.Count==0 && interestFree.Requests[0].ApprovalRequest!.State==ApprovalState.Pending,
    "fee rejection leaves the request pending without an agreement");
await Denies(()=>interestFree.Loans.ApproveAsync(interestFree.Requests[0].Id,pureTerms with {OtherApprovedMonthlyAmount=-1},
    "loan-negative-fee-001",interestFree.Context,default),"interest_free_loan_required","negative additional charge is also prohibited");
var pure=await interestFree.Loans.ApproveAsync(interestFree.Requests[0].Id,pureTerms,"loan-pure-principal-01",interestFree.Context,default);
var pureSchedule=interestFree.Agreements.Single().Schedule;
Check(pureSchedule.Count==10 && pureSchedule.All(x=>x.PrincipalAmount==1000 && x.OtherApprovedAmount==0) &&
    pureSchedule.Sum(x=>x.PrincipalAmount)==10000,"10,000 principal produces exactly ten 1,000 installments without charges");
try{new RepaymentScheduleEntry(Guid.NewGuid(),1,interestFree.Day,100,1,"PKR");Check(false,"domain prevents new additional charges");}
catch(ArgumentException){Check(true,"domain prevents new additional charges");}
await interestFree.Loans.RecordRepaymentAsync(pure.Id,new(null,RepaymentEntryKind.ReceivedPayment,10000,"PKR",interestFree.Day,"SYNTHETIC-PAID-EXACT"),
    "loan-exact-payment-01",interestFree.Context,default);
await Denies(()=>interestFree.Loans.RecordRepaymentAsync(pure.Id,new(null,RepaymentEntryKind.ReceivedPayment,1,"PKR",interestFree.Day,"SYNTHETIC-OVERPAY"),
    "loan-overpayment-001",interestFree.Context,default),"repayment_exceeds_outstanding_balance","paid principal cannot be overpaid");
var roundedTerms=pureTerms with {ApprovedPrincipal=100,NumberOfInstallments=3};
await interestFree.Loans.ApproveAsync(interestFree.Requests[1].Id,roundedTerms,"loan-rounded-principal",interestFree.Context,default);
Check(interestFree.Agreements.Last().Schedule.Sum(x=>x.PrincipalAmount)==100 &&
    interestFree.Agreements.Last().Schedule.All(x=>x.OtherApprovedAmount==0),"rounding preserves exact approved principal");
var terms=new AdvanceLoanApproveRequest(1000,"PKR",f.Day,"Synthetic approved loan terms",2,f.Day);
var approved=await f.Loans.ApproveAsync(f.Requests[0].Id,terms,"loan-approve-001",f.Context,default);
Check(approved.ApprovedPrincipal==1000 && f.Decisions.Single().ActorRoleCode==FinanceFixture.Role,
    "approval records actual assigned actor role, not hardcoded HR");
Check((await f.Loans.ApproveAsync(f.Requests[0].Id,terms,"loan-approve-001",f.Context,default)).Id==approved.Id && f.Agreements.Count==1,
    "same loan approval replays exactly one agreement");
await Denies(()=>f.Loans.ApproveAsync(f.Requests[1].Id,terms,"loan-approve-001",f.Context,default),
    "idempotency_key_reused","same approval body/key on another request must conflict");
Check(f.Requests[1].ApprovalRequest!.State==ApprovalState.Pending,"cross-request replay leaves second loan pending");
var second=await f.Loans.ApproveAsync(f.Requests[1].Id,terms,"loan-approve-002",f.Context,default);
var repayment=new RecordRepaymentRequest(null,RepaymentEntryKind.ReceivedPayment,100,"PKR",f.Day,"SYNTHETIC-RECEIVED-100");
var paid=await f.Loans.RecordRepaymentAsync(approved.Id,repayment,"loan-payment-001",f.Context,default);
Check((await f.Loans.RecordRepaymentAsync(approved.Id,repayment,"loan-payment-001",f.Context,default)).Id==paid.Id && f.Ledger.Count==1,
    "same repayment replays one signed ledger entry");
await Denies(()=>f.Loans.RecordRepaymentAsync(second.Id,repayment,"loan-payment-001",f.Context,default),
    "idempotency_key_reused","same repayment body/key cannot replay another agreement");
await Denies(()=>f.Loans.RecordRepaymentAsync(approved.Id,repayment with {Amount=101},"loan-payment-001",f.Context,default),
    "idempotency_key_reused","changed repayment amount conflicts");
var reverse=new ReverseRepaymentRequest(paid.Id,f.Day,"SYNTHETIC-REVERSAL","Correct synthetic receipt");
var reversed=await f.Loans.ReverseRepaymentAsync(approved.Id,reverse,"loan-reverse-001",f.Context,default);
Check(reversed.Amount==-100 && f.Ledger.Sum(x=>x.Amount)==0,"reversal restores balance exactly once");
Check((await f.Loans.ReverseRepaymentAsync(approved.Id,reverse,"loan-reverse-001",f.Context,default)).Id==reversed.Id,
    "same reversal replays one negative entry");
await Denies(()=>f.Loans.ReverseRepaymentAsync(second.Id,reverse,"loan-reverse-001",f.Context,default),
    "idempotency_key_reused","same reversal body/key cannot replay another agreement");
await Denies(()=>f.Loans.ReverseRepaymentAsync(approved.Id,reverse,"loan-reverse-002",f.Context,default),
    "repayment_already_reversed","different key cannot refund an original payment twice");
var before=f.Ledger.Count;
await Denies(()=>f.Loans.RecordRepaymentAsync(approved.Id,repayment with {Kind=RepaymentEntryKind.PayrollDeduction},
    "loan-unproved-payroll",f.Context,default),"payroll_posting_evidence_required","client-selected payroll kind is not posting proof");
Check(f.Ledger.Count==before,"missing posting evidence cannot reduce outstanding balance");
var foreignSchedule=f.Agreements.Single(x=>x.Id==second.Id).Schedule.First().Id;
await Denies(()=>f.Loans.RecordRepaymentAsync(approved.Id,repayment with {RepaymentScheduleEntryId=foreignSchedule},
    "loan-foreign-schedule",f.Context,default),"repayment_schedule_mismatch","foreign schedule cannot attach to another loan");
f.Identity=f.Identity with {MfaSatisfied=false};
await Denies(()=>f.Loans.RecordRepaymentAsync(approved.Id,repayment,"loan-payment-001",f.Context,default),
    "access_denied","non-MFA replay denied");
f.Identity=f.Identity with {MfaSatisfied=true,Client=SessionClient.Mobile};
await Denies(()=>f.Loans.RecordRepaymentAsync(approved.Id,repayment,"loan-payment-001",f.Context,default),
    "access_denied","mobile management repayment denied");
f.Identity=f.Identity with {Client=SessionClient.Web};f.SessionCurrent=false;
await Denies(()=>f.Loans.RecordRepaymentAsync(approved.Id,repayment,"loan-payment-001",f.Context,default),
    "invalid_authentication","revoked session cannot replay repayment");
f.SessionCurrent=true;f.Grants.RemoveAll(x=>x.Permission==AdvanceLoanPermissions.Repay);
await Denies(()=>f.Loans.ReverseRepaymentAsync(approved.Id,reverse,"loan-reverse-001",f.Context,default),
    "access_denied","revoked repayment permission cannot replay reversal");
Check(f.Audits.Any(x=>x.EventType=="hr.advancesloans.repayment.record"),"repayments use protected operation audit");
foreach(var failure in failures)Console.Error.WriteLine("FAIL: "+failure);
Console.WriteLine($"Finance posting/loan identity: {checks-failures.Count}/{checks}; synthetic service IO only, no SQL or payment acceptance.");
return failures.Count==0?0:1;

public class FinanceProxy:DispatchProxy
{
    public Func<MethodInfo,object?[],object?> Handler=null!;
    protected override object? Invoke(MethodInfo? m,object?[]? a)=>Handler(m!,a??[]);
    public static T Make<T>(Func<MethodInfo,object?[],object?> handler)where T:class{
        var proxy=Create<T,FinanceProxy>();((FinanceProxy)(object)proxy).Handler=handler;return proxy;}
}
public sealed class FinanceClock:TimeProvider
{public override DateTimeOffset GetUtcNow()=>new(2026,10,15,10,0,0,TimeSpan.Zero);}
public sealed class FinanceFixture
{
    public const string Role="Explicit-Finance-Reviewer";
    public DateOnly Day=new(2026,9,1);
    public AuthenticatedAccount Identity=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,SessionClient.Web,true);
    public AuthRequestContext Context=new("127.0.0.1","synthetic-finance-boundaries");
    public bool SessionCurrent=true;
    public List<PermissionGrant> Grants=[];
    public List<AdvanceLoanRequest> Requests=[];
    public List<AdvanceLoanAgreement> Agreements=[];
    public List<ApprovalDecision> Decisions=[];
    public List<RepaymentLedgerEntry> Ledger=[];
    public List<PayrollHandoffProcessingRecord> Processed=[];
    public List<IdempotencyReceipt> Receipts=[];
    public List<AuditEvent> Audits=[];
    public PayrollHandoffBatch Batch;
    public AdvanceLoanService Loans;
    public PayrollHandoffService Handoff;
    public FinanceFixture()
    {
        foreach(var definition in AdvanceLoanPermissions.Definitions.Concat(PayrollHandoffPermissions.Definitions))
            Grants.Add(new(definition.Code,definition.Sensitive,Guid.NewGuid(),AccessScopeKind.All,null));
        var now=new FinanceClock().GetUtcNow();
        for(var i=0;i<2;i++){
            var request=new AdvanceLoanRequest(Guid.NewGuid(),AdvanceLoanKind.Loan,1000,"PKR","Synthetic loan",Day,Guid.NewGuid().ToString(),now);
            request.Submit(new ApprovalRequest(AdvanceLoanService.RequestType,request.Id,request.RequestedByUserId,"synthetic-source",1,now));
            Requests.Add(request);
        }
        var files=new Dictionary<string,(string Purpose,byte[] Data)>();
        var store=FinanceProxy.Make<IPrivateFileStore>((m,a)=>{
            if(m.Name=="WriteAsync"){
                var bytes=(byte[])a[0]!;var id=Guid.NewGuid().ToString("N");files[id]=((string)a[1]!,bytes);
                return Task.FromResult(new PrivateFileReference(id,"1",Convert.ToHexString(SHA256.HashData(bytes)),"synthetic",bytes.Length));
            }
            var reference=(PrivateFileReference)a[0]!;var file=files[reference.StorageKey];
            if(file.Purpose!=(string)a[1]! || reference.Sha256!=Convert.ToHexString(SHA256.HashData(file.Data)))throw new Exception("Wrong protected evidence");
            return Task.FromResult(file.Data);
        });
        var access=FinanceProxy.Make<IAccountAccessReader>((_,_)=>Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var authorization=FinanceProxy.Make<IAccessManagementRepository>((m,a)=>m.Name switch{
            "InTransactionAsync"=>((Delegate)a[0]!).DynamicInvoke(a[1]),
            "GetAssignmentRoleCodeAsync"=>Task.FromResult<string?>(Role),_=>throw new Exception(m.Name)});
        var journal=FinanceProxy.Make<ISchedulingJournalRepository>((m,a)=>m.Name switch{
            "IsCurrentSessionAsync"=>Task.FromResult(SessionCurrent),
            "FindReceiptAsync"=>Task.FromResult(Receipts.SingleOrDefault(x=>x.ActorUserId==(string)a[0]!&&x.Operation==(string)a[1]!&&x.Key==(string)a[2]!)),
            "AddReceipt"=>Add(Receipts,(IdempotencyReceipt)a[0]!),"AddAudit"=>Add(Audits,(AuditEvent)a[0]!),
            "SaveChangesAsync"=>Task.CompletedTask,_=>throw new Exception(m.Name)});
        var operation=new AttendanceOperation(FinanceProxy.Make<ICurrentAuthentication>((_,_)=>Identity),access,authorization,journal,
            FinanceProxy.Make<IAuthenticationRepository>((m,_)=>throw new Exception(m.Name)),new FinanceClock(),store);
        var loans=FinanceProxy.Make<IAdvanceLoanRepository>((m,a)=>m.Name switch{
            "FindRequestAsync"=>Task.FromResult(Requests.SingleOrDefault(x=>x.Id==(Guid)a[0]!)),
            "FindAgreementWithDetailsAsync"=>Task.FromResult(Agreements.SingleOrDefault(x=>x.Id==(Guid)a[0]!)),
            "FindLedgerEntryByIdempotencyKeyAsync"=>Task.FromResult(Ledger.FirstOrDefault(x=>x.IdempotencyKey==(string)a[0]!)),
            "FindLedgerEntryAsync"=>Task.FromResult(Ledger.SingleOrDefault(x=>x.Id==(Guid)a[0]!)),
            "GetTotalRepaidAmountAsync"=>Task.FromResult(Ledger.Where(x=>x.AdvanceLoanAgreementId==(Guid)a[0]!).Sum(x=>x.Amount)),
            "ListLedgerEntriesAsync"=>Task.FromResult<IReadOnlyList<RepaymentLedgerEntry>>(Ledger.Where(x=>((IReadOnlyCollection<Guid>)a[0]!).Contains(x.AdvanceLoanAgreementId)).ToArray()),
            "AddAgreement"=>AddAgreement((AdvanceLoanAgreement)a[0]!, (ApprovalDecision)a[1]!, (IEnumerable<RepaymentScheduleEntry>)a[2]!),
            "AddLedgerEntry"=>Add(Ledger,(RepaymentLedgerEntry)a[0]!),"AddDecision"=>Add(Decisions,(ApprovalDecision)a[0]!),
            "SaveChangesAsync"=>Task.CompletedTask,_=>throw new Exception(m.Name)});
        Loans=new(operation,loans);
        Batch=new("SYNTHETIC-BATCH",Day,Day.AddMonths(1),"PKR",Guid.NewGuid().ToString(),now);
        Batch.Lines.Add(new PayrollHandoffLine(Batch.Id,Requests[0].EmployeeId,PayrollComponentKind.LoanRepayment,
            "hr.advance-loan.schedule",Guid.NewGuid(),1,Guid.NewGuid(),-100,"PKR",null,Guid.NewGuid(),"synthetic-private-reference","synthetic-line"));
        Batch.Lock(Guid.NewGuid(),Identity.AccountId.ToString(),now);
        var handoffs=FinanceProxy.Make<IPayrollHandoffRepository>((m,a)=>m.Name switch{
            "FindBatchAsync"=>Task.FromResult<PayrollHandoffBatch?>(Batch),
            "DecidersAsync"=>Task.FromResult<IReadOnlyList<Guid>>([Identity.AccountId]),
            "ProcessingReferenceExistsAsync"=>Task.FromResult(Processed.Any(x=>x.ExternalProcessingReference==(string)a[1]!)),
            "ProcessingAsync"=>Task.FromResult<IReadOnlyList<PayrollHandoffProcessingRecord>>(Processed.ToArray()),
            "FindAgreementIdForScheduleAsync"=>Task.FromResult<Guid?>(Requests[0].Id),
            "Add"=>a[0] is RepaymentLedgerEntry entry?Add(Ledger,entry):Add(Processed,(PayrollHandoffProcessingRecord)a[0]!),
            "SaveChangesAsync"=>Task.CompletedTask,_=>throw new Exception(m.Name)});
        Handoff=new(operation,handoffs,FinanceProxy.Make<IMonthLockRepository>((m,_)=>throw new Exception(m.Name)),
            FinanceProxy.Make<IAttendancePayrollInputLedger>((m,_)=>throw new Exception(m.Name)),
            FinanceProxy.Make<IAllowanceRepository>((m,_)=>throw new Exception(m.Name)),
            FinanceProxy.Make<IAllowanceSettlementStore>((m,_)=>throw new Exception(m.Name)),access,store);
    }
    private object? AddAgreement(AdvanceLoanAgreement agreement,ApprovalDecision decision,IEnumerable<RepaymentScheduleEntry> schedule){
        typeof(AdvanceLoanAgreement).GetProperty(nameof(AdvanceLoanAgreement.AdvanceLoanRequest))!.SetValue(agreement,Requests.Single(x=>x.Id==agreement.AdvanceLoanRequestId));
        foreach(var row in schedule)agreement.Schedule.Add(row);Agreements.Add(agreement);Decisions.Add(decision);return null;}
    private static object? Add<T>(List<T> rows,T row){rows.Add(row);return null;}
}
