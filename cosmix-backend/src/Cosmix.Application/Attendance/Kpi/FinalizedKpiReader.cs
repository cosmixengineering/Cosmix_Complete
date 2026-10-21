using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.Kpi;

/// <summary>Called inside an already authorized transaction. A prepared preview is never a finalized score.</summary>
public sealed class FinalizedKpiReader(IKpiRepository repository, AttendanceOperation operation) : IFinalizedKpiReader
{
    public async Task<FinalizedKpiScore?> ReadAsync(Guid employee, DateOnly from, CancellationToken ct)
    {
        var audits = await repository.FinalizationsAsync(employee, from, ct);
        if (audits.Count > 128) throw AttendanceOperation.Conflict("kpi_revision_limit");
        var scores = new List<FinalizedKpiScore>();
        foreach (var audit in audits)
        {
            if (audit.EventType != AttendanceKpiService.FinalizedEvent || audit.TargetType != AttendanceKpiService.TargetType ||
                audit.EmployeeScopeId != employee || audit.TargetId != AttendanceKpiService.Target(employee, from) || audit.ProtectedChangesReference is null)
                throw AttendanceOperation.Conflict("kpi_final_evidence_conflict");
            var score = await operation.ReadPrivateAsync<FinalizedKpiScore>(audit.ProtectedChangesReference, AttendanceKpiService.FinalPurpose(employee, from), ct);
            var row = await repository.FindAsync(employee, score.RequestId, ct);
            if (score.SchemaVersion != 1 || score.Id == Guid.Empty || score.EmployeeId != employee || score.From != from ||
                score.ToExclusive != from.AddYears(1) || score.PolicyVersionId == Guid.Empty || score.ExactScore is < 0 or > 100 ||
                score.ActorAccountId.ToString() != audit.ActorUserId || score.FinalizedAtUtc != audit.OccurredAt ||
                row?.Request.RequestType != AttendanceKpiService.RequestType || row.Request.State != ApprovalState.Approved ||
                row.Decisions.Count != 1 || row.Decisions[0].Id != score.DecisionId || row.Decisions[0].Decision != ApprovalState.Approved ||
                row.Decisions[0].ActorUserId != audit.ActorUserId || row.Decisions[0].ActorRoleCode != audit.ActorRoleCode ||
                row.Decisions[0].DecidedAt != score.FinalizedAtUtc || row.Decisions[0].ProtectedChangeReference != row.Request.SnapshotPrivateReference ||
                !AttendanceScopeEvaluation.IsValid(score.OriginalScopes) || !score.OriginalScopes.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId == employee))
                throw AttendanceOperation.Conflict("kpi_final_evidence_conflict");
            var request = await operation.ReadPrivateAsync<KpiRequestSnapshot>(row.Request.SnapshotPrivateReference,
                AttendanceKpiService.RequestPurpose(employee), ct);
            AttendanceKpiService.VerifyManifest(request.Source);
            if (request.SchemaVersion != 1 || request.EmployeeId != employee || request.Source.EmployeeId != employee ||
                request.Source.From != score.From || request.Source.ToExclusive != score.ToExclusive ||
                request.AsOf.AddDays(1) != score.ToExclusive || request.RequesterAccountId.ToString() != row.Request.RequesterUserId ||
                request.Source.PolicyVersionId != score.PolicyVersionId ||
                request.Source.Fingerprint != score.SourceFingerprint || request.Source.Calculation.Fingerprint != score.CalculationFingerprint ||
                request.Source.Calculation.ExactScore != score.ExactScore || request.Source.Calculation.DisplayScore != score.DisplayScore ||
                request.ExpectedPreviousFinalizationId != score.SupersedesId)
                throw AttendanceOperation.Conflict("kpi_final_source_conflict");
            scores.Add(score);
        }
        var ordered = scores.OrderBy(x => x.Revision).ToArray();
        for (var i = 0; i < ordered.Length; i++)
            if (ordered[i].Revision != i + 1 || ordered[i].SupersedesId != (i == 0 ? (Guid?)null : ordered[i - 1].Id) ||
                i > 0 && ordered[i].FinalizedAtUtc < ordered[i - 1].FinalizedAtUtc)
                throw AttendanceOperation.Conflict("kpi_final_revision_conflict");
        return ordered.LastOrDefault();
    }
}
