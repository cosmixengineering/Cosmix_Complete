using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Attendance.Punches.CaptureValidation;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Attendance.Punches;

namespace Cosmix.Application.Attendance.Audit;

/// <summary>Read-only historical evidence, separate from approval, interpretation and pay.</summary>
public sealed class AttendanceAuditService(AttendanceOperation operation, IAttendanceAuditRepository repository,
    IAttendanceSummaryRepository summaries, IAccountAccessReader access, IAttendanceCursorProtector cursors,
    ICaptureValidationEvidenceRepository validationEvidence, ISchedulingJournalRepository journal, TimeProvider clock)
{
    private const int ScanLimit = 200;
    private const string SourceUnavailable = "attendance_audit_source_unavailable";

    public Task<AttendancePunchAuditPage> PunchesAsync(Guid employeeId, AttendanceAuditRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        Validate(employeeId, request);
        const string purpose = "attendance.audit.punches";
        var digest = Digest(employeeId, request);
        return operation.ReadAsync<AttendancePunchAuditPage>(AttendanceAuditPermissions.Read, purpose, employeeId, context,
            async (actor, token) =>
            {
                var position = Position(actor, request, purpose, digest);
                var candidates = await repository.PunchesAsync(employeeId, request, position, ScanLimit + 1, token);
                var grants = await CurrentGrants(actor, token);
                var items = new List<AttendancePunchAuditItem>(); var scanned = 0;
                var itemScopes = new List<IReadOnlyList<AttendanceResourceScope>>();
                foreach (var candidate in candidates.Take(ScanLimit))
                {
                    var raw = candidate.Punch;
                    scanned++; position = new(raw.ReceivedAtUtc, raw.Id);
                    try
                    {
                        ValidatePunch(employeeId, raw);
                        if (candidate.Assessment is { } assessment &&
                            (assessment.EmployeeId != employeeId || assessment.RawPunchEventId != raw.Id))
                            throw AttendanceOperation.Conflict(SourceUnavailable);
                        if (!InRange(BusinessDate(raw), request)) throw AttendanceOperation.Conflict(SourceUnavailable);
                        var scopes = await SourceScopes(employeeId, [(BusinessDate(raw), raw.SiteId)], token);
                        if (!AttendanceScopeEvaluation.Allows(actor.Identity, grants, AttendanceAuditPermissions.Read, scopes)) continue;
                        var interpreted = candidate.Assessment;
                        var validation = await ReadValidation(raw, interpreted, token);
                        grants = await CurrentGrants(actor, token);
                        if (!AttendanceScopeEvaluation.Allows(actor.Identity, grants, AttendanceAuditPermissions.Read, scopes)) continue;
                        items.Add(new(new(raw.Id, raw.Action.ToString(), raw.ReceivedAtUtc, BusinessDate(raw),
                            interpreted?.Outcome.ToString() ?? "Unverified", interpreted?.ReasonCode ?? "ASSESSMENT_NOT_AVAILABLE",
                            raw.SiteId, raw.ShiftVersionId, null, raw.IsOffline, interpreted?.Id,
                            BusinessDateSource: raw.BusinessDate.HasValue ? "server_roster" : "legacy_received_date",
                            BranchId: raw.BranchId, OfficeRosterAssignmentId: raw.OfficeRosterAssignmentId,
                            OfficeGeofenceVersionId: raw.OfficeGeofenceVersionId, Validation: validation),
                            interpreted?.Revision, interpreted?.AssessedAtUtc, raw.DeviceCapturedAtUtc));
                        itemScopes.Add(scopes);
                    }
                    catch (AuthenticationFailure failure) when (failure.Code == SourceUnavailable) { continue; }
                    if (items.Count == request.PageSize) break;
                }
                grants = await CurrentGrants(actor, token);
                var authorized = items.Where((_, index) => AttendanceScopeEvaluation.Allows(actor.Identity, grants,
                    AttendanceAuditPermissions.Read, itemScopes[index])).ToArray();
                return new(authorized, Continue(actor, purpose, digest, position, candidates.Count > scanned));
            }, ct, resourceScopes: (_, token) => Historical(employeeId, request.From, request.To.AddDays(-1), token));
    }

    public Task<AttendanceSessionAuditPage> SessionsAsync(Guid employeeId, AttendanceAuditRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        Validate(employeeId, request);
        const string purpose = "attendance.audit.sessions";
        var digest = Digest(employeeId, request);
        return operation.ReadAsync<AttendanceSessionAuditPage>(AttendanceAuditPermissions.Read, purpose, employeeId, context,
            async (actor, token) =>
            {
                var position = Position(actor, request, purpose, digest);
                var candidates = await repository.SessionsAsync(employeeId, request, position, ScanLimit + 1, token);
                var grants = await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, token);
                var items = new List<AttendanceSessionAuditItem>(); var scanned = 0;
                foreach (var candidate in candidates.Take(ScanLimit))
                {
                    var session = candidate.Session;
                    scanned++; position = new(session.CalculatedAtUtc, session.Id);
                    try
                    {
                        if (session.EmployeeId != employeeId || !InRange(session.AttendanceDate, request) ||
                            session.CheckInPunchId != candidate.CheckIn?.Id || session.CheckOutPunchId != candidate.CheckOut?.Id ||
                            !session.CorrectionId.HasValue && !session.CheckInPunchId.HasValue)
                            throw AttendanceOperation.Conflict(SourceUnavailable);
                        var anchors = new List<(DateOnly Date, Guid? Site)> { (session.AttendanceDate, session.SiteId) };
                        if (candidate.CheckIn is { } checkIn)
                        {
                            ValidatePunch(employeeId, checkIn);
                            if (checkIn.Action != PunchAction.CheckIn) throw AttendanceOperation.Conflict(SourceUnavailable);
                            anchors.Add((BusinessDate(checkIn), checkIn.SiteId));
                        }
                        if (candidate.CheckOut is { } checkOut)
                        {
                            ValidatePunch(employeeId, checkOut);
                            if (checkOut.Action != PunchAction.CheckOut) throw AttendanceOperation.Conflict(SourceUnavailable);
                            anchors.Add((BusinessDate(checkOut), checkOut.SiteId));
                        }
                        var scopes = await SourceScopes(employeeId, anchors, token);
                        if (!AttendanceScopeEvaluation.Allows(actor.Identity, grants, AttendanceAuditPermissions.Read, scopes)) continue;
                        items.Add(new(AttendanceCaptureService.Map(session), session.CalculatedAtUtc,
                            candidate.IsLatestRevision, session.CorrectionId));
                    }
                    catch (AuthenticationFailure failure) when (failure.Code == SourceUnavailable) { continue; }
                    if (items.Count == request.PageSize) break;
                }
                return new(items, Continue(actor, purpose, digest, position, candidates.Count > scanned));
            }, ct, resourceScopes: (_, token) => Historical(employeeId, request.From, request.To.AddDays(-1), token));
    }

    private async Task<PunchValidationResponse?> ReadValidation(RawPunchEvent raw, PunchAssessment? assessment, CancellationToken ct)
    {
        CaptureValidationEvidence? retained;
        try { retained = await validationEvidence.FinalAsync(raw.EmployeeId, raw.Id, ct); }
        catch (AuthenticationFailure failure) when (failure.Code == "capture_validation_final_binding_conflict")
        { throw AttendanceOperation.Conflict(SourceUnavailable); }
        ct.ThrowIfCancellationRequested();
        if (retained is null) return null;
        var final = retained.FinalAssessment;
        var now = clock.GetUtcNow();
        if (retained.Id == Guid.Empty || retained.EmployeeId != raw.EmployeeId || retained.RawPunchEventId != raw.Id ||
            retained.Action != raw.Action || retained.IsOffline != raw.IsOffline ||
            retained.RegisteredDeviceId != raw.RegisteredDeviceId || retained.RequestSha256 != raw.RequestSha256 ||
            retained.ReceivedAtUtc != raw.ReceivedAtUtc || retained.EvaluatedAtUtc.Offset != TimeSpan.Zero ||
            retained.AssessedAtUtc.Offset != TimeSpan.Zero || retained.EvaluatedAtUtc < retained.ReceivedAtUtc ||
            retained.AssessedAtUtc < retained.EvaluatedAtUtc || retained.AssessedAtUtc > now ||
            assessment is null || assessment.Revision != 1 || assessment.AssessedAtUtc != retained.AssessedAtUtc ||
            final is null || final.Decision is null || final.Decision.Outcome != assessment.Outcome ||
            final.Decision.ReasonCode != assessment.ReasonCode || !Enum.IsDefined(final.Decision.Outcome) ||
            retained.CandidateDecision is null || !Enum.IsDefined(retained.CandidateDecision.Outcome) ||
            !CaptureValidationPolicyRules.Text(retained.CandidateDecision.ReasonCode, 100) ||
            !CaptureValidationPolicyRules.Text(final.Decision.ReasonCode, 100) ||
            raw.IsOffline && final.Decision.Outcome != PunchAssessmentOutcome.Unverified ||
            retained.CandidateDecision.Outcome is not (PunchAssessmentOutcome.Accepted or PunchAssessmentOutcome.AcceptedWithException) &&
                final.Decision.Outcome is PunchAssessmentOutcome.Accepted or PunchAssessmentOutcome.AcceptedWithException ||
            final.Codes is not { Count: > 0 } || final.Codes.Any(x => !CaptureValidationPolicyRules.Text(x, 100)) ||
            final.Codes.Distinct(StringComparer.Ordinal).Count() != final.Codes.Count ||
            !final.Codes.Contains(final.Decision.ReasonCode, StringComparer.Ordinal) ||
            final.RelatedRawPunchIds is null || final.RelatedRawPunchIds.Any(x => x == Guid.Empty || x == raw.Id) ||
            final.RelatedRawPunchIds.Distinct().Count() != final.RelatedRawPunchIds.Count)
            throw AttendanceOperation.Conflict(SourceUnavailable);
        var hasPolicy = final.PolicyVersionId.HasValue;
        if (final.PolicyVersionId == Guid.Empty || hasPolicy != final.PolicyRevision.HasValue ||
            hasPolicy != (final.SourceFingerprint is not null) || final.PolicyRevision is < 1 ||
            hasPolicy && !CaptureValidationPolicyRules.Hash(final.SourceFingerprint) ||
            !hasPolicy && final.Decision.Outcome is PunchAssessmentOutcome.Accepted or PunchAssessmentOutcome.AcceptedWithException)
            throw AttendanceOperation.Conflict(SourceUnavailable);
        return new(retained.Id, final.PolicyVersionId, final.PolicyRevision, final.SourceFingerprint,
            Array.AsReadOnly(final.Codes.ToArray()), Array.AsReadOnly(final.RelatedRawPunchIds.ToArray()));
    }

    private async Task<IReadOnlyList<PermissionGrant>> CurrentGrants(AttendanceActor actor, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!await journal.IsCurrentSessionAsync(actor.Identity, ct))
            throw new AuthenticationFailure("invalid_authentication", 401);
        var grants = await access.GetGrantsAsync(actor.Identity.AccountId, clock.GetUtcNow(), ct);
        ct.ThrowIfCancellationRequested();
        if (!AccessPermissionEvaluator.ForSession(actor.Identity, grants).Any(x => x.Permission == AttendanceAuditPermissions.Read))
            throw new AuthenticationFailure("access_denied", 403);
        return grants;
    }

    private async Task<IReadOnlyList<AttendanceResourceScope>> SourceScopes(Guid employeeId,
        IEnumerable<(DateOnly Date, Guid? Site)> anchors, CancellationToken ct)
    {
        var result = new List<AttendanceResourceScope>();
        foreach (var group in anchors.Distinct().GroupBy(x => x.Date))
        {
            var scopes = await Historical(employeeId, group.Key, group.Key, ct);
            result.AddRange(scopes);
            foreach (var anchor in group)
                if (!anchor.Site.HasValue || !scopes.Any(x => x.Alternatives.Any(y => y.SiteId == anchor.Site)))
                    result.Add(new([new AccessResource(EmployeeId: employeeId, SiteId: anchor.Site)]));
        }
        if (!AttendanceScopeEvaluation.IsValid(result)) throw AttendanceOperation.Conflict(SourceUnavailable);
        return result;
    }

    private async Task<IReadOnlyList<AttendanceResourceScope>> Historical(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (from == default || to == DateOnly.MaxValue) throw AttendanceOperation.Conflict(SourceUnavailable);
        IReadOnlyList<AttendanceResourceScope> scopes;
        try { scopes = await summaries.ResourceScopesAsync(employeeId, from, to, ct); }
        catch (AuthenticationFailure failure) when (failure.StatusCode is 404 or 409)
        { throw AttendanceOperation.Conflict(SourceUnavailable); }
        if (!AttendanceScopeEvaluation.IsValid(scopes) || scopes.Any(x => x.Alternatives.Any(y => y.EmployeeId != employeeId)))
            throw AttendanceOperation.Conflict(SourceUnavailable);
        return scopes;
    }

    private AttendanceCursorPosition? Position(AttendanceActor actor, AttendanceAuditRequest request, string purpose, string digest) =>
        request.Cursor is null ? null : cursors.Unprotect(request.Cursor, actor.Identity.AccountId, purpose, digest);
    private string? Continue(AttendanceActor actor, string purpose, string digest, AttendanceCursorPosition? position, bool more) =>
        more && position is not null ? cursors.Protect(actor.Identity.AccountId, purpose, digest, position) : null;
    private static string Digest(Guid employeeId, AttendanceAuditRequest request) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { employeeId, request.From, request.To, request.PageSize })));
    private static DateOnly BusinessDate(RawPunchEvent raw) => raw.BusinessDate ?? RosterSchedule.Today(raw.ReceivedAtUtc);
    private static bool InRange(DateOnly date, AttendanceAuditRequest request) => date >= request.From && date < request.To;
    private static void ValidatePunch(Guid employeeId, RawPunchEvent raw)
    {
        if (raw.EmployeeId != employeeId || BusinessDate(raw) == default || BusinessDate(raw) == DateOnly.MaxValue)
            throw AttendanceOperation.Conflict(SourceUnavailable);
    }
    private static void Validate(Guid employeeId, AttendanceAuditRequest request)
    {
        if (employeeId == Guid.Empty || request.From == default || request.To == DateOnly.MaxValue ||
            request.To <= request.From || request.To.DayNumber - request.From.DayNumber > 366 || request.PageSize is < 1 or > 100)
            throw AttendanceOperation.Invalid("invalid_attendance_audit_request");
    }
}
