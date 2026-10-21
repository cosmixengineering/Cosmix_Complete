using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Common.Audit;

namespace Cosmix.Application.Attendance.FaceVerification;

public sealed record FaceEnrollmentHistoryRequest(DateTimeOffset FromUtc, DateTimeOffset ToUtc,
    int PageSize = 25, string? Cursor = null);
public sealed record FaceEnrollmentAttemptResponse(Guid AttemptId, DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc, string Stage, string Code, string Provider, string ProviderVersion);
public sealed record FaceEnrollmentAttemptPage(IReadOnlyList<FaceEnrollmentAttemptResponse> Items,
    string? NextCursor);

public interface IFaceEnrollmentHistoryRepository
{
    Task<IReadOnlyList<AuditEvent>> CandidatesAsync(Guid employeeId, DateTimeOffset fromUtc,
        DateTimeOffset toUtc, AttendanceCursorPosition? before, int take, CancellationToken ct);
}

/// <summary>Reads retained journal metadata; this is neither template access nor provider acceptance.
/// Candidate scanning is bounded and hidden original scopes never contribute a public total count.</summary>
public sealed class FaceEnrollmentHistoryService(AttendanceOperation operation, IFaceRepository employees,
    IFaceEnrollmentHistoryRepository repository, FaceEnrollmentJournal journal, IAccountAccessReader access,
    IAttendanceCursorProtector cursors)
{
    private const int CandidateLimit = 100;
    private const string CursorPurpose = "attendance.face.enrollment.history.v1";

    public Task<FaceEnrollmentAttemptPage> HistoryAsync(Guid employeeId, FaceEnrollmentHistoryRequest request,
        AuthRequestContext context, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || request is null || request.FromUtc.Offset != TimeSpan.Zero ||
            request.ToUtc.Offset != TimeSpan.Zero || request.FromUtc >= request.ToUtc ||
            request.ToUtc - request.FromUtc > TimeSpan.FromDays(366) || request.PageSize is < 1 or > 100 ||
            request.Cursor is { Length: > 4096 } || request.Cursor is not null && string.IsNullOrWhiteSpace(request.Cursor))
            throw AttendanceOperation.Invalid("invalid_face_enrollment_history_request");
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { employeeId, request.FromUtc, request.ToUtc, request.PageSize })));
        return operation.ReadAsync(AttendanceCapturePermissions.FaceRead, "attendance.face.enrollment.attempt.history",
            employeeId, context, async (actor, token) =>
            {
                var before = request.Cursor is null ? null : cursors.Unprotect(request.Cursor,
                    actor.Identity.AccountId, CursorPurpose, fingerprint);
                var candidates = await repository.CandidatesAsync(employeeId, request.FromUtc, request.ToUtc,
                    before, CandidateLimit + 1, token);
                var grants = await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, token);
                var items = new List<FaceEnrollmentAttemptResponse>();
                AttendanceCursorPosition? last = null;
                var scanned = 0;
                foreach (var candidate in candidates.Take(CandidateLimit))
                {
                    scanned++;
                    last = new(candidate.OccurredAt, candidate.Id);
                    var start = await journal.ReadHistoryStartAsync(candidate, employeeId, token);
                    if (!start.Resources.All(resource => AccessPermissionEvaluator.Allows(actor.Identity, grants,
                            AttendanceCapturePermissions.FaceRead, resource))) continue;
                    var terminal = await journal.ReadHistoryOutcomeAsync(start, token);
                    items.Add(new(start.ReceiptId, start.StartedAtUtc, terminal?.CompletedAtUtc,
                        terminal?.Stage ?? "reconciliation_pending", terminal?.Code ?? "face_enrollment_reconciliation_pending",
                        start.Provider, start.ProviderVersion));
                    if (items.Count == request.PageSize) break;
                }
                // Continuation describes remaining bounded scanning, never the number of visible/hidden records.
                // An empty page may still have a cursor after original-scope filtering.
                var next = last is not null && candidates.Count > scanned
                    ? cursors.Protect(actor.Identity.AccountId, CursorPurpose, fingerprint, last) : null;
                return new FaceEnrollmentAttemptPage(items, next);
            }, ct, resources: (actor, token) => employees.ResourcesAsync(employeeId, RosterSchedule.Today(actor.Now), token));
    }
}
