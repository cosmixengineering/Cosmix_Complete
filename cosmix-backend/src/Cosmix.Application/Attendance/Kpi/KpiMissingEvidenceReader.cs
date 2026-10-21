using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.Kpi;

public sealed class KpiMissingEvidenceReader(IKpiMissingEvidenceRepository repository, IKpiMissingEvidenceSourceReader sources,
    KpiMissingEvidenceEvidenceReader evidence) : IReviewedKpiMissingEvidenceReader
{
    public async Task<ReviewedKpiMissingEvidenceSource> ReadAsync(Guid employee, DateOnly from, DateOnly to, CancellationToken ct)
    {
        KpiMissingEvidenceRules.Range(employee, from, to); repository.RequireTransaction();
        var rows = await repository.RangeAsync(employee, from, to, ct);
        if (rows.Count > 512) throw AttendanceOperation.Conflict("kpi_missing_evidence_range_limit");
        var scopes = new List<AttendanceResourceScope>(await sources.ScopesAsync(employee, from, to, ct));
        var events = new List<ReviewedKpiMissingEvidenceEvent>(); var issues = new List<string>(); var retained = new List<object>();
        var revisions = new List<ReviewedKpiMissingEvidenceRevision>();
        foreach (var request in rows.OrderBy(x => x.RequestType, StringComparer.Ordinal).ThenBy(x => x.Id)) {
            try {
                var row = await evidence.FindAsync(employee, request.Id, ct);
                var snapshot = await evidence.SnapshotAsync(row, ct);
                if (snapshot.Source.BusinessDate < from || snapshot.Source.BusinessDate >= to)
                    throw AttendanceOperation.Conflict("kpi_missing_evidence_range_provenance_conflict");
                scopes.AddRange(snapshot.Source.Scopes);
                retained.Add(new { row.Request.Id, row.Request.State, row.Request.RowVersion,
                    Snapshot = KpiMissingEvidenceRules.Hash(snapshot), Decisions = row.Decisions.Select(x => new {
                        x.Id, x.Decision, x.ActorUserId, x.ActorRoleCode, x.DecidedAt, x.ProtectedChangeReference }) });
                if (row.Request.State == ApprovalState.Pending) { issues.Add("kpi_missing_evidence_review_pending"); continue; }
                if (row.Request.State is ApprovalState.Returned or ApprovalState.Rejected) continue;
                var history = await evidence.HistoryAsync(row, snapshot, ct);
                revisions.AddRange(history.Revisions); scopes.AddRange(history.Scopes); retained.Add(history);
                var active = history.Active;
                var current = await sources.ReadAsync(employee, snapshot.Source.LeaveRequestId, snapshot.Source.BusinessDate, ct);
                scopes.AddRange(current.Scopes);
                if (KpiMissingEvidenceRules.Hash(current) != KpiMissingEvidenceRules.Hash(active.VerifiedSource)) {
                    issues.Add("kpi_missing_evidence_source_changed"); issues.AddRange(current.Issues); continue;
                }
                if (KpiMissingEvidenceEvidenceReader.RevisionCount(current) != active.ActiveCount)
                    throw AttendanceOperation.Conflict("kpi_missing_evidence_revision_count_conflict");
                if (active.ActiveCount == 0) continue;
                events.Add(new(active.SupersedesRevisionId.HasValue ? active.Id : active.ApprovalDecisionId,
                    employee, current.BusinessDate, current.LeaveRequestId, 1,
                    row.Request.Id, active.ApprovalDecisionId, current.PolicyVersionId!.Value, current.PolicyAssignmentIds,
                    current.LeaveAllocationId!.Value, current.LeaveApprovalId!.Value, current.SummaryId!.Value, current.ShiftVersionId!.Value,
                    current.Duration!, current.Fingerprint, current.EvidenceFingerprint, active.ProofFingerprint, active.ReviewAuthority, active.RetainedScopes));
            } catch (AuthenticationFailure failure) when (failure.StatusCode is 404 or 409) { issues.Add(failure.Code); }
        }
        var duplicateKeys = revisions.GroupBy(x => (x.VerifiedSource.BusinessDate, x.VerifiedSource.LeaveRequestId))
            .Where(x => x.Select(r => r.ApprovalRequestId).Distinct().Count() > 1).Select(x => x.Key).ToHashSet();
        duplicateKeys.UnionWith(events.GroupBy(x => (x.BusinessDate, x.LeaveRequestId)).Where(x => x.Count() > 1).Select(x => x.Key));
        if (duplicateKeys.Count > 0) {
            issues.Add("kpi_missing_evidence_duplicate_reviewed_event");
            events.RemoveAll(x => duplicateKeys.Contains((x.BusinessDate, x.LeaveRequestId)));
        }
        foreach (var candidate in await sources.CandidatesAsync(employee, from, to, ct)) {
            try {
                var source = await sources.ReadAsync(employee, candidate.LeaveId, candidate.Date, ct);
                scopes.AddRange(source.Scopes); retained.Add(source);
                if (source.State == "pending") issues.AddRange(source.Issues.Count > 0 ? source.Issues : ["kpi_missing_verified_eligibility_required"]);
                else if (source.State == "eligible_for_review" && !events.Any(x => x.BusinessDate == candidate.Date && x.LeaveRequestId == candidate.LeaveId))
                    issues.Add("kpi_missing_evidence_independent_review_required");
                else if (source.State is not ("eligible_for_review" or "evidence_present" or "ineligible" or "disabled" or "withdrawal_required"))
                    issues.Add("kpi_missing_unknown_source_state");
            } catch (AuthenticationFailure failure) when (failure.StatusCode is 404 or 409) { issues.Add(failure.Code); }
        }
        var required = KpiMissingEvidenceRules.Scopes(employee, scopes);
        var ordered = events.OrderBy(x => x.BusinessDate).ThenBy(x => x.LeaveRequestId).ToArray();
        var reasons = issues.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var ids = rows.Select(x => x.Id).Concat(ordered.SelectMany(x => new[] {
            x.Id, x.LeaveRequestId, x.LeaveAllocationId, x.LeaveApprovalId, x.PolicyVersionId, x.SummaryId, x.ShiftVersionId
        })).Concat(revisions.SelectMany(x => new[] { x.Id, x.ApprovalRequestId, x.ApprovalDecisionId }
            .Concat(SourceIds(x.VerifiedSource)).Concat(x.PreviousSource is null ? [] : SourceIds(x.PreviousSource))))
            .Distinct().Order().ToArray();
        return new(reasons.Length == 0 ? "verified" : "pending", ordered, ids, required,
            KpiMissingEvidenceRules.Hash(new { employee, from, to, ordered, ids, required, reasons, retained }), reasons) { Revisions = revisions };
    }
    private static IEnumerable<Guid> SourceIds(KpiMissingEvidenceSource source) =>
        new Guid?[] { source.LeaveRequestId, source.LeaveAllocationId, source.LeaveApprovalId, source.SummaryId, source.ShiftVersionId, source.PolicyVersionId }
            .OfType<Guid>().Concat(source.PolicyAssignmentIds).Concat(source.RelatedSourceIds ?? []);
}
