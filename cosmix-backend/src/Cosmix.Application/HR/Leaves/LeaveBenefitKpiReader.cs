using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.HR.Leaves;

public sealed record LeaveBenefitKpiEvidence(FinalizedKpiScore Score, string VerifiedSourceFingerprint,
    IReadOnlyList<Guid> ResolvedDisputeIds);

/// <summary>Reads verified KPI evidence inside the caller's AttendanceOperation transaction. Grants no benefit or authority.</summary>
public sealed class LeaveBenefitKpiReader(AttendanceOperation operation, IFinalizedKpiReader finalized,
    IKpiDisputeRepository disputes, KpiDisputeEvidenceReader disputeEvidence, IKpiRepository kpiRequests, IKpiSourceReader kpiSources)
{
    public async Task<LeaveBenefitKpiEvidence> ReadAsync(Guid employee, DateOnly from, DateOnly toExclusive,
        DateTimeOffset now, CancellationToken ct)
    {
        if (from.Year is < 1901 or > 9997 || toExclusive != from.AddYears(1))
            throw AttendanceOperation.Conflict("leave_benefit_year_invalid");
        if (toExclusive > LeaveRules.LocalDate(now))
            throw AttendanceOperation.Conflict("leave_benefit_completed_year_required");
        var score = await finalized.ReadAsync(employee, from, ct)
            ?? throw AttendanceOperation.Conflict("leave_benefit_finalized_kpi_required");
        KpiDisputeRules.Final(score, employee, from);
        if (score.ToExclusive != toExclusive || score.FinalizedAtUtc > now)
            throw AttendanceOperation.Conflict("leave_benefit_kpi_ineligible");

        var pending = await kpiRequests.PendingAsync(employee, ct);
        if (pending.Count > 128) throw AttendanceOperation.Conflict("kpi_revision_limit");
        foreach (var item in pending)
        {
            if (item.RequestType != AttendanceKpiService.RequestType || item.SubjectId != employee || item.State != ApprovalState.Pending)
                throw AttendanceOperation.Conflict("leave_benefit_kpi_pending_evidence_conflict");
            var prepared = await operation.ReadPrivateAsync<KpiRequestSnapshot>(item.SnapshotPrivateReference, AttendanceKpiService.RequestPurpose(employee), ct);
            var pendingSource = prepared.Source;
            if (prepared.SchemaVersion != 1 || prepared.EmployeeId != employee || prepared.RequesterAccountId == Guid.Empty ||
                prepared.RequesterAccountId.ToString() != item.RequesterUserId || pendingSource is null || pendingSource.EmployeeId != employee ||
                pendingSource.From == default || pendingSource.From.Year >= 9999 || pendingSource.From.Day != 1 ||
                pendingSource.ToExclusive != pendingSource.From.AddYears(1) || prepared.AsOf != pendingSource.ToExclusive.AddDays(-1))
                throw AttendanceOperation.Conflict("leave_benefit_kpi_pending_evidence_conflict");
            if (pendingSource.From < toExclusive && pendingSource.ToExclusive > from)
                throw AttendanceOperation.Conflict("leave_benefit_kpi_review_pending");
        }
        var disputeRows = await disputes.YearAsync(employee, from, null, null, ct);
        if (disputeRows.Count > KpiDisputeRules.YearLimit) throw AttendanceOperation.Conflict("kpi_dispute_history_limit");
        var verifiedDisputes = new List<KpiDisputeEvidence>();
        foreach (var row in disputeRows) verifiedDisputes.Add(await disputeEvidence.ReadAsync(employee, row.Id, ct));
        foreach (var item in verifiedDisputes)
        {
            var resolved = item.Response?.Outcome == KpiDisputeOutcome.ReviewedNoChange ||
                item.Response?.Outcome == KpiDisputeOutcome.CorrectionRequired && item.Amendment is not null && item.Amendment.AmendedFinal.Revision <= score.Revision ||
                item.Response?.Outcome == KpiDisputeOutcome.Returned && verifiedDisputes.Any(x => x.Draft.ResubmissionOf == item.Row.Request.Id);
            if (!resolved) throw AttendanceOperation.Conflict("leave_benefit_kpi_dispute_unresolved");
        }
        var source = await kpiSources.ReadAsync(employee, toExclusive.AddDays(-1), ct);
        KpiDisputeRules.Source(source, score);
        if (!KpiDisputeRules.MatchesFinal(source, score)) throw AttendanceOperation.Conflict("leave_benefit_kpi_source_changed");
        return new(score, source.Fingerprint, verifiedDisputes.Select(x => x.Row.Request.Id).Order().ToArray());
    }
}
