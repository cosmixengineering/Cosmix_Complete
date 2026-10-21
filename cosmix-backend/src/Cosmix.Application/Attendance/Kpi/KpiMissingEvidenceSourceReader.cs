using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.Policies.Advanced;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.Attendance.Kpi;

public sealed class KpiMissingEvidenceSourceReader(ILeaveRepository leaves, LeaveEvidence evidence, LeaveProvenance provenance,
    AttendanceSummaryService summaries, IAttendanceSummaryRepository scopes, IAdvancedAttendancePolicyReader policies,
    IKpiMissingEvidenceRepository repository, AttendanceOperation operation) : IKpiMissingEvidenceSourceReader
{
    public Task<IReadOnlyList<AttendanceResourceScope>> ScopesAsync(Guid employee, DateOnly from, DateOnly to, CancellationToken ct)
    { KpiMissingEvidenceRules.Range(employee, from, to); return scopes.ResourceScopesAsync(employee, from, to.AddDays(-1), ct); }

    public async Task<IReadOnlyList<(Guid LeaveId, DateOnly Date)>> CandidatesAsync(Guid employee, DateOnly from, DateOnly to, CancellationToken ct)
    {
        KpiMissingEvidenceRules.Range(employee, from, to);
        var rows = await leaves.OverlappingAsync(employee, from, to.AddDays(-1), ct);
        if (rows.Count > 512) throw AttendanceOperation.Conflict("kpi_missing_evidence_leave_limit");
        var days = rows.Where(x => x.Request.EmployeeId == employee && x.SupersededByRequestId is null &&
                !LeaveRules.Cancelled(x) && x.Approval?.State != ApprovalState.Cancelled)
            .SelectMany(x => x.Request.Days.Where(d => d.Date >= from && d.Date < to).Select(d => (LeaveId: x.Request.Id, d.Date)))
            .Distinct().OrderBy(x => x.Date).ThenBy(x => x.LeaveId).Take(4097).ToArray();
        if (days.Length > 4096) throw AttendanceOperation.Conflict("kpi_missing_evidence_candidate_limit");
        return days;
    }

    public async Task<KpiMissingEvidenceSource> ReadAsync(Guid employee, Guid leaveId, DateOnly date, CancellationToken ct)
    {
        KpiMissingEvidenceRules.Range(employee, date, KpiMissingEvidenceRules.End(date));
        if (leaveId == Guid.Empty) throw AttendanceOperation.Invalid("invalid_kpi_missing_leave");
        repository.RequireTransaction();
        var required = KpiMissingEvidenceRules.Scopes(employee, await ScopesAsync(employee, date, date.AddDays(1), ct));
        var row = await leaves.FindAsync(leaveId, employee, ct) ?? throw AttendanceOperation.Missing("leave_not_found");
        var policy = await policies.ReadAsync(employee, AdvancedAttendanceFamily.Kpi, date, ct);
        var day = await summaries.LoadAuthorizedDayAsync(employee, date, ct);
        var attachments = await evidence.ReadAsync(row.Request, ct);
        foreach (var attachment in attachments) await evidence.DownloadAsync(row.Request, attachment.Kind, ct);
        await provenance.VerifyAsync(row, ct);
        var allocation = row.Request.Days.SingleOrDefault(x => x.Date == date);
        var settings = policy.Rules is null ? null : KpiMissingEvidenceRules.Eligibility(policy.Rules);
        var issues = new List<string>(); string? duration = null;
        var evidenceHash = KpiMissingEvidenceRules.Hash(new { row.Request.EvidencePrivateReference,
            Attachments = attachments.OrderBy(x => x.Kind).Select(x => new { x.Kind, x.File, x.ScannerVersion }) });
        var originalDecisionProofs = new List<LeaveDecisionProof>();
        var relatedIds = new List<Guid>();
        if (!Guid.TryParse(row.Request.RequestedByUserId, out var requesterId) ||
            await repository.AccountEmployeeAsync(requesterId, ct) != employee) issues.Add("leave_requester_identity_required");
        foreach (var decision in row.Decisions) {
            if (!Guid.TryParse(decision.ActorUserId, out var actorId) || await repository.AccountEmployeeAsync(actorId, ct) is not Guid actorEmployee ||
                actorEmployee == employee) issues.Add("leave_historical_self_review_or_actor_unavailable");
            if (decision.ProtectedChangeReference is null) { issues.Add("leave_decision_proof_required"); continue; }
            var proof = await operation.ReadPrivateAsync<LeaveDecisionProof>(decision.ProtectedChangeReference, LeaveService.DecisionPurpose(leaveId), ct);
            originalDecisionProofs.Add(proof);
            if (proof.RequestId != leaveId || proof.Decision != decision.Decision || proof.SubmissionDigest != LeaveProvenance.SubmissionDigest(row.Request) ||
                decision.Decision == ApprovalState.Approved && (proof.Treatment != row.Request.PayTreatment || proof.PolicyVersionId != row.Request.PolicyVersionId))
                issues.Add("leave_original_decision_proof_changed");
        }
        if (row.Request.EmployeeId != employee || row.Type.Id != row.Request.LeaveTypeId || !Enum.IsDefined(row.Type.Kind) || row.Type.Kind == LeaveKind.Other ||
            row.Approval is null || !Enum.IsDefined(row.Approval.State))
            issues.Add("leave_typed_current_eligibility_required");
        if (row.Cancellations.Any(x => x.Approval?.State == ApprovalState.Pending)) issues.Add("leave_cancellation_pending");
        foreach (var cancellation in row.Cancellations) {
            relatedIds.Add(cancellation.Request.Id);
            if (!LeaveRules.ApprovalValid(cancellation.Approval, cancellation.Decisions, cancellation.Request.Id,
                    cancellation.Request.RequestedByUserId, LeaveRules.CancellationType)) {
                issues.Add("leave_cancellation_provenance_required"); continue;
            }
            relatedIds.Add(cancellation.Approval!.Id);
            foreach (var decision in cancellation.Decisions) {
                relatedIds.Add(decision.Id);
                if (!Guid.TryParse(decision.ActorUserId, out var actorId) || await repository.AccountEmployeeAsync(actorId, ct) is not Guid actorEmployee ||
                    actorEmployee == employee) issues.Add("leave_cancellation_historical_self_review_or_actor_unavailable");
            }
            var frozen = await operation.ReadPrivateAsync<LeaveService.CancellationSnapshot>(cancellation.Approval!.SnapshotPrivateReference,
                LeaveService.CancellationPurpose(cancellation.Request.Id), ct);
            if (frozen != new LeaveService.CancellationSnapshot(cancellation.Request.Id, leaveId,
                cancellation.Request.RequestedByUserId, cancellation.Request.Reason, row.Approval!.Id))
                issues.Add("leave_cancellation_source_changed");
        }
        if (allocation is null || day.EmployeeId != employee || day.BusinessDate != date || !AttendanceSummaryService.IsSavedRevisionCurrent(day))
            issues.Add("kpi_missing_saved_daily_source_required");
        else {
            LeaveService.ValidateSchedule(allocation, day.Roster);
            if (allocation.ShiftVersionId is null || day.LatestSavedRevision!.ShiftVersionId != allocation.ShiftVersionId)
                issues.Add("kpi_missing_leave_roster_source_changed");
        }
        if (policy.State != "configured" || policy.EmployeeId != employee || policy.BusinessDate != date ||
            policy.Family != AdvancedAttendanceFamily.Kpi || policy.Rules?.Family != AdvancedAttendanceFamily.Kpi ||
            policy.Rules?.Authority?.ReviewPermission != KpiPermissions.Decide || policy.Rules?.Kpi?.MissingEvidenceEffectEnabled is null ||
            policy.PolicyVersionId is null || policy.AssignmentIds.Count == 0) issues.Add("kpi_missing_policy_configuration_required");
        else if (policy.Rules.Kpi.MissingEvidenceEffectEnabled == true && (settings is null || policy.Rules.Kpi.MissingEvidencePoints is not (> 0 and <= 100)))
            issues.Add("kpi_missing_evidence_eligibility_configuration_required");
        long? standardTicks = null; object? standardSource = null;
        if (settings?.DurationBasis == KpiMissingDurationSetting.DatedRosterWindow && day.Roster.StartsAt.HasValue && day.Roster.EndsAt.HasValue)
            standardTicks = (day.Roster.EndsAt.Value - day.Roster.StartsAt.Value).Ticks;
        else if (settings?.DurationBasis == KpiMissingDurationSetting.PublishedLeaveStandardDay) {
            var selected = row.Request.PolicyVersionId.HasValue ? await leaves.PolicyAsync(employee, row.Type.Id,
                row.Request.PolicyVersionId.Value, row.Request.StartDate, row.Request.EndDate, ct) : null;
            if (selected is not { ProvenanceValid: true }) issues.Add("kpi_missing_leave_standard_day_policy_required");
            else {
                var configured = LeavePolicySettings.Parse(selected.Version.RulesJson, selected.Version.RulesSchemaVersion);
                if (!configured.EligibleStaffCategoryIds.Contains(selected.StaffCategoryId)) issues.Add("kpi_missing_leave_standard_day_not_applicable");
                standardTicks = configured.StandardDayMinutes * TimeSpan.TicksPerMinute;
                standardSource = new { selected.Version.Id, selected.Version.RulesJson, selected.Version.RulesSchemaVersion,
                    selected.StaffCategoryId, selected.ProvenanceValid, configured.StandardDayMinutes };
            }
        }
        if (allocation is not null && standardTicks.HasValue) duration = KpiMissingEvidenceRules.Duration((allocation.EndsAtUtc - allocation.StartsAtUtc).Ticks, standardTicks.Value);
        if (policy.Rules?.Kpi?.MissingEvidenceEffectEnabled == true && duration is null or "unavailable")
            issues.Add("kpi_missing_explicit_day_duration_basis_required");
        if (policy.Rules?.Kpi?.MissingEvidenceEffectEnabled == true && row.Type.Kind == LeaveKind.Sick &&
            settings?.Cases?.Contains(KpiMissingCaseSetting.MedicalSickLeave) == true && duration is not ("full_day" or "half_day"))
            issues.Add("kpi_missing_partial_medical_leave_eligibility_required");
        object? correctionSource = null;
        if (row.SupersededByRequestId is { } successorId) {
            var successor = await leaves.FindAsync(successorId, employee, ct);
            if (successor is null || successor.Request.EmployeeId != employee || successor.Request.PreviousRequestId != leaveId ||
                row.Approval?.State != ApprovalState.Returned) issues.Add("leave_correction_link_provenance_required");
            else {
                await provenance.VerifyAsync(successor, ct);
                if (!Guid.TryParse(successor.Request.RequestedByUserId, out var successorRequester) ||
                    await repository.AccountEmployeeAsync(successorRequester, ct) != employee)
                    issues.Add("leave_correction_requester_provenance_required");
                relatedIds.Add(successor.Request.Id); relatedIds.Add(successor.Approval!.Id);
                foreach (var decision in successor.Decisions) {
                    relatedIds.Add(decision.Id);
                    if (!Guid.TryParse(decision.ActorUserId, out var actorId) || await repository.AccountEmployeeAsync(actorId, ct) is not Guid actorEmployee ||
                        actorEmployee == employee || decision.ProtectedChangeReference is null) {
                        issues.Add("leave_correction_decision_provenance_required"); continue;
                    }
                    var proof = await operation.ReadPrivateAsync<LeaveDecisionProof>(decision.ProtectedChangeReference,
                        LeaveService.DecisionPurpose(successor.Request.Id), ct);
                    if (proof.RequestId != successor.Request.Id || proof.Decision != decision.Decision ||
                        proof.SubmissionDigest != LeaveProvenance.SubmissionDigest(successor.Request) ||
                        decision.Decision == ApprovalState.Approved && (proof.Treatment != successor.Request.PayTreatment || proof.PolicyVersionId != successor.Request.PolicyVersionId))
                        issues.Add("leave_correction_decision_source_changed");
                }
                var successorAttachments = await evidence.ReadAsync(successor.Request, ct);
                foreach (var attachment in successorAttachments) await evidence.DownloadAsync(successor.Request, attachment.Kind, ct);
                evidenceHash = KpiMissingEvidenceRules.Hash(new { OriginalEvidenceFingerprint = evidenceHash,
                    CorrectedRequestId = successor.Request.Id, Evidence = successorAttachments.OrderBy(x => x.Kind)
                        .Select(x => new { x.Kind, x.File, x.ScannerVersion }) });
                correctionSource = new { LeaveRequestId = successor.Request.Id, Submission = LeaveProvenance.SubmissionDigest(successor.Request),
                    LeaveVersion = successor.Request.RowVersion, ApprovalId = successor.Approval!.Id, successor.Approval.State, ApprovalVersion = successor.Approval.RowVersion };
            }
        }
        var requiredKind = row.Type.Kind == LeaveKind.Sick ? LeaveEvidenceKind.Medical : LeaveEvidenceKind.Emergency;
        if (attachments.Any(x => !Enum.IsDefined(x.Kind)) || attachments.Select(x => x.Kind).Distinct().Count() != attachments.Count ||
            attachments.Any(x => x.Kind == LeaveEvidenceKind.Other) && !attachments.Any(x => x.Kind == requiredKind))
            issues.Add("leave_evidence_typed_classification_required");
        var hash = KpiMissingEvidenceRules.Hash(new { employee, date, Leave = new {
            row.Request.Id, Submission = LeaveProvenance.SubmissionDigest(row.Request), row.Request.RowVersion,
            row.Request.SubmittedAt, row.Request.PayTreatment, row.Request.PolicyVersionId, row.SupersededByRequestId,
            Type = new { row.Type.Id, row.Type.Kind, row.Type.Code, row.Type.RequiresEvidence, row.Type.RowVersion },
            Approval = row.Approval is { } approval ? new { approval.Id, approval.State, approval.RequestType, approval.SubjectId,
                approval.RequesterUserId, approval.RequestedAt, approval.ClosedAt, approval.RowVersion, approval.SnapshotPrivateReference } : null,
            Decisions = row.Decisions.Select(x => new { x.Id, x.ApprovalRequestId, x.ActorUserId, x.ActorRoleCode, x.Decision,
                x.DecidedAt, x.Reason, x.ProtectedChangeReference }), originalDecisionProofs,
            Cancellations = row.Cancellations.Select(x => new { x.Request.Id, State = x.Approval?.State,
                Version = x.Approval?.RowVersion, Decisions = x.Decisions.Select(d => new { d.Id, d.Decision, d.ActorUserId, d.DecidedAt }) })
        }, day, policy, evidenceHash, standardSource, correctionSource, duration, required, issues });
        string state;
        if (issues.Count > 0) state = "pending";
        else if (row.SupersededByRequestId.HasValue || LeaveRules.Cancelled(row) || row.Approval?.State == ApprovalState.Cancelled) state = "withdrawal_required";
        else if (policy.Rules!.Kpi!.MissingEvidenceEffectEnabled == false) state = "disabled";
        else if (settings!.AllowedLeaveStates!.All(x => x != row.Approval!.State)) state = "ineligible";
        else if (row.Type.Kind == LeaveKind.Sick && settings.Cases!.Contains(KpiMissingCaseSetting.MedicalSickLeave) && duration is "full_day" or "half_day")
            state = attachments.Any(x => x.Kind == LeaveEvidenceKind.Medical) ? "evidence_present" : "eligible_for_review";
        else if (row.Type.Kind == LeaveKind.Emergency && settings.Cases!.Contains(KpiMissingCaseSetting.EmergencyHalfDay) && duration == "half_day")
            state = attachments.Any(x => x.Kind == LeaveEvidenceKind.Emergency) ? "evidence_present" : "eligible_for_review";
        else state = "ineligible";
        return new(state, employee, date, leaveId, allocation?.Id, row.Approval?.Id, row.Approval?.State, row.Request.RequestedByUserId,
            day.LatestSavedRevision?.Id, day.Roster.ShiftVersionId, duration, policy.PolicyVersionId, policy.AssignmentIds,
            policy.Rules?.Kpi?.MissingEvidencePoints, KpiMissingEvidenceRules.Hash(new { state, hash }), evidenceHash, required, issues) {
            RelatedSourceIds = relatedIds.Distinct().Order().ToArray()
        };
    }
}
