using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.MonthLocks;

public sealed record MonthLockOvertimeDependency(OvertimeRequest Request, WorkSession? Session,
    ApprovalRequest? Approval, ApprovalDecision? Decision);
public sealed record MonthLockOvertimeInspection(string Fingerprint, IReadOnlyList<MonthLockIssue> Issues);

/// <summary>Binds exact persisted OT dependencies without asserting that protected award contents
/// have been verified or that Finance can consume retained lock membership.</summary>
public static class MonthLockOvertimeReadiness
{
    public static MonthLockOvertimeInspection Inspect(DateOnly start, DateOnly end,
        IReadOnlyList<MonthLockOvertimeDependency> sources)
    {
        if (start.Day != 1 || start.Year is < 1900 or > 9998 || end != start.AddMonths(1) ||
            sources.Count > MonthLockCoverage.MaximumCells || sources.Any(x => x.Request is null))
            throw AttendanceOperation.Conflict("month_lock_overtime_contract_invalid");
        var issues = new List<MonthLockIssue>();
        foreach (var group in sources.GroupBy(x => x.Request.Id))
        {
            foreach (var row in group)
            {
                var request = row.Request; var session = row.Session;
                var approval = row.Approval; var decision = row.Decision;
                DateOnly? date = session?.AttendanceDate;
                void Issue(string code) => issues.Add(new(code, request.EmployeeId, date));
                if (group.Count() != 1) Issue("overtime_decision_evidence_ambiguous");
                if (session is null || session.Id != request.WorkSessionId || session.EmployeeId != request.EmployeeId ||
                    session.AttendanceDate < start || session.AttendanceDate >= end || session.EndsAtUtc is null ||
                    request.StartsAtUtc < session.StartsAtUtc || request.EndsAtUtc > session.EndsAtUtc ||
                    request.SiteId is null || session.SiteId != request.SiteId || session.BranchId.HasValue)
                    Issue("overtime_session_evidence_unavailable");
                if (!ValidReference(request.PrivateEvidenceReference)) Issue("overtime_source_evidence_unavailable");
                if (approval is null || approval.Id != request.ApprovalRequestId || approval.SubjectId == Guid.Empty ||
                    approval.RequestType != OvertimeService.RequestType || approval.Stage != 1 ||
                    approval.RequesterUserId != request.RequestedBy || approval.RequestedAt != request.RequestedAtUtc ||
                    approval.SnapshotPrivateReference != request.PrivateEvidenceReference || !Enum.IsDefined(approval.State))
                {
                    Issue("overtime_approval_evidence_unavailable");
                    continue;
                }
                if (approval.State == ApprovalState.Pending)
                {
                    Issue("pending_overtime");
                    if (decision is not null || approval.ClosedAt.HasValue) Issue("overtime_decision_evidence_unavailable");
                    continue;
                }
                if (approval.State == ApprovalState.Cancelled)
                {
                    if (decision is not null || approval.ClosedAt is null || approval.ClosedAt < approval.RequestedAt)
                        Issue("overtime_decision_evidence_unavailable");
                    continue;
                }
                if (decision is null || decision.Id == Guid.Empty || decision.ApprovalRequestId != approval.Id ||
                    decision.Decision != approval.State || decision.DecidedAt != approval.ClosedAt ||
                    decision.DecidedAt < approval.RequestedAt || decision.ActorUserId == approval.RequesterUserId ||
                    !Guid.TryParse(decision.ActorUserId, out var reviewer) || reviewer == Guid.Empty ||
                    string.IsNullOrWhiteSpace(decision.ActorRoleCode) || !ValidReference(decision.ProtectedChangeReference))
                    Issue("overtime_decision_evidence_unavailable");
                // Header provenance is necessary, but it is not a verified award or a downstream binding.
                if (approval.State == ApprovalState.Approved) Issue("overtime_locked_handoff_unavailable");
            }
        }
        var distinct = issues.Distinct().OrderBy(x => x.Code, StringComparer.Ordinal)
            .ThenBy(x => x.EmployeeId).ThenBy(x => x.BusinessDate).ToArray();
        var digest = MonthLockEvidence.Fingerprint(new { schema = "ot-finalization-dependencies-v1", start, end,
            sources = sources.Select(Fingerprint).Order(StringComparer.Ordinal).ToArray(), issues = distinct });
        return new(digest, distinct);
    }

    private static bool ValidReference(string? serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized)) return false;
        try
        {
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(serialized);
            return reference is not null && MonthLockEvidence.Hash(reference.Sha256) && reference.SizeBytes > 0 &&
                !string.IsNullOrWhiteSpace(reference.StorageKey) && !string.IsNullOrWhiteSpace(reference.ObjectVersion) &&
                !string.IsNullOrWhiteSpace(reference.KeyReference);
        }
        catch (JsonException) { return false; }
    }

    private static string Fingerprint(MonthLockOvertimeDependency row) => MonthLockEvidence.Fingerprint(new
    {
        request = row.Request,
        session = row.Session is not { } session ? null : new
        {
            session.Id, session.EmployeeId, session.AttendanceDate, session.SessionKey, session.Revision,
            session.StartsAtUtc, session.EndsAtUtc, session.SiteId, session.BranchId, session.ShiftVersionId,
            session.PolicyVersionId, session.CheckInPunchId, session.CheckOutPunchId, session.CorrectionId,
            session.CalculatedAtUtc, session.EndsContinuousDuty, session.EmployeeAssignmentId,
            session.OfficeRosterAssignmentId, session.OfficeGeofenceVersionId, session.HolidayCalendarId
        },
        approval = row.Approval is not { } approval ? null : new
        {
            approval.Id, approval.RequestType, approval.SubjectId, approval.Stage, approval.RequesterUserId,
            approval.RequestedAt, approval.State, approval.ClosedAt, approval.SnapshotPrivateReference,
            approval.RouteRuleId, approval.RowVersion
        },
        decision = row.Decision is not { } decision ? null : new
        {
            decision.Id, decision.ApprovalRequestId, decision.Decision, decision.ActorUserId,
            decision.ActorRoleCode, decision.DecidedAt, decision.Reason, decision.ProtectedChangeReference
        }
    });
}
