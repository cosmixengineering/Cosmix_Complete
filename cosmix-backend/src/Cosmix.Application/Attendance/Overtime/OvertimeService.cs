using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.Overtime;

public sealed partial class OvertimeService(AttendanceOperation operation, IOvertimeRepository repository,
    OvertimeSources sources, IAttendanceSummaryRepository summaries, ICorrectionReviewRepository employment,
    IPrivateFileStore files, IAccountAccessReader access, IAttendanceCursorProtector cursors, INotificationOutbox notifications,
    IWorkTreatmentCreditReader? workCredits = null, ILeaveRecoveryCreditReader? recoveryCredits = null)
{
    public const string RequestType = "attendance.overtime";
    internal static string Purpose(Guid employeeId) => $"attendance:overtime:{employeeId:N}:proposal";
    private static string DecisionPurpose(Guid employeeId, Guid requestId) => $"attendance:overtime:{employeeId:N}:{requestId:N}:decision";

    public Task<OvertimeSourceResponse> SourcesAsync(Guid? employeeId, DateOnly date, bool self, AuthRequestContext context, CancellationToken ct)
    {
        RosterSchedule.ValidateDate(date); RequireTarget(employeeId, self);
        return operation.ReadAsync(self ? OvertimePermissions.Self : OvertimePermissions.Submit, "attendance.overtime.sources", employeeId, context,
            async (actor, token) =>
            {
                var source = (await sources.LoadAsync(employeeId ?? actor.Identity.EmployeeId, date, token)).Source;
                await RequireWorkAvailable(source.EmployeeId, date, source.DurationPolicy.EligibleIntervals, token);
                return source;
            }, ct,
            self: self, resourceScopes: self ? null : (_, token) => summaries.ResourceScopesAsync(employeeId!.Value, date, date, token));
    }
    public Task<OvertimeResponse> SubmitAsync(Guid? employeeId, SubmitOvertimeRequest request, string key, bool self, AuthRequestContext context, CancellationToken ct)
    {
        RequireTarget(employeeId, self); OvertimeRules.Validate(request);
        return operation.WriteAsync(self ? OvertimePermissions.Self : OvertimePermissions.Submit, "attendance.overtime.submit", employeeId, key,
            request, request.Reason, context, async (actor, token) =>
            {
                var id = employeeId ?? actor.Identity.EmployeeId;
                var source = await PrepareSubmission(id, request, actor, self, token);
                var row = await StageSubmission(id, request, source, actor, null, token);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<OvertimeResponse>(row.Request.Id, null, Map(row, null));
            }, ct, self: self, resourceScopes: self ? null : (_, token) => summaries.ResourceScopesAsync(employeeId!.Value, request.BusinessDate, request.BusinessDate, token),
            beforeReplay: async (actor, token) =>
            {
                await sources.ValidateSubmissionReplayAsync(employeeId ?? actor.Identity.EmployeeId, request, token);
                await RequireWorkAvailable(employeeId ?? actor.Identity.EmployeeId, request.BusinessDate,
                    [new(request.StartsAtUtc, request.EndsAtUtc)], token);
            });
    }
    public Task<OvertimeDetail> DetailAsync(Guid? employeeId, Guid id, bool self, bool requesterOnly, AuthRequestContext context, CancellationToken ct)
    {
        RequireTarget(employeeId, self); if (id == Guid.Empty) throw AttendanceOperation.Invalid();
        var permission = self ? OvertimePermissions.Self : requesterOnly ? OvertimePermissions.Submit : OvertimePermissions.Review;
        return operation.ReadAsync(permission, "attendance.overtime.detail", employeeId, context, async (actor, token) =>
        {
            var data = await Evidence(employeeId ?? actor.Identity.EmployeeId, id, token);
            if (!self && requesterOnly && data.Snapshot.RequesterAccountId != actor.Identity.AccountId) throw AttendanceOperation.Missing();
            var canReadEvidence = !self || data.Snapshot.RequesterAccountId == actor.Identity.AccountId;
            return new OvertimeDetail(Map(data.Row, data.Decision), data.Snapshot.Inputs.Source,
                canReadEvidence ? data.Snapshot.Proposal.WrittenEvidence : null, canReadEvidence ? "authorized" : "requester_only");
        }, ct, self: self, resourceScopes: self ? null : (_, token) => ReviewScopes(employeeId!.Value, id, token));
    }
    public Task<OvertimeResponse> CancelAsync(Guid? employeeId, Guid id, CancelOvertimeRequest request, string key, bool self, AuthRequestContext context, CancellationToken ct)
    {
        RequireTarget(employeeId, self); var version = SchedulingOperation.Version(request.ExpectedVersion);
        return operation.WriteAsync(self ? OvertimePermissions.Self : OvertimePermissions.Submit, "attendance.overtime.cancel", employeeId, key,
            new { id, request }, request.Reason, context, async (actor, token) =>
            {
                var data = await Evidence(employeeId ?? actor.Identity.EmployeeId, id, token);
                if (data.Snapshot.RequesterAccountId != actor.Identity.AccountId) throw AttendanceOperation.Missing();
                RequirePending(data.Row, version); var before = Map(data.Row, data.Decision);
                repository.ExpectVersion(data.Row.Approval, version); data.Row.Approval.Cancel(actor.Identity.AccountId.ToString(), actor.Now);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<OvertimeResponse>(id, before, Map(data.Row, null));
            }, ct, self: self, resourceScopes: self ? null : (_, token) => ReviewScopes(employeeId!.Value, id, token));
    }
    public Task<OvertimeResponse> DecideAsync(Guid employeeId, Guid id, DecideOvertimeRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        RequireTarget(employeeId, false); ValidateDecision(request);
        return operation.WriteAsync(OvertimePermissions.Decide, "attendance.overtime.decide", employeeId, key,
            new { id, request }, request.Reason, context, async (actor, token) =>
            {
                var prepared = await PrepareDecision(employeeId, id, request, actor, token);
                var before = Map(prepared.Data.Row, prepared.Data.Decision);
                var result = await StageDecision(prepared, request, actor, context, token);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<OvertimeResponse>(id, before, result with { Version = Convert.ToBase64String(prepared.Data.Row.Approval.RowVersion) });
            }, ct, resourceScopes: (_, token) => ReviewScopes(employeeId, id, token),
            beforeReplay: async (_, token) =>
            {
                await sources.RevalidateTaxonomyAsync((await Evidence(employeeId, id, token)).Snapshot.Inputs, token);
                await ValidateApprovedWorkReplayAsync(employeeId, id, token);
            });
    }
    public Task<OvertimeFinancialResponse> FinancialAsync(Guid? employeeId, Guid id, bool self, AuthRequestContext context, CancellationToken ct)
    {
        RequireTarget(employeeId, self);
        return operation.ReadAsync(self ? OvertimePermissions.FinancialSelf : OvertimePermissions.FinancialRead,
            "attendance.overtime.financial.read", employeeId, context, async (actor, token) =>
            {
                var data = await Evidence(employeeId ?? actor.Identity.EmployeeId, id, token);
                var financial = data.Decision?.FinancialEvidence;
                return new OvertimeFinancialResponse(id, data.Row.Request.EmployeeId, data.Row.BusinessDate,
                    financial is null ? "not_awarded" : "calculated_pending_payroll", data.Decision?.ApprovedTicks,
                    financial?.Amount, financial?.Currency, financial?.FormulaVersion,
                    self ? null : MapFinancialAward(data));
            }, ct, self: self, resourceScopes: self ? null : (_, token) => ReviewScopes(employeeId!.Value, id, token));
    }

    private async Task<OvertimeSourceSnapshot> PrepareSubmission(Guid employeeId, SubmitOvertimeRequest request, AttendanceActor actor, bool self, CancellationToken ct)
    {
        var source = await sources.LoadAsync(employeeId, request.BusinessDate, ct);
        if (!string.Equals(source.Source.SourceFingerprint, request.ExpectedSourceFingerprint, StringComparison.OrdinalIgnoreCase))
            throw AttendanceOperation.Conflict("overtime_source_changed");
        var selected = OvertimeRules.Selected(source.Source, request, actor.Now);
        if (!await employment.HistoricallyEligibleAsync(employeeId, selected.StartsAtUtc, selected.EndsAtUtc!.Value, ct))
            throw AttendanceOperation.Conflict("overtime_historical_employment_unavailable");
        if (!await employment.PreservesAssignmentAsync(employeeId, source.Employment.AssignmentId, selected.SiteId,
            selected.StartsAtUtc, selected.EndsAtUtc.Value, ct)) throw AttendanceOperation.Conflict("overtime_site_transfer_conflict");
        if (await repository.HasConflictingRequestAsync(employeeId, request.StartsAtUtc, request.EndsAtUtc, null, ct))
            throw AttendanceOperation.Conflict("overtime_interval_already_requested");
        await RequireWorkAvailable(employeeId, request.BusinessDate, [new(request.StartsAtUtc, request.EndsAtUtc)], ct);
        if (request.ResubmissionOfRequestId.HasValue)
        {
            var previous = await Evidence(employeeId, request.ResubmissionOfRequestId.Value, ct);
            if (previous.Snapshot.RequesterAccountId != actor.Identity.AccountId || previous.Row.Approval.State != ApprovalState.Returned)
                throw AttendanceOperation.Conflict("overtime_resubmission_requires_own_returned_request");
            var grants = await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, ct);
            if (!self && !AttendanceScopeEvaluation.Allows(actor.Identity, grants, OvertimePermissions.Submit,
                await ReviewScopes(employeeId, previous.Row.Request.Id, ct))) throw AttendanceOperation.Denied();
        }
        return source;
    }
    private async Task<OvertimeRecord> StageSubmission(Guid employeeId, SubmitOvertimeRequest request, OvertimeSourceSnapshot inputs,
        AttendanceActor actor, Guid? bulkId, CancellationToken ct)
    {
        var submissionId = Guid.NewGuid();
        var snapshot = new OvertimeSubmissionSnapshot(1, submissionId, employeeId, actor.Identity.AccountId, request, inputs);
        var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot), Purpose(employeeId), ct));
        await sources.RevalidateTaxonomyAsync(inputs, ct);
        await RequireWorkAvailable(employeeId, request.BusinessDate, [new(request.StartsAtUtc, request.EndsAtUtc)], ct);
        var approval = new ApprovalRequest(RequestType, submissionId, actor.Identity.AccountId.ToString(), reference, 1, actor.Now);
        var requestRow = new OvertimeRequest(employeeId, request.WorkSessionId, request.StartsAtUtc, request.EndsAtUtc,
            OvertimeRules.RequestedMinutes(request), inputs.Source.DurationPolicy.PolicyVersionId, inputs.Source.DurationPolicy.RateRuleCode,
            request.Reason.Trim(), reference, actor.Identity.AccountId.ToString(), actor.Now, approval.Id, inputs.Source.SiteId, request.JobReference, bulkId);
        repository.Add(requestRow, approval);
        return new() { Request = requestRow, Approval = approval, BusinessDate = request.BusinessDate };
    }
    private async Task<PreparedDecision> PrepareDecision(Guid employeeId, Guid id, DecideOvertimeRequest request, AttendanceActor actor, CancellationToken ct)
    {
        var data = await Evidence(employeeId, id, ct); RequirePending(data.Row, SchedulingOperation.Version(request.ExpectedVersion));
        if (data.Snapshot.RequesterAccountId == actor.Identity.AccountId || actor.Identity.EmployeeId == employeeId)
            throw new AuthenticationFailure("overtime_self_decision_forbidden", 403);
        int? minutes = null; long? ticks = null; OvertimeFinancialEvidence? financial = null;
        if (request.Decision == ApprovalState.Approved)
        {
            var current = await sources.LoadAsync(employeeId, data.Row.BusinessDate, ct);
            if (current.Source.SourceFingerprint != data.Snapshot.Inputs.Source.SourceFingerprint) throw AttendanceOperation.Conflict("overtime_source_changed");
            ticks = OvertimeRules.ApprovedTicks(current.Source, data.Snapshot.Proposal, actor.Now);
            minutes = checked((int)(ticks.Value / TimeSpan.TicksPerMinute));
            var selected = current.Source.Sessions.Single(x => x.Id == data.Row.Request.WorkSessionId);
            if (!await employment.HistoricallyEligibleAsync(employeeId, selected.StartsAtUtc, selected.EndsAtUtc!.Value, ct))
                throw AttendanceOperation.Conflict("overtime_historical_employment_unavailable");
            if (!await employment.PreservesAssignmentAsync(employeeId, current.Employment.AssignmentId, selected.SiteId,
                selected.StartsAtUtc, selected.EndsAtUtc.Value, ct)) throw AttendanceOperation.Conflict("overtime_site_transfer_conflict");
            if (await repository.HasConflictingRequestAsync(employeeId, data.Row.Request.StartsAtUtc, data.Row.Request.EndsAtUtc, id, ct))
                throw AttendanceOperation.Conflict("overtime_interval_already_requested");
            OvertimeFinancialAwardRules.RequireSupportedFormula(current.Source);
            await RequireApprovedWorkAvailable(data, ticks.Value, ct);
            financial = await sources.CalculateAmountAsync(current, ticks.Value, ct);
        }
        return new(data, minutes, ticks, financial);
    }
    private async Task<OvertimeResponse> StageDecision(PreparedDecision prepared, DecideOvertimeRequest request, AttendanceActor actor,
        AuthRequestContext context, CancellationToken ct)
    {
        var (row, snapshot, _) = prepared.Data;
        var policy = snapshot.Inputs.Source.DurationPolicy;
        var approved = request.Decision == ApprovalState.Approved;
        var output = new OvertimeDecisionSnapshot(1, row.Request.Id, row.Request.EmployeeId, snapshot.Inputs.Source.SourceFingerprint,
            request.Decision, prepared.ApprovedMinutes, approved ? policy.PolicyVersionId : null, approved ? policy.CalculationVersion : null,
            approved ? snapshot.Inputs.Source.Eligibility.VersionId : null, approved ? snapshot.Inputs.CompensationRecordId : null,
            approved ? policy.RateRuleCode : null, approved ? "calculated_pending_payroll" : "not_awarded", prepared.ApprovedTicks, prepared.Financial);
        var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(output), DecisionPurpose(row.Request.EmployeeId, row.Request.Id), ct));
        await sources.RevalidateTaxonomyAsync(snapshot.Inputs, ct);
        if (approved) await RequireApprovedWorkAvailable(prepared.Data, prepared.Financial!.ApprovedTicks, ct);
        repository.ExpectVersion(row.Approval, SchedulingOperation.Version(request.ExpectedVersion));
        var decision = row.Approval.Decide(request.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode, request.Reason.Trim(), actor.Now, reference);
        repository.Add(decision);
        await notifications.StageAsync(new NotificationIntent(decision.Id, snapshot.RequesterAccountId, NotificationTemplates.Decision, row.Request.Id), context, ct);
        return Map(row with { Decision = decision }, output);
    }

    private async Task<EvidenceState> Evidence(Guid employeeId, Guid id, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || id == Guid.Empty) throw AttendanceOperation.Invalid();
        var row = await repository.FindAsync(employeeId, id, ct) ?? throw AttendanceOperation.Missing();
        var snapshot = await operation.ReadPrivateAsync<OvertimeSubmissionSnapshot>(row.Request.PrivateEvidenceReference, Purpose(employeeId), ct);
        var p = snapshot.Proposal; var source = snapshot.Inputs.Source;
        OvertimeTaxonomy.ValidateRetained(snapshot.Inputs);
        if (snapshot.SchemaVersion != 1 || snapshot.EmployeeId != employeeId || source.EmployeeId != employeeId ||
            snapshot.SubmissionId != row.Approval.SubjectId || snapshot.RequesterAccountId.ToString() != row.Approval.RequesterUserId ||
            row.Request.RequestedBy != row.Approval.RequesterUserId || row.Request.ApprovalRequestId != row.Approval.Id ||
            row.Approval.RequestType != RequestType || row.Approval.Stage != 1 || row.Request.PrivateEvidenceReference != row.Approval.SnapshotPrivateReference ||
            row.Request.RequestedAtUtc != row.Approval.RequestedAt || row.BusinessDate != p.BusinessDate || source.BusinessDate != p.BusinessDate ||
            p.WorkSessionId != row.Request.WorkSessionId || p.StartsAtUtc != row.Request.StartsAtUtc || p.EndsAtUtc != row.Request.EndsAtUtc ||
            OvertimeRules.RequestedMinutes(p) != row.Request.RequestedMinutes || p.Reason.Trim() != row.Request.Reason || p.JobReference?.Trim() != row.Request.JobReference ||
            row.Request.SiteId != source.SiteId || row.Request.PolicyVersionId != source.DurationPolicy.PolicyVersionId || row.Request.RateRuleCode != source.DurationPolicy.RateRuleCode ||
            !AttendanceScopeEvaluation.IsValid(snapshot.Inputs.OriginalScopes) || snapshot.Inputs.OriginalScopes.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId != employeeId) ||
            !string.Equals(p.ExpectedSourceFingerprint, source.SourceFingerprint, StringComparison.OrdinalIgnoreCase) || OvertimeRules.Fingerprint(snapshot.Inputs) != source.SourceFingerprint)
            throw AttendanceOperation.Conflict("overtime_evidence_unavailable");
        OvertimeDecisionSnapshot? output = null;
        if (row.Approval.State is ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected)
        {
            var decision = row.Decision;
            if (decision is null || decision.ApprovalRequestId != row.Approval.Id || decision.Decision != row.Approval.State ||
                decision.DecidedAt != row.Approval.ClosedAt || decision.ActorUserId == row.Approval.RequesterUserId || decision.ProtectedChangeReference is null)
                throw AttendanceOperation.Conflict("overtime_decision_evidence_unavailable");
            output = await operation.ReadPrivateAsync<OvertimeDecisionSnapshot>(decision.ProtectedChangeReference, DecisionPurpose(employeeId, id), ct);
            if (output.SchemaVersion != 1 || output.RequestId != id || output.EmployeeId != employeeId || output.Decision != decision.Decision ||
                output.SourceFingerprint != source.SourceFingerprint ||
                (decision.Decision == ApprovalState.Approved ? output.ApprovedMinutes != OvertimeRules.ApprovedMinutes(source, p, decision.DecidedAt) ||
                    output.PolicyVersionId != source.DurationPolicy.PolicyVersionId || output.CalculationVersion != source.DurationPolicy.CalculationVersion ||
                    output.EligibilityVersionId != source.Eligibility.VersionId || output.CompensationRecordId != snapshot.Inputs.CompensationRecordId || output.RateRuleCode != source.DurationPolicy.RateRuleCode ||
                    output.ApprovedTicks != OvertimeRules.ApprovedTicks(source, p, decision.DecidedAt) || !ValidFinancial(output, snapshot.Inputs) :
                    output.ApprovedMinutes.HasValue || output.ApprovedTicks.HasValue || output.FinancialEvidence is not null || output.FinancialState != "not_awarded" ||
                    output.PolicyVersionId.HasValue || output.CalculationVersion is not null || output.EligibilityVersionId.HasValue || output.CompensationRecordId.HasValue || output.RateRuleCode is not null))
                throw AttendanceOperation.Conflict("overtime_decision_evidence_unavailable");
        }
        else if (row.Decision is not null) throw AttendanceOperation.Conflict("overtime_decision_evidence_unavailable");
        return new(row, snapshot, output);
    }
    private async Task<IReadOnlyList<AttendanceResourceScope>> ReviewScopes(Guid employeeId, Guid id, CancellationToken ct)
    {
        var state = await Evidence(employeeId, id, ct);
        return [.. state.Snapshot.Inputs.OriginalScopes, .. await summaries.ResourceScopesAsync(employeeId, state.Row.BusinessDate, state.Row.BusinessDate, ct)];
    }
    private async Task ValidateApprovedWorkReplayAsync(Guid employeeId, Guid id, CancellationToken ct)
    {
        if (workCredits is null) return;
        var data = await Evidence(employeeId, id, ct);
        if (data.Row.Approval.State == ApprovalState.Approved)
            await RequireApprovedWorkAvailable(data, data.Decision!.FinancialEvidence!.ApprovedTicks, ct);
    }
    private Task RequireApprovedWorkAvailable(EvidenceState data, long approvedTicks, CancellationToken ct)
    {
        var proposal = data.Snapshot.Proposal;
        var approved = OvertimeFinancialAwardRules.ApprovedIntervals(data.Snapshot.Inputs.Source, proposal);
        if (approved.Sum(x => (x.EndsAtUtc - x.StartsAtUtc).Ticks) != approvedTicks)
            throw AttendanceOperation.Conflict("overtime_decision_evidence_unavailable");
        // The full claim remains reserved even when a helper awards only disjoint subsets.
        return RequireWorkAvailable(data.Row.Request.EmployeeId, data.Row.BusinessDate,
            [.. approved, new(proposal.StartsAtUtc, proposal.EndsAtUtc)], ct);
    }
    private async Task RequireWorkAvailable(Guid employeeId, DateOnly date,
        IReadOnlyList<OvertimeEligibleInterval> intervals, CancellationToken ct)
    {
        if (intervals.Count == 0) return;
        if (recoveryCredits is null) throw AttendanceOperation.Conflict("leave_recovery_allocation_reader_unavailable");
        // Retained physical ranges remain exclusive even across different duty business dates.
        OvertimeFinancialAwardRules.RequireUnallocated(employeeId, intervals, await recoveryCredits.ReadReservedAsync(employeeId, ct));
        if (workCredits is not null)
            OvertimeFinancialAwardRules.RequireUnallocated(employeeId, intervals, await workCredits.ReadAsync(employeeId, date, date, ct));
    }
    private static OvertimeResponse Map(OvertimeRecord row, OvertimeDecisionSnapshot? decision) => new(row.Request.Id,
        row.Request.EmployeeId, row.BusinessDate, row.Request.WorkSessionId, row.Request.StartsAtUtc, row.Request.EndsAtUtc,
        row.Request.RequestedMinutes, decision?.ApprovedMinutes, row.Request.SiteId, row.Request.JobReference, row.Approval.State.ToString(),
        Convert.ToBase64String(row.Approval.RowVersion), row.Request.RequestedAtUtc, row.Request.BulkSubmissionId, row.Decision?.Id,
        decision?.ApprovedMinutes.HasValue == true ? "approved_work_minutes" : row.Approval.State == ApprovalState.Pending ? "awaiting_hr_review" : "not_awarded",
        decision?.FinancialEvidence is null ? "not_awarded" : "calculated_restricted", null, decision?.ApprovedTicks);
    private static bool ValidFinancial(OvertimeDecisionSnapshot decision, OvertimeSourceSnapshot inputs)
    {
        var value = decision.FinancialEvidence;
        return decision.FinancialState == "calculated_pending_payroll" && value is not null && value.FormulaVersion == inputs.Source.DurationPolicy.RateRuleCode &&
            value.CompensationRecordId == inputs.CompensationRecordId && value.SourceBusinessDate == inputs.Source.BusinessDate &&
            value.SalaryCalculationDays == ConfiguredOvertimeFormula.RetainedDivisorDays(inputs.Source) &&
            value.ApprovedTicks == decision.ApprovedTicks && value.Currency == "PKR" && value.MonthlyBaseSalary >= 0 &&
            value.Amount == ConfiguredOvertimeFormula.RetainedAmount(inputs.Source, value.MonthlyBaseSalary, value.ApprovedTicks);
    }
    private static void RequireTarget(Guid? employeeId, bool self)
    { if (employeeId == Guid.Empty || self == employeeId.HasValue) throw AttendanceOperation.Invalid(); }
    private static void ValidateDecision(DecideOvertimeRequest request)
    {
        if (request.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected)) throw AttendanceOperation.Invalid("invalid_overtime_decision");
        SchedulingOperation.Version(request.ExpectedVersion); OvertimeRules.Text(request.Reason, 1000);
    }
    private static void RequirePending(OvertimeRecord row, byte[] version)
    {
        if (!row.Approval.RowVersion.SequenceEqual(version)) throw AttendanceOperation.Conflict("overtime_version_conflict");
        if (row.Approval.State != ApprovalState.Pending) throw AttendanceOperation.Conflict("overtime_request_closed");
    }
    private sealed record EvidenceState(OvertimeRecord Row, OvertimeSubmissionSnapshot Snapshot, OvertimeDecisionSnapshot? Decision);
    private sealed record PreparedDecision(EvidenceState Data, int? ApprovedMinutes, long? ApprovedTicks, OvertimeFinancialEvidence? Financial);
}
