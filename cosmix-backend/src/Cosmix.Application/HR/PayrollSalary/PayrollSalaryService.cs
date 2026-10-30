using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.PayrollSalary;

namespace Cosmix.Application.HR.PayrollSalary;

public sealed class PayrollSalaryService(AttendanceOperation operation,IPayrollSalaryRepository repository,IPrivateFileStore files)
{
    public Task<CompanyPayrollReadinessReport> ReadinessAsync(CompanyPayrollReadinessService readiness,
        int year, int month, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(PayrollSalaryPermissions.Read, "payroll.company-readiness.read", null, context,
            (_, token) => readiness.EvaluateAsync(year, month, token), ct, concurrentRead: true);

    public const string ComponentRequestType="payroll.salary-component";
    public const string PolicyRequestType="payroll.salary-policy";
    public Task<PayrollSalaryComponentPage> ComponentsAsync(Guid? employeeId,int page,int pageSize,AuthRequestContext context,CancellationToken ct)
    {
        if(page is <1 or >4096 || pageSize is <1 or >100 || employeeId==Guid.Empty)throw AttendanceOperation.Invalid();
        return operation.ReadAsync(PayrollSalaryPermissions.Read,"payroll.salary-components.read",null,context,async(_,token)=>{
            var all=await repository.ComponentsAsync(employeeId,token); Bound(all.Count,65536);
            return new PayrollSalaryComponentPage(all.OrderByDescending(x=>x.RecordedAt).ThenBy(x=>x.Id).Skip((page-1)*pageSize).Take(pageSize)
                .Select(x=>Map(x,x.ApprovalRequest)).ToArray(),page,pageSize,all.Count>page*pageSize);
        },ct);
    }
    public Task<PayrollSalaryComponentResponse> RequestComponentAsync(PayrollSalaryComponentInput input,string key,AuthRequestContext context,CancellationToken ct)=>
        operation.WriteAsync(PayrollSalaryPermissions.Request,"payroll.salary-component.request",null,key,input,input.Reason,context,
            async(actor,token)=>{var response=await StageComponentAsync(input,actor,null,token);return new AttendanceChange<PayrollSalaryComponentResponse>(response.Id,null,response);},ct);

    public async Task ValidateComponentInTransactionAsync(PayrollSalaryComponentInput input,AttendanceActor actor,CancellationToken ct)
    {
        await operation.RequireAllPermissionAsync(actor,PayrollSalaryPermissions.Request,ct);
        if(input is null || input.EmployeeId==Guid.Empty || string.IsNullOrWhiteSpace(input.Code) || input.Code.Trim().Length>40 ||
            input.Code.Trim().Any(x=>!char.IsAsciiLetterOrDigit(x) && x!='_' && x!='-') ||
            input.Code.Trim().ToUpperInvariant() is "BASE" or "BASIC" or "BASESALARY" ||
            input.Currency!="PKR" || !Enum.IsDefined(input.Kind) || input.Amount<0 || input.Amount>9999999999999999.99m ||
            decimal.Round(input.Amount,2)!=input.Amount || input.EffectiveFrom==default || input.EffectiveToExclusive<=input.EffectiveFrom ||
            string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length>1000 ||
            input.Kind is PayrollSalaryComponentKind.OneTimeEarning or PayrollSalaryComponentKind.OneTimeDeduction &&
                (input.EffectiveToExclusive.HasValue || input.ProrateWithBase))throw AttendanceOperation.Invalid("invalid_salary_component");
        if(!await repository.EmployeeApprovedAsync(input.EmployeeId,ct))throw AttendanceOperation.Conflict("salary_employee_not_approved");
        var history=(await repository.ComponentsAsync(input.EmployeeId,ct)).Where(x=>x.Code==input.Code.Trim().ToUpperInvariant()).ToArray();Bound(history.Length,65536);
        if(history.Any(x=>x.ApprovalRequest.State==ApprovalState.Pending))throw AttendanceOperation.Conflict("salary_component_request_pending");
        var previous=history.Where(x=>x.ApprovalRequest.State==ApprovalState.Approved).MaxBy(x=>x.Revision);
        if(previous?.Id!=input.ExpectedPreviousId || previous is not null &&
            (input.EffectiveFrom<=previous.EffectiveFrom || input.Kind!=previous.Kind))throw AttendanceOperation.Conflict("salary_component_source_changed");
        var until=input.Kind is PayrollSalaryComponentKind.OneTimeEarning or PayrollSalaryComponentKind.OneTimeDeduction?input.EffectiveFrom.AddDays(1):input.EffectiveToExclusive;
        if(await repository.HasPreparedEmployeePeriodAsync(input.EmployeeId,input.EffectiveFrom,until,ct))throw AttendanceOperation.Conflict("salary_period_already_prepared");
    }
    /// <summary>Canonical transaction-local intake shared by manual and imported rows.</summary>
    public async Task<PayrollSalaryComponentResponse> StageComponentAsync(PayrollSalaryComponentInput input,AttendanceActor actor,Guid? sourceImportId,CancellationToken ct)
    {
        await ValidateComponentInTransactionAsync(input,actor,ct);
        var code=input.Code.Trim().ToUpperInvariant();var history=await repository.ComponentsAsync(input.EmployeeId,ct);
        var revision=history.Where(x=>x.Code==code).Select(x=>x.Revision).DefaultIfEmpty().Max()+1;
        var id=Guid.NewGuid();var normalized=input with {Code=code,Reason=input.Reason.Trim()};
        var reference=AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new ComponentProof(1,id,normalized,sourceImportId,actor.Identity.AccountId,actor.Now)),ComponentPurpose(id),ct));
        var request=new ApprovalRequest(ComponentRequestType,id,actor.Identity.AccountId.ToString(),reference,1,actor.Now);
        var row=new PayrollSalaryComponent(id,input.EmployeeId,code,input.Kind,input.Amount,input.Currency,input.EffectiveFrom,input.EffectiveToExclusive,
            input.ProrateWithBase,revision,input.ExpectedPreviousId,sourceImportId,request.Id,actor.Identity.AccountId.ToString(),actor.Now);
        repository.Add(row,request);await repository.SaveChangesAsync(ct);return Map(row,request);
    }
    public Task<PayrollSalaryComponentResponse> ReviewComponentAsync(Guid id,PayrollSalaryReviewRequest request,string key,AuthRequestContext context,CancellationToken ct)=>
        operation.WriteAsync(PayrollSalaryPermissions.Review,"payroll.salary-component.review",null,key,new{id,request},request.Reason,context,
            async(actor,token)=>{var row=await repository.ComponentAsync(id,true,token)??throw AttendanceOperation.Missing("salary_component_not_found");
                repository.ExpectVersion(row.ApprovalRequest,SchedulingOperation.Version(request.ExpectedVersion));
                var response=await DecideComponentInTransactionAsync(id,request.Decision,request.Reason,actor,token);
                return new AttendanceChange<PayrollSalaryComponentResponse>(id,null,response);},ct);
    public async Task<PayrollSalaryComponentResponse> DecideComponentInTransactionAsync(Guid id,ApprovalState decision,string reason,AttendanceActor actor,CancellationToken ct)
    {
        await operation.RequireAllPermissionAsync(actor,PayrollSalaryPermissions.Review,ct);
        var row=await repository.ComponentAsync(id,true,ct)??throw AttendanceOperation.Missing("salary_component_not_found");
        var request=row.ApprovalRequest;ValidateDecision(request,decision,actor,row.EmployeeId);
        await ComponentProofAsync(row,ct);
        if(decision==ApprovalState.Approved)
        {
            if(!await repository.EmployeeApprovedAsync(row.EmployeeId,ct))throw AttendanceOperation.Conflict("salary_employee_not_approved");
            var previous=(await repository.ComponentsAsync(row.EmployeeId,ct)).Where(x=>x.Code==row.Code && x.Id!=id && x.ApprovalRequest.State==ApprovalState.Approved).MaxBy(x=>x.Revision);
            if(previous?.Id!=row.SupersedesId || previous is not null && previous.EffectiveFrom>=row.EffectiveFrom)throw AttendanceOperation.Conflict("salary_component_source_changed");
            if(await repository.HasPreparedEmployeePeriodAsync(row.EmployeeId,row.EffectiveFrom,row.Kind is PayrollSalaryComponentKind.OneTimeEarning or PayrollSalaryComponentKind.OneTimeDeduction?row.EffectiveFrom.AddDays(1):row.EffectiveToExclusive,ct))
                throw AttendanceOperation.Conflict("salary_period_already_prepared");
        }
        var proof=await DecisionProofAsync(id,request,decision,reason,actor,ct);
        repository.Add(request.Decide(decision,actor.Identity.AccountId.ToString(),actor.RoleCode,reason.Trim(),actor.Now,proof));
        await repository.SaveChangesAsync(ct);return Map(row,request);
    }
    public Task<IReadOnlyList<PayrollSalaryPolicyResponse>> PoliciesAsync(AuthRequestContext context,CancellationToken ct)=>
        operation.ReadAsync<IReadOnlyList<PayrollSalaryPolicyResponse>>(PayrollSalaryPermissions.Read,"payroll.salary-policy.read",null,context,
            async(_,token)=>{var rows=await repository.PoliciesAsync(token);Bound(rows.Count,4096);return rows.OrderByDescending(x=>x.Revision).Select(Map).ToArray();},ct);
    public Task<PayrollSalaryPolicyResponse> RequestPolicyAsync(PayrollSalaryPolicyInput input,string key,AuthRequestContext context,CancellationToken ct)=>
        operation.WriteAsync(PayrollSalaryPermissions.Request,"payroll.salary-policy.request",null,key,input,input.Reason,context,async(actor,token)=>{
            try{input.Rules.Validate();}catch(ArgumentException){throw AttendanceOperation.Invalid("invalid_salary_policy");}
            if(input.EffectiveFrom==default || input.EffectiveFrom.Day!=1)throw AttendanceOperation.Invalid("salary_policy_month_boundary_required");
            var rows=await repository.PoliciesAsync(token);Bound(rows.Count,4096);
            var previous=rows.Where(x=>x.ApprovalRequest.State==ApprovalState.Approved).MaxBy(x=>x.Revision);
            if(rows.Any(x=>x.ApprovalRequest.State==ApprovalState.Pending) || previous?.Id!=input.ExpectedPreviousId || previous is not null && input.EffectiveFrom<=previous.EffectiveFrom)
                throw AttendanceOperation.Conflict("salary_policy_source_changed");
            if(await repository.HasPreparedPeriodAsync(input.EffectiveFrom,null,token))throw AttendanceOperation.Conflict("salary_period_already_prepared");
            var id=Guid.NewGuid();var reference=AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new PolicyProof(1,id,input,actor.Identity.AccountId,actor.Now)),PolicyPurpose(id),token));
            var approval=new ApprovalRequest(PolicyRequestType,id,actor.Identity.AccountId.ToString(),reference,1,actor.Now);
            var row=new PayrollSalaryPolicy(id,rows.Select(x=>x.Revision).DefaultIfEmpty().Max()+1,input.EffectiveFrom,input.Rules,input.ExpectedPreviousId,approval.Id,actor.Identity.AccountId.ToString(),actor.Now);
            repository.Add(row,approval);await repository.SaveChangesAsync(token);
            return new AttendanceChange<PayrollSalaryPolicyResponse>(id,null,new(id,row.Revision,row.EffectiveFrom,input.Rules,approval.Id,approval.State.ToString(),Convert.ToBase64String(approval.RowVersion)));
        },ct);
    public Task<PayrollSalaryPolicyResponse> ReviewPolicyAsync(Guid id,PayrollSalaryReviewRequest input,string key,AuthRequestContext context,CancellationToken ct)=>
        operation.WriteAsync(PayrollSalaryPermissions.Review,"payroll.salary-policy.review",null,key,new{id,input},input.Reason,context,async(actor,token)=>{
            var row=await repository.PolicyAsync(id,true,token)??throw AttendanceOperation.Missing("salary_policy_not_found");
            var approval=row.ApprovalRequest;repository.ExpectVersion(approval,SchedulingOperation.Version(input.ExpectedVersion));ValidateDecision(approval,input.Decision,actor,null);
            await PolicyProofAsync(row,token);
            if(input.Decision==ApprovalState.Approved){var rows=await repository.PoliciesAsync(token);var previous=rows.Where(x=>x.Id!=id && x.ApprovalRequest.State==ApprovalState.Approved).MaxBy(x=>x.Revision);
                if(previous?.Id!=row.SupersedesId || previous is not null && row.EffectiveFrom<=previous.EffectiveFrom)throw AttendanceOperation.Conflict("salary_policy_source_changed");
                if(await repository.HasPreparedPeriodAsync(row.EffectiveFrom,null,token))throw AttendanceOperation.Conflict("salary_period_already_prepared");}
            var proof=await DecisionProofAsync(id,approval,input.Decision,input.Reason,actor,token);
            repository.Add(approval.Decide(input.Decision,actor.Identity.AccountId.ToString(),actor.RoleCode,input.Reason.Trim(),actor.Now,proof));await repository.SaveChangesAsync(token);
            return new AttendanceChange<PayrollSalaryPolicyResponse>(id,null,Map(row));
        },ct);
    internal async Task ValidateApprovedAsync(PayrollSalaryComponent row,CancellationToken ct){await ComponentProofAsync(row,ct);await ApprovedProofAsync(row.Id,row.ApprovalRequest,ct);}
    internal async Task<PayrollSalaryPolicyRules> ValidateApprovedAsync(PayrollSalaryPolicy row,CancellationToken ct){await PolicyProofAsync(row,ct);await ApprovedProofAsync(row.Id,row.ApprovalRequest,ct);return Rules(row);}
    private async Task ComponentProofAsync(PayrollSalaryComponent row,CancellationToken ct)
    {
        var proof=await operation.ReadPrivateAsync<ComponentProof>(row.ApprovalRequest.SnapshotPrivateReference,ComponentPurpose(row.Id),ct);var x=proof.Input;
        if(proof.SchemaVersion!=1 || proof.Id!=row.Id || proof.Actor.ToString()!=row.RecordedBy || proof.At!=row.RecordedAt || proof.SourceImportId!=row.SourceImportId ||
            x.EmployeeId!=row.EmployeeId || x.Code!=row.Code || x.Kind!=row.Kind || x.Amount!=row.Amount || x.Currency!=row.Currency || x.EffectiveFrom!=row.EffectiveFrom ||
            x.EffectiveToExclusive!=row.EffectiveToExclusive || x.ProrateWithBase!=row.ProrateWithBase || x.ExpectedPreviousId!=row.SupersedesId ||
            row.ApprovalRequest.SubjectId!=row.Id || row.ApprovalRequest.RequestType!=ComponentRequestType || row.ApprovalRequest.RequesterUserId!=row.RecordedBy)
            throw AttendanceOperation.Conflict("salary_component_evidence_invalid");
    }
    private async Task PolicyProofAsync(PayrollSalaryPolicy row,CancellationToken ct)
    {
        var proof=await operation.ReadPrivateAsync<PolicyProof>(row.ApprovalRequest.SnapshotPrivateReference,PolicyPurpose(row.Id),ct);
        if(proof.SchemaVersion!=1 || proof.Id!=row.Id || proof.Actor.ToString()!=row.RecordedBy || proof.At!=row.RecordedAt || proof.Input.EffectiveFrom!=row.EffectiveFrom ||
            JsonSerializer.Serialize(proof.Input.Rules)!=row.RulesJson || proof.Input.ExpectedPreviousId!=row.SupersedesId || row.ApprovalRequest.SubjectId!=row.Id ||
            row.ApprovalRequest.RequestType!=PolicyRequestType || row.ApprovalRequest.RequesterUserId!=row.RecordedBy)throw AttendanceOperation.Conflict("salary_policy_evidence_invalid");
    }
    private async Task ApprovedProofAsync(Guid id,ApprovalRequest request,CancellationToken ct)
    {
        var decisions=await repository.DecisionsAsync(request.Id,ct);var decision=decisions.SingleOrDefault();
        if(request.State!=ApprovalState.Approved || decision is null || decision.Decision!=ApprovalState.Approved || decision.DecidedAt!=request.ClosedAt ||
            decision.ActorUserId==request.RequesterUserId || string.IsNullOrWhiteSpace(decision.ProtectedChangeReference))throw AttendanceOperation.Conflict("salary_approval_evidence_invalid");
        var proof=await operation.ReadPrivateAsync<DecisionProof>(decision.ProtectedChangeReference,DecisionPurpose(id),ct);
        if(proof.SubjectId!=id || proof.RequestId!=request.Id || proof.State!=ApprovalState.Approved || proof.Actor.ToString()!=decision.ActorUserId ||
            proof.At!=decision.DecidedAt || proof.SourceReference!=request.SnapshotPrivateReference)throw AttendanceOperation.Conflict("salary_approval_evidence_invalid");
    }
    private async Task<string> DecisionProofAsync(Guid id,ApprovalRequest request,ApprovalState state,string reason,AttendanceActor actor,CancellationToken ct)=>
        AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new DecisionProof(id,request.Id,state,actor.Identity.AccountId,actor.Now,request.SnapshotPrivateReference,reason.Trim())),DecisionPurpose(id),ct));
    private static void ValidateDecision(ApprovalRequest request,ApprovalState decision,AttendanceActor actor,Guid? employeeId)
    {
        if(request.State!=ApprovalState.Pending || decision is not (ApprovalState.Approved or ApprovalState.Rejected))throw AttendanceOperation.Conflict("salary_review_state_invalid");
        if(request.RequesterUserId==actor.Identity.AccountId.ToString() || employeeId==actor.Identity.EmployeeId)throw AttendanceOperation.Denied();
    }
    internal static PayrollSalaryPolicyRules Rules(PayrollSalaryPolicy row){var rules=JsonSerializer.Deserialize<PayrollSalaryPolicyRules>(row.RulesJson)??throw AttendanceOperation.Conflict("salary_policy_invalid");rules.Validate();return rules;}
    private static PayrollSalaryComponentResponse Map(PayrollSalaryComponent x,ApprovalRequest a)=>new(x.Id,x.EmployeeId,x.Code,x.Kind,x.Amount,x.Currency,x.EffectiveFrom,x.EffectiveToExclusive,x.ProrateWithBase,x.Revision,x.SupersedesId,x.SourceImportId,a.Id,a.State.ToString(),Convert.ToBase64String(a.RowVersion));
    private static PayrollSalaryPolicyResponse Map(PayrollSalaryPolicy x)=>new(x.Id,x.Revision,x.EffectiveFrom,Rules(x),x.ApprovalRequestId,x.ApprovalRequest.State.ToString(),Convert.ToBase64String(x.ApprovalRequest.RowVersion));
    private static void Bound(int count,int maximum){if(count>maximum)throw AttendanceOperation.Conflict("salary_source_limit");}
    internal static string ComponentPurpose(Guid id)=>$"payroll:salary-component:{id:N}";
    internal static string PolicyPurpose(Guid id)=>$"payroll:salary-policy:{id:N}";
    private static string DecisionPurpose(Guid id)=>$"payroll:salary-decision:{id:N}";
    private sealed record ComponentProof(int SchemaVersion,Guid Id,PayrollSalaryComponentInput Input,Guid? SourceImportId,Guid Actor,DateTimeOffset At);
    private sealed record PolicyProof(int SchemaVersion,Guid Id,PayrollSalaryPolicyInput Input,Guid Actor,DateTimeOffset At);
    private sealed record DecisionProof(Guid SubjectId,Guid RequestId,ApprovalState State,Guid Actor,DateTimeOffset At,string SourceReference,string Reason);
}
