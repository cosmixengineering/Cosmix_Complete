using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.CarryForward;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.Corrections;

/// <summary>One permission-configured HR reviewer, with immutable valid-duty interpretation.
/// No role name, automatic grant, manual Check-In exception or payroll rule is inferred.</summary>
public sealed class CorrectionReviewService(AttendanceOperation operation, IAttendanceCorrectionRepository repository,
    ICorrectionReviewRepository review, AttendanceCorrectionService intake, IAttendanceSummaryRepository summaries,
    AttendanceSummaryService calculator, IIndividualRosterResolver rosters, IAccountAccessReader access,
    IAttendanceCursorProtector cursors, INotificationOutbox notifications, ICarryForwardDutyOverlap dutyOverlap,
    IOfficeAttendanceSourceReader offices, ICorrectionAlternativeEvidenceReader alternatives, IAccessManagementRepository authority, TimeProvider clock,
    AlternativeCorrectionApprovalJournal alternativeApprovals)
{
    public Task<CorrectionSourceResponse> SourcesAsync(Guid employeeId, DateOnly date, Guid? punchId, AuthRequestContext context, CancellationToken ct)
    {
        ValidateDate(date); if (employeeId == Guid.Empty || punchId == Guid.Empty) throw AttendanceOperation.Invalid();
        return operation.ReadAsync(CorrectionPermissions.Submit, "attendance.correction.sources.employee", employeeId, context,
            async (actor, token) =>
            {
                await intake.RequireHistoricalDate(employeeId, date, actor.Now, token);
                return (await CurrentSource(employeeId, date, punchId, token)).Source;
            }, ct, resourceScopes: async (_, token) => (await CurrentSource(employeeId, date, punchId, token)).Scopes);
    }

    public Task<CorrectionResponse> SubmitAsync(Guid employeeId, CreateCorrectionRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        AttendanceCorrectionService.ValidateProposal(request);
        if (employeeId == Guid.Empty || request.ExpectedSourceFingerprint is null) throw AttendanceOperation.Invalid("correction_source_fingerprint_required");
        return operation.WriteAsync(CorrectionPermissions.Submit, "attendance.correction.submit.employee", employeeId, key, request,
            request.Reason, context, (actor, token) => intake.SubmitAuthorizedAsync(employeeId, request, actor, token), ct,
            resourceScopes: async (_, token) => (await CurrentSource(employeeId, request.BusinessDate, request.RawPunchId, token)).Scopes);
    }

    public Task<CorrectionDetailResponse> DetailAsync(Guid employeeId, Guid id, bool ownRequest, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(ownRequest ? CorrectionPermissions.Submit : CorrectionPermissions.Review,
            ownRequest ? "attendance.correction.detail.requester" : "attendance.correction.detail.review", employeeId, context,
            async (actor, token) =>
            {
                var (row, snapshot) = await Evidence(employeeId, id, token);
                if (ownRequest && snapshot.RequesterAccountId != actor.Identity.AccountId) throw AttendanceOperation.Missing();
                if (!ownRequest && snapshot.AlternativeEvidence is not null) await CorrectionAlternativePolicyService.RequireAdmin(actor,
                    CorrectionPermissions.AlternativesDecide, await ReviewScopes(employeeId, id, token), access, authority, clock, token);
                return new CorrectionDetailResponse(AttendanceCorrectionService.Map(row, snapshot.Proposal.AlternativeKind), snapshot.Proposal.WrittenEvidence, snapshot.Source, snapshot.AlternativeEvidence);
            }, ct, resourceScopes: (_, token) => ReviewScopes(employeeId, id, token));

    public Task<CorrectionQueueResponse> OwnHistoryAsync(Guid employeeId, CorrectionQueueRequest request, AuthRequestContext context, CancellationToken ct)
    {
        if (request.Scope != CorrectionQueueScope.Employee || request.TargetId != employeeId) throw AttendanceOperation.Invalid("invalid_correction_queue");
        return QueueCore(request, true, context, ct);
    }
    public Task<CorrectionResponse> CancelAsync(Guid employeeId, Guid id, CancelCorrectionRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        var version = SchedulingOperation.Version(request.ExpectedVersion);
        return operation.WriteAsync(CorrectionPermissions.Submit, "attendance.correction.cancel.requester", employeeId, key,
            new { id, request.ExpectedVersion }, request.Reason, context, async (actor, token) =>
            {
                var (row, snapshot) = await Evidence(employeeId, id, token);
                if (snapshot.RequesterAccountId != actor.Identity.AccountId) throw AttendanceOperation.Missing();
                RequirePending(row, version); var before = AttendanceCorrectionService.Map(row);
                repository.ExpectVersion(row.Approval, version); row.Approval.Cancel(actor.Identity.AccountId.ToString(), actor.Now);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<CorrectionResponse>(id, before, AttendanceCorrectionService.Map(row));
            }, ct, resourceScopes: (_, token) => ReviewScopes(employeeId, id, token));
    }

    public Task<CorrectionQueueResponse> QueueAsync(CorrectionQueueRequest request, AuthRequestContext context, CancellationToken ct) => QueueCore(request, false, context, ct);
    public Task<CorrectionQueueResponse> AlternativeQueueAsync(CorrectionQueueRequest request, AuthRequestContext context, CancellationToken ct) => QueueCore(request, false, context, ct, true);
    public Task<CorrectionDetailResponse> AlternativeDetailAsync(Guid employeeId, Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync<CorrectionDetailResponse>(CorrectionPermissions.AlternativesDecide, "attendance.correction.alternative.detail", employeeId, context,
            async (actor, token) =>
            {
                var (row, saved) = await Evidence(employeeId, id, token);
                if (saved.AlternativeEvidence is null) throw AttendanceOperation.Missing();
                await CorrectionAlternativePolicyService.RequireAdmin(actor, CorrectionPermissions.AlternativesDecide,
                    await ReviewScopes(employeeId, id, token), access, authority, clock, token);
                return new(AttendanceCorrectionService.Map(row, saved.Proposal.AlternativeKind), saved.Proposal.WrittenEvidence, saved.Source, saved.AlternativeEvidence);
            }, ct, resourceScopes: (_, token) => ReviewScopes(employeeId, id, token), requireSystemRole: true);

    private Task<CorrectionQueueResponse> QueueCore(CorrectionQueueRequest request, bool own, AuthRequestContext context, CancellationToken ct, bool alternative = false)
    {
        ValidateRange(request.From, request.To);
        if (!Enum.IsDefined(request.Scope) || !Enum.IsDefined(request.State) || request.PageSize is < 1 or > 100 ||
            request.TargetId == Guid.Empty || (request.Scope == CorrectionQueueScope.All) != !request.TargetId.HasValue)
            throw AttendanceOperation.Invalid("invalid_correction_queue");
        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request with { Cursor = null })));
        var permission = own ? CorrectionPermissions.Submit : alternative ? CorrectionPermissions.AlternativesDecide : CorrectionPermissions.Review;
        var purpose = own ? "attendance.corrections.requester" : alternative ? "attendance.corrections.alternative.review" : "attendance.corrections.review";
        return operation.ReadAsync(permission, purpose, null, context, async (actor, token) =>
        {
            if (alternative && actor.RoleCode != "ADMIN") throw AttendanceOperation.Denied();
            var position = request.Cursor is null ? null : cursors.Unprotect(request.Cursor, actor.Identity.AccountId, purpose, digest);
            const int scanLimit = 200;
            var rows = await review.CandidatesAsync(request, position, scanLimit + 1, token, own ? actor.Identity.AccountId : null);
            var grants = await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, token);
            var result = new List<CorrectionResponse>(); var scanned = 0;
            foreach (var row in rows.Take(scanLimit))
            {
                scanned++; position = new(row.Correction.RequestedAtUtc, row.Correction.Id);
                var (_, evidence) = await Evidence(row.Correction.EmployeeId, row.Correction.Id, token);
                if (!own && alternative != (evidence.AlternativeEvidence is not null)) continue;
                // Legacy intake without original scope proof needs owner cancellation/re-submission.
                // It must never acquire a guessed original authorization path.
                if (evidence.OriginalResourceScopes is null) continue;
                var rowScopes = await ReviewScopes(row.Correction.EmployeeId, row.Correction.Id, token);
                if (AttendanceScopeEvaluation.Allows(actor.Identity, grants, permission, rowScopes))
                {
                    if (alternative) await CorrectionAlternativePolicyService.RequireAdmin(actor, permission, rowScopes, access, authority, clock, token);
                    result.Add(AttendanceCorrectionService.Map(row, evidence.Proposal.AlternativeKind));
                }
                if (result.Count == request.PageSize) break;
            }
            var more = rows.Count > scanned;
            return new CorrectionQueueResponse(result, more && position is not null ?
                cursors.Protect(actor.Identity.AccountId, purpose, digest, position) : null);
        }, ct, resourceScopes: request.Scope == CorrectionQueueScope.All ? null : async (_, token) =>
        {
            var target = request.TargetId!.Value;
            if (!await review.ScopeExistsAsync(request.Scope, target, token)) throw AttendanceOperation.Missing();
            if (request.Scope == CorrectionQueueScope.Employee)
                return await summaries.ResourceScopesAsync(target, request.From, request.To.AddDays(-1), token);
            return [new([request.Scope switch
            {
                CorrectionQueueScope.Site => new AccessResource(SiteId: target),
                CorrectionQueueScope.Department => new AccessResource(DepartmentId: target),
                CorrectionQueueScope.Team => new AccessResource(TeamId: target),
                CorrectionQueueScope.Project => new AccessResource(ProjectId: target),
                _ => throw AttendanceOperation.Invalid()
            }])];
        }, requireSystemRole: alternative);
    }

    public Task<CorrectionDecisionResponse> DecideAsync(Guid employeeId, Guid id, DecideCorrectionRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        var version = SchedulingOperation.Version(request.ExpectedVersion);
        if (request.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected) ||
            request.AlternativeKind.HasValue && !Enum.IsDefined(request.AlternativeKind.Value)) throw AttendanceOperation.Invalid("invalid_correction_decision");
        return operation.WriteAsync(request.AlternativeKind.HasValue ? CorrectionPermissions.AlternativesDecide : CorrectionPermissions.Decide, "attendance.correction.decide", employeeId, key,
            new { id, request }, request.Reason, context, async (actor, token) =>
            {
                if (request.AlternativeKind.HasValue && actor.Identity.EmployeeId == employeeId)
                    throw new AuthenticationFailure("correction_beneficiary_cannot_decide", 403);
                var (row, evidence) = await Evidence(employeeId, id, token);
                if (evidence.Proposal.AlternativeKind != request.AlternativeKind) throw AttendanceOperation.Conflict("alternative_decision_kind_required");
                RequirePending(row, version);
                if (row.Approval.RequesterUserId == actor.Identity.AccountId.ToString()) throw new AuthenticationFailure("correction_requester_cannot_decide", 403);
                var before = AttendanceCorrectionService.Map(row);
                WorkSession? session = null; int summaryRevision = 0;
                if (request.Decision == ApprovalState.Approved)
                {
                    if (evidence.SchemaVersion is not (3 or 4 or 5)) throw AttendanceOperation.Conflict("correction_legacy_source_refresh_required");
                    if (evidence.AlternativeEvidence is not null) await alternatives.RevalidateAsync(employeeId, row.Correction.AttendanceDate,
                        evidence.AlternativeEvidence, actor.Now, token);
                    if (row.Correction.Kind is not (AttendanceCorrectionKind.CheckInTime or AttendanceCorrectionKind.CheckOutTime or AttendanceCorrectionKind.ManualSession))
                        throw AttendanceOperation.Conflict("correction_type_authority_unavailable");
                    var state = await repository.SourceAsync(employeeId, row.Correction.AttendanceDate, row.Correction.RawPunchEventId, token);
                    if (state.PeriodLocked) throw AttendanceOperation.Conflict("correction_locked_period_adjustment_required");
                    var current = await CurrentSource(employeeId, row.Correction.AttendanceDate, row.Correction.RawPunchEventId, token);
                    if (current.Source.SourceFingerprint != evidence.Source.SourceFingerprint) throw AttendanceOperation.Conflict("correction_source_version_conflict");
                    session = row.Correction.Kind == AttendanceCorrectionKind.ManualSession
                        ? await PrepareManualSession(row, evidence, state, actor.Now, token)
                        : await PrepareSession(row, evidence, state, actor.Now, token);
                    summaryRevision = state.Summary?.Revision ?? 0;
                }
                if (request.AlternativeKind.HasValue) await CorrectionAlternativePolicyService.RequireAdmin(actor, CorrectionPermissions.AlternativesDecide,
                    await ReviewScopes(employeeId, id, token), access, authority, clock, token);
                var decisionAt = actor.Now;
                if (request.AlternativeKind.HasValue)
                {
                    decisionAt = clock.GetUtcNow();
                    if (decisionAt < actor.Now) throw AttendanceOperation.Denied();
                    if (session is not null)
                        session = new(session.SessionKey, session.Revision, session.EmployeeId, session.AttendanceDate,
                            session.StartsAtUtc, session.EndsAtUtc, session.ShiftVersionId, session.PolicyVersionId, decisionAt, null,
                            correctionId: session.CorrectionId, siteId: session.SiteId, endsContinuousDuty: session.EndsContinuousDuty,
                            branchId: session.BranchId, officeRosterAssignmentId: session.OfficeRosterAssignmentId,
                            employeeAssignmentId: session.EmployeeAssignmentId, officeGeofenceVersionId: session.OfficeGeofenceVersionId,
                            holidayCalendarId: session.HolidayCalendarId);
                }
                repository.ExpectVersion(row.Approval, version);
                var decision = row.Approval.Decide(request.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode, request.Reason.Trim(), decisionAt,
                    row.Correction.PrivateEvidenceReference);
                review.Add(decision, session);
                // SQL-backed summary provenance queries must see the staged approved correction.
                // This is a flush INSIDE AttendanceOperation's uncommitted serializable transaction.
                await repository.SaveChangesAsync(token);
                AttendanceSummaryRevision? summary = null;
                if (session is not null)
                {
                    using var stagedApproval = evidence.AlternativeEvidence is not null
                        ? await alternativeApprovals.StageAsync(row, decision, session, actor, context, token) : null;
                    summary = (await calculator.AppendAuthorizedRevisionAsync(employeeId,
                        new(row.Correction.AttendanceDate, summaryRevision, request.Reason), actor, token)).After;
                }
                await notifications.StageAsync(new(decision.Id, evidence.RequesterAccountId, NotificationTemplates.Decision, id), context, token);
                return new AttendanceChange<CorrectionDecisionResponse>(id, before,
                    new(AttendanceCorrectionService.Map(row with { AppliedSessionRevisionId = session?.Id, AppliedSummaryRevisionId = summary?.Id }, evidence.Proposal.AlternativeKind), decision.Id, session?.Id, summary?.Id));
            }, ct, resourceScopes: (_, token) => ReviewScopes(employeeId, id, token),
            beforeReplay: async (actor, token) =>
            {
                if (request.AlternativeKind.HasValue && actor.Identity.EmployeeId == employeeId)
                    throw new AuthenticationFailure("correction_beneficiary_cannot_decide", 403);
                var (_, saved) = await Evidence(employeeId, id, token);
                if (saved.Proposal.AlternativeKind != request.AlternativeKind) throw AttendanceOperation.Conflict("alternative_decision_kind_required");
                if (request.AlternativeKind.HasValue) await CorrectionAlternativePolicyService.RequireAdmin(actor, CorrectionPermissions.AlternativesDecide,
                    await ReviewScopes(employeeId, id, token), access, authority, clock, token);
            }, requireSystemRole: request.AlternativeKind.HasValue);
    }

    private async Task<WorkSession> PrepareSession(CorrectionRecord row, CorrectionEvidenceSnapshot evidence, CorrectionSourceState state, DateTimeOffset now, CancellationToken ct)
    {
        var proposal = evidence.Proposal;
        var candidates = state.SessionRevisions.Where(x => proposal.TargetWorkSessionId.HasValue ? x.Id == proposal.TargetWorkSessionId :
            proposal.Kind == AttendanceCorrectionKind.CheckInTime ? x.CheckInPunchId == proposal.RawPunchId : x.CheckOutPunchId == proposal.RawPunchId).ToArray();
        if (candidates.Length != 1) throw AttendanceOperation.Conflict("correction_target_session_changed");
        var original = candidates[0];
        if (evidence.Source.DutyProofs?.SingleOrDefault(x => x.WorkSessionId == original.Id) is not { HistoricallyVerified: true } proof ||
            proof.CheckInPunchId != original.CheckInPunchId || original.CheckInPunchId is null)
            throw AttendanceOperation.Conflict("correction_valid_checkin_required");
        var checkIn = await review.PunchAsync(original.EmployeeId, original.CheckInPunchId.Value, ct) ?? throw AttendanceOperation.Conflict("correction_valid_checkin_required");
        var checkOut = original.CheckOutPunchId is Guid checkOutId ? await review.PunchAsync(original.EmployeeId, checkOutId, ct) : null;
        var office = evidence.Source.OfficeSource;
        if (original.BranchId.HasValue && (office is null || !CorrectionOfficeEvidence.Matches(original, office) ||
            !CorrectionOfficeEvidence.Matches(checkIn, office, true) || checkOut is not null && !CorrectionOfficeEvidence.Matches(checkOut, office, true)) ||
            !original.BranchId.HasValue && office is not null)
            throw AttendanceOperation.Conflict("correction_original_context_unavailable");
        if (original.CheckOutPunchId.HasValue && checkOut is null || checkIn.BusinessDate != original.AttendanceDate ||
            checkIn.SiteId != original.SiteId || checkIn.ShiftVersionId != original.ShiftVersionId ||
            evidence.Source.HistoricalRoster?.ShiftVersionId != original.ShiftVersionId || evidence.Source.HistoricalRoster?.SiteId != original.SiteId ||
            proposal.RawPunchId.HasValue && (proposal.Kind == AttendanceCorrectionKind.CheckInTime ? original.CheckInPunchId : original.CheckOutPunchId) != proposal.RawPunchId ||
            !proposal.RawPunchId.HasValue && (original.EndsAtUtc.HasValue || original.CheckOutPunchId.HasValue))
            throw AttendanceOperation.Conflict("correction_original_context_unavailable");
        var start = proposal.Kind == AttendanceCorrectionKind.CheckInTime ? proposal.RequestedTimeUtc!.Value : original.StartsAtUtc;
        var end = proposal.Kind == AttendanceCorrectionKind.CheckOutTime ? proposal.RequestedTimeUtc : original.EndsAtUtc;
        if (end is null || end <= start || end > now || start > now) throw AttendanceOperation.Conflict("correction_closed_valid_window_required");
        if (proposal.Kind == AttendanceCorrectionKind.CheckInTime &&
            (start < evidence.Source.HistoricalRoster!.StartsAt || start >= evidence.Source.HistoricalRoster.EndsAt))
            throw AttendanceOperation.Conflict("correction_checkin_outside_retained_schedule");
        if (state.SessionRevisions.Any(x => x.Id != original.Id && !x.EndsAtUtc.HasValue)) throw AttendanceOperation.Conflict("correction_other_open_session");
        if (!await review.PreservesAssignmentAsync(original.EmployeeId, checkIn.EmployeeAssignmentId, original.SiteId, start, end.Value, ct))
            throw AttendanceOperation.Conflict("correction_crosses_site_transfer");
        if (await dutyOverlap.HasOverlapAsync(original.EmployeeId, [original.SessionKey], start, end.Value, ct)) throw AttendanceOperation.Conflict("correction_session_overlap");
        return new(original.SessionKey, checked(original.Revision + 1), original.EmployeeId, original.AttendanceDate, start, end,
            original.ShiftVersionId, original.PolicyVersionId, now, checkIn, checkOut, row.Correction.Id, original.SiteId, original.EndsContinuousDuty,
            original.BranchId, original.OfficeRosterAssignmentId, original.EmployeeAssignmentId, original.OfficeGeofenceVersionId, original.HolidayCalendarId);
    }

    private async Task<WorkSession> PrepareManualSession(CorrectionRecord row, CorrectionEvidenceSnapshot evidence,
        CorrectionSourceState state, DateTimeOffset now, CancellationToken ct)
    {
        var roster = evidence.Source.HistoricalRoster;
        var office = evidence.Source.OfficeSource;
        if ((state.Assessment?.ReasonCode == "OUTSIDE_GEOFENCE" || state.Assessment is { LocationReliable: true, InsideGeofence: false }) &&
            evidence.Proposal.AlternativeKind != CorrectionAlternativeKind.OutOfBoundary)
            throw AttendanceOperation.Conflict("out_of_boundary_admin_alternative_required");
        var start = evidence.Proposal.RequestedTimeUtc; var end = evidence.Proposal.RequestedEndUtc;
        if (roster is not { Status: "scheduled", ShiftVersionId: not null, StartsAt: not null, EndsAt: not null } ||
            (office is null ? roster.SiteId is null || roster.BranchId.HasValue : roster.SiteId.HasValue || roster.BranchId != office.BranchId) ||
            roster.Explanation?.MembershipRecordId is not Guid membership || start is null || end is null || start >= end || end > now ||
            start < roster.StartsAt || start >= roster.EndsAt)
            throw AttendanceOperation.Conflict("correction_manual_schedule_unavailable");
        if (state.Punch is not null && (state.Punch.Action != Cosmix.Domain.Attendance.Punches.PunchAction.CheckIn ||
            state.Assessment is null || state.Assessment.Outcome is Cosmix.Domain.Attendance.Punches.PunchAssessmentOutcome.Accepted or Cosmix.Domain.Attendance.Punches.PunchAssessmentOutcome.AcceptedWithException ||
            state.SessionRevisions.Any(x => x.CheckInPunchId == state.Punch.Id || x.CheckOutPunchId == state.Punch.Id)))
            throw AttendanceOperation.Conflict("correction_manual_source_already_interpreted");
        if (state.SessionRevisions.Any(x => !x.EndsAtUtc.HasValue)) throw AttendanceOperation.Conflict("correction_other_open_session");
        if (!await review.HistoricallyEligibleAsync(row.Correction.EmployeeId, start.Value, end.Value, ct))
            throw AttendanceOperation.Conflict("correction_employee_historically_ineligible");
        if (!await review.PreservesAssignmentAsync(row.Correction.EmployeeId, membership, roster.SiteId, start.Value, end.Value, ct))
            throw AttendanceOperation.Conflict("correction_crosses_site_transfer");
        if (await dutyOverlap.HasOverlapAsync(row.Correction.EmployeeId, [], start.Value, end.Value, ct))
            throw AttendanceOperation.Conflict("correction_session_overlap");
        // This is explicit HR-authorized manual interpretation. No raw event, accepted assessment,
        // successful face result, policy version or continuous-duty reset is fabricated.
        return new(Guid.NewGuid(), 1, row.Correction.EmployeeId, row.Correction.AttendanceDate, start.Value, end.Value,
            roster.ShiftVersionId.Value, null, now, null, correctionId: row.Correction.Id, siteId: roster.SiteId,
            branchId: office?.BranchId, officeRosterAssignmentId: office?.OfficeRosterAssignmentId,
            employeeAssignmentId: office?.EmployeeAssignmentId, officeGeofenceVersionId: office?.OfficeGeofenceVersionId,
            holidayCalendarId: office?.HolidayCalendarId);
    }

    private async Task<(CorrectionSourceResponse Source, IReadOnlyList<AttendanceResourceScope> Scopes)> CurrentSource(Guid employeeId, DateOnly date, Guid? raw, CancellationToken ct)
    {
        var state = await repository.SourceAsync(employeeId, date, raw, ct);
        AttendanceCorrectionService.RequireOwnedSource(state, employeeId, raw, date);
        var roster = await rosters.ResolveAsync(employeeId, date, ct);
        var office = await CorrectionOfficeEvidence.ReadAsync(employeeId, date, roster, offices,
            state.SessionRevisions.FirstOrDefault(x => x.BranchId.HasValue)?.OfficeGeofenceVersionId, ct);
        return CorrectionSourceEvidence.Capture(employeeId, date, state, roster,
            await summaries.ResourceScopesAsync(employeeId, date, date, ct), office);
    }
    private async Task<(CorrectionRecord Row, CorrectionEvidenceSnapshot Evidence)> Evidence(Guid employeeId, Guid id, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || id == Guid.Empty) throw AttendanceOperation.Invalid();
        var row = await review.FindAsync(employeeId, id, ct) ?? throw AttendanceOperation.Missing();
        if (!Guid.TryParse(row.Approval.RequesterUserId, out var requester)) throw AttendanceOperation.Conflict("correction_evidence_unavailable");
        var evidence = await operation.ReadPrivateAsync<CorrectionEvidenceSnapshot>(row.Correction.PrivateEvidenceReference, AttendanceCorrectionService.Purpose(employeeId), ct);
        if (!AttendanceCorrectionService.Matches(row, evidence, requester)) throw AttendanceOperation.Conflict("correction_evidence_unavailable");
        return (row, evidence);
    }
    private async Task<IReadOnlyList<AttendanceResourceScope>> ReviewScopes(Guid employeeId, Guid id, CancellationToken ct)
    {
        var (row, evidence) = await Evidence(employeeId, id, ct);
        if (evidence.OriginalResourceScopes is null) throw AttendanceOperation.Conflict("correction_legacy_source_refresh_required");
        return [.. evidence.OriginalResourceScopes, .. await summaries.ResourceScopesAsync(employeeId, row.Correction.AttendanceDate, row.Correction.AttendanceDate, ct)];
    }
    private static void RequirePending(CorrectionRecord row, byte[] version)
    {
        if (!row.Approval.RowVersion.SequenceEqual(version)) throw AttendanceOperation.Conflict("correction_version_conflict");
        if (row.Approval.State != ApprovalState.Pending) throw AttendanceOperation.Conflict("correction_not_pending");
    }
    private static void ValidateDate(DateOnly date) { if (date == default || date == DateOnly.MaxValue) throw AttendanceOperation.Invalid(); }
    private static void ValidateRange(DateOnly from, DateOnly to)
    { ValidateDate(from); if (to <= from || to.DayNumber - from.DayNumber > 366) throw AttendanceOperation.Invalid("invalid_correction_range"); }
}
