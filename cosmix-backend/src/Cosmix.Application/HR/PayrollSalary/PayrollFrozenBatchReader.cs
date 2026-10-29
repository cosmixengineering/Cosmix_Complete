using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.PayrollHandoff;

namespace Cosmix.Application.HR.PayrollSalary;

public sealed record PayrollFrozenEvidence(int SchemaVersion,Guid BatchId,string CalculationVersion,DateOnly PeriodStart,
    DateOnly PeriodEndExclusive,string Currency,string Fingerprint,IReadOnlyList<PayrollHandoffLineResponse> Lines,
    IReadOnlyList<PayrollSalaryEmployeeTotal> Employees,IReadOnlyDictionary<Guid,string> LineReferences,string PreparedByUserId,DateTimeOffset PreparedAt);
public sealed record PayrollFrozenLineEvidence(int SchemaVersion,Guid BatchId,Guid EmployeeId,string Component,string SourceType,
    Guid SourceId,int SourceVersion,Guid AttendancePeriodLockEventId,decimal Amount,string Currency,Guid? PolicyVersionId,
    Guid? ApprovalRequestId,string SourceEvidenceReference,Guid ActorAccountId,DateTimeOffset PreparedAtUtc,object? Calculation=null);

/// <summary>Reads the single frozen salary authority inside the caller's authorized transaction.</summary>
public sealed class PayrollFrozenBatchReader(IPayrollHandoffRepository repository,IPrivateFileStore files):IPayrollFrozenBatchReader
{
    public async Task<PayrollFrozenBatch> ReadAsync(Guid batchId,CancellationToken ct)
    {
        var batch=await repository.FindBatchAsync(batchId,false,ct)??throw AttendanceOperation.Missing("payroll_handoff_batch_not_found");
        return await ValidateAsync(batch,repository,files,true,ct);
    }
    public static async Task<PayrollFrozenBatch> ValidateAsync(PayrollHandoffBatch batch,IPayrollHandoffRepository repository,
        IPrivateFileStore files,bool requireApproved,CancellationToken ct)
    {
        if(batch.CalculationVersion!="salary-v2" || batch.CalculationPrivateReference is null || batch.FrozenFingerprint is null ||
            batch.ReviewState is "cancelled" or "rejected" or "returned" || requireApproved && (batch.LockedAt is null || batch.HrApprovedAt is null ||
                batch.ApprovalRequestId is null || batch.HrApprovalRequestId is null || batch.ReviewState is not ("locked" or "finance_handed_off")))
            throw AttendanceOperation.Conflict("complete_approved_salary_required");
        var proof=await ReadAsync<PayrollFrozenEvidence>(files,batch.CalculationPrivateReference,Purpose(batch.Id),ct);
        var lines=Lines(batch);var totals=Totals(lines);
        if (batch.Lines.Any(x => x.PayrollHandoffBatchId != batch.Id || x.Currency != batch.Currency) ||
            lines.GroupBy(x => (x.EmployeeId, x.SourceType, x.SourceId, x.SourceVersion, x.Component)).Any(g => g.Count() != 1) ||
            lines.GroupBy(x => x.EmployeeId).Any(g => g.Count(x => x.Component == PayrollComponentKind.BaseCompensation.ToString()) != 1) ||
            lines.Any(x => x.Component == PayrollComponentKind.BaseCompensation.ToString() && (x.SourceType != "attendance.payroll-salary.base" || x.Amount < 0)))
            throw AttendanceOperation.Conflict("salary_frozen_evidence_invalid");
        if (proof.Lines is null || proof.Employees is null || proof.LineReferences is null)
            throw AttendanceOperation.Conflict("salary_frozen_evidence_invalid");
        // SQL decimal(18,2) adds a scale to whole amounts (30000 -> 30000.00).
        // Keep the original protected fingerprint authoritative, including for existing
        // schema-2 evidence, and compare every retained line/total by typed value.
        // Rehashing SQL's representation would invalidate an unchanged approved salary.
        var fingerprint=Fingerprint(batch,proof.Lines);
        if(proof.SchemaVersion!=2 || proof.BatchId!=batch.Id || proof.CalculationVersion!=batch.CalculationVersion || proof.PeriodStart!=batch.PeriodStart ||
            proof.PeriodEndExclusive!=batch.PeriodEnd || proof.Currency!=batch.Currency || proof.Fingerprint!=fingerprint || batch.FrozenFingerprint!=fingerprint ||
            proof.PreparedByUserId!=batch.PreparedByUserId || proof.PreparedAt!=batch.PreparedAt ||
            !proof.Lines.SequenceEqual(lines) || !proof.Employees.SequenceEqual(totals) ||
            totals.Count==0 || totals.Any(x=>x.NetPay<0) || totals.Any(x=>!lines.Any(l=>l.EmployeeId==x.EmployeeId && l.Component==PayrollComponentKind.BaseCompensation.ToString())))
            throw AttendanceOperation.Conflict("salary_frozen_evidence_invalid");
        foreach(var row in batch.Lines)
        {
            if(!proof.LineReferences.TryGetValue(row.Id,out var reference) || reference!=row.CalculationPrivateReference)throw AttendanceOperation.Conflict("salary_line_evidence_invalid");
            var line=await ReadAsync<PayrollFrozenLineEvidence>(files,reference,$"hr:payroll-handoff:{batch.Id:N}:line",ct);
            if(line.SchemaVersion is not (1 or 2) || line.BatchId!=batch.Id || line.EmployeeId!=row.EmployeeId || line.Component!=row.Component.ToString() ||
                line.SourceType!=row.SourceType || line.SourceId!=row.SourceId || line.SourceVersion!=row.SourceVersion || line.AttendancePeriodLockEventId!=row.AttendancePeriodLockEventId ||
                line.Amount!=row.Amount || line.Currency!=row.Currency || line.PolicyVersionId!=row.PolicyVersionId || line.ApprovalRequestId!=row.ApprovalRequestId ||
                line.ActorAccountId.ToString()!=batch.PreparedByUserId || line.PreparedAtUtc!=batch.PreparedAt || string.IsNullOrWhiteSpace(line.SourceEvidenceReference))
                throw AttendanceOperation.Conflict("salary_line_evidence_invalid");
        }
        if(requireApproved)
        {
            await ValidateHrApprovalAsync(batch, repository, files, ct);
            await ApprovalAsync(batch,batch.ApprovalRequestId!.Value,PayrollHandoffService.LockRequestType,batch.LockedByUserId!,batch.LockedAt!.Value,repository,files,ct);
            if(batch.HrApprovedByUserId==batch.PreparedByUserId || batch.LockedByUserId==batch.PreparedByUserId || batch.LockedByUserId==batch.HrApprovedByUserId)
                throw AttendanceOperation.Conflict("salary_approval_actor_conflict");
        }
        return new(batch.Id,batch.CalculationVersion,batch.PeriodStart,batch.PeriodEnd,batch.Currency,fingerprint,lines,totals,batch.CalculationPrivateReference);
    }
    public static async Task ValidateHrApprovalAsync(PayrollHandoffBatch batch, IPayrollHandoffRepository repository,
        IPrivateFileStore files, CancellationToken ct)
    {
        if (batch.HrApprovalRequestId is not Guid request || batch.HrApprovedAt is not DateTimeOffset at ||
            string.IsNullOrWhiteSpace(batch.HrApprovedByUserId) || batch.HrApprovedByUserId == batch.PreparedByUserId ||
            at < batch.PreparedAt || batch.LockedAt.HasValue && batch.LockedAt < at)
            throw AttendanceOperation.Conflict("salary_approval_evidence_invalid");
        await ApprovalAsync(batch, request, PayrollHandoffService.HrReviewRequestType, batch.HrApprovedByUserId, at, repository, files, ct);
    }
    private static async Task ApprovalAsync(PayrollHandoffBatch batch,Guid id,string type,string actor,DateTimeOffset at,
        IPayrollHandoffRepository repository,IPrivateFileStore files,CancellationToken ct)
    {
        var approval=await repository.ApprovalAsync(id,ct);var decisions=await repository.DecisionRecordsAsync(id,ct);
        if(approval is null || approval.State!=ApprovalState.Approved || approval.SubjectId!=batch.Id || approval.RequestType!=type ||
            approval.RequesterUserId!=batch.PreparedByUserId || approval.ClosedAt!=at || decisions.Count!=1 || decisions[0].Decision!=ApprovalState.Approved ||
            decisions[0].ActorUserId!=actor || decisions[0].DecidedAt!=at)throw AttendanceOperation.Conflict("salary_approval_evidence_invalid");
        var proof=await ReadAsync<JsonElement>(files,approval.SnapshotPrivateReference,type==PayrollHandoffService.LockRequestType?
            $"hr:payroll-handoff:{batch.Id:N}:lock":$"hr:payroll-handoff:{batch.Id:N}:hr-review",ct);
        if(!proof.TryGetProperty("BatchId",out var subject) || subject.GetGuid()!=batch.Id ||
            !proof.TryGetProperty("Fingerprint",out var digest) || digest.GetString()!=batch.FrozenFingerprint)
            throw AttendanceOperation.Conflict("salary_approval_evidence_invalid");
    }
    private static async Task<T> ReadAsync<T>(IPrivateFileStore files,string reference,string purpose,CancellationToken ct)
    {
        try{var parsed=JsonSerializer.Deserialize<PrivateFileReference>(reference)??throw new JsonException();
            return JsonSerializer.Deserialize<T>(await files.ReadAsync(parsed,purpose,ct))??throw new JsonException();}
        catch(JsonException){throw AttendanceOperation.Conflict("salary_private_evidence_invalid");}
    }
    public static IReadOnlyList<PayrollHandoffLineResponse> Lines(PayrollHandoffBatch batch)=>batch.Lines.OrderBy(x=>x.EmployeeId).ThenBy(x=>x.Component)
        .ThenBy(x=>x.SourceType,StringComparer.Ordinal).ThenBy(x=>x.SourceId).ThenBy(x=>x.SourceVersion)
        .Select(x=>new PayrollHandoffLineResponse(x.Id,x.EmployeeId,x.Component.ToString(),x.SourceType,x.SourceId,x.SourceVersion,x.AttendancePeriodLockEventId,x.Amount,x.Currency,x.PolicyVersionId,x.ApprovalRequestId,x.TraceId)).ToArray();
    public static string Fingerprint(PayrollHandoffBatch batch,IReadOnlyList<PayrollHandoffLineResponse> lines)=>
        MonthLockEvidence.Fingerprint(new{batch.Id,CalculationVersion="salary-v2",batch.PeriodStart,PeriodEndExclusive=batch.PeriodEnd,batch.Currency,Lines=lines});
    public static IReadOnlyList<PayrollSalaryEmployeeTotal> Totals(IEnumerable<PayrollHandoffLineResponse> lines)=>
        lines.GroupBy(x=>x.EmployeeId).OrderBy(x=>x.Key).Select(g=>new PayrollSalaryEmployeeTotal(g.Key,g.Where(x=>x.Amount>0).Sum(x=>x.Amount),-g.Where(x=>x.Amount<0).Sum(x=>x.Amount),g.Sum(x=>x.Amount))).ToArray();
    public static string Purpose(Guid id)=>$"hr:payroll-handoff:{id:N}:salary";
}
