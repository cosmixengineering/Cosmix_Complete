using System.Numerics;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Domain.HR.PayrollSalary;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.PayrollSalary;

public sealed record PayrollSalaryProjectedLine(PayrollHandoffPreviewLine Line,string EvidenceReference,object Calculation);
public sealed record PayrollSalaryProjectionResult(IReadOnlyList<PayrollSalaryProjectedLine> Lines,string Fingerprint);

/// <summary>Transaction-local read-only salary projection. Rates and paid time come from the exact attendance revision.</summary>
internal sealed class PayrollSalaryProjection(IPayrollSalaryRepository repository,PayrollSalaryService sources,AttendanceOperation operation)
{
    public async Task<PayrollSalaryProjectionResult> ProjectAsync(AttendancePayrollInputRevision revision,
        AttendancePayrollInputEvidence evidence,AttendanceActor actor,CancellationToken ct)
    {
        await operation.RequireAllPermissionAsync(actor,PayrollSalaryPermissions.Read,ct);
        var month=revision.PeriodStart;var end=month.AddMonths(1);var employee=revision.EmployeeId;
        var policies=await repository.PoliciesAsync(ct);
        if(policies.Count>4096)throw AttendanceOperation.Conflict("salary_source_limit");
        var policy=policies.Where(x=>x.ApprovalRequest.State==ApprovalState.Approved && x.EffectiveFrom<=month).MaxBy(x=>x.EffectiveFrom)
            ??throw AttendanceOperation.Conflict("salary_policy_unavailable");
        var rules=await sources.ValidateApprovedAsync(policy,ct);
        var days=evidence.Calculation.SourceManifest.Days.Where(x=>x.BusinessDate>=month && x.BusinessDate<end).OrderBy(x=>x.BusinessDate).ToArray();
        if(days.Length==0 || days.Any(x=>x.State!="available" || x.Compensation is null || x.Compensation.Currency!="PKR" ||
            x.Compensation.EffectiveFrom>x.BusinessDate || x.Compensation.EffectiveTo<=x.BusinessDate || x.Time?.Status!="calculated" ||
            x.Time.PayableTicks is null or <0 || x.Snapshot?.TimeInput is null))throw AttendanceOperation.Conflict("salary_days_unavailable");
        var inputs=days.Select(x=>{
            var input=x.Snapshot!.TimeInput;var scheduled=checked((long)input.ScheduledMinutes*TimeSpan.TicksPerMinute);
            var capacity=x.Time!.RequiredDutyTicks ?? (scheduled-(input.Rules.PaidTime!.BreakTreatment==AttendanceBreakTreatment.DeductScheduledUnpaidBreak
                ? Math.Min(scheduled,checked((long)input.UnpaidBreakMinutes*TimeSpan.TicksPerMinute)):0));
            return new PayrollSalaryDay(x.BusinessDate,x.Compensation!.Id,x.Compensation.WageBasis,x.Compensation.BaseAmount,x.Compensation.GrossAmount,x.Time.PayableTicks!.Value,capacity);
        }).ToArray();
        PayrollSalaryCalculationResult calculation;
        try{calculation=PayrollSalaryCalculation.Calculate(month,inputs,rules);}catch(ArgumentException){throw AttendanceOperation.Conflict("salary_calculation_unavailable");}
        var all=await repository.ComponentsAsync(employee,ct);
        if(all.Count>65536)throw AttendanceOperation.Conflict("salary_source_limit");
        var approved=all.Where(x=>x.ApprovalRequest.State==ApprovalState.Approved).ToArray();
        var selected=new List<(PayrollSalaryComponent Row,DateOnly[] Dates)>();
        foreach(var row in approved.Where(x=>x.EffectiveFrom<end))
        {
            var oneTime=row.Kind is PayrollSalaryComponentKind.OneTimeEarning or PayrollSalaryComponentKind.OneTimeDeduction;
            if(oneTime && row.EffectiveFrom<month)continue;
            var next=approved.Where(x=>x.Code==row.Code && x.Revision>row.Revision).MinBy(x=>x.EffectiveFrom)?.EffectiveFrom;
            var dates=days.Where(x=>x.BusinessDate>=row.EffectiveFrom && (row.EffectiveToExclusive is null || x.BusinessDate<row.EffectiveToExclusive) &&
                (next is null || x.BusinessDate<next)).Select(x=>x.BusinessDate).ToArray();
            if(oneTime)dates=days.Where(x=>x.BusinessDate==row.EffectiveFrom).Select(x=>x.BusinessDate).ToArray();
            if(dates.Length==0)continue;
            if(row.Currency!="PKR")throw AttendanceOperation.Conflict("salary_currency_conflict");
            await sources.ValidateApprovedAsync(row,ct);selected.Add((row,dates));
        }
        if(rules.GrossTreatment==PayrollGrossTreatment.ReconcileFixedEarnings)
            foreach(var day in days)
            {
                var fixedEarnings=selected.Where(x=>x.Row.Kind==PayrollSalaryComponentKind.FixedEarning && x.Dates.Contains(day.BusinessDate)).Sum(x=>x.Row.Amount);
                // Nonmonthly rates have different units from monthly fixed components; require their explicitly approved gross rate to equal base.
                if(day.Compensation!.WageBasis!=Cosmix.Domain.HR.Compensation.WageBasis.Monthly ?
                    day.Compensation.GrossAmount!=day.Compensation.BaseAmount : fixedEarnings!=day.Compensation.GrossAmount-day.Compensation.BaseAmount)
                    throw AttendanceOperation.Conflict("salary_gross_components_do_not_reconcile");
            }
        var lines=new List<PayrollSalaryProjectedLine>();
        var baseProof=new{Version=PayrollSalaryCalculation.Version,PolicyId=policy.Id,policy.Revision,policy.RulesJson,PolicyReference=policy.ApprovalRequest.SnapshotPrivateReference,
            SourceRevisionId=revision.Id,revision.InputFingerprint,Days=inputs,Calculation=calculation};
        lines.Add(new(new(employee,PayrollComponentKind.BaseCompensation.ToString(),"attendance.payroll-salary.base",revision.Id,revision.Revision,
            calculation.BasePay,"PKR",null,null),revision.ProtectedEvidenceReference,baseProof));
        // Aggregate each recurring code's exact fractions across revisions before rounding once.
        foreach(var group in selected.GroupBy(x=>x.Row.Code).OrderBy(x=>x.Key,StringComparer.Ordinal))
        {
            BigInteger n=0,d=1;var rows=group.OrderBy(x=>x.Row.Revision).ToArray();
            if(rows.Select(x=>x.Row.Kind).Distinct().Count()!=1)throw AttendanceOperation.Conflict("salary_component_kind_conflict");
            foreach(var item in rows)
            {
                var (partN,partD)=PayrollSalaryCalculation.Rational(item.Row.Amount);
                if(item.Row.ProrateWithBase){partN*=item.Dates.Length;partD*=days.Length==DateTime.DaysInMonth(month.Year,month.Month) || rules.Proration==PayrollProration.CalendarDays
                    ? DateTime.DaysInMonth(month.Year,month.Month):rules.FixedDivisorDays!.Value;}
                else if(rows.Length>1 && item.Row.Kind is PayrollSalaryComponentKind.FixedEarning or PayrollSalaryComponentKind.FixedDeduction)
                    throw AttendanceOperation.Conflict("salary_unprorated_component_revision_conflict");
                PayrollSalaryCalculation.Add(ref n,ref d,partN,partD);
            }
            var amount=PayrollSalaryCalculation.Round(n,d,rules.Rounding);var last=rows[^1].Row;
            if(last.Kind is PayrollSalaryComponentKind.FixedDeduction or PayrollSalaryComponentKind.OneTimeDeduction)amount=-amount;
            var component=last.Kind==PayrollSalaryComponentKind.FixedEarning?PayrollComponentKind.FixedAllowance:PayrollComponentKind.SalaryAdjustment;
            var proof=new{Version=PayrollSalaryCalculation.Version,PolicyId=policy.Id,policy.RulesJson,Code=group.Key,
                Sources=rows.Select(x=>new{x.Row.Id,x.Row.Revision,x.Row.Amount,x.Row.Kind,x.Row.ProrateWithBase,x.Dates,Reference=x.Row.ApprovalRequest.SnapshotPrivateReference}).ToArray(),
                Numerator=n.ToString(),Denominator=d.ToString(),Amount=amount};
            lines.Add(new(new(employee,component.ToString(),"hr.payroll-salary.component",last.Id,last.Revision,amount,"PKR",null,last.ApprovalRequestId),
                last.ApprovalRequest.SnapshotPrivateReference,proof));
        }
        return new(lines,MonthLockEvidence.Fingerprint(lines));
    }
}
