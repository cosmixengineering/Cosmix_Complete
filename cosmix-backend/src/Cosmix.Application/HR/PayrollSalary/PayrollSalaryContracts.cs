using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.PayrollSalary;

namespace Cosmix.Application.HR.PayrollSalary;

public static class PayrollSalaryPermissions
{
    public const string Read = "attendance.payroll-salary.read";
    public const string Request = "attendance.payroll-salary.request";
    public const string Review = "attendance.payroll-salary.review";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Read,"Read salary component and calculation policy history",true,true,false),
        new(Request,"Request salary component and calculation policy changes",true,true,false),
        new(Review,"Independently approve salary component and calculation policy changes",true,true,false)];
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PayrollSalaryComponentInput(Guid EmployeeId,[Required,StringLength(40)] string Code,
    PayrollSalaryComponentKind Kind,decimal Amount,[Required,StringLength(3)] string Currency,DateOnly EffectiveFrom,
    DateOnly? EffectiveToExclusive,bool ProrateWithBase,Guid? ExpectedPreviousId,[Required,StringLength(1000)] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PayrollSalaryPolicyInput(DateOnly EffectiveFrom,PayrollSalaryPolicyRules Rules,
    Guid? ExpectedPreviousId,[Required,StringLength(1000)] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PayrollSalaryReviewRequest([Required,StringLength(12,MinimumLength=12)] string ExpectedVersion,
    ApprovalState Decision,[Required,StringLength(1000)] string Reason);
public sealed record PayrollSalaryComponentResponse(Guid Id,Guid EmployeeId,string Code,PayrollSalaryComponentKind Kind,
    decimal Amount,string Currency,DateOnly EffectiveFrom,DateOnly? EffectiveToExclusive,bool ProrateWithBase,int Revision,
    Guid? SupersedesId,Guid? SourceImportId,Guid ApprovalRequestId,string State,string Version);
public sealed record PayrollSalaryComponentPage(IReadOnlyList<PayrollSalaryComponentResponse> Items,int Page,int PageSize,bool HasMore);
public sealed record PayrollSalaryPolicyResponse(Guid Id,int Revision,DateOnly EffectiveFrom,PayrollSalaryPolicyRules Rules,
    Guid ApprovalRequestId,string State,string Version);
public sealed record PayrollSalaryEmployeeTotal(Guid EmployeeId,decimal GrossEarnings,decimal TotalDeductions,decimal NetPay);
public sealed record PayrollSalaryEmployeeBasis(Guid EmployeeId,decimal BasePay,decimal GrossEarnings,decimal ExistingDeductions,string Currency);
public sealed record PayrollSalaryAdditionalLine(PayrollHandoffPreviewLine Line,string EvidenceReference);
public sealed record PayrollStatutoryProjection(string Fingerprint,IReadOnlyList<PayrollSalaryAdditionalLine> Lines);
public interface IPayrollStatutorySourceReader
{
    Task<PayrollStatutoryProjection> ProjectAsync(DateOnly periodStart,DateOnly periodEndExclusive,
        IReadOnlyList<PayrollSalaryEmployeeBasis> employees,CancellationToken ct);
}
public sealed record PayrollFrozenBatch(Guid BatchId,string CalculationVersion,DateOnly PeriodStart,DateOnly PeriodEndExclusive,
    string Currency,string Fingerprint,IReadOnlyList<PayrollHandoffLineResponse> Lines,
    IReadOnlyList<PayrollSalaryEmployeeTotal> Employees,string PrivateEvidenceReference);
public interface IPayrollFrozenBatchReader { Task<PayrollFrozenBatch> ReadAsync(Guid batchId,CancellationToken ct); }
public interface IPayrollSalaryRepository
{
    Task<bool> EmployeeApprovedAsync(Guid employeeId,CancellationToken ct);
    Task<IReadOnlyList<PayrollSalaryComponent>> ComponentsAsync(Guid? employeeId,CancellationToken ct);
    Task<PayrollSalaryComponent?> ComponentAsync(Guid id,bool tracked,CancellationToken ct);
    Task<IReadOnlyList<PayrollSalaryPolicy>> PoliciesAsync(CancellationToken ct);
    Task<PayrollSalaryPolicy?> PolicyAsync(Guid id,bool tracked,CancellationToken ct);
    Task<IReadOnlyList<ApprovalDecision>> DecisionsAsync(Guid approvalRequestId,CancellationToken ct);
    Task<bool> HasPreparedPeriodAsync(DateOnly from,DateOnly? until,CancellationToken ct);
    void Add(PayrollSalaryComponent row,ApprovalRequest approval);
    void Add(PayrollSalaryPolicy row,ApprovalRequest approval);
    void Add(ApprovalDecision decision);
    void ExpectVersion(ApprovalRequest row,byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}
