using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.FaceVerification;

namespace Cosmix.Application.Attendance.Corrections;

/// <summary>Versioned source evidence only. It grants no review/application authority.</summary>
internal static class CorrectionSourceEvidence
{
    public static (CorrectionSourceResponse Source, IReadOnlyList<AttendanceResourceScope> Scopes) Capture(
        Guid employeeId, DateOnly date, CorrectionSourceState state, EffectiveRosterResponse roster,
        IReadOnlyList<AttendanceResourceScope> scopes, OfficeAttendanceSource? officeSource = null)
    {
        if (roster.EmployeeId != employeeId || roster.BusinessDate != date || state.SessionRevisions is null || state.SessionRevisions.Count > 4096 ||
            state.SessionRevisions.Any(x => x.EmployeeId != employeeId || x.AttendanceDate != date) ||
            state.SessionRevisions.Select(x => x.SessionKey).Distinct().Count() != state.SessionRevisions.Count ||
            state.SessionRevisions.Select(x => x.Id).Distinct().Count() != state.SessionRevisions.Count)
            throw AttendanceOperation.Conflict("correction_source_unavailable");
        if (roster.BranchId.HasValue != (officeSource is not null) || officeSource is null &&
            (roster.Explanation?.Source == "office" || state.Punch?.BranchId.HasValue == true ||
             state.SessionRevisions.Any(x => x.BranchId.HasValue || x.OfficeRosterAssignmentId.HasValue || x.OfficeGeofenceVersionId.HasValue)) ||
            officeSource is not null &&
            (state.SessionRevisions.Any(x => !CorrectionOfficeEvidence.Matches(x, officeSource)) ||
             state.Punch is not null && !CorrectionOfficeEvidence.Matches(state.Punch, officeSource, false)))
            throw AttendanceOperation.Conflict("correction_office_source_unavailable");
        if (state.CheckInSources is not null && (state.CheckInSources.Count != state.SessionRevisions.Count ||
            state.CheckInSources.Select(x => x.WorkSessionId).Distinct().Count() != state.CheckInSources.Count ||
            state.CheckInSources.Any(x => !state.SessionRevisions.Any(s => s.Id == x.WorkSessionId && s.CheckInPunchId == x.Punch?.Id))))
            throw AttendanceOperation.Conflict("correction_source_unavailable");
        var sessions = state.SessionRevisions.OrderBy(x => x.SessionKey).ThenBy(x => x.Revision).ThenBy(x => x.Id)
            .Select(x => new CorrectionSessionSource(x.Id, x.SessionKey, x.Revision, x.StartsAtUtc, x.EndsAtUtc,
                x.CheckInPunchId, x.CheckOutPunchId, x.CorrectionId, x.SiteId, x.ShiftVersionId, x.PolicyVersionId,
                x.EndsContinuousDuty, x.BranchId, x.OfficeRosterAssignmentId,
                x.BranchId.HasValue ? x.EmployeeAssignmentId : null, x.OfficeGeofenceVersionId,
                x.BranchId.HasValue ? x.HolidayCalendarId : null)).ToArray();
        var sourceScopes = CanonicalScopes(employeeId, scopes).ToList();
        if (state.Punch is not null && !sourceScopes.SelectMany(x => x.Alternatives).Any(x => x.SiteId == state.Punch.SiteId))
            sourceScopes.Add(new([new(EmployeeId: employeeId, SiteId: state.Punch.SiteId)]));
        var original = CanonicalScopes(employeeId, sourceScopes);
        var punch = state.Punch;
        var originalPunch = punch is null ? null : new CorrectionPunchContext(punch.BusinessDate, punch.SiteId,
            punch.EmployeeAssignmentId, punch.ShiftVersionId, punch.GeofenceVersionId, punch.SiteRosterAssignmentId,
            punch.TemporaryShiftChangeId, punch.HolidayCalendarId, punch.IsOffline,
            punch.BranchId, punch.OfficeRosterAssignmentId, punch.OfficeGeofenceVersionId);
        var source = new CorrectionSourceResponse(date, state.Punch?.Id, state.Punch?.Action.ToString(), state.Punch?.ReceivedAtUtc,
            state.Assessment?.Id, state.Assessment?.Outcome.ToString(), state.Summary?.Id, state.Summary?.Revision,
            state.Summary?.PolicyVersionId is not null ? state.Summary.Status.ToString() : null, state.PeriodLocked,
            RoutingStatus: state.CheckInSources is null ? "routing_not_configured" : "hr_review_required",
            SummaryCalculationState: state.Summary is null ? "not_calculated" : state.Summary.PolicyVersionId is null ? "factual_only" : "policy_calculated",
            HistoricalRoster: roster, SessionRevisions: sessions, OriginalPunchContext: originalPunch,
            DutyProofs: state.CheckInSources?.OrderBy(x => x.WorkSessionId).Select(x => new CorrectionDutyProof(
                x.WorkSessionId, x.Punch?.Id, x.Assessment?.Id, x.Assessment?.Outcome.ToString(), x.Verification?.Id,
                Verified(employeeId, x) && (officeSource is null || x.Punch is not null && CorrectionOfficeEvidence.Matches(x.Punch, officeSource, true)))).ToArray(),
            OfficeSource: officeSource);
        return (source with { SourceFingerprint = Fingerprint(employeeId, source, original) }, original);
    }

