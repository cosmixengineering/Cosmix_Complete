using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Attendance.Corrections;

namespace Cosmix.Application.Attendance.Corrections;

/// <summary>Read-only exception visibility. Detection is not an approval, resolution or pay decision.</summary>
public sealed class AttendanceExceptionService(AttendanceOperation operation, IAttendanceExceptionRepository repository,
    IAttendanceSummaryRepository summaries, IAccountAccessReader access, IAttendanceCursorProtector cursors)
{
    public Task<AttendanceExceptionNotice> OwnDetailAsync(Guid id, AuthRequestContext context, CancellationToken ct)
    {
        if (id == Guid.Empty) throw AttendanceOperation.Invalid();
        return operation.ReadAsync(CorrectionPermissions.ExceptionsSelf, "attendance.exception.self.detail", null, context,
            async (actor, token) =>
            {
                var row = await Find(actor.Identity.EmployeeId, id, token);
                ValidateSources(row, await repository.SourcesAsync(row, token));
                return Notice(row);
            }, ct, self: true);
    }

    public Task<AttendanceExceptionDetail> DetailAsync(Guid employeeId, Guid id, AuthRequestContext context, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || id == Guid.Empty) throw AttendanceOperation.Invalid();
        return operation.ReadAsync(AttendanceExceptionPermissions.Read, "attendance.exception.detail", employeeId, context,
            async (_, token) =>
            {
                var row = await Find(employeeId, id, token);
                return new AttendanceExceptionDetail(Map(row), row.Reason);
            }, ct, resourceScopes: async (_, token) => await Scopes(await Find(employeeId, id, token), token));
    }

    public Task<AttendanceExceptionQueueResponse> QueueAsync(AttendanceExceptionQueueRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        if (request.From == default || request.To == DateOnly.MaxValue || request.To <= request.From ||
            request.To.DayNumber - request.From.DayNumber > 366 || !Enum.IsDefined(request.Scope) ||
            request.Kind.HasValue && !Enum.IsDefined(request.Kind.Value) || request.PageSize is < 1 or > 100 ||
            request.TargetId == Guid.Empty || (request.Scope == CorrectionQueueScope.All) != !request.TargetId.HasValue)
            throw AttendanceOperation.Invalid("invalid_attendance_exception_queue");
        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request with { Cursor = null })));
        const string purpose = "attendance.exceptions.read";
        return operation.ReadAsync<AttendanceExceptionQueueResponse>(AttendanceExceptionPermissions.Read, purpose, null, context, async (actor, token) =>
        {
            var position = request.Cursor is null ? null : cursors.Unprotect(request.Cursor, actor.Identity.AccountId, purpose, digest);
            const int scanLimit = 200;
            var rows = await repository.CandidatesAsync(request, position, scanLimit + 1, token);
            var grants = await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, token);
            var result = new List<AttendanceExceptionListItem>(); var scanned = 0;
            foreach (var row in rows.Take(scanLimit))
            {
                scanned++; position = new(row.DetectedAtUtc, row.Id);
                IReadOnlyList<AttendanceResourceScope> scopes;
                try { scopes = await Scopes(row, token); }
                catch (AuthenticationFailure failure) when (failure.Code == "attendance_exception_source_unavailable")
                { continue; }
                if (AttendanceScopeEvaluation.Allows(actor.Identity, grants, AttendanceExceptionPermissions.Read, scopes))
                    result.Add(Map(row));
                if (result.Count == request.PageSize) break;
            }
            return new(result, rows.Count > scanned && position is not null ?
                cursors.Protect(actor.Identity.AccountId, purpose, digest, position) : null);
        }, ct, resourceScopes: request.Scope == CorrectionQueueScope.All ? null : async (_, token) =>
        {
            var target = request.TargetId!.Value;
            if (!await repository.ScopeExistsAsync(request.Scope, target, token)) throw AttendanceOperation.Missing();
            // Per-row history is checked below. This gate authorizes the requested collection without
            // requiring unrelated dates elsewhere in the requested employee range.
            return [new([request.Scope switch
            {
                CorrectionQueueScope.Employee => new AccessResource(EmployeeId: target),
                CorrectionQueueScope.Site => new AccessResource(SiteId: target),
                CorrectionQueueScope.Department => new AccessResource(DepartmentId: target),
                CorrectionQueueScope.Team => new AccessResource(TeamId: target),
                CorrectionQueueScope.Project => new AccessResource(ProjectId: target),
                _ => throw AttendanceOperation.Invalid()
            }])];
        });
    }

    private async Task<AttendanceException> Find(Guid employeeId, Guid id, CancellationToken ct)
    {
        var row = await repository.FindAsync(employeeId, id, ct) ?? throw AttendanceOperation.Missing();
        if (row.EmployeeId != employeeId || row.Id != id) throw AttendanceOperation.Missing();
        return row;
    }

    private async Task<IReadOnlyList<AttendanceResourceScope>> Scopes(AttendanceException row, CancellationToken ct)
    {
        var sources = await repository.SourcesAsync(row, ct);
        ValidateSources(row, sources);
        var anchors = new List<(DateOnly Date, Guid? Site)>();
        if (sources.Punch is { } punch) anchors.Add((punch.BusinessDate ?? RosterSchedule.Today(punch.ReceivedAtUtc), punch.SiteId));
        if (sources.Session is { } session) anchors.Add((session.AttendanceDate, session.SiteId));
        var scopes = new List<AttendanceResourceScope>();
        foreach (var date in anchors.Select(x => x.Date).Append(row.AttendanceDate).Distinct())
        {
            IReadOnlyList<AttendanceResourceScope> historical;
            try { historical = await summaries.ResourceScopesAsync(row.EmployeeId, date, date, ct); }
            catch (AuthenticationFailure failure) when (failure.StatusCode is 404 or 409)
            { throw AttendanceOperation.Conflict("attendance_exception_source_unavailable"); }
            if (!AttendanceScopeEvaluation.IsValid(historical) || historical.Any(x => x.Alternatives.Any(y => y.EmployeeId != row.EmployeeId)))
                throw AttendanceOperation.Conflict("attendance_exception_source_unavailable");
            scopes.AddRange(historical);
            // Preserve original sites even after a retroactive assignment change. Never borrow a
            // different day's team/department/project path for a retained source site.
            foreach (var anchor in anchors.Where(x => x.Date == date).Distinct())
                if (!anchor.Site.HasValue || !historical.Any(x => x.Alternatives.Any(y => y.SiteId == anchor.Site)))
                    scopes.Add(new([new AccessResource(EmployeeId: row.EmployeeId, SiteId: anchor.Site)]));
        }
        if (!AttendanceScopeEvaluation.IsValid(scopes)) throw AttendanceOperation.Conflict("attendance_exception_source_unavailable");
        return scopes;
    }

    private static void ValidateSources(AttendanceException row, AttendanceExceptionSources sources)
    {
        if (row.AttendanceDate == default || row.AttendanceDate == DateOnly.MaxValue ||
            row.RawPunchEventId != sources.Punch?.Id || sources.Punch is { } punch && punch.EmployeeId != row.EmployeeId ||
            row.WorkSessionId != sources.Session?.Id || sources.Session is { } session &&
                (session.EmployeeId != row.EmployeeId || session.AttendanceDate != row.AttendanceDate) ||
            row.DailyAttendanceSummaryId != sources.Summary?.Id || sources.Summary is { } summary &&
                (summary.EmployeeId != row.EmployeeId || summary.AttendanceDate != row.AttendanceDate))
            throw AttendanceOperation.Conflict("attendance_exception_source_unavailable");
    }

    private static AttendanceExceptionListItem Map(AttendanceException row) => new(row.Id, row.EmployeeId,
        row.AttendanceDate, row.Kind.ToString(), row.DetectedAtUtc, row.RawPunchEventId, row.WorkSessionId, row.DailyAttendanceSummaryId);

    internal static AttendanceExceptionNotice Notice(AttendanceException row) => new(row.Id, row.AttendanceDate,
        row.Kind switch
        {
            AttendanceExceptionKind.MissingCheckOut => "missing_checkout",
            AttendanceExceptionKind.OverlappingShift => "overlapping_shift_requires_review",
            AttendanceExceptionKind.SequenceError => "punch_sequence_requires_review",
            AttendanceExceptionKind.OutsideGeofence => "location_outside_attendance_boundary",
            AttendanceExceptionKind.UnreliableLocation => "location_quality_requires_review",
            AttendanceExceptionKind.FaceVerification => "check_in_verification_required",
            _ => "attendance_evidence_requires_review"
        }, row.DetectedAtUtc, row.RawPunchEventId, row.WorkSessionId, row.DailyAttendanceSummaryId);
}
