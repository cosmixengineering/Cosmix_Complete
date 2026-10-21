using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

/// <summary>Called within the authorized calculation transaction. It never infers an allocation from a request total.</summary>
public sealed class LeaveAttendanceContextReader(ILeaveRepository repository, LeaveProvenance provenance,
    AttendanceOperation operation) : ILeaveAttendanceContextReader
{
    public async Task<LeaveAttendanceContext> ReadAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        var rows = await repository.ForDateAsync(employeeId, date, ct);
        if (rows.Count(x => x.SupersededByRequestId is null && x.Approval?.State == ApprovalState.Approved && !LeaveRules.Cancelled(x)) > 32)
            return new(LeaveContextStatus.Conflict, [], LeaveProvenance.Digest(new { employeeId, date, reason = "leave_context_limit" }), ["leave_context_limit"]);
        var reasons = new List<string>(); var effects = new List<ApprovedLeaveDayEffect>(); var conflict = false;
        foreach (var row in rows)
        {
            try
            {
                await provenance.VerifyAsync(row, ct);
                foreach (var c in row.Cancellations)
                {
                    if (c.Request.RequestedByUserId != row.Request.RequestedByUserId || c.Request.LeaveRequestId != row.Request.Id ||
                        !LeaveRules.ApprovalValid(c.Approval, c.Decisions, c.Request.Id, c.Request.RequestedByUserId, LeaveRules.CancellationType))
                        throw AttendanceOperation.Conflict("leave_cancellation_provenance_unavailable");
                    var snapshot = await operation.ReadPrivateAsync<LeaveService.CancellationSnapshot>(c.Approval!.SnapshotPrivateReference,
                        LeaveService.CancellationPurpose(c.Request.Id), ct);
                    if (snapshot != new LeaveService.CancellationSnapshot(c.Request.Id, row.Request.Id, c.Request.RequestedByUserId, c.Request.Reason, row.Approval!.Id))
                        throw AttendanceOperation.Conflict("leave_cancellation_snapshot_mismatch");
                }
                if (row.SupersededByRequestId.HasValue || LeaveRules.Cancelled(row) || row.Approval!.State is ApprovalState.Rejected or ApprovalState.Cancelled) continue;
                if (!row.Request.Days.Any(x => x.Date == date)) continue;
                if (row.Approval.State != ApprovalState.Approved || row.Request.PayTreatment == LeavePayTreatment.PendingReview ||
                    row.Cancellations.Any(x => x.Approval?.State == ApprovalState.Pending))
                { reasons.Add("leave_review_or_treatment_pending"); continue; }
                var decision = row.Decisions.Single();
                if (decision.ProtectedChangeReference is null) throw AttendanceOperation.Conflict("leave_decision_evidence_unavailable");
                var proof = await operation.ReadPrivateAsync<LeaveDecisionProof>(decision.ProtectedChangeReference, LeaveService.DecisionPurpose(row.Request.Id), ct);
                if (proof.RequestId != row.Request.Id || proof.Decision != ApprovalState.Approved || proof.Treatment != row.Request.PayTreatment ||
                    proof.PolicyVersionId is null || proof.PolicyVersionId != row.Request.PolicyVersionId || proof.SubmissionDigest != LeaveProvenance.SubmissionDigest(row.Request) || proof.PolicyRulesJson is null)
                    throw AttendanceOperation.Conflict("leave_decision_evidence_mismatch");
                var settings = LeavePolicySettings.Parse(proof.PolicyRulesJson);
                var current = await repository.PolicyAsync(row.Request.EmployeeId, row.Type.Id, proof.PolicyVersionId.Value,
                    row.Request.StartDate, row.Request.EndDate, ct) ?? throw AttendanceOperation.Conflict("leave_policy_unavailable");
                var version = current.Version;
                if (version.Authority != Cosmix.Domain.HR.Policies.PolicyAuthority.DirectHr || version.ApprovalRequestId is not null ||
                    version.Policy.Category != Cosmix.Domain.HR.Policies.PolicyCategory.Leave || !LeavePolicySettings.SupportsSchema(version.RulesSchemaVersion) ||
                    version.RulesJson != proof.PolicyRulesJson || version.EffectiveFrom > row.Request.StartDate || version.EffectiveTo <= row.Request.EndDate ||
                    proof.Usage is null || proof.Holidays is null || !current.ProvenanceValid || current.StaffCategoryId != proof.StaffCategoryId || current.JoinedOn != proof.JoinedOn)
                    throw AttendanceOperation.Conflict("leave_decision_policy_mismatch");
                LeavePolicySettings.Parse(version.RulesJson, version.RulesSchemaVersion);
                if (!current.Holidays.Order().SequenceEqual(proof.Holidays.Order())) throw AttendanceOperation.Conflict("leave_decision_holiday_mismatch");
                if (proof.StaffCategoryId is null || !settings.EligibleStaffCategoryIds.Contains(proof.StaffCategoryId.Value))
                    throw AttendanceOperation.Conflict("leave_decision_policy_unavailable");
                LeaveTreatmentRules.Validate(row, proof.Treatment, new(version, proof.StaffCategoryId.Value, proof.JoinedOn, true, proof.Holidays, proof.Usage));
                var treatment = row.Request.PayTreatment;
                if (treatment == LeavePayTreatment.DoesNotReduceRegularLeave)
                {
                    if (row.Type.Kind != LeaveKind.SiteInjuryRest || !settings.PaidKinds.Contains(LeaveKind.SiteInjuryRest))
                        throw AttendanceOperation.Conflict("leave_injury_pay_unavailable");
                    treatment = LeavePayTreatment.Paid;
                }
                foreach (var day in row.Request.Days.Where(x => x.Date == date))
                {
                    if (day.ShiftVersionId is null || day.StartsAtUtc >= day.EndsAtUtc || LeaveRules.LocalDate(day.StartsAtUtc) != date ||
                        day.Minutes != (day.EndsAtUtc - day.StartsAtUtc).TotalMinutes) throw AttendanceOperation.Conflict("leave_allocation_unavailable");
                    effects.Add(new(row.Request.Id, day.Id, row.Approval.Id, decision.Id, row.Request.PolicyVersionId, date,
                        day.StartsAtUtc, day.EndsAtUtc, day.Minutes, treatment, day.ShiftVersionId));
                }
            }
            catch (AuthenticationFailure e) when (e.StatusCode is 400 or 409 or 503)
            { conflict = true; reasons.Add("leave_provenance_or_evidence_unavailable"); }
        }
        var ordered = effects.OrderBy(x => x.StartsAtUtc).ThenBy(x => x.AllocationId).ToArray();
        if (ordered.Zip(ordered.Skip(1)).Any(x => x.First.EndsAtUtc > x.Second.StartsAtUtc)) { conflict = true; reasons.Add("approved_leave_overlap"); }
        var status = conflict ? LeaveContextStatus.Conflict : reasons.Count > 0 ? LeaveContextStatus.Pending : LeaveContextStatus.Complete;
        var digest = LeaveProvenance.Digest(new { employeeId, date, status, effects = ordered, reasons = reasons.Distinct().Order(),
            history = rows.OrderBy(x => x.Request.Id).Select(x => new { x.Request.Id, state = x.Approval?.State, x.Request.PayTreatment, x.SupersededByRequestId,
                decisions = x.Decisions.Select(d => d.Id).Order(), cancellations = x.Cancellations.Select(c => new { c.Request.Id, state = c.Approval?.State }) }) });
        return new(status, ordered, digest, reasons.Distinct().Order().ToArray());
    }
}
