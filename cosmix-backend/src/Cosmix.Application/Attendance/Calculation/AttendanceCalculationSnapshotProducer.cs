using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.Policies;
using Cosmix.Domain.Attendance.Calculation;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Application.HR.StaffCategories;

namespace Cosmix.Application.Attendance.Calculation;

/// <summary>Reads actual bounded producers inside the summary caller's existing authorized transaction.
/// Known missing dependencies become unavailable results; corrupt evidence and storage/access failures propagate.</summary>
public sealed class AttendanceCalculationSnapshotProducer(IAttendancePolicyResolver policies,
    ILeaveAttendanceContextReader leaves, IAttendanceSummaryRepository summaries, IOvertimeRepository overtime,
    IAttendanceCalculationRepository repository, ICorrectionReviewRepository corrections,
    OvertimeService overtimeEvidence, AttendanceOperation operation, TimeProvider clock,
    IOfficeAttendanceSourceReader? offices = null,
    ICorrectionAlternativeEvidenceReader? alternatives = null,
    IStaffTaxonomyResolver? taxonomy = null,
    IAttendanceReviewedWorkReader? reviewedWork = null) : IAttendanceCalculationSnapshotProducer
{
    public async Task<AttendanceCalculationSnapshotResult> ReadAsync(Guid employeeId, DateOnly date, FactualAttendance facts,
        EffectiveRosterResponse roster, CancellationToken ct)
    {
        RosterSchedule.ValidateDate(date);
        if (employeeId == Guid.Empty || roster.EmployeeId != employeeId || roster.BusinessDate != date ||
            roster.TimeZone != RosterSchedule.TimeZoneId || !OvertimeRules.Hash(facts.InputFingerprint))
            throw AttendanceOperation.Conflict("attendance_calculation_identity_conflict");
        var policy = await policies.ResolveAsync(employeeId, date, ct);
        var leave = await leaves.ReadAsync(employeeId, date, ct);
        if (!OvertimeRules.Hash(policy.Fingerprint) || !OvertimeRules.Hash(leave.Digest) ||
            !Enum.IsDefined(leave.Status)) throw AttendanceOperation.Conflict("attendance_dependency_contract_invalid");
        AttendanceCalculationSnapshotResult Missing(string code, object? sourceEvidence = null) => new(code, null,
            Hash(new { schema = 1, employeeId, date, facts.InputFingerprint, roster, policy.Fingerprint,
                LeaveDigest = leave.Digest, code, sourceEvidence }));
        if (policy.State != "effective") return Missing(policy.State switch
        {
            "missing" => "policy_configuration_required",
            "conflict" => "policy_configuration_conflict",
            "historical_membership_unavailable" => "policy_membership_unavailable",
            "invalid_configuration" => "policy_configuration_invalid",
            _ => throw AttendanceOperation.Conflict("attendance_policy_contract_invalid")
        });
        var validated = AttendancePolicyRulesCodec.Validate(policy.Rules);
        if (policy.PolicyVersionId is null || policy.PolicyVersionId == Guid.Empty || policy.AssignmentIds.Count == 0 ||
            policy.AssignmentIds.Count > 512 || policy.AssignmentIds.Any(x => x == Guid.Empty) ||
            policy.AssignmentIds.Distinct().Count() != policy.AssignmentIds.Count || !validated.IsValid ||
            policy.CanonicalJson != validated.CanonicalJson)
            throw AttendanceOperation.Conflict("attendance_policy_contract_invalid");
        if (leave.Status != LeaveContextStatus.Complete)
            return Missing(leave.Status == LeaveContextStatus.Pending ? "leave_decision_pending" : "leave_effect_conflict");
        if (roster.Status is not ("scheduled" or "rest_day") || roster.ShiftVersionId is null ||
            roster.AssignmentId is null || roster.Explanation?.MembershipRecordId is null || roster.SiteId is null && roster.BranchId is null ||
            roster.ConfiguredScheduledMinutes is null || roster.UnpaidBreakMinutes is null || roster.PaidBreakMinutes is null)
            return Missing("schedule_configuration_required");
        if (facts.OpenSessions != 0) return Missing("attendance_session_still_open");
        var employment = await overtime.EmploymentAsync(employeeId, date, ct);
        if (employment is null) return Missing("attendance_employment_unavailable");
        if (employment.AssignmentId != roster.Explanation.MembershipRecordId || employment.SiteId != roster.SiteId ||
            roster.BranchId.HasValue && employment.BranchId != roster.BranchId ||
            !roster.BranchId.HasValue && employment.BranchId.HasValue ||
            employment.EffectiveFrom > date || employment.EffectiveTo <= date)
            throw AttendanceOperation.Conflict("attendance_employment_source_conflict");
        await ValidateTaxonomyAsync(employment, roster, null, ct);
        var revisions = await summaries.SessionsAsync(employeeId, date, ct);
        var actualFacts = AttendanceFacts.Calculate(employeeId, date, revisions);
        if (!Same(actualFacts, facts)) throw AttendanceOperation.Conflict("attendance_factual_source_changed");
        var now = clock.GetUtcNow();
        if (date > RosterSchedule.Today(now) || facts.SessionIds.Count == 0 && roster.Status == "scheduled" &&
            (roster.EndsAt is null || roster.EndsAt > now))
            return Missing("attendance_day_not_complete");
        var currentRows = revisions.Where(x => facts.SessionIds.Contains(x.Id)).OrderBy(x => x.SessionKey).ThenBy(x => x.Id).ToArray();
        OfficeAttendanceSource? officeSource = null;
        if (roster.BranchId.HasValue)
        {
            if (offices is null) return Missing("office_attendance_source_unavailable");
            var boundaries = currentRows.Select(x => x.OfficeGeofenceVersionId).Distinct().ToArray();
            if (boundaries.Length > 1 || currentRows.Any(x => x.OfficeGeofenceVersionId is null))
                throw AttendanceOperation.Conflict("attendance_office_source_conflict");
            var context = await offices.ReadAsync(employeeId, date, roster, boundaries.SingleOrDefault(), ct);
            if (context.Status == "office_boundary_not_configured") return Missing(context.Status);
            if (context.Status != "configured" || context.Source is null || context.Boundary is null ||
                OfficeAttendanceSource.Capture(employeeId, date, roster, context.Boundary) != context.Source ||
                boundaries.Length != 0 && boundaries[0] != context.Source.OfficeGeofenceVersionId)
                throw AttendanceOperation.Conflict("attendance_office_source_conflict");
            officeSource = context.Source;
        }
        else if (roster.Explanation.Source == "office" || roster.Explanation.OfficeRosterAssignmentId.HasValue ||
            currentRows.Any(x => x.BranchId.HasValue || x.OfficeRosterAssignmentId.HasValue || x.OfficeGeofenceVersionId.HasValue))
            throw AttendanceOperation.Conflict("attendance_office_source_conflict");
        var sessions = (await overtime.SessionSourcesAsync(employeeId, date, ct)).OrderBy(x => x.SessionKey).ThenBy(x => x.Id).ToArray();
        if (sessions.Length > 512 || sessions.Length != facts.SessionIds.Count ||
            sessions.Select(x => x.SessionKey).Distinct().Count() != sessions.Length ||
            !sessions.Select(x => x.Id).Order().SequenceEqual(facts.SessionIds.Order()))
            throw AttendanceOperation.Conflict("attendance_session_source_conflict");
        var rawIds = currentRows.SelectMany(x => new[] { x.CheckInPunchId, x.CheckOutPunchId }).Where(x => x.HasValue)
            .Select(x => x!.Value).Distinct().Order().ToArray();
        var punches = (await repository.PunchesAsync(employeeId, rawIds, ct)).OrderBy(x => x.Id).ToArray();
        if (!punches.Select(x => x.Id).SequenceEqual(rawIds)) throw AttendanceOperation.Conflict("attendance_punch_source_conflict");
        var correctionProofs = new List<AttendanceCorrectionProvenance>();
        foreach (var session in sessions)
        {
            var row = currentRows.Single(x => x.Id == session.Id);
            if (!session.ValidDutyEvidence || session.EndsAtUtc is null || session.StartsAtUtc.Offset != TimeSpan.Zero ||
                session.EndsAtUtc.Value.Offset != TimeSpan.Zero || session.EndsAtUtc <= session.StartsAtUtc ||
                session.EndsAtUtc > now || row.CalculatedAtUtc > now ||
                session.EndsAtUtc - session.StartsAtUtc > TimeSpan.FromDays(7) || session.Revision < 1 ||
                session.SiteId != roster.SiteId || session.ShiftVersionId != roster.ShiftVersionId ||
                session.StartsAtUtc != row.StartsAtUtc || session.EndsAtUtc != row.EndsAtUtc ||
                session.SessionKey != row.SessionKey || session.Revision != row.Revision || session.CorrectionId != row.CorrectionId)
                throw AttendanceOperation.Conflict("attendance_duty_evidence_conflict");
            if (officeSource is not null && (!OfficeMatches(row, officeSource) ||
                session.BranchId != officeSource.BranchId || session.OfficeRosterAssignmentId != officeSource.OfficeRosterAssignmentId ||
                session.EmployeeAssignmentId != officeSource.EmployeeAssignmentId || session.OfficeGeofenceVersionId != officeSource.OfficeGeofenceVersionId ||
                session.HolidayCalendarId != officeSource.HolidayCalendarId ||
                !session.CorrectionId.HasValue && (row.CheckInPunchId is null || row.CheckOutPunchId is null)))
                throw AttendanceOperation.Conflict("attendance_office_source_conflict");
            foreach (var punch in punches.Where(x => x.Id == row.CheckInPunchId || x.Id == row.CheckOutPunchId))
            {
                if (punch.EmployeeId != employeeId || punch.BusinessDate != date || punch.SiteId != roster.SiteId ||
                    punch.EmployeeAssignmentId != employment.AssignmentId || punch.ShiftVersionId != roster.ShiftVersionId ||
                    punch.SiteRosterAssignmentId != roster.Explanation.SiteAssignmentId ||
                    punch.TemporaryShiftChangeId != roster.Explanation.TemporaryChangeId || punch.HolidayCalendarId != roster.Holiday?.CalendarId ||
                    punch.Action != (punch.Id == row.CheckInPunchId ? PunchAction.CheckIn : PunchAction.CheckOut) ||
                    punch.LatestAssessmentCount != 1 || punch.AssessmentRevision is null or < 1 ||
                    punch.AssessmentId != (punch.Id == row.CheckInPunchId ? session.CheckInAssessmentId : session.CheckOutAssessmentId))
                    throw AttendanceOperation.Conflict("attendance_punch_context_conflict");
                if (officeSource is not null && (punch.BranchId != officeSource.BranchId ||
                    punch.OfficeRosterAssignmentId != officeSource.OfficeRosterAssignmentId || punch.OfficeGeofenceVersionId != officeSource.OfficeGeofenceVersionId ||
                    !session.CorrectionId.HasValue && punch.ReceivedAtUtc != (punch.Id == row.CheckInPunchId ? session.StartsAtUtc : session.EndsAtUtc)))
                    throw AttendanceOperation.Conflict("attendance_punch_context_conflict");
                // A time correction does not itself authorize a location exception. Explicit manual
                // approved duty has no raw session punch and is verified separately below.
                if (punch.AssessmentOutcome != PunchAssessmentOutcome.Accepted)
                    return Missing("attendance_exception_review_required", punches);
            }
            if (!await corrections.HistoricallyEligibleAsync(employeeId, session.StartsAtUtc, session.EndsAtUtc.Value, ct) ||
                !(officeSource is null
                    ? await corrections.PreservesAssignmentAsync(employeeId, employment.AssignmentId, session.SiteId, session.StartsAtUtc, session.EndsAtUtc.Value, ct)
                    : await OfficeMembershipMatches(officeSource, session.StartsAtUtc, session.EndsAtUtc.Value, ct)))
                return Missing("attendance_historical_employment_unavailable");
            if (await corrections.OverlapsAsync(employeeId, session.SessionKey, session.StartsAtUtc, session.EndsAtUtc.Value, ct))
                throw AttendanceOperation.Conflict("attendance_cross_date_session_overlap");
            if (session.CorrectionId.HasValue)
            {
                try
                {
                    correctionProofs.Add(await ValidateCorrection(employeeId, date, session, row, officeSource, roster.Taxonomy, ct));
                }
                catch (AuthenticationFailure failure) when (failure.StatusCode == 503)
                {
                    return Missing("correction_evidence_unavailable");
                }
            }
        }
        if (leave.Effects.Count > 512 || leave.Effects.Select(x => x.AllocationId).Distinct().Count() != leave.Effects.Count ||
            leave.Effects.Any(x => x.Date != date || x.LeaveRequestId == Guid.Empty || x.AllocationId == Guid.Empty ||
                x.ApprovalRequestId == Guid.Empty || x.ApprovalDecisionId == Guid.Empty || x.PolicyVersionId is null || x.PolicyVersionId == Guid.Empty ||
                x.ShiftVersionId != roster.ShiftVersionId ||
                x.StartsAtUtc.Offset != TimeSpan.Zero || x.EndsAtUtc.Offset != TimeSpan.Zero || x.EndsAtUtc <= x.StartsAtUtc ||
                (x.EndsAtUtc - x.StartsAtUtc).Ticks != (long)x.Minutes * TimeSpan.TicksPerMinute || x.Minutes <= 0 ||
                x.Treatment is not (LeavePayTreatment.Paid or LeavePayTreatment.Unpaid)))
            throw AttendanceOperation.Conflict("attendance_leave_contract_invalid");
        var awards = new List<AttendanceOvertimeProvenance>();
        var approvedIntervals = new List<AttendanceTimeInterval>();
        var pending = false;
        var awardSourceChanged = false;
        long approvedTicks = 0;
        var references = await repository.OvertimeAsync(employeeId, date, ct);
        if (references.Count > 512 || references.Select(x => x.Id).Distinct().Count() != references.Count)
            throw AttendanceOperation.Conflict("attendance_overtime_source_conflict");
        if (officeSource is not null && references.Count != 0)
            throw AttendanceOperation.Conflict("attendance_office_overtime_ineligible");
        foreach (var reference in references.OrderBy(x => x.Id))
        {
            var evidence = await overtimeEvidence.ReadAttendanceEvidenceAsync(employeeId, reference.Id, ct);
            var row = evidence.Row;
            if (row.BusinessDate != date || !Enum.IsDefined(row.Approval.State) || reference.DecisionCount != (row.Decision is null ? 0 : 1))
                throw AttendanceOperation.Conflict("attendance_overtime_source_conflict");
            awards.Add(new(row.Request.Id, row.Decision?.Id, row.Approval.State.ToString(), evidence.ApprovedTicks, evidence.ApprovedIntervals, evidence.Digest));
            pending |= row.Approval.State == ApprovalState.Pending;
            if (row.Approval.State != ApprovalState.Approved) continue;
            if (!Same(evidence.Source.Source.Sessions.OrderBy(x => x.SessionKey).ThenBy(x => x.Id).ToArray(), sessions) ||
                !Same(evidence.Source.Source.Roster, roster) || !Same(evidence.Source.Employment, employment))
                awardSourceChanged = true;
            if (evidence.ApprovedIntervals.Any(interval => approvedIntervals.Any(x => x.StartsAtUtc < interval.EndsAtUtc && interval.StartsAtUtc < x.EndsAtUtc)) ||
                evidence.ApprovedTicks is null or < 0 || evidence.ApprovedIntervals.Sum(x => (x.EndsAtUtc - x.StartsAtUtc).Ticks) != evidence.ApprovedTicks)
                throw AttendanceOperation.Conflict("attendance_overtime_award_conflict");
            approvedIntervals.AddRange(evidence.ApprovedIntervals);
            approvedTicks = checked(approvedTicks + evidence.ApprovedTicks.Value);
        }
        var occupiedTo = sessions.Length == 0 ? (DateOnly?)null : sessions.Max(x =>
            RosterSchedule.Today(x.EndsAtUtc!.Value.AddTicks(-1)).AddDays(1));
        await ValidateTaxonomyAsync(employment, roster, occupiedTo, ct);
        var input = new AttendanceTimeInput(policy.Rules!, roster.StartsAt?.ToUniversalTime(), roster.EndsAt?.ToUniversalTime(),
            roster.ConfiguredScheduledMinutes.Value, roster.UnpaidBreakMinutes.Value, roster.Status == "rest_day",
            roster.Holiday?.Status == "configured" && roster.Holiday.BusinessDate == date ? roster.Holiday.Holidays.Count != 0 : null,
            sessions.Select(x => new AttendanceTimeInterval(x.StartsAtUtc, x.EndsAtUtc!.Value)).ToArray(),
            leave.Effects.OrderBy(x => x.StartsAtUtc).ThenBy(x => x.AllocationId)
                .Select(x => new AttendanceLeaveTime(new(x.StartsAtUtc, x.EndsAtUtc), x.Treatment == LeavePayTreatment.Paid)).ToArray(),
            approvedIntervals.OrderBy(x => x.StartsAtUtc).ToArray());
        var reviewed = reviewedWork is null ? null : await reviewedWork.ReadAsync(employeeId, date, roster, sessions, approvedIntervals, ct);
        if (reviewed?.Time is not null) input = input with { ReviewedWork = reviewed.Time };
        var snapshot = new AttendanceCalculationSnapshot(1, employeeId, date, AttendanceDailyTimeCalculation.CalculationVersionFor(input),
            facts.InputFingerprint, roster, employment, policy, leave, sessions, punches,
            correctionProofs.OrderBy(x => x.CorrectionId).ToArray(), awards, input, approvedTicks, officeSource, reviewed);
        return new(awardSourceChanged ? "overtime_award_source_changed" : pending ? "overtime_decision_pending" : "complete", snapshot, Hash(snapshot));
    }

    private async Task ValidateTaxonomyAsync(OvertimeEmploymentSource employment, EffectiveRosterResponse roster,
        DateOnly? occupiedTo, CancellationToken ct)
    {
        var office = roster.BranchId.HasValue;
        var to = RosterSchedule.OccupiedEndExclusive(roster) ??
            throw AttendanceOperation.Conflict("attendance_taxonomy_source_changed");
        if (occupiedTo > to) to = occupiedTo.Value;
        var resolution = await RosterStaffTaxonomy.ResolveAsync(taxonomy, employment.StaffCategory, roster.BusinessDate, to, ct);
        if (!RosterStaffTaxonomy.Valid(roster.Taxonomy, employment.StaffCategory,
                office ? StaffWorkLocation.Office : StaffWorkLocation.Site,
                office ? StaffScheduleBasis.Office : StaffScheduleBasis.Site) ||
            !RosterStaffTaxonomy.Matches(employment.StaffCategory, resolution, roster.Taxonomy))
            throw AttendanceOperation.Conflict("attendance_taxonomy_source_changed");
    }

    private async Task<AttendanceCorrectionProvenance> ValidateCorrection(Guid employeeId, DateOnly date,
        OvertimeSessionSource session, WorkSession applied, OfficeAttendanceSource? officeSource,
        RosterStaffTaxonomySource? taxonomySource, CancellationToken ct)
    {
        var row = await corrections.FindAsync(employeeId, session.CorrectionId!.Value, ct);
        if (row is null || session.CorrectionDecisionId is null || row.Approval.State != ApprovalState.Approved ||
            !Guid.TryParse(row.Approval.RequesterUserId, out var requester) || row.Correction.AttendanceDate != date ||
            row.Approval.ClosedAt is null || applied.CalculatedAtUtc != row.Approval.ClosedAt ||
            !await repository.CorrectionDecisionMatchesAsync(session.CorrectionDecisionId.Value, row.Approval.Id, row.Correction.PrivateEvidenceReference, ct))
            throw AttendanceOperation.Conflict("attendance_correction_source_conflict");
        var evidence = await operation.ReadPrivateAsync<CorrectionEvidenceSnapshot>(row.Correction.PrivateEvidenceReference,
            AttendanceCorrectionService.Purpose(employeeId), ct);
        var special = evidence.AlternativeEvidence is not null;
        if (!AttendanceCorrectionService.Matches(row, evidence, requester) || evidence.SchemaVersion != (special ? 5 : officeSource is null ? 3 : 4) ||
            evidence.Source.HistoricalRoster is not { } historical || historical.ShiftVersionId != session.ShiftVersionId || historical.SiteId != session.SiteId ||
            historical.Taxonomy != taxonomySource)
            throw AttendanceOperation.Conflict("attendance_correction_source_conflict");
        if (officeSource is not null && evidence.Source.OfficeSource != officeSource)
            throw AttendanceOperation.Conflict("attendance_correction_source_conflict");
        var p = evidence.Proposal;
        if (p.Kind == AttendanceCorrectionKind.ManualSession)
        {
            if (session.StartsAtUtc != p.RequestedTimeUtc || session.EndsAtUtc != p.RequestedEndUtc || session.Revision != 1 ||
                applied.CheckInPunchId is not null || applied.CheckOutPunchId is not null ||
                historical.Status != "scheduled" || historical.StartsAt is null || historical.EndsAt is null ||
                session.StartsAtUtc < historical.StartsAt || session.StartsAtUtc >= historical.EndsAt || session.EndsAtUtc > row.Approval.ClosedAt ||
                session.PolicyVersionId is not null || p.RawPunchId.HasValue &&
                    (evidence.Source.Action != "CheckIn" || evidence.Source.AssessmentOutcome is null or "Accepted" or "AcceptedWithException" ||
                     evidence.Source.SessionRevisions!.Any(x => x.CheckInPunchId == p.RawPunchId || x.CheckOutPunchId == p.RawPunchId)) ||
                evidence.Source.SessionRevisions!.Any(x => x.SessionKey == session.SessionKey))
                throw AttendanceOperation.Conflict("attendance_correction_application_conflict");
        }
        else
        {
            var prior = evidence.Source.SessionRevisions!.SingleOrDefault(x => x.SessionKey == session.SessionKey && x.Revision == session.Revision - 1);
            if (prior is null || p.Kind is not (AttendanceCorrectionKind.CheckInTime or AttendanceCorrectionKind.CheckOutTime) ||
                p.TargetWorkSessionId.HasValue && p.TargetWorkSessionId != prior.Id ||
                prior.SiteId != session.SiteId || prior.ShiftVersionId != session.ShiftVersionId || prior.PolicyVersionId != session.PolicyVersionId ||
                applied.CheckInPunchId != prior.CheckInPunchId || applied.CheckOutPunchId != prior.CheckOutPunchId ||
                session.StartsAtUtc != (p.Kind == AttendanceCorrectionKind.CheckInTime ? p.RequestedTimeUtc : prior.StartsAtUtc) ||
                session.EndsAtUtc != (p.Kind == AttendanceCorrectionKind.CheckOutTime ? p.RequestedTimeUtc : prior.EndsAtUtc) ||
                evidence.Source.DutyProofs?.SingleOrDefault(x => x.WorkSessionId == prior.Id) is not { HistoricallyVerified: true } proof ||
                proof.CheckInPunchId != prior.CheckInPunchId || prior.CheckInPunchId is null || session.EndsAtUtc > row.Approval.ClosedAt)
                throw AttendanceOperation.Conflict("attendance_correction_application_conflict");
            if (officeSource is not null && (prior.BranchId != officeSource.BranchId ||
                prior.OfficeRosterAssignmentId != officeSource.OfficeRosterAssignmentId || prior.EmployeeAssignmentId != officeSource.EmployeeAssignmentId ||
                prior.OfficeGeofenceVersionId != officeSource.OfficeGeofenceVersionId || prior.HolidayCalendarId != officeSource.HolidayCalendarId))
                throw AttendanceOperation.Conflict("attendance_correction_application_conflict");
        }
        if (special)
            return new(row.Correction.Id, session.CorrectionDecisionId.Value, session.Id,
                await ValidateAlternativeApproval(row, evidence, requester, session, applied, ct));
        // Initial application is calculated before the new summary link exists. The exact approved
        // session/proposal binding above proves application without creating a summary dependency cycle.
        return new(row.Correction.Id, session.CorrectionDecisionId.Value, session.Id, Hash(evidence));
    }

    private async Task<string> ValidateAlternativeApproval(CorrectionRecord row, CorrectionEvidenceSnapshot evidence,
        Guid requester, OvertimeSessionSource session, WorkSession applied, CancellationToken ct)
    {
        var alternative = evidence.AlternativeEvidence;
        if (alternative is null || evidence.Proposal.Kind != AttendanceCorrectionKind.ManualSession ||
            evidence.Proposal.AlternativeKind != alternative.Policy.Kind ||
            evidence.Proposal.ExpectedAlternativePolicyId != alternative.Policy.Id ||
            repository is not IAttendanceAlternativeCorrectionApprovalReader approvals)
            throw AttendanceOperation.Conflict("attendance_alternative_approval_unavailable");
        var requestedAt = clock.GetUtcNow();
        var proof = await approvals.ReadAsync(row.Correction.EmployeeId, row.Correction.Id, session.CorrectionDecisionId!.Value, ct);
        var checkedAt = clock.GetUtcNow();
        if (proof is null || checkedAt < requestedAt || proof.Decision is not { } decision ||
            decision.Id != session.CorrectionDecisionId || decision.ApprovalRequestId != row.Approval.Id ||
            decision.Decision != ApprovalState.Approved || decision.DecidedAt != row.Approval.ClosedAt ||
            decision.DecidedAt != applied.CalculatedAtUtc || decision.DecidedAt > checkedAt ||
            decision.ProtectedChangeReference != row.Correction.PrivateEvidenceReference || decision.ActorRoleCode != "ADMIN" ||
            !Guid.TryParse(decision.ActorUserId, out var actorId) || actorId == Guid.Empty || actorId == requester ||
            proof.Actor is null || proof.Actor.AccountId != actorId || proof.Actor.EmployeeId == Guid.Empty || proof.Actor.SessionId == Guid.Empty ||
            proof.Actor.Client != SessionClient.Web || !proof.Actor.MfaSatisfied || proof.Actor.SecurityVersion < 1 ||
            proof.SystemRole != SystemRoleKind.Admin || proof.RoleAssignmentId == Guid.Empty ||
            proof.AuthorityAtUtc.Offset != TimeSpan.Zero ||
            (proof.IsAuthorizedStagedApplication ? proof.AuthorityAtUtc < requestedAt || proof.AuthorityAtUtc > checkedAt : proof.AuthorityAtUtc != decision.DecidedAt) ||
            !AttendanceScopeEvaluation.IsValid(proof.AuthorizationScopes) ||
            proof.AuthorizationScopes.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId != row.Correction.EmployeeId) ||
            evidence.OriginalResourceScopes!.Any(x => !proof.AuthorizationScopes.Any(y => Same(x, y))) ||
            proof.Permissions is null || proof.Permissions.Count is < 1 or > AttendanceScopeEvaluation.MaximumTotalAlternatives ||
            proof.Permissions.Any(x => x is null))
            throw AttendanceOperation.Conflict("attendance_alternative_approval_unavailable");
        var currentScopes = await summaries.ResourceScopesAsync(row.Correction.EmployeeId, row.Correction.AttendanceDate, row.Correction.AttendanceDate, ct);
        var permissions = proof.Permissions.Where(x => x.Permission == CorrectionPermissions.AlternativesDecide &&
            x.RoleAssignmentId == proof.RoleAssignmentId && x.IsEffectiveAt(proof.AuthorityAtUtc)).ToArray();
        if (!AttendanceScopeEvaluation.Allows(proof.Actor, permissions, CorrectionPermissions.AlternativesDecide, proof.AuthorizationScopes) ||
            !AttendanceScopeEvaluation.Allows(proof.Actor, permissions, CorrectionPermissions.AlternativesDecide, currentScopes))
            throw AttendanceOperation.Conflict("attendance_alternative_approval_unavailable");
        if (proof.IsAuthorizedStagedApplication)
        {
            // The approval transaction has flushed its exact decision/session but has not yet
            // staged the outer audit. Only the trusted transaction hook can identify this state.
            if (proof.Audit is not null || alternatives is null)
                throw AttendanceOperation.Conflict("attendance_alternative_approval_unavailable");
            await alternatives.RevalidateAsync(row.Correction.EmployeeId, row.Correction.AttendanceDate,
                alternative, checkedAt, ct);
            var finalAt = clock.GetUtcNow();
            if (finalAt < checkedAt || !AttendanceScopeEvaluation.Allows(proof.Actor,
                permissions.Where(x => x.IsEffectiveAt(finalAt)), CorrectionPermissions.AlternativesDecide, proof.AuthorizationScopes) ||
                !AttendanceScopeEvaluation.Allows(proof.Actor, permissions.Where(x => x.IsEffectiveAt(finalAt)),
                    CorrectionPermissions.AlternativesDecide, currentScopes))
                throw AttendanceOperation.Conflict("attendance_alternative_approval_unavailable");
        }
        else
        {
            var audit = proof.Audit;
            if (audit is null || audit.EventType != "attendance.correction.decide" || audit.ActorRoleCode != "ADMIN" ||
                audit.ActorUserId != decision.ActorUserId || audit.TargetType != "Attendance" || audit.TargetId != row.Correction.Id.ToString() ||
                audit.EmployeeScopeId != row.Correction.EmployeeId || audit.OccurredAt < decision.DecidedAt || audit.OccurredAt > checkedAt ||
                audit.ProtectedChangesReference is null)
                throw AttendanceOperation.Conflict("attendance_alternative_approval_unavailable");
            var saved = await operation.ReadPrivateAsync<AlternativeDecisionAudit>(audit.ProtectedChangesReference,
                $"attendance:operation:{actorId:N}:attendance.correction.decide:audit", ct);
            if (saved.Operation != audit.EventType || saved.ResourceId != row.Correction.Id || saved.After is null || saved.After.Request is null ||
                saved.After.DecisionId != decision.Id || saved.After.Request.Id != row.Correction.Id ||
                saved.After.Request.EmployeeId != row.Correction.EmployeeId || saved.After.Request.BusinessDate != row.Correction.AttendanceDate ||
                saved.After.Request.State != "Approved" || saved.After.Request.AlternativeKind != evidence.Proposal.AlternativeKind ||
                saved.After.AppliedSessionRevisionId != applied.Id)
                throw AttendanceOperation.Conflict("attendance_alternative_approval_unavailable");
        }
        return Hash(new { evidence, decision.Id, decision.ActorUserId, decision.ActorRoleCode, decision.DecidedAt, proof.Actor,
            proof.RoleAssignmentId, proof.SystemRole, Permissions = proof.Permissions.Select(x => new {
                x.Permission, x.RoleAssignmentId, x.ScopeKind, x.TargetId, x.AuthorityTimingKnown, x.ValidFromUtc, x.ValidUntilUtc
            }).OrderBy(x => JsonSerializer.Serialize(x), StringComparer.Ordinal).ToArray() });
    }

    private sealed record AlternativeDecisionAudit([property: System.Text.Json.Serialization.JsonPropertyName("operation")] string Operation,
        Guid ResourceId, CorrectionDecisionResponse After);

    private static bool OfficeMatches(WorkSession row, OfficeAttendanceSource source) => row.SiteId is null &&
        row.EmployeeId == source.EmployeeId && row.AttendanceDate == source.BusinessDate && row.BranchId == source.BranchId &&
        row.OfficeRosterAssignmentId == source.OfficeRosterAssignmentId && row.EmployeeAssignmentId == source.EmployeeAssignmentId &&
        row.OfficeGeofenceVersionId == source.OfficeGeofenceVersionId && row.ShiftVersionId == source.ShiftVersionId &&
        row.HolidayCalendarId == source.HolidayCalendarId;

    private async Task<bool> OfficeMembershipMatches(OfficeAttendanceSource source, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        // Membership must cover each local date of an overnight interval, including its retained start date.
        for (var day = RosterSchedule.Today(from); day <= RosterSchedule.Today(to.AddTicks(-1)); day = day.AddDays(1))
            if (!await offices!.CurrentMembershipMatchesAsync(source.EmployeeId, source.BranchId, source.EmployeeAssignmentId, day, ct))
                return false;
        return true;
    }

    private static bool Same<T>(T first, T second) => JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second);
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}

