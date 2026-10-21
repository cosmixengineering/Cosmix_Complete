using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Domain.Attendance.PayrollInputs;

namespace Cosmix.Application.Attendance.MonthLocks;

/// <summary>Exact approved adjustment membership; factual duty alone never supplies a money value.</summary>
public static class MonthLockFinancialAdjustments
{
    public static void Validate(DateOnly month, IReadOnlyList<Guid> employees,
        IReadOnlyList<MonthLockPayrollInputEntry> payrollInputs,
        IReadOnlyList<AttendancePayrollFinancialMonthEntry>? entries)
    {
        if (entries is null || entries.Count > MonthLockCoverage.MaximumCells ||
            entries.Any(x => x is null || x.AdjustmentId == Guid.Empty || !employees.Contains(x.EmployeeId) ||
                x.SourceMonth.Day != 1 || x.SourceMonth >= month || x.TargetMonth != month || x.Revision < 1 ||
                x.SourceRevisionId == Guid.Empty || x.ConfirmedPostingId == Guid.Empty || x.TargetRevisionId == Guid.Empty ||
                !MonthLockEvidence.Hash(x.TargetInputFingerprint) || !MonthLockEvidence.Hash(x.PreviewFingerprint) ||
                !MonthLockEvidence.Hash(x.EvidenceFingerprint) || string.IsNullOrWhiteSpace(x.EvidenceReference) ||
                !AttendancePayrollFinancialRules.Money(x.SignedAmount) ||
                x.Currency != "PKR" || x.Status is not ("approved" or "posted") ||
                (x.Status == "approved" ? x.PostingResolutionId is not null || x.PostingEvidenceFingerprint is not null :
                    x.PostingResolutionId is null || x.PostingResolutionId == Guid.Empty || !MonthLockEvidence.Hash(x.PostingEvidenceFingerprint)) ||
                x.FactualAdjustmentIds is not { Count: > 0 and <= 31 } ||
                x.FactualAdjustmentIds.Any(id => id == Guid.Empty) ||
                x.FactualAdjustmentIds.Distinct().Count() != x.FactualAdjustmentIds.Count ||
                !payrollInputs.Any(p => p.EmployeeId == x.EmployeeId && p.MonthStart == month &&
                    p.SourceRevisionId == x.TargetRevisionId && p.InputFingerprint == x.TargetInputFingerprint)) ||
            entries.Select(x => x.AdjustmentId).Distinct().Count() != entries.Count ||
            entries.Select(x => (x.EmployeeId, x.SourceMonth, x.Revision)).Distinct().Count() != entries.Count ||
            !entries.SequenceEqual(entries.OrderBy(x => x.EmployeeId).ThenBy(x => x.SourceMonth).ThenBy(x => x.Revision)))
            throw AttendanceOperation.Conflict("month_lock_financial_adjustments_invalid");
    }

    public static IReadOnlyList<MonthLockIssue> UnvaluedCarryForwards(
        IReadOnlyList<MonthLockCarryForwardEntry> carry,
        IReadOnlyList<AttendancePayrollFinancialMonthEntry> entries) => carry
        .GroupBy(x => (x.EmployeeId, x.SourceBusinessDate))
        .Select(group => group.OrderByDescending(x => x.Revision).First())
        .Where(source => !entries.Where(x => x.EmployeeId == source.EmployeeId &&
                x.SourceMonth == new DateOnly(source.SourceBusinessDate.Year, source.SourceBusinessDate.Month, 1))
            .OrderByDescending(x => x.Revision).Take(1).Any(x => x.FactualAdjustmentIds.Contains(source.AdjustmentId)))
        .Select(x => new MonthLockIssue("carry_forward_financial_calculation_unavailable", x.EmployeeId, x.SourceBusinessDate))
        .Distinct().ToArray();

    public static void RequireRetainedMatch(IReadOnlyList<AttendancePayrollFinancialMonthEntry> retained,
        IReadOnlyList<AttendancePayrollFinancialMonthEntry> current)
    {
        if (retained.Count != current.Count) throw AttendanceOperation.Conflict("month_lock_financial_adjustments_changed");
        foreach (var original in retained)
        {
            var now = current.SingleOrDefault(x => x.AdjustmentId == original.AdjustmentId);
            if (now is null || now.Status != original.Status && !(original.Status == "approved" && now.Status == "posted"))
                throw AttendanceOperation.Conflict("month_lock_financial_adjustments_changed");
            // Later confirmed posting is append-only evidence, not permission to rewrite the frozen input.
            var immutable = now with { Status = original.Status, PostingResolutionId = original.PostingResolutionId,
                PostingEvidenceFingerprint = original.PostingEvidenceFingerprint };
            if (MonthLockEvidence.Fingerprint(immutable) != MonthLockEvidence.Fingerprint(original) ||
                original.Status == "posted" && (now.PostingResolutionId != original.PostingResolutionId ||
                    now.PostingEvidenceFingerprint != original.PostingEvidenceFingerprint))
                throw AttendanceOperation.Conflict("month_lock_financial_adjustments_changed");
        }
    }
}
