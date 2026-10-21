using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Overtime;

namespace Cosmix.Application.Attendance.MonthLocks;

/// <summary>Protected direct-lock evidence validation; it does not authorize a caller.</summary>
public static class MonthLockEvidence
{
    public static string Purpose(Guid periodId, Guid eventId) => $"attendance:month-lock:{periodId:N}:{eventId:N}";
    public static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    public static bool Hash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    public static void Validate(MonthLockExisting existing, MonthLockSnapshot snapshot)
    {
        var period = existing.Period; var item = existing.Event;
        if (item is null || !period.IsLocked || !item.IsLocked || item.AttendancePeriodLockId != period.Id || item.Revision != period.Revision ||
            item.ApprovalRequestId.HasValue || string.IsNullOrWhiteSpace(item.ProtectedEvidenceReference) || snapshot.SchemaVersion is not (1 or 2 or 3 or 4) ||
            snapshot.EventId != item.Id || snapshot.PeriodId != period.Id || snapshot.Revision != item.Revision ||
            snapshot.MonthStart != period.PeriodStart || snapshot.MonthEndExclusive != period.PeriodEndExclusive ||
            snapshot.Actor != item.Actor || snapshot.Reason != item.Reason || snapshot.LockedAtUtc != item.AtUtc || !Hash(snapshot.PreviewFingerprint) ||
            snapshot.Summaries is not { Count: > 0 and <= MonthLockCoverage.MaximumCells } || snapshot.CarryForwards is null ||
            snapshot.CarryForwards.Count > MonthLockCoverage.MaximumCells)
            throw AttendanceOperation.Conflict("month_lock_evidence_unavailable");
        if (snapshot.SchemaVersion == 1 && snapshot.PayrollInputs is not null)
            throw AttendanceOperation.Conflict("month_lock_evidence_unavailable");
        if (snapshot.SchemaVersion < 3 && snapshot.Overtime is not null)
            throw AttendanceOperation.Conflict("month_lock_evidence_unavailable");
        if (snapshot.SchemaVersion < 4 && snapshot.FinancialAdjustments is not null)
            throw AttendanceOperation.Conflict("month_lock_evidence_unavailable");
        if (snapshot.SchemaVersion >= 2)
            ValidateFinancialInspection(new(snapshot.PreviewFingerprint, snapshot.PayrollInputs ?? [], []), snapshot.MonthStart,
                snapshot.Summaries.Select(x => x.EmployeeId).Distinct().Order().ToArray());
        if (snapshot.SchemaVersion >= 3)
            ValidateOvertimeMembership(snapshot.MonthStart, snapshot.MonthEndExclusive, snapshot.Summaries, snapshot.Overtime);
        if (snapshot.SchemaVersion == 4)
        {
            MonthLockFinancialAdjustments.Validate(snapshot.MonthStart,
                snapshot.Summaries.Select(x => x.EmployeeId).Distinct().Order().ToArray(),
                snapshot.PayrollInputs!, snapshot.FinancialAdjustments);
            if (MonthLockFinancialAdjustments.UnvaluedCarryForwards(snapshot.CarryForwards, snapshot.FinancialAdjustments!).Count != 0)
                throw AttendanceOperation.Conflict("month_lock_financial_adjustments_invalid");
        }
        var actual = existing.Summaries.OrderBy(x => x.EmployeeId).ThenBy(x => x.AttendanceDate)
            .Select(x => new MonthLockSummaryEntry(x.EmployeeId, x.AttendanceDate, x.Id, x.InputFingerprint,
                x.PolicyVersionId ?? Guid.Empty, x.CalculationVersion)).ToArray();
        if (!actual.SequenceEqual(snapshot.Summaries) || snapshot.Summaries.Select(x => new { x.EmployeeId, x.BusinessDate }).Distinct().Count() != snapshot.Summaries.Count ||
            snapshot.Summaries.Any(x => x.EmployeeId == Guid.Empty || x.SummaryId == Guid.Empty || x.PolicyVersionId == Guid.Empty ||
                x.BusinessDate < snapshot.MonthStart || x.BusinessDate >= snapshot.MonthEndExclusive || !Hash(x.InputFingerprint) || string.IsNullOrWhiteSpace(x.CalculationVersion)) ||
            snapshot.CarryForwards.Select(x => x.AdjustmentId).Distinct().Count() != snapshot.CarryForwards.Count ||
            snapshot.CarryForwards.Select(x => (x.EmployeeId, x.SourceBusinessDate, x.Revision)).Distinct().Count() != snapshot.CarryForwards.Count ||
            snapshot.CarryForwards.Any(x => x.AdjustmentId == Guid.Empty || x.EmployeeId == Guid.Empty || x.Revision < 1 ||
                x.SourceBusinessDate >= snapshot.MonthStart || x.TargetMonthStart != snapshot.MonthStart || x.TargetMonthEndExclusive != snapshot.MonthEndExclusive ||
                !Hash(x.InputFingerprint) || !Hash(x.InterpretationFingerprint)) ||
            !snapshot.CarryForwards.SequenceEqual(snapshot.CarryForwards.OrderBy(x => x.AdjustmentId)))
            throw AttendanceOperation.Conflict("month_lock_evidence_unavailable");
    }
    public static void ValidateOvertimeMembership(DateOnly start, DateOnly end,
        IReadOnlyList<MonthLockSummaryEntry> summaries, IReadOnlyList<MonthLockOvertimeEntry>? entries)
    {
        if (entries is null || entries.Count > MonthLockCoverage.MaximumCells ||
            entries.Any(x => x?.Award is null || x.Award.Inputs?.Source is null || x.Award.FinancialEvidence is null ||
                x.Award.Authorization is null || x.Award.ApprovedIntervals is null ||
                string.IsNullOrWhiteSpace(x.SourceEvidenceReference) || string.IsNullOrWhiteSpace(x.DecisionEvidenceReference)))
            throw AttendanceOperation.Conflict("month_lock_overtime_contract_invalid");
        if (entries.Select(x => x.Award.RequestId).Distinct().Count() != entries.Count ||
            !entries.SequenceEqual(entries.OrderBy(x => x.Award.EmployeeId).ThenBy(x => x.Award.RequestId)) ||
            entries.Any(x => x.Award.SchemaVersion != 1 || x.Award.RequestId == Guid.Empty || x.Award.DecisionId == Guid.Empty ||
                x.Award.ApprovalRequestId == Guid.Empty || x.Award.SubmissionId == Guid.Empty ||
                x.Award.WorkSessionId == Guid.Empty || x.Award.SiteId == Guid.Empty ||
                x.Award.SourceBusinessDate < start || x.Award.SourceBusinessDate >= end ||
                !summaries.Any(s => s.EmployeeId == x.Award.EmployeeId && s.BusinessDate == x.Award.SourceBusinessDate) ||
                !Hash(x.Award.AwardFingerprint) || x.Award.AwardFingerprint != OvertimeFinancialAwardRules.Fingerprint(x.Award)))
            throw AttendanceOperation.Conflict("month_lock_overtime_contract_invalid");
    }
    public static void RequireOvertimeMatch(IReadOnlyList<MonthLockOvertimeEntry> retained,
        IReadOnlyList<MonthLockOvertimeEntry> current)
    {
        if (Fingerprint(retained) != Fingerprint(current))
            throw AttendanceOperation.Conflict("month_lock_overtime_membership_conflict");
    }
    public static void RequireOvertimeDependencies(IReadOnlyList<MonthLockOvertimeEntry> entries,
        IReadOnlyList<MonthLockOvertimeDependency> dependencies)
    {
        var approved = dependencies.Where(x => x.Approval?.State == Cosmix.Domain.Common.Approvals.ApprovalState.Approved).ToArray();
        if (entries.Count != approved.Length || entries.Any(entry => approved.Count(source =>
            source.Request.Id == entry.Award.RequestId && source.Request.EmployeeId == entry.Award.EmployeeId &&
            source.Session?.AttendanceDate == entry.Award.SourceBusinessDate && source.Approval!.Id == entry.Award.ApprovalRequestId &&
            source.Decision?.Id == entry.Award.DecisionId && source.Request.PrivateEvidenceReference == entry.SourceEvidenceReference &&
            source.Decision?.ProtectedChangeReference == entry.DecisionEvidenceReference) != 1))
            throw AttendanceOperation.Conflict("month_lock_overtime_membership_conflict");
    }
    public static void ValidateFinancialInspection(MonthLockFinancialInspection result, DateOnly month,
        IReadOnlyList<Guid> employees)
    {
        if (!Hash(result.Fingerprint) || result.PayrollInputs is null || result.Issues is null ||
            result.PayrollInputs.Count > MonthLockCoverage.MaximumEmployees || result.Issues.Count > MonthLockCoverage.MaximumCells ||
            result.PayrollInputs.Select(x => x.EmployeeId).Distinct().Count() != result.PayrollInputs.Count ||
            !result.PayrollInputs.SequenceEqual(result.PayrollInputs.OrderBy(x => x.EmployeeId)) ||
            result.PayrollInputs.Any(x => !employees.Contains(x.EmployeeId) || x.MonthStart != month ||
                x.SourceRevisionId == Guid.Empty || x.SourceRevision < 1 || !Hash(x.InputFingerprint) ||
                !Hash(x.SourceEvidenceSha256) || string.IsNullOrWhiteSpace(x.CalculationVersion)) ||
            result.Issues.Any(x => string.IsNullOrWhiteSpace(x.Code) || x.EmployeeId.HasValue && !employees.Contains(x.EmployeeId.Value)))
            throw AttendanceOperation.Conflict("month_lock_financial_contract_invalid");
        if (result.Issues.Count == 0 && (!result.PayrollInputs.Select(x => x.EmployeeId).SequenceEqual(employees) ||
            result.PayrollInputs.Any(x => x.HandoffId is null || x.HandoffId == Guid.Empty ||
                !Hash(x.HandoffEvidenceFingerprint) || !Hash(x.LegalActivationFingerprint))))
            throw AttendanceOperation.Conflict("month_lock_financial_contract_invalid");
    }
}
