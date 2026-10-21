using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Attendance.CarryForward;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.CarryForward;

/// <summary>Append-only factual adjustments to a later unlocked calendar month. Source locks,
/// raw evidence, source summaries and WorkSessions are never rewritten or synthesized.</summary>
public sealed partial class CarryForwardService(AttendanceOperation operation, ICarryForwardRepository repository,
    CarryForwardEvidence evidence, IAttendanceSummaryRepository summaries, ICorrectionReviewRepository review,
    ICarryForwardDutyOverlap overlap, IAccountAccessReader access, IAttendanceCursorProtector cursors,
    IPrivateFileStore files, INotificationOutbox notifications, ICorrectionAlternativeEvidenceReader alternatives,
    IAccessManagementRepository authority, TimeProvider clock)
{
    public Task<CarryForwardSourceResponse> SourceAsync(Guid? employeeId, DateOnly businessDate, Guid? rawPunchId,
        bool self, AuthRequestContext context, CancellationToken ct)
    {
        ValidateDate(businessDate); ValidateEmployee(employeeId, self); if (rawPunchId == Guid.Empty) throw AttendanceOperation.Invalid();
        return operation.ReadAsync(Permission(self), "attendance.carry-forward.source", employeeId, context,
            async (actor, token) => (await CurrentSource(Employee(actor, employeeId, self), businessDate, rawPunchId, token)).Source, ct,
            self: self, resourceScopes: self ? null : async (_, token) => (await CurrentSource(employeeId!.Value, businessDate, rawPunchId, token)).Scopes);
    }
    public Task<CarryForwardRequestResponse> SubmitAsync(Guid? employeeId, SubmitCarryForwardRequest request, string key,
        bool self, AuthRequestContext context, CancellationToken ct)
    {
        ValidateEmployee(employeeId, self); ValidateProposal(request);
        return operation.WriteAsync(Permission(self), "attendance.carry-forward.submit", employeeId, key, request, request.Reason, context,
            async (actor, token) =>
            {
                var employee = Employee(actor, employeeId, self);
                var current = await CurrentSource(employee, request.SourceBusinessDate, request.RawPunchId, token);
                RequireExpected(request, current.Source);
                if (request.ResubmissionOfRequestId is Guid previousId)
                {
                    var previous = await evidence.RequestAsync(employee, previousId, token);
                    if (previous.Snapshot.RequesterAccountId != actor.Identity.AccountId) throw AttendanceOperation.Missing();
                    if (previous.Row.Approval.State != ApprovalState.Returned) throw AttendanceOperation.Conflict("carry_forward_resubmission_requires_returned");
                    if (!self) await RequireAccess(actor, Permission(self), await RequestScopes(employee, previousId, token), token);
                }
                if (await repository.EquivalentPendingAsync(employee, request, token)) throw AttendanceOperation.Conflict("equivalent_carry_forward_pending");
                var submission = Guid.NewGuid();
                var proposed = await Propose(current.Source, request, submission, actor.Now, token);
                var alternative = request.AlternativeKind.HasValue ? await alternatives.CaptureAsync(employee, request.SourceBusinessDate,
                    request.AlternativeKind.Value, request.ExpectedAlternativePolicyId!.Value, request.AlternativeEvidenceDocumentIds, actor.Now, token) : null;
                if (alternative is not null && !CorrectionAlternativePolicyService.Valid(alternative, employee)) throw AttendanceOperation.Conflict("alternative_evidence_unavailable");
                var snapshot = new CarryForwardProposalSnapshot(alternative is not null ? 3 : current.Source.OfficeSource is null ? 1 : 2,
                    submission, employee, actor.Identity.AccountId, request, current.Source, proposed, current.Scopes, alternative);
                var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot), CarryForwardEvidence.ProposalPurpose(employee), token));
                var approval = new ApprovalRequest(AttendanceCarryForwardAdjustment.RequestType, submission, actor.Identity.AccountId.ToString(), reference, 1, actor.Now);
                var original = current.Source.Sessions.SingleOrDefault(x => x.SessionKey == request.TargetSessionKey);
                var correction = new AttendanceCorrection(employee, request.SourceBusinessDate, request.Kind, approval.Id, actor.Identity.AccountId.ToString(), actor.Now,
                    request.Reason.Trim(), reference, originalTimeUtc: request.Kind == AttendanceCorrectionKind.CheckOutTime ? original?.EndsAtUtc : original?.StartsAtUtc,
                    requestedTimeUtc: request.RequestedTimeUtc, requestedEndUtc: request.RequestedEndUtc, rawPunchId: request.RawPunchId, originalSummaryId: current.Source.SourceSummaryId);
                repository.Add(correction, approval); await repository.SaveChangesAsync(token);
                var response = Map(new() { Correction = correction, Approval = approval }, snapshot);
                return new AttendanceChange<CarryForwardRequestResponse>(correction.Id, null, response);
            }, ct, self: self, resourceScopes: self ? null : async (_, token) => (await CurrentSource(employeeId!.Value, request.SourceBusinessDate, request.RawPunchId, token)).Scopes);
    }
    public Task<CarryForwardDetailResponse> DetailAsync(Guid? employeeId, Guid id, bool self, bool review,
        AuthRequestContext context, CancellationToken ct)
    {
        ValidateEmployee(employeeId, self); if (id == Guid.Empty || self && review) throw AttendanceOperation.Invalid();
        return operation.ReadAsync(self ? CorrectionPermissions.Self : review ? CorrectionPermissions.Review : CorrectionPermissions.Submit,
            review ? "attendance.carry-forward.detail.review" : "attendance.carry-forward.detail.requester", employeeId, context,
            async (actor, token) =>
            {
                var (row, saved) = await evidence.RequestAsync(Employee(actor, employeeId, self), id, token);
                if (!review && saved.RequesterAccountId != actor.Identity.AccountId) throw AttendanceOperation.Missing();
                if (review && saved.AlternativeEvidence is not null) await CorrectionAlternativePolicyService.RequireAdmin(actor,
                    CorrectionPermissions.AlternativesDecide, await RequestScopes(employeeId!.Value, id, token), access, authority, clock, token);
                if (row.Adjustment is not null) _ = await evidence.AdjustmentAsync(row.Adjustment, token);
                return new CarryForwardDetailResponse(Map(row, saved), saved.Proposal.WrittenEvidence, saved.Source, saved.ProposedInterpretation, saved.AlternativeEvidence);
            }, ct, self: self, resourceScopes: self ? null : (_, token) => RequestScopes(employeeId!.Value, id, token));
    }
    public Task<CarryForwardDetailResponse> AlternativeDetailAsync(Guid employee, Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync<CarryForwardDetailResponse>(CorrectionPermissions.AlternativesDecide, "attendance.carry-forward.alternative.detail", employee, context,
            async (actor, token) =>
            {
                var (row, saved) = await evidence.RequestAsync(employee, id, token);
                if (saved.AlternativeEvidence is null) throw AttendanceOperation.Missing();
                await CorrectionAlternativePolicyService.RequireAdmin(actor, CorrectionPermissions.AlternativesDecide,
                    await RequestScopes(employee, id, token), access, authority, clock, token);
                if (row.Adjustment is not null) _ = await evidence.AdjustmentAsync(row.Adjustment, token);
                return new(Map(row, saved), saved.Proposal.WrittenEvidence, saved.Source, saved.ProposedInterpretation, saved.AlternativeEvidence);
            }, ct, resourceScopes: (_, token) => RequestScopes(employee, id, token), requireSystemRole: true);
    public Task<CarryForwardRequestResponse> CancelAsync(Guid? employeeId, Guid id, CancelCorrectionRequest request, string key,
        bool self, AuthRequestContext context, CancellationToken ct)
    {
        ValidateEmployee(employeeId, self); if (id == Guid.Empty) throw AttendanceOperation.Invalid();
        var version = SchedulingOperation.Version(request.ExpectedVersion);
        return operation.WriteAsync(Permission(self), "attendance.carry-forward.cancel", employeeId, key, new { id, request.ExpectedVersion }, request.Reason, context,
            async (actor, token) =>
            {
                var (row, saved) = await evidence.RequestAsync(Employee(actor, employeeId, self), id, token);
                if (saved.RequesterAccountId != actor.Identity.AccountId) throw AttendanceOperation.Missing(); RequirePending(row, version);
                var before = Map(row, saved); repository.ExpectVersion(row.Approval, version); row.Approval.Cancel(actor.Identity.AccountId.ToString(), actor.Now);
                await repository.SaveChangesAsync(token); return new AttendanceChange<CarryForwardRequestResponse>(id, before, Map(row, saved));
            }, ct, self: self, resourceScopes: self ? null : (_, token) => RequestScopes(employeeId!.Value, id, token));
    }
    public Task<CarryForwardDecisionResponse> DecideAsync(Guid employeeId, Guid id, DecideCorrectionRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || id == Guid.Empty || request.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected))
            throw AttendanceOperation.Invalid("invalid_carry_forward_decision");
        var version = SchedulingOperation.Version(request.ExpectedVersion);
        if (request.AlternativeKind.HasValue && !Enum.IsDefined(request.AlternativeKind.Value)) throw AttendanceOperation.Invalid("invalid_correction_decision");
        return operation.WriteAsync(request.AlternativeKind.HasValue ? CorrectionPermissions.AlternativesDecide : CorrectionPermissions.Decide, "attendance.carry-forward.decide", employeeId, key, new { id, request }, request.Reason, context,
            async (actor, token) =>
            {
                var (row, saved) = await evidence.RequestAsync(employeeId, id, token); RequirePending(row, version);
                if (saved.Proposal.AlternativeKind != request.AlternativeKind) throw AttendanceOperation.Conflict("alternative_decision_kind_required");
                if (request.AlternativeKind.HasValue && actor.Identity.EmployeeId == employeeId)
                    throw new AuthenticationFailure("correction_beneficiary_cannot_decide", 403);
                if (saved.RequesterAccountId == actor.Identity.AccountId) throw new AuthenticationFailure("correction_requester_cannot_decide", 403);
                var before = Map(row, saved);
                CarryForwardFrozenRows? frozen = null; IReadOnlyList<CarryForwardSession>? after = null; AttendanceCarryForwardAdjustment? prior = null;
                if (request.Decision == ApprovalState.Approved)
                {
                    if (saved.AlternativeEvidence is not null) await alternatives.RevalidateAsync(employeeId, row.Correction.AttendanceDate, saved.AlternativeEvidence, actor.Now, token);
                    var current = await CurrentSource(employeeId, row.Correction.AttendanceDate, row.Correction.RawPunchEventId, token);
                    RequireExpected(saved.Proposal, current.Source);
                    if (current.Source.SourceFingerprint != saved.Source.SourceFingerprint) throw AttendanceOperation.Conflict("carry_forward_source_version_conflict");
                    var proposed = await Propose(current.Source, saved.Proposal, saved.SubmissionId, actor.Now, token);
                    if (!proposed.SequenceEqual(saved.ProposedInterpretation)) throw AttendanceOperation.Conflict("carry_forward_interpretation_changed");
                    frozen = (await evidence.FrozenAsync(employeeId, row.Correction.AttendanceDate, token)).Rows;
                    prior = await repository.LatestAsync(employeeId, row.Correction.AttendanceDate, token);
                    after = proposed.Select(x => x.IsManual && x.OriginCorrectionId is null ? x with { OriginCorrectionId = row.Correction.Id } : x).ToArray();
                }
                if (request.AlternativeKind.HasValue) await CorrectionAlternativePolicyService.RequireAdmin(actor, CorrectionPermissions.AlternativesDecide,
                    await RequestScopes(employeeId, id, token), access, authority, clock, token);
                repository.ExpectVersion(row.Approval, version);
                var decision = row.Approval.Decide(request.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode, request.Reason.Trim(), actor.Now, row.Correction.PrivateEvidenceReference);
                AttendanceCarryForwardAdjustment? adjustment = null;
                if (after is not null && frozen is not null)
                {
                    var input = saved.Source.SourceFingerprint; var fingerprint = CarryForwardEvidence.InterpretationFingerprint(after);
                    var retained = new CarryForwardInterpretationSnapshot(saved.AlternativeEvidence is not null ? 3 : saved.Source.OfficeSource is null ? 1 : 2, employeeId, row.Correction.AttendanceDate, frozen.Lock.Event!.Id, frozen.Summary.Id,
                        row.Correction.Id, decision.Id, checked((prior?.Revision ?? 0) + 1), prior?.Id, saved.Source.TargetMonthStart, saved.Source.TargetMonthEndExclusive,
                        saved.Source.CurrentWorkedTicks, CarryForwardRules.TotalTicks(after), input, fingerprint, actor.Now,
                        frozen.Sessions.Select(x => x.Id).Order().ToArray(), frozen.Sessions.Select(x => x.SessionKey).Order().ToArray(), after, saved.OriginalScopes, saved.AlternativeEvidence);
                    var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(retained), CarryForwardEvidence.InterpretationPurpose(employeeId), token));
                    adjustment = new(row.Correction, row.Approval, decision, frozen.Lock.Period, frozen.Lock.Event!, frozen.Membership, frozen.Summary,
                        frozen.Sessions, prior, saved.Source.TargetMonthStart, after.Select(x => new AttendanceCarryForwardWindow(x.SessionKey, x.StartsAtUtc, x.EndsAtUtc)).ToArray(),
                        input, fingerprint, reference, actor.Now);
                }
                repository.Add(decision, adjustment); await repository.SaveChangesAsync(token);
                await notifications.StageAsync(new(decision.Id, saved.RequesterAccountId, NotificationTemplates.Decision, id), context, token);
                return new AttendanceChange<CarryForwardDecisionResponse>(id, before, new(Map(row with { Adjustment = adjustment }, saved), decision.Id,
                    adjustment?.Id, adjustment?.DeltaWorkedTicks, adjustment is null ? null : CarryForwardRules.MinuteDelta(adjustment.BeforeWorkedTicks, adjustment.AfterWorkedTicks)));
            }, ct, resourceScopes: (_, token) => RequestScopes(employeeId, id, token), beforeReplay: async (actor, token) =>
            {
                var (_, saved) = await evidence.RequestAsync(employeeId, id, token);
                if (saved.Proposal.AlternativeKind != request.AlternativeKind) throw AttendanceOperation.Conflict("alternative_decision_kind_required");
                if (request.AlternativeKind.HasValue && actor.Identity.EmployeeId == employeeId)
                    throw new AuthenticationFailure("correction_beneficiary_cannot_decide", 403);
                if (request.AlternativeKind.HasValue) await CorrectionAlternativePolicyService.RequireAdmin(actor, CorrectionPermissions.AlternativesDecide,
                    await RequestScopes(employeeId, id, token), access, authority, clock, token);
            }, requireSystemRole: request.AlternativeKind.HasValue);
    }

    private async Task<(CarryForwardSourceResponse Source, IReadOnlyList<AttendanceResourceScope> Scopes)> CurrentSource(Guid employee, DateOnly date, Guid? rawId, CancellationToken ct)
    {
        var (frozen, day) = await evidence.FrozenAsync(employee, date, ct);
        var office = await evidence.OfficeSourceAsync(employee, date, day.Roster, frozen.Sessions, ct, day);
        IReadOnlyList<CarryForwardSession> sessions = frozen.Sessions.Select(x => new CarryForwardSession(x.SessionKey, x.Id, x.StartsAtUtc, x.EndsAtUtc!.Value,
            x.SiteId, x.ShiftVersionId, x.CheckInPunchId is null && x.CorrectionId.HasValue, x.CorrectionId, x.CheckInPunchId, x.CheckOutPunchId, office)).ToArray();
        var prior = await repository.LatestAsync(employee, date, ct);
        CarryForwardInterpretationSnapshot? priorEvidence = null;
        if (prior is not null) { priorEvidence = await evidence.AdjustmentAsync(prior, ct); sessions = priorEvidence.Sessions; }
        var target = CarryForwardRules.FirstAvailableMonth(date, await repository.LockedRangesAsync(date, ct));
        CarryForwardRawEvidence? raw = null;
        if (rawId is Guid id)
        {
            raw = await repository.RawAsync(employee, id, ct) ?? throw AttendanceOperation.Missing();
            if (raw.Punch.BusinessDate != date || raw.Punch.EmployeeId != employee || raw.Assessment is null || raw.Assessment.EmployeeId != employee || raw.Assessment.RawPunchEventId != id)
                throw AttendanceOperation.Conflict("carry_forward_raw_source_unavailable");
            if (office is not null && !CorrectionOfficeEvidence.Matches(raw.Punch, office, false))
                throw AttendanceOperation.Conflict("carry_forward_raw_source_unavailable");
        }
        var ticks = CarryForwardRules.TotalTicks(sessions);
        var source = new CarryForwardSourceResponse(employee, date, frozen.Lock.Event!.Id, frozen.Summary.Id, frozen.Summary.Revision, prior?.Id, prior?.Revision ?? 0,
            target.Start, target.EndExclusive, ticks, (int)(ticks / TimeSpan.TicksPerMinute), "", sessions, day.Roster, rawId, raw?.Assessment?.Id, OfficeSource: office);
        source = source with { SourceFingerprint = CarryForwardEvidence.SourceFingerprint(source) };
        var scopes = (await CurrentScopes(employee, date, target.Start, target.EndExclusive, ct)).ToList();
        if (raw is not null && !scopes.SelectMany(x => x.Alternatives).Any(x => x.SiteId == raw.Punch.SiteId))
            scopes.Add(new([new(EmployeeId: employee, SiteId: raw.Punch.SiteId)]));
        return (source, [.. priorEvidence?.OriginalScopes ?? [], .. scopes]);
    }
    private async Task<IReadOnlyList<CarryForwardSession>> Propose(CarryForwardSourceResponse source, SubmitCarryForwardRequest request,
        Guid submission, DateTimeOffset now, CancellationToken ct)
    {
        var roster = source.HistoricalRoster;
        var office = source.OfficeSource;
        if (roster is not { Status: "scheduled", StartsAt: not null, EndsAt: not null, ShiftVersionId: not null } ||
            (office is null ? roster.SiteId is null || roster.BranchId.HasValue : roster.SiteId.HasValue || roster.BranchId != office.BranchId) ||
            roster.EmployeeId != source.EmployeeId || roster.BusinessDate != source.SourceBusinessDate || roster.Explanation?.MembershipRecordId is not Guid membership)
            throw AttendanceOperation.Conflict("carry_forward_historical_schedule_unavailable");
        var result = source.Sessions.ToList(); CarryForwardSession changed;
        if (request.Kind == AttendanceCorrectionKind.ManualSession)
        {
            if (request.TargetSessionKey.HasValue) throw AttendanceOperation.Invalid();
            if (request.RawPunchId is Guid rawId)
            {
                var raw = await repository.RawAsync(source.EmployeeId, rawId, ct) ?? throw AttendanceOperation.Missing();
                if (raw.Punch.Action != PunchAction.CheckIn || raw.Assessment is null || raw.Assessment.Outcome is PunchAssessmentOutcome.Accepted or PunchAssessmentOutcome.AcceptedWithException ||
                    result.Any(x => x.CheckInPunchId == rawId || x.CheckOutPunchId == rawId)) throw AttendanceOperation.Conflict("carry_forward_manual_source_already_interpreted");
                if ((raw.Assessment.ReasonCode == "OUTSIDE_GEOFENCE" || raw.Assessment is { LocationReliable: true, InsideGeofence: false }) &&
                    request.AlternativeKind != CorrectionAlternativeKind.OutOfBoundary) throw AttendanceOperation.Conflict("out_of_boundary_admin_alternative_required");
            }
            changed = new(submission, null, request.RequestedTimeUtc, request.RequestedEndUtc!.Value, roster.SiteId, roster.ShiftVersionId.Value, true, null, null, null, office);
            result.Add(changed);
        }
        else
        {
            var original = result.SingleOrDefault(x => x.SessionKey == request.TargetSessionKey) ?? throw AttendanceOperation.Conflict("carry_forward_target_session_changed");
            if (original.SiteId != roster.SiteId || original.OfficeSource != office || original.ShiftVersionId != roster.ShiftVersionId || request.RawPunchId.HasValue &&
                (request.Kind == AttendanceCorrectionKind.CheckInTime ? original.CheckInPunchId : original.CheckOutPunchId) != request.RawPunchId)
                throw AttendanceOperation.Conflict("carry_forward_original_context_unavailable");
            changed = request.Kind == AttendanceCorrectionKind.CheckInTime ? original with { StartsAtUtc = request.RequestedTimeUtc } : original with { EndsAtUtc = request.RequestedTimeUtc };
            result[result.IndexOf(original)] = changed;
        }
        if (changed.StartsAtUtc < roster.StartsAt || changed.StartsAtUtc >= roster.EndsAt || changed.EndsAtUtc <= changed.StartsAtUtc || changed.EndsAtUtc > now)
            throw AttendanceOperation.Conflict("carry_forward_closed_schedule_window_required");
        if (!await review.HistoricallyEligibleAsync(source.EmployeeId, changed.StartsAtUtc, changed.EndsAtUtc, ct)) throw AttendanceOperation.Conflict("correction_employee_historically_ineligible");
        if (!await review.PreservesAssignmentAsync(source.EmployeeId, membership, roster.SiteId, changed.StartsAtUtc, changed.EndsAtUtc, ct))
            throw AttendanceOperation.Conflict("correction_crosses_site_transfer");
        _ = CarryForwardRules.TotalTicks(result);
        if (CarryForwardEvidence.InterpretationFingerprint(result) == CarryForwardEvidence.InterpretationFingerprint(source.Sessions))
            throw AttendanceOperation.Conflict("carry_forward_no_interpretation_change");
        if (await overlap.HasOverlapAsync(source.EmployeeId, source.Sessions.Select(x => x.SessionKey).ToArray(), changed.StartsAtUtc, changed.EndsAtUtc, ct))
            throw AttendanceOperation.Conflict("carry_forward_session_overlap");
        return result.OrderBy(x => x.StartsAtUtc).ThenBy(x => x.SessionKey).ToArray();
    }

    private async Task<IReadOnlyList<AttendanceResourceScope>> CurrentScopes(Guid employee, DateOnly date, DateOnly targetStart, DateOnly targetEnd, CancellationToken ct) =>
        [.. await summaries.ResourceScopesAsync(employee, date, date, ct), .. await summaries.ResourceScopesAsync(employee, targetStart, targetEnd.AddDays(-1), ct)];
    private async Task<IReadOnlyList<AttendanceResourceScope>> RequestScopes(Guid employee, Guid id, CancellationToken ct)
    {
        var (_, snapshot) = await evidence.RequestAsync(employee, id, ct);
        return [.. snapshot.OriginalScopes, .. await CurrentScopes(employee, snapshot.Source.SourceBusinessDate, snapshot.Source.TargetMonthStart, snapshot.Source.TargetMonthEndExclusive, ct)];
    }
    private async Task RequireAccess(AttendanceActor actor, string permission, IReadOnlyList<AttendanceResourceScope> scopes, CancellationToken ct)
    { if (!AttendanceScopeEvaluation.Allows(actor.Identity, await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, ct), permission, scopes)) throw AttendanceOperation.Denied(); }
    private static string Permission(bool self) => self ? CorrectionPermissions.Self : CorrectionPermissions.Submit;
    private static Guid Employee(AttendanceActor actor, Guid? employee, bool self) => self ? actor.Identity.EmployeeId : employee!.Value;
    private static void ValidateEmployee(Guid? employee, bool self) { if (employee == Guid.Empty || !self && !employee.HasValue) throw AttendanceOperation.Invalid(); }
    private static void ValidateDate(DateOnly date) { if (date == default || date == DateOnly.MaxValue) throw AttendanceOperation.Invalid(); }
    private static void ValidateProposal(SubmitCarryForwardRequest request)
    {
        ValidateDate(request.SourceBusinessDate);
        AttendanceCorrectionService.ValidateAlternative(request.Kind, request.AlternativeKind, request.ExpectedAlternativePolicyId, request.AlternativeEvidenceDocumentIds);
        if (request.Kind is not (AttendanceCorrectionKind.CheckInTime or AttendanceCorrectionKind.CheckOutTime or AttendanceCorrectionKind.ManualSession) ||
            request.TargetSessionKey == Guid.Empty || request.RawPunchId == Guid.Empty || request.ExpectedPriorAdjustmentId == Guid.Empty || request.ResubmissionOfRequestId == Guid.Empty ||
            request.ExpectedSourceLockEventId == Guid.Empty || request.ExpectedSourceSummaryId == Guid.Empty || !MonthLocks.MonthLockEvidence.Hash(request.ExpectedSourceFingerprint) ||
            request.ExpectedTargetMonthStart.Day != 1 || request.RequestedTimeUtc == default || request.RequestedTimeUtc.Offset != TimeSpan.Zero ||
            request.Kind == AttendanceCorrectionKind.ManualSession && (!request.RequestedEndUtc.HasValue || request.TargetSessionKey.HasValue) ||
            request.Kind != AttendanceCorrectionKind.ManualSession && (request.RequestedEndUtc.HasValue || !request.TargetSessionKey.HasValue) ||
            request.RequestedEndUtc.HasValue && (request.RequestedEndUtc.Value.Offset != TimeSpan.Zero || request.RequestedEndUtc <= request.RequestedTimeUtc) ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000 || request.Reason.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(request.WrittenEvidence) || request.WrittenEvidence.Length > 4000 || request.WrittenEvidence.Any(x => char.IsControl(x) && x is not ('\r' or '\n' or '\t')))
            throw AttendanceOperation.Invalid("invalid_carry_forward_proposal");
    }
    private static void RequireExpected(SubmitCarryForwardRequest request, CarryForwardSourceResponse source)
    {
        if (request.ExpectedTargetMonthStart != source.TargetMonthStart) throw AttendanceOperation.Conflict("carry_forward_target_month_changed");
        if (request.ExpectedSourceLockEventId != source.SourceLockEventId || request.ExpectedSourceSummaryId != source.SourceSummaryId || request.ExpectedPriorAdjustmentId != source.PriorAdjustmentId ||
            !string.Equals(request.ExpectedSourceFingerprint, source.SourceFingerprint, StringComparison.OrdinalIgnoreCase)) throw AttendanceOperation.Conflict("carry_forward_source_version_conflict");
    }
    private static void RequirePending(CarryForwardRequestRow row, byte[] version)
    {
        if (!row.Approval.RowVersion.SequenceEqual(version)) throw AttendanceOperation.Conflict("carry_forward_version_conflict");
        if (row.Approval.State != ApprovalState.Pending) throw AttendanceOperation.Conflict("carry_forward_not_pending");
    }
    private static CarryForwardRequestResponse Map(CarryForwardRequestRow row, CarryForwardProposalSnapshot snapshot) =>
        new(row.Correction.Id, row.Correction.EmployeeId, row.Correction.AttendanceDate, row.Correction.Kind.ToString(), row.Approval.State.ToString(),
            Convert.ToBase64String(row.Approval.RowVersion), row.Correction.RequestedAtUtc, snapshot.Source.TargetMonthStart, snapshot.Source.TargetMonthEndExclusive,
            snapshot.Source.SourceLockEventId, snapshot.Source.SourceSummaryId, snapshot.Source.PriorAdjustmentId, row.Adjustment?.Id,
            snapshot.Proposal.AlternativeKind.HasValue ? "one_admin_alternative_reviewer" : "one_hr_reviewer", AlternativeKind: snapshot.Proposal.AlternativeKind);
}
