using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Overtime;

namespace Cosmix.Application.HR.Reports;

public sealed partial class HrReportService
{
    private async Task<IReadOnlyList<OvertimeReportRow>> OvertimeRowsAsync(HrReportFilter filter,
        HrReportSelection selection, CancellationToken ct)
    {
        var rows = await repository.GetOvertimeAsync(filter, selection, ct);
        if (overtime is null || attendance is null) return rows;
        var result = new List<OvertimeReportRow>();
        foreach (var row in rows)
        {
            if (!selection.EmployeeIds.Contains(row.EmployeeId)) throw AttendanceOperation.Denied();
            // This existing reader validates the immutable proposal, decision, duration and formula.
            // The report already owns the transaction; do not call the public financial endpoint here.
            var retained = await overtime.ReadAttendanceEvidenceAsync(row.EmployeeId, row.RequestId, ct);
            if (selection.Access is null || !selection.Access.Allows(retained.Source.OriginalScopes))
                throw AttendanceOperation.Denied();
            if (selection.FullExport) selection.AdditionalScopeGroups.AddRange(retained.Source.OriginalScopes);
            if (retained.Row.Request.WorkSessionId != row.WorkSessionId ||
                retained.Row.Request.RequestedAtUtc != row.RequestedAtUtc ||
                retained.Row.Request.StartsAtUtc != row.StartsAtUtc || retained.Row.Request.EndsAtUtc != row.EndsAtUtc ||
                retained.Row.Approval.State.ToString() != row.ApprovalStatus)
                throw AttendanceOperation.Conflict("hr_report_overtime_source_changed");
            var decision = retained.Row.Decision;
            OvertimeDecisionSnapshot? snapshot = null;
            if (decision is not null)
                snapshot = await attendance.ReadPrivateAsync<OvertimeDecisionSnapshot>(decision.ProtectedChangeReference!,
                    $"attendance:overtime:{row.EmployeeId:N}:{row.RequestId:N}:decision", ct);
            if (snapshot is not null && (snapshot.RequestId != row.RequestId || snapshot.EmployeeId != row.EmployeeId ||
                snapshot.Decision != retained.Row.Approval.State || snapshot.ApprovedTicks != retained.ApprovedTicks ||
                snapshot.SourceFingerprint != retained.Source.Source.SourceFingerprint))
                throw AttendanceOperation.Conflict("hr_report_overtime_source_changed");
            result.Add(row with
            {
                ApprovedMinutes = snapshot?.ApprovedMinutes, ApprovedTicks = snapshot?.ApprovedTicks,
                PayableAmount = snapshot?.FinancialEvidence?.Amount, Currency = snapshot?.FinancialEvidence?.Currency,
                DecisionId = decision?.Id, FinancialState = snapshot?.FinancialState ?? "not_awarded"
            });
        }
        return new HrReportRows<OvertimeReportRow>(result, rows is HrReportRows<OvertimeReportRow> page ? page.TotalCount : rows.Count);
    }
}
