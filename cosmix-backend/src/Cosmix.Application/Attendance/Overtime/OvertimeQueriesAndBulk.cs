using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;

namespace Cosmix.Application.Attendance.Overtime;

public sealed partial class OvertimeService
{
    public Task<OvertimePage> HistoryAsync(Guid? employeeId, OvertimeHistoryRequest request, bool self, AuthRequestContext context, CancellationToken ct)
    {
        RequireTarget(employeeId, self);
        return QueueCore(new(request.From, request.To, self ? CorrectionQueueScope.All : CorrectionQueueScope.Employee,
            employeeId, request.State, request.PageSize, request.Cursor), self, requesterOnly: !self, context, ct);
    }
    public Task<OvertimePage> QueueAsync(OvertimeQueueRequest request, AuthRequestContext context, CancellationToken ct) =>
        QueueCore(request, self: false, requesterOnly: false, context, ct);
    private Task<OvertimePage> QueueCore(OvertimeQueueRequest request, bool self, bool requesterOnly, AuthRequestContext context, CancellationToken ct)
    {
        if (request.From == default || request.To <= request.From || request.To.DayNumber - request.From.DayNumber > 366 ||
            request.PageSize is < 1 or > 100 || !Enum.IsDefined(request.Scope) || request.State.HasValue && !Enum.IsDefined(request.State.Value) ||
            request.TargetId == Guid.Empty || (request.Scope == CorrectionQueueScope.All) != !request.TargetId.HasValue)
            throw AttendanceOperation.Invalid("invalid_overtime_queue");
        var permission = self ? OvertimePermissions.Self : requesterOnly ? OvertimePermissions.Submit : OvertimePermissions.Review;
        var purpose = self ? "attendance.overtime.self-history" : requesterOnly ? "attendance.overtime.requester-history" : "attendance.overtime.review";
        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request with { Cursor = null })));
        return operation.ReadAsync(permission, purpose, null, context, async (actor, token) =>
        {
            var query = self ? request with { Scope = CorrectionQueueScope.Employee, TargetId = actor.Identity.EmployeeId } : request;
            var after = request.Cursor is null ? null : cursors.Unprotect(request.Cursor, actor.Identity.AccountId, purpose, digest);
            var candidates = await repository.CandidatesAsync(query, after, 201, requesterOnly ? actor.Identity.AccountId : null, token);
            IReadOnlyList<PermissionGrant> grants = self ? [] : await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, token);
            var result = new List<OvertimeResponse>(); var scanned = 0;
            foreach (var row in candidates.Take(200))
            {
                scanned++; after = new(row.Request.RequestedAtUtc, row.Request.Id);
                var data = await Evidence(row.Request.EmployeeId, row.Request.Id, token);
                if (self ? row.Request.EmployeeId == actor.Identity.EmployeeId : AttendanceScopeEvaluation.Allows(actor.Identity, grants,
                    permission, [.. data.Snapshot.Inputs.OriginalScopes, .. await summaries.ResourceScopesAsync(row.Request.EmployeeId, row.BusinessDate, row.BusinessDate, token)]))
                    result.Add(Map(data.Row, data.Decision));
                if (result.Count == request.PageSize) break;
            }
            return new OvertimePage(result, candidates.Count > scanned && after is not null ? cursors.Protect(actor.Identity.AccountId, purpose, digest, after) : null);
        }, ct, self: self, resourceScopes: self || request.Scope == CorrectionQueueScope.All ? null : async (_, token) =>
        {
            var target = request.TargetId!.Value;
            if (!await employment.ScopeExistsAsync(request.Scope, target, token)) throw AttendanceOperation.Missing();
            if (request.Scope == CorrectionQueueScope.Employee) return await summaries.ResourceScopesAsync(target, request.From, request.To.AddDays(-1), token);
            return [new([request.Scope switch
            {
                CorrectionQueueScope.Site => new AccessResource(SiteId: target),
                CorrectionQueueScope.Department => new AccessResource(DepartmentId: target),
                CorrectionQueueScope.Team => new AccessResource(TeamId: target),
                CorrectionQueueScope.Project => new AccessResource(ProjectId: target),
                _ => throw AttendanceOperation.Invalid()
            }])];
        });
    }
    public Task<OvertimeBulkResponse> SubmitBulkAsync(SubmitOvertimeBulkRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        if (request.Employees is not { Count: >= 1 and <= 25 } || request.Employees.Any(x => x is null || x.EmployeeId == Guid.Empty || x.Request is null) ||
            request.Employees.Select(x => x.EmployeeId).Distinct().Count() != request.Employees.Count) throw AttendanceOperation.Invalid("invalid_overtime_named_batch");
        foreach (var item in request.Employees) OvertimeRules.Validate(item.Request);
        return operation.WriteAsync(OvertimePermissions.Submit, "attendance.overtime.submit.bulk", null, key, request, request.Reason, context,
            async (actor, token) =>
            {
                var validated = new List<(OvertimeBulkItem Item, OvertimeSourceSnapshot Source)>();
                foreach (var item in request.Employees) validated.Add((item, await PrepareSubmission(item.EmployeeId, item.Request, actor, false, token)));
                var bulkId = Guid.NewGuid(); var rows = new List<OvertimeRecord>();
                foreach (var item in validated) rows.Add(await StageSubmission(item.Item.EmployeeId, item.Item.Request, item.Source, actor, bulkId, token));
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<OvertimeBulkResponse>(bulkId, null, new(bulkId, rows.Select(x => Map(x, null)).ToArray()));
            }, ct, resourceScopes: async (_, token) =>
            {
                var required = new List<AttendanceResourceScope>();
                foreach (var item in request.Employees) required.AddRange(await summaries.ResourceScopesAsync(item.EmployeeId, item.Request.BusinessDate, item.Request.BusinessDate, token));
                return required;
            }, beforeReplay: async (_, token) =>
            {
                foreach (var item in request.Employees)
                {
                    await sources.ValidateSubmissionReplayAsync(item.EmployeeId, item.Request, token);
                    await RequireWorkAvailable(item.EmployeeId, item.Request.BusinessDate,
                        [new(item.Request.StartsAtUtc, item.Request.EndsAtUtc)], token);
                }
            });
    }
    public Task<OvertimeBulkResponse> DecideBulkAsync(DecideOvertimeBulkRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        if (request.Employees is not { Count: >= 1 and <= 25 } || request.Employees.Any(x => x is null || x.EmployeeId == Guid.Empty || x.RequestId == Guid.Empty || x.Decision is null) ||
            request.Employees.Select(x => x.EmployeeId).Distinct().Count() != request.Employees.Count ||
            request.Employees.Select(x => x.RequestId).Distinct().Count() != request.Employees.Count) throw AttendanceOperation.Invalid("invalid_overtime_named_batch");
        foreach (var item in request.Employees) ValidateDecision(item.Decision);
        return operation.WriteAsync(OvertimePermissions.Decide, "attendance.overtime.decide.bulk", null, key, request, request.Reason, context,
            async (actor, token) =>
            {
                var validated = new List<(OvertimeBulkDecisionItem Item, PreparedDecision Prepared)>();
                foreach (var item in request.Employees) validated.Add((item, await PrepareDecision(item.EmployeeId, item.RequestId, item.Decision, actor, token)));
                var before = validated.Select(x => Map(x.Prepared.Data.Row, x.Prepared.Data.Decision)).ToArray();
                var responses = new List<OvertimeResponse>();
                foreach (var item in validated) responses.Add(await StageDecision(item.Prepared, item.Item.Decision, actor, context, token));
                await repository.SaveChangesAsync(token);
                var bulkId = Guid.NewGuid();
                return new AttendanceChange<OvertimeBulkResponse>(bulkId, before,
                    new(bulkId, responses.Select((row, index) => row with { Version = Convert.ToBase64String(validated[index].Prepared.Data.Row.Approval.RowVersion) }).ToArray()));
            }, ct, resourceScopes: async (_, token) =>
            {
                var required = new List<AttendanceResourceScope>();
                foreach (var item in request.Employees) required.AddRange(await ReviewScopes(item.EmployeeId, item.RequestId, token));
                return required;
            }, beforeReplay: async (_, token) =>
            {
                foreach (var item in request.Employees)
                {
                    await sources.RevalidateTaxonomyAsync((await Evidence(item.EmployeeId, item.RequestId, token)).Snapshot.Inputs, token);
                    await ValidateApprovedWorkReplayAsync(item.EmployeeId, item.RequestId, token);
                }
            });
    }
}
