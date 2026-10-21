using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.Corrections;

/// <summary>Proposal intake only. No unknown approval chain, reviewer or payroll effect is inferred.</summary>
public sealed class AttendanceCorrectionService(AttendanceOperation operation, IAttendanceCorrectionRepository repository,
    IPrivateFileStore files, IIndividualRosterResolver rosters, IAttendanceSummaryRepository summaries,
    IOfficeAttendanceSourceReader offices, ICorrectionAlternativeEvidenceReader alternatives)
{
    public const string RequestType = "attendance.correction";

    public Task<CorrectionSourceResponse> SourcesAsync(DateOnly date, Guid? punchId, AuthRequestContext context, CancellationToken ct)
    {
        ValidateDate(date);
        if (punchId == Guid.Empty) throw AttendanceOperation.Invalid();
        return operation.ReadAsync(CorrectionPermissions.Self, "attendance.correction.sources", null, context,
            async (actor, token) =>
            {
                await RequireHistoricalDate(actor.Identity.EmployeeId, date, actor.Now, token);
                var source = await repository.SourceAsync(actor.Identity.EmployeeId, date, punchId, token);
                RequireOwnedSource(source, actor.Identity.EmployeeId, punchId, date);
                var roster = await rosters.ResolveAsync(actor.Identity.EmployeeId, date, token);
                var officeSource = await CorrectionOfficeEvidence.ReadAsync(actor.Identity.EmployeeId, date, roster, offices,
                    source.SessionRevisions.FirstOrDefault(x => x.BranchId.HasValue)?.OfficeGeofenceVersionId, token);
                var captured = CorrectionSourceEvidence.Capture(actor.Identity.EmployeeId, date, source, roster,
                    await summaries.ResourceScopesAsync(actor.Identity.EmployeeId, date, date, token), officeSource);
                return captured.Source;
            }, ct, self: true);
    }

    public async Task<CorrectionResponse> SubmitAsync(CreateCorrectionRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        ValidateProposal(request);
        if (request.ExpectedSourceFingerprint is null)
        {
            // Do not invalidate an already committed pre-fingerprint request. The omitted nullable
            // property preserves its legacy digest, and replay still requires live authentication.
            var prior = await operation.TryReplayAsync<CorrectionResponse>(CorrectionPermissions.Self,
                "attendance.correction.submit", null, key, request, request.Reason, context, ct, self: true);
            if (prior.Found) return prior.Result!;
            throw AttendanceOperation.Invalid("correction_source_fingerprint_required");
        }
        return await operation.WriteAsync(CorrectionPermissions.Self, "attendance.correction.submit", null, key, request,
            request.Reason, context, async (actor, token) =>
            {
                return await SubmitAuthorizedAsync(actor.Identity.EmployeeId, request, actor, token);
            }, ct, self: true);
    }

    // Caller owns the live authorization transaction and resolved employee scopes.
    internal async Task<AttendanceChange<CorrectionResponse>> SubmitAuthorizedAsync(Guid employeeId,
        CreateCorrectionRequest request, AttendanceActor actor, CancellationToken token)
    {
        await RequireHistoricalDate(employeeId, request.BusinessDate, actor.Now, token);
        if (request.RequestedTimeUtc > actor.Now || request.RequestedEndUtc > actor.Now)
            throw AttendanceOperation.Invalid("correction_time_in_future");
        var source = await repository.SourceAsync(employeeId, request.BusinessDate, request.RawPunchId, token);
        RequireOwnedSource(source, employeeId, request.RawPunchId, request.BusinessDate);
        if (source.PeriodLocked) throw AttendanceOperation.Conflict("correction_locked_period_adjustment_required");
        if (source.Assessment?.Id != request.ExpectedAssessmentId || source.Summary?.Id != request.ExpectedSummaryId)
            throw AttendanceOperation.Conflict("correction_source_version_conflict");
        if (request.Kind == AttendanceCorrectionKind.CheckInTime && source.Punch?.Action != Cosmix.Domain.Attendance.Punches.PunchAction.CheckIn ||
            request.Kind == AttendanceCorrectionKind.CheckOutTime && request.RawPunchId.HasValue && source.Punch?.Action != Cosmix.Domain.Attendance.Punches.PunchAction.CheckOut)
            throw AttendanceOperation.Invalid("correction_punch_action_mismatch");
        if (request.TargetWorkSessionId.HasValue && !source.SessionRevisions.Any(x => x.Id == request.TargetWorkSessionId &&
            (request.RawPunchId.HasValue || x.EndsAtUtc is null && x.CheckOutPunchId is null)))
            throw AttendanceOperation.Conflict("correction_target_session_changed");
        if (request.ResubmissionOfCorrectionId.HasValue)
        {
            var previous = await RequireOwn(employeeId, actor.Identity.AccountId, request.ResubmissionOfCorrectionId.Value, token);
            if (previous.Approval.State != ApprovalState.Returned) throw AttendanceOperation.Conflict("correction_resubmission_requires_returned");
        }
                if (request.Kind == AttendanceCorrectionKind.DayStatus && source.Summary is null)
            throw AttendanceOperation.Conflict("correction_summary_required");
        var roster = await rosters.ResolveAsync(employeeId, request.BusinessDate, token);
        RequireRoster(request, roster, employeeId);
        var officeSource = await CorrectionOfficeEvidence.ReadAsync(employeeId, request.BusinessDate, roster, offices,
            source.SessionRevisions.FirstOrDefault(x => x.BranchId.HasValue)?.OfficeGeofenceVersionId, token);
        var captured = CorrectionSourceEvidence.Capture(employeeId, request.BusinessDate, source, roster,
            await summaries.ResourceScopesAsync(employeeId, request.BusinessDate, request.BusinessDate, token), officeSource);
        if (!string.Equals(request.ExpectedSourceFingerprint, captured.Source.SourceFingerprint, StringComparison.OrdinalIgnoreCase))
            throw AttendanceOperation.Conflict("correction_source_version_conflict");
        if (await repository.HasPendingEquivalentAsync(employeeId, request, token))
            throw AttendanceOperation.Conflict("equivalent_correction_pending");
        var alternative = request.AlternativeKind.HasValue ? await alternatives.CaptureAsync(employeeId, request.BusinessDate, request.AlternativeKind.Value,
            request.ExpectedAlternativePolicyId!.Value, request.AlternativeEvidenceDocumentIds, actor.Now, token) : null;
        if (alternative is not null && !CorrectionAlternativePolicyService.Valid(alternative, employeeId))
            throw AttendanceOperation.Conflict("alternative_evidence_unavailable");
        var submissionId = Guid.NewGuid();
        if ((officeSource is not null || alternative is not null) && captured.Source.DutyProofs is null) throw AttendanceOperation.Conflict("correction_source_unavailable");
        var snapshot = new CorrectionEvidenceSnapshot(alternative is not null ? 5 : officeSource is not null ? 4 : captured.Source.DutyProofs is null ? 2 : 3,
            submissionId, employeeId, actor.Identity.AccountId, request, captured.Source, captured.Scopes, alternative);
        var evidence = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot), Purpose(employeeId), token));
        // One reviewer is confirmed; the explicit scoped decide permission supplies authority, not a role name.
        var approval = new ApprovalRequest(RequestType, submissionId, actor.Identity.AccountId.ToString(), evidence, 1, actor.Now);
        var correction = new AttendanceCorrection(employeeId, request.BusinessDate, request.Kind, approval.Id,
            actor.Identity.AccountId.ToString(), actor.Now, request.Reason.Trim(), evidence,
            originalTimeUtc: source.Punch?.ReceivedAtUtc, requestedTimeUtc: request.RequestedTimeUtc,
            requestedEndUtc: request.RequestedEndUtc, originalStatus: source.Summary?.Status,
            requestedStatus: request.RequestedStatus, rawPunchId: source.Punch?.Id, originalSummaryId: source.Summary?.Id);
        repository.Add(approval, correction);
        await repository.SaveChangesAsync(token);
        return new AttendanceChange<CorrectionResponse>(correction.Id, null, Map(new(correction, approval), request.AlternativeKind));
    }

    public Task<PagedResult<CorrectionResponse>> ListAsync(CorrectionListRequest request, AuthRequestContext context, CancellationToken ct)
    {
        ValidateList(request);
        return operation.ReadAsync(CorrectionPermissions.Self, "attendance.correction.history.self", null, context,
            async (actor, token) =>
            {
                var result = await repository.ListOwnAsync(actor.Identity.EmployeeId, actor.Identity.AccountId, request, token);
                var mapped = new List<CorrectionResponse>();
                foreach (var row in result.Items)
                {
                    var saved = await operation.ReadPrivateAsync<CorrectionEvidenceSnapshot>(row.Correction.PrivateEvidenceReference, Purpose(row.Correction.EmployeeId), token);
                    if (!Matches(row, saved, actor.Identity.AccountId)) throw AttendanceOperation.Conflict("correction_evidence_unavailable");
                    mapped.Add(Map(row, saved.Proposal.AlternativeKind));
                }
                return new PagedResult<CorrectionResponse>(mapped, result.TotalCount, result.Page, result.PageSize);
            }, ct, self: true);
    }
    public Task<CorrectionDetailResponse> DetailAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(CorrectionPermissions.Self, "attendance.correction.detail.self", null, context,
            async (actor, token) =>
            {
                var record = await RequireOwn(actor.Identity.EmployeeId, actor.Identity.AccountId, id, token);
                var snapshot = await operation.ReadPrivateAsync<CorrectionEvidenceSnapshot>(record.Correction.PrivateEvidenceReference,
                    Purpose(actor.Identity.EmployeeId), token);
                if (!Matches(record, snapshot, actor.Identity.AccountId)) throw AttendanceOperation.Conflict("correction_evidence_unavailable");
                return new CorrectionDetailResponse(Map(record, snapshot.Proposal.AlternativeKind), snapshot.Proposal.WrittenEvidence, snapshot.Source, snapshot.AlternativeEvidence);
            }, ct, self: true);

    public Task<CorrectionResponse> CancelAsync(Guid id, CancelCorrectionRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (id == Guid.Empty) throw AttendanceOperation.Invalid();
        var version = SchedulingOperation.Version(request.ExpectedVersion);
        RequireText(request.Reason, 1000);
        return operation.WriteAsync(CorrectionPermissions.Self, "attendance.correction.cancel", null, key,
            new { id, request.ExpectedVersion }, request.Reason, context, async (actor, token) =>
            {
                var record = await RequireOwn(actor.Identity.EmployeeId, actor.Identity.AccountId, id, token);
                if (!record.Approval.RowVersion.SequenceEqual(version)) throw AttendanceOperation.Conflict("correction_version_conflict");
                if (record.Approval.State != ApprovalState.Pending) throw AttendanceOperation.Conflict("correction_not_pending");
                var before = Map(record);
                repository.ExpectVersion(record.Approval, version);
                record.Approval.Cancel(actor.Identity.AccountId.ToString(), actor.Now);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<CorrectionResponse>(record.Correction.Id, before, Map(record));
            }, ct, self: true);
    }

    public Task<PagedResult<AttendanceExceptionNotice>> ExceptionsAsync(CorrectionListRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        ValidateList(request);
        return operation.ReadAsync(CorrectionPermissions.ExceptionsSelf, "attendance.exceptions.self", null, context,
            async (actor, token) =>
            {
                var result = await repository.ExceptionsAsync(actor.Identity.EmployeeId, request, token);
                return new PagedResult<AttendanceExceptionNotice>(result.Items.Select(AttendanceExceptionService.Notice).ToArray(),
                    result.TotalCount, result.Page, result.PageSize);
            }, ct, self: true);
    }

    private async Task<CorrectionRecord> RequireOwn(Guid employeeId, Guid accountId, Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) throw AttendanceOperation.Invalid();
        var record = await repository.FindOwnAsync(employeeId, accountId, id, ct) ?? throw AttendanceOperation.Missing();
        if (record.Correction.EmployeeId != employeeId || record.Approval.SubjectId == Guid.Empty ||
            record.Correction.RequestedBy != accountId.ToString() || record.Approval.RequesterUserId != accountId.ToString() ||
            record.Correction.ApprovalRequestId != record.Approval.Id || record.Approval.RequestType != RequestType ||
            record.Correction.PrivateEvidenceReference != record.Approval.SnapshotPrivateReference) throw AttendanceOperation.Missing();
        return record;
    }
    internal async Task RequireHistoricalDate(Guid employeeId, DateOnly date, DateTimeOffset now, CancellationToken ct)
    {
        if (date > RosterSchedule.Today(now)) throw AttendanceOperation.Invalid("correction_date_in_future");
        var joined = await repository.JoinedOnAsync(employeeId, ct);
        if (!joined.HasValue || date < joined) throw AttendanceOperation.Conflict("correction_date_before_employment");
    }
    internal static void RequireOwnedSource(CorrectionSourceState source, Guid employeeId, Guid? expected, DateOnly date)
    {
        if (expected.HasValue && source.Punch?.Id != expected || source.Punch is not null && source.Punch.EmployeeId != employeeId)
            throw AttendanceOperation.Missing();
        if (source.Punch is not null && (source.Punch.BusinessDate ?? RosterSchedule.Today(source.Punch.ReceivedAtUtc)) != date)
            throw AttendanceOperation.Invalid("correction_punch_date_mismatch");
        if (source.Assessment is not null && source.Assessment.RawPunchEventId != source.Punch?.Id ||
            source.Summary is not null && (source.Summary.EmployeeId != employeeId || source.Summary.AttendanceDate != date))
            throw AttendanceOperation.Conflict("correction_source_unavailable");
    }
    private static void RequireRoster(CreateCorrectionRequest request, EffectiveRosterResponse roster, Guid employeeId)
    {
        if (request.Kind == AttendanceCorrectionKind.DayStatus) return;
        if (roster.EmployeeId != employeeId || roster.BusinessDate != request.BusinessDate || roster.Status != "scheduled" ||
            !roster.ShiftVersionId.HasValue || !roster.StartsAt.HasValue || !roster.EndsAt.HasValue || roster.EndsAt <= roster.StartsAt)
            throw AttendanceOperation.Conflict("correction_roster_unavailable");
        // A manual proposal must relate to the retained business-date duty window. No grace/OT threshold is guessed.
        // Existing raw punch proposals retain their raw business date; the proposed time is evidence for a future reviewer.
        if (request.Kind == AttendanceCorrectionKind.ManualSession &&
            (request.RequestedTimeUtc >= roster.EndsAt || request.RequestedEndUtc <= roster.StartsAt))
            throw AttendanceOperation.Invalid("correction_session_outside_roster");
    }
    public static void ValidateProposal(CreateCorrectionRequest request)
    {
        if (request is null) throw AttendanceOperation.Invalid();
        ValidateDate(request.BusinessDate); RequireText(request.Reason, 1000); RequireText(request.WrittenEvidence, 4000);
        if (request.ExpectedSourceFingerprint is not null && !CorrectionSourceEvidence.IsFingerprint(request.ExpectedSourceFingerprint))
            throw AttendanceOperation.Invalid("invalid_correction_source_fingerprint");
        ValidateAlternative(request.Kind, request.AlternativeKind, request.ExpectedAlternativePolicyId, request.AlternativeEvidenceDocumentIds);
        if (!Enum.IsDefined(request.Kind) || request.RawPunchId == Guid.Empty || request.ExpectedAssessmentId == Guid.Empty ||
            request.TargetWorkSessionId == Guid.Empty || request.ResubmissionOfCorrectionId == Guid.Empty ||
            request.ExpectedSummaryId == Guid.Empty || request.RequestedStatus.HasValue && !Enum.IsDefined(request.RequestedStatus.Value) ||
            !request.RawPunchId.HasValue && request.ExpectedAssessmentId.HasValue)
            throw AttendanceOperation.Invalid("invalid_correction_proposal");
        var timedPunch = request.Kind is AttendanceCorrectionKind.CheckInTime or AttendanceCorrectionKind.CheckOutTime;
        if (request.TargetWorkSessionId.HasValue && !timedPunch ||
            timedPunch && ((!request.RawPunchId.HasValue && !(request.Kind == AttendanceCorrectionKind.CheckOutTime && request.TargetWorkSessionId.HasValue)) ||
            !request.RequestedTimeUtc.HasValue || request.RequestedEndUtc.HasValue || request.RequestedStatus.HasValue) ||
            request.Kind == AttendanceCorrectionKind.ManualSession && (!request.RequestedTimeUtc.HasValue ||
                !request.RequestedEndUtc.HasValue || request.RequestedEndUtc <= request.RequestedTimeUtc || request.RequestedStatus.HasValue) ||
            request.Kind == AttendanceCorrectionKind.DayStatus && (request.RawPunchId.HasValue || !request.RequestedStatus.HasValue ||
                request.RequestedTimeUtc.HasValue || request.RequestedEndUtc.HasValue))
            throw AttendanceOperation.Invalid("invalid_correction_typed_values");
    }
    private static void ValidateDate(DateOnly date)
    { if (date == default || date == DateOnly.MaxValue) throw AttendanceOperation.Invalid("invalid_correction_date"); }
    private static void ValidateList(CorrectionListRequest request)
    {
        ValidateDate(request.From);
        if (request.To <= request.From || request.To.DayNumber - request.From.DayNumber > 366 ||
            request.Page is < 1 or > 1000000 || request.PageSize is < 1 or > 100) throw AttendanceOperation.Invalid("invalid_correction_range");
    }
    private static void RequireText(string? text, int maximum)
    { if (string.IsNullOrWhiteSpace(text) || text.Length > maximum || text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'))) throw AttendanceOperation.Invalid(); }
    public static CorrectionResponse Map(CorrectionRecord row) => new(row.Correction.Id, row.Correction.EmployeeId, row.Correction.AttendanceDate,
        row.Correction.Kind.ToString(), row.Correction.RawPunchEventId, row.Correction.OriginalSummaryId, row.Correction.OriginalTimeUtc,
        row.Correction.RequestedTimeUtc, row.Correction.RequestedEndUtc, row.Correction.RequestedStatus?.ToString(), row.Correction.Reason,
        row.Correction.RequestedAtUtc, row.Approval.State.ToString(), row.Approval.State == ApprovalState.Pending ? "hr_review_required" : "closed",
        Convert.ToBase64String(row.Approval.RowVersion), row.Approval.State == ApprovalState.Approved && row.AppliedSessionRevisionId.HasValue && row.AppliedSummaryRevisionId.HasValue,
        row.AppliedSessionRevisionId, row.AppliedSummaryRevisionId,
        row.AppliedSessionRevisionId.HasValue && row.AppliedSummaryRevisionId.HasValue ?
            row.Correction.Kind == AttendanceCorrectionKind.ManualSession ? "hr_approved_manual_duty" : "corrected_verified_duty" : null);
    internal static CorrectionResponse Map(CorrectionRecord row, CorrectionAlternativeKind? alternative) => alternative.HasValue ? Map(row) with {
        AlternativeKind = alternative, RoutingStatus = row.Approval.State == ApprovalState.Pending ? "admin_alternative_review_required" : "closed",
        Interpretation = row.AppliedSessionRevisionId.HasValue && row.AppliedSummaryRevisionId.HasValue ? "admin_approved_alternative_duty" : null } : Map(row);
    internal static bool Matches(CorrectionRecord row, CorrectionEvidenceSnapshot snapshot, Guid actor) => snapshot is not null &&
        snapshot.SchemaVersion is 1 or 2 or 3 or 4 or 5 && snapshot.SubmissionId == row.Approval.SubjectId && snapshot.EmployeeId == row.Correction.EmployeeId && snapshot.RequesterAccountId == actor &&
        snapshot.Proposal is not null && snapshot.Source is not null && snapshot.Proposal.BusinessDate == row.Correction.AttendanceDate &&
        snapshot.Proposal.Kind == row.Correction.Kind && snapshot.Proposal.RawPunchId == row.Correction.RawPunchEventId &&
        snapshot.Proposal.ExpectedSummaryId == row.Correction.OriginalSummaryId && snapshot.Proposal.RequestedTimeUtc == row.Correction.RequestedTimeUtc &&
        snapshot.Proposal.RequestedEndUtc == row.Correction.RequestedEndUtc && snapshot.Proposal.RequestedStatus == row.Correction.RequestedStatus &&
        snapshot.Proposal.Reason?.Trim() == row.Correction.Reason && !string.IsNullOrWhiteSpace(snapshot.Proposal.WrittenEvidence) &&
        snapshot.Proposal.WrittenEvidence.Length <= 4000 && snapshot.Source.BusinessDate == row.Correction.AttendanceDate &&
        snapshot.Source.OriginalTimeUtc == row.Correction.OriginalTimeUtc && snapshot.Source.RawPunchId == row.Correction.RawPunchEventId &&
        snapshot.Source.LatestAssessmentId == snapshot.Proposal.ExpectedAssessmentId && snapshot.Source.LatestSummaryId == row.Correction.OriginalSummaryId &&
        CorrectionSourceEvidence.Matches(snapshot);
    internal static string Purpose(Guid employeeId) => $"attendance:correction:{employeeId:N}:proposal";
    internal static void ValidateAlternative(AttendanceCorrectionKind kind, CorrectionAlternativeKind? alternative, Guid? policy,
        IReadOnlyList<Guid>? documents)
    {
        if (alternative.HasValue ? !Enum.IsDefined(alternative.Value) || kind != AttendanceCorrectionKind.ManualSession ||
                policy is null || policy == Guid.Empty || documents is { Count: > 8 } ||
                documents is not null && (documents.Any(x => x == Guid.Empty) || documents.Distinct().Count() != documents.Count)
            : policy.HasValue || documents is not null) throw AttendanceOperation.Invalid("invalid_alternative_duty_proposal");
    }
}
