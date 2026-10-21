using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Attendance.Calculation;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.Attendance.PayrollInputs;

/// <summary>Revalues the exact retained monthly source with approved replacement duty only.
/// Cross-month late dependencies and reviewed special-work interpretations remain fail-closed.</summary>
public static class AttendancePayrollFinancialCalculation
{
    public static AttendancePayrollInputResult Calculate(AttendancePayrollInputCalculation source,
        IReadOnlyList<AttendancePayrollFinancialCorrection> corrections)
    {
        var manifest = source.SourceManifest;
        if (source.State != "ready" || source.Issues.Count != 0 || manifest.SchemaVersion != 1 ||
            manifest.CalculationVersion != AttendancePayrollInputRules.Version ||
            source.EmployeeId != manifest.EmployeeId || source.MonthStart != manifest.MonthStart ||
            source.MonthEndExclusive != source.MonthStart.AddMonths(1) || manifest.MonthEndExclusive != source.MonthEndExclusive ||
            source.SourceFrom != manifest.SourceFrom || source.SourceTo != manifest.SourceTo ||
            source.SourceFrom < source.MonthStart || source.SourceTo >= source.MonthEndExclusive ||
            source.SourceTo < source.SourceFrom || manifest.PriorConfirmedOrdinary is { Count: > 0 } ||
            manifest.Days.Count is < 1 or > 31 || manifest.Days.Select(x => x.BusinessDate).Distinct().Count() != manifest.Days.Count ||
            corrections.Count is < 1 or > 31 || corrections.Select(x => x.AdjustmentId).Distinct().Count() != corrections.Count ||
            corrections.Select(x => x.Evidence.SourceBusinessDate).Distinct().Count() != corrections.Count)
            throw AttendanceOperation.Conflict("financial_carry_forward_month_dependency_unavailable");
        var baseline = new List<AttendancePayrollInputDay>();
        var corrected = new List<AttendancePayrollInputDay>();
        foreach (var day in manifest.Days.OrderBy(x => x.BusinessDate))
        {
            var snapshot = day.Snapshot;
            var policy = day.Policy ?? snapshot?.Policy;
            if (day.BusinessDate < source.SourceFrom || day.BusinessDate > source.SourceTo ||
                day.State != "available" || snapshot is null || snapshot.EmployeeId != source.EmployeeId ||
                snapshot.BusinessDate != day.BusinessDate || snapshot.ReviewedWork is not null || snapshot.TimeInput.ReviewedWork is not null ||
                policy?.State != "effective" || policy.PolicyVersionId is null ||
                !Same(policy, snapshot.Policy) || !Same(policy.Rules, snapshot.TimeInput.Rules) ||
                day.Compensation is null || !Same(day.Time, AttendanceDailyTimeCalculation.Calculate(snapshot.TimeInput)) ||
                !AttendancePayrollAttestationRules.Hash(day.Fingerprint))
                throw AttendanceOperation.Conflict("financial_carry_forward_source_invalid");
            if (policy.Rules?.LateDeduction is { Enabled: true } late && late.ResetPeriod != AttendanceLateResetPeriod.CalendarMonth)
                throw AttendanceOperation.Conflict("financial_carry_forward_month_dependency_unavailable");
            var input = new AttendancePayrollInputDay(day.BusinessDate, day.State, policy.PolicyVersionId,
                policy.Rules, snapshot.TimeInput, day.Time, day.Compensation, day.Fingerprint,
                AttendancePayrollInputProducer.SourceIds(snapshot));
            baseline.Add(input);
            var change = corrections.SingleOrDefault(x => x.Evidence.SourceBusinessDate == day.BusinessDate);
            if (change is null) { corrected.Add(input); continue; }
            var proof = change.Evidence;
            if (change.AdjustmentId == Guid.Empty || proof.EmployeeId != source.EmployeeId || proof.CorrectionId == Guid.Empty ||
                proof.ApprovalDecisionId == Guid.Empty || proof.Revision < 1 || proof.AlternativeEvidence is not null ||
                proof.Sessions.Count is < 1 or > 512 ||
                !proof.FrozenSessionIds.Order().SequenceEqual(snapshot.Sessions.Select(x => x.Id).Order()) ||
                !proof.FrozenSessionKeys.Order().SequenceEqual(snapshot.Sessions.Select(x => x.SessionKey).Order()) ||
                proof.Sessions.Any(x => x.ShiftVersionId != snapshot.Roster.ShiftVersionId || x.OfficeSource != snapshot.OfficeSource ||
                    x.SiteId != snapshot.Roster.SiteId))
                throw AttendanceOperation.Conflict("financial_carry_forward_correction_source_conflict");
            var timeInput = snapshot.TimeInput with
            {
                Duty = proof.Sessions.OrderBy(x => x.StartsAtUtc).ThenBy(x => x.SessionKey)
                    .Select(x => new AttendanceTimeInterval(x.StartsAtUtc, x.EndsAtUtc)).ToArray()
            };
            var time = AttendanceDailyTimeCalculation.Calculate(timeInput);
            if (time.Status != "calculated") throw AttendanceOperation.Conflict("financial_carry_forward_time_unavailable");
            corrected.Add(input with { TimeInput = timeInput, Time = time,
                SourceFingerprint = CarryForward.CarryForwardEvidence.Fingerprint(new { day.Fingerprint, change }),
                SourceIds = input.SourceIds.Concat([change.AdjustmentId, proof.CorrectionId, proof.ApprovalDecisionId]).Distinct().Order().ToArray() });
        }
        if (corrections.Any(x => !manifest.Days.Any(day => day.BusinessDate == x.Evidence.SourceBusinessDate)))
            throw AttendanceOperation.Conflict("financial_carry_forward_correction_source_conflict");
        var original = AttendancePayrollInputRules.Calculate(source.MonthStart, baseline);
        if (original.State != "ready" || !Same(original.Lines, source.Lines))
            throw AttendanceOperation.Conflict("financial_carry_forward_baseline_conflict");
        var result = AttendancePayrollInputRules.Calculate(source.MonthStart, corrected);
        if (result.State != "ready" || result.Issues.Count != 0)
            throw AttendanceOperation.Conflict("financial_carry_forward_calculation_unavailable");
        return result;
    }
    private static bool Same<T>(T first, T second) => JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second);
}