/// <summary>Supplemental implemented interface on the selected calculation repository. Never derive
/// staged authorization merely from a missing audit/summary. Verify the actual same-assignment fixed
/// Admin, MFA/session and exact dedicated permission in the active approval transaction, or return
/// retained decision-time authority from verified private audit/history without current-role inference.</summary>
public interface IAttendanceAlternativeCorrectionApprovalReader
{
    Task<AttendanceAlternativeCorrectionApprovalProof?> ReadAsync(Guid employeeId, Guid correctionId,
        Guid decisionId, CancellationToken ct);
}

/// <summary>Internal verified source projection, never HTTP input or public attendance evidence.
/// Permission timing must come from authoritative snapshots, not serialization of PermissionGrant
/// (whose timing fields are JsonIgnore). No entity/model fields or new database queue are introduced.</summary>
public sealed record AttendanceAlternativeCorrectionApprovalProof(ApprovalDecision Decision, AuthenticatedAccount Actor,
    SystemRoleKind SystemRole, Guid RoleAssignmentId, IReadOnlyList<PermissionGrant> Permissions,
    IReadOnlyList<AttendanceResourceScope> AuthorizationScopes, DateTimeOffset AuthorityAtUtc,
    bool IsAuthorizedStagedApplication, AuditEvent? Audit);
