using Cosmix.Application.Attendance.Calculation;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.WorkTreatments;

/// <summary>Reuse the existing exact correction/private-source validators and retained alternative authority.
/// A correction-shaped row alone is never accepted as verified actual duty.</summary>
public sealed class WorkTreatmentCorrectionProofs(ICorrectionReviewRepository corrections,
    IAttendanceAlternativeCorrectionApprovalReader alternatives)
{
    public async Task<bool> EligibleAsync(WorkSession session, CancellationToken ct) => session.EndsAtUtc.HasValue &&
        await corrections.HistoricallyEligibleAsync(session.EmployeeId, session.StartsAtUtc, session.EndsAtUtc.Value, ct) &&
        await corrections.PreservesAssignmentAsync(session.EmployeeId, session.EmployeeAssignmentId, session.SiteId, session.StartsAtUtc, session.EndsAtUtc.Value, ct);

    public async Task<bool> VerifyAsync(WorkSession session, AttendanceCorrection correction, ApprovalRequest approval,
        ApprovalDecision decision, CorrectionEvidenceSnapshot snapshot, CancellationToken ct) {
        var row = await corrections.FindAsync(session.EmployeeId, correction.Id, ct);
        if (row is null || row.Approval.Id != approval.Id || row.AppliedSessionRevisionId != session.Id || row.AppliedSummaryRevisionId is null ||
            session.CorrectionId != correction.Id || session.AttendanceDate != correction.AttendanceDate ||
            snapshot.SchemaVersion is not (3 or 4 or 5) || !Guid.TryParse(approval.RequesterUserId, out var requester) ||
            !AttendanceCorrectionService.Matches(row, snapshot, requester)) return false;
        if (snapshot.SchemaVersion == 5) {
            var proof = await alternatives.ReadAsync(session.EmployeeId, correction.Id, decision.Id, ct);
            if (proof is null || proof.Decision.Id != decision.Id || proof.Actor.AccountId.ToString() != decision.ActorUserId ||
                proof.Actor.EmployeeId == session.EmployeeId || proof.Audit is null || proof.IsAuthorizedStagedApplication) return false;
        }
        if (session.BranchId.HasValue && (snapshot.Source.OfficeSource is null || !CorrectionOfficeEvidence.Matches(session, snapshot.Source.OfficeSource))) return false;
        return correction.Kind switch {
            AttendanceCorrectionKind.ManualSession => session.StartsAtUtc == correction.RequestedTimeUtc && session.EndsAtUtc == correction.RequestedEndUtc,
            AttendanceCorrectionKind.CheckInTime => session.StartsAtUtc == correction.RequestedTimeUtc,
            AttendanceCorrectionKind.CheckOutTime => session.EndsAtUtc == correction.RequestedTimeUtc,
            _ => false
        };
    }
}