    public static bool IsFingerprint(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    public static bool Matches(CorrectionEvidenceSnapshot snapshot)
    {
        if (snapshot.SchemaVersion == 5)
        {
            var alternative = snapshot.AlternativeEvidence;
            if (snapshot.Proposal.Kind != Domain.Attendance.Corrections.AttendanceCorrectionKind.ManualSession ||
                !snapshot.Proposal.AlternativeKind.HasValue || alternative is null ||
                !CorrectionAlternativePolicyService.Valid(alternative, snapshot.EmployeeId) ||
                alternative.Policy.Kind != snapshot.Proposal.AlternativeKind || alternative.Policy.Id != snapshot.Proposal.ExpectedAlternativePolicyId ||
                !alternative.Documents.Select(x => x.Id).Order().SequenceEqual((snapshot.Proposal.AlternativeEvidenceDocumentIds ?? []).Order())) return false;
            return Matches(snapshot with { SchemaVersion = snapshot.Source.OfficeSource is not null ? 4 : 3,
                AlternativeEvidence = null, Proposal = snapshot.Proposal with { AlternativeKind = null, ExpectedAlternativePolicyId = null, AlternativeEvidenceDocumentIds = null } });
        }
        if (snapshot.AlternativeEvidence is not null || snapshot.Proposal.AlternativeKind.HasValue || snapshot.Proposal.ExpectedAlternativePolicyId.HasValue ||
            snapshot.Proposal.AlternativeEvidenceDocumentIds is not null) return false;
        // Old evidence stays readable, but absence of session/scope proof must never be inferred as an
        // empty source. Any later application workflow must explicitly refresh/resubmit legacy proposals.
        if (snapshot.SchemaVersion == 1)
            return snapshot.OriginalResourceScopes is null && snapshot.Source.SourceFingerprint is null &&
                snapshot.Source.SessionRevisions is null && snapshot.Source.OriginalPunchContext is null &&
                snapshot.Proposal.ExpectedSourceFingerprint is null && snapshot.Source.DutyProofs is null &&
                snapshot.Proposal.TargetWorkSessionId is null && snapshot.Proposal.ResubmissionOfCorrectionId is null && !HasOffice(snapshot.Source);
        if (snapshot.SchemaVersion is not (2 or 3 or 4) ||
            (snapshot.SchemaVersion == 4 ? snapshot.Source.OfficeSource is null || snapshot.Source.DutyProofs is null : HasOffice(snapshot.Source)) ||
            (snapshot.SchemaVersion == 2) != (snapshot.Source.DutyProofs is null) || !IsFingerprint(snapshot.Source.SourceFingerprint) ||
            !string.Equals(snapshot.Proposal.ExpectedSourceFingerprint, snapshot.Source.SourceFingerprint, StringComparison.OrdinalIgnoreCase) ||
            snapshot.Source.SessionRevisions is not { Count: <= 4096 } || snapshot.OriginalResourceScopes is null)
            return false;
        try
        {
            return (snapshot.Source.OfficeSource is null || ValidOfficeSnapshot(snapshot)) && string.Equals(snapshot.Source.SourceFingerprint,
                Fingerprint(snapshot.EmployeeId, snapshot.Source, CanonicalScopes(snapshot.EmployeeId, snapshot.OriginalResourceScopes)),
                StringComparison.Ordinal);
        }
        catch (AuthenticationFailure) { return false; }
    }

    private static bool Verified(Guid employeeId, CorrectionCheckInState state) => state.Punch is { Action: PunchAction.CheckIn } punch &&
        punch.EmployeeId == employeeId && state.Assessment is { } assessment && assessment.EmployeeId == employeeId &&
        assessment.RawPunchEventId == punch.Id && assessment.Outcome is PunchAssessmentOutcome.Accepted or PunchAssessmentOutcome.AcceptedWithException &&
        state.Verification is { Outcome: FaceVerificationOutcome.Passed, LivenessPassed: true, MatchPassed: true } proof &&
        assessment.FaceVerificationAttemptId == proof.Id && proof.EmployeeId == employeeId && proof.ConsumedByPunchId == punch.Id &&
        proof.ConsumedAtUtc.HasValue && proof.RegisteredDeviceId == punch.RegisteredDeviceId && proof.PunchIdempotencyKey == punch.IdempotencyKey;

    private static IReadOnlyList<AttendanceResourceScope> CanonicalScopes(Guid employeeId, IReadOnlyList<AttendanceResourceScope> scopes)
    {
        if (!AttendanceScopeEvaluation.IsValid(scopes) || scopes.SelectMany(x => x.Alternatives).Any(x => x.EmployeeId != employeeId))
            throw AttendanceOperation.Conflict("correction_source_unavailable");
        return scopes.Select(scope => new AttendanceResourceScope(scope.Alternatives.Distinct()
                .OrderBy(x => x.EmployeeId).ThenBy(x => x.DepartmentId).ThenBy(x => x.TeamId).ThenBy(x => x.SiteId).ThenBy(x => x.ProjectId).ToArray()))
            .OrderBy(scope => JsonSerializer.Serialize(scope.Alternatives), StringComparer.Ordinal).ToArray();
    }

    private static bool HasOffice(CorrectionSourceResponse source) => source.OfficeSource is not null ||
        source.HistoricalRoster?.BranchId is not null || source.OriginalPunchContext?.BranchId is not null ||
        source.SessionRevisions?.Any(x => x.BranchId.HasValue || x.OfficeRosterAssignmentId.HasValue || x.OfficeGeofenceVersionId.HasValue) == true;

    private static bool ValidOfficeSnapshot(CorrectionEvidenceSnapshot snapshot)
    {
        var source = snapshot.Source.OfficeSource!; var roster = snapshot.Source.HistoricalRoster;
        return source.EmployeeId == snapshot.EmployeeId && source.BusinessDate == snapshot.Source.BusinessDate &&
            source.BranchId != Guid.Empty && source.OfficeRosterAssignmentId != Guid.Empty && source.EmployeeAssignmentId != Guid.Empty &&
            source.OfficeGeofenceVersionId != Guid.Empty && source.BoundaryVersionNumber > 0 &&
            roster?.BranchId == source.BranchId && roster.SiteId is null && roster.AssignmentId == source.OfficeRosterAssignmentId &&
            roster.ShiftVersionId == source.ShiftVersionId && roster.Holiday?.CalendarId == source.HolidayCalendarId &&
            roster.Explanation?.MembershipRecordId == source.EmployeeAssignmentId && roster.Explanation.OfficeRosterAssignmentId == source.OfficeRosterAssignmentId &&
            snapshot.Source.SessionRevisions!.All(x => x.BranchId == source.BranchId && x.SiteId is null && x.ShiftVersionId == source.ShiftVersionId &&
                x.OfficeRosterAssignmentId == source.OfficeRosterAssignmentId && x.EmployeeAssignmentId == source.EmployeeAssignmentId &&
                x.OfficeGeofenceVersionId == source.OfficeGeofenceVersionId && x.HolidayCalendarId == source.HolidayCalendarId);
    }

    private static string Fingerprint(Guid employeeId, CorrectionSourceResponse source, IReadOnlyList<AttendanceResourceScope> scopes) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        { schema = source.OfficeSource is not null ? "correction-source-v4-office" : source.DutyProofs is null ? "correction-source-v2" : "correction-source-v3", employeeId, source = source with { SourceFingerprint = null }, originalResourceScopes = scopes })));
}
