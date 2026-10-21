using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.CarryForward;

public sealed partial class CarryForwardService
{
    public Task<CarryForwardRequestPage> HistoryAsync(Guid? employeeId, CarryForwardHistoryRequest request, bool self,
        AuthRequestContext context, CancellationToken ct)
    {
        ValidateEmployee(employeeId, self); ValidateRange(request.From, request.To, request.PageSize);
        if (request.State.HasValue && !Enum.IsDefined(request.State.Value)) throw AttendanceOperation.Invalid();
        var purpose = "attendance.carry-forward.requester";
        var hash = CarryForwardEvidence.Fingerprint(new { employeeId, self, request.From, request.To, request.State, request.PageSize });
        return operation.ReadAsync(Permission(self), purpose, employeeId, context, async (actor, token) =>
        {
            var employee = Employee(actor, employeeId, self);
            return await Scan(new(request.From, request.To, CorrectionQueueScope.Employee, employee, request.State ?? ApprovalState.Pending, request.PageSize, request.Cursor),
                request.State is null, true, self, actor, Permission(self), purpose, hash, token);
        }, ct, self: self, resourceScopes: self ? null : (_, token) => summaries.ResourceScopesAsync(employeeId!.Value, request.From, request.To.AddDays(-1), token));
    }
    public Task<CarryForwardRequestPage> QueueAsync(CorrectionQueueRequest request, AuthRequestContext context, CancellationToken ct) => QueueCore(request,context,ct,false);
    public Task<CarryForwardRequestPage> AlternativeQueueAsync(CorrectionQueueRequest request, AuthRequestContext context, CancellationToken ct) => QueueCore(request,context,ct,true);
    private Task<CarryForwardRequestPage> QueueCore(CorrectionQueueRequest request, AuthRequestContext context, CancellationToken ct, bool alternative)
    {
        ValidateRange(request.From, request.To, request.PageSize);
        if (!Enum.IsDefined(request.Scope) || !Enum.IsDefined(request.State) || request.TargetId == Guid.Empty ||
            (request.Scope == CorrectionQueueScope.All) != !request.TargetId.HasValue) throw AttendanceOperation.Invalid("invalid_carry_forward_queue");
        var purpose = alternative ? "attendance.carry-forward.alternative.review" : "attendance.carry-forward.review";
        var permission = alternative ? CorrectionPermissions.AlternativesDecide : CorrectionPermissions.Review;
        var hash = CarryForwardEvidence.Fingerprint(request with { Cursor = null });
        return operation.ReadAsync(permission, purpose, null, context,
            (actor, token) => Scan(request, false, false, false, actor, permission, purpose, hash, token, alternative), ct,
            resourceScopes: request.Scope == CorrectionQueueScope.All ? null : async (_, token) =>
            {
                var target = request.TargetId!.Value;
                if (!await review.ScopeExistsAsync(request.Scope, target, token)) throw AttendanceOperation.Missing();
                if (request.Scope == CorrectionQueueScope.Employee) return await summaries.ResourceScopesAsync(target, request.From, request.To.AddDays(-1), token);
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
    private async Task<CarryForwardRequestPage> Scan(CorrectionQueueRequest request, bool allStates, bool own, bool self,
        AttendanceActor actor, string permission, string purpose, string hash, CancellationToken ct, bool alternative = false)
    {
        if (alternative && actor.RoleCode != "ADMIN") throw AttendanceOperation.Denied();
        var position = request.Cursor is null ? null : cursors.Unprotect(request.Cursor, actor.Identity.AccountId, purpose, hash);
        const int scanLimit = 200;
        var rows = await repository.CandidatesAsync(request, position, scanLimit + 1, own ? actor.Identity.AccountId : null, ct, allStates);
        var grants = await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, ct);
        var result = new List<CarryForwardRequestResponse>(); var scanned = 0;
        foreach (var candidate in rows.Take(scanLimit))
        {
            scanned++; position = new(candidate.Correction.RequestedAtUtc, candidate.Correction.Id);
            var (row, retained) = await evidence.RequestAsync(candidate.Correction.EmployeeId, candidate.Correction.Id, ct);
            if (!own && alternative != (retained.AlternativeEvidence is not null)) continue;
            if (own && retained.RequesterAccountId != actor.Identity.AccountId || self && row.Correction.EmployeeId != actor.Identity.EmployeeId) continue;
            if (!self && !AttendanceScopeEvaluation.Allows(actor.Identity, grants, permission, await RequestScopes(row.Correction.EmployeeId, row.Correction.Id, ct))) continue;
            if (alternative) await CorrectionAlternativePolicyService.RequireAdmin(actor, permission,
                await RequestScopes(row.Correction.EmployeeId, row.Correction.Id, ct), access, authority, clock, ct);
            if (row.Adjustment is not null) _ = await evidence.AdjustmentAsync(row.Adjustment, ct);
            result.Add(Map(row, retained)); if (result.Count == request.PageSize) break;
        }
        return new(result, rows.Count > scanned && position is not null ? cursors.Protect(actor.Identity.AccountId, purpose, hash, position) : null);
    }
    public Task<CarryForwardMonthResponse> MonthAsync(Guid? employeeId, CarryForwardMonthRequest request, bool self,
        AuthRequestContext context, CancellationToken ct)
    {
        ValidateEmployee(employeeId, self);
        if (request.Month == default || request.Month.Day != 1 || request.Month.Year == 9999 && request.Month.Month == 12 ||
            request.Page is < 1 or > 1000000 || request.PageSize is < 1 or > 100) throw AttendanceOperation.Invalid("invalid_carry_forward_month");
        var end = request.Month.AddMonths(1); var permission = self ? CorrectionPermissions.Self : CorrectionPermissions.Review;
        return operation.ReadAsync(permission, "attendance.carry-forward.month", employeeId, context, async (actor, token) =>
        {
            var employee = Employee(actor, employeeId, self); var rows = await repository.MonthAsync(employee, request.Month, end, token);
            if (rows.Count > 4096) throw AttendanceOperation.Conflict("carry_forward_month_limit");
            var grants = await access.GetGrantsAsync(actor.Identity.AccountId, actor.Now, token);
            long ticks = 0, minutes = 0; var entries = new List<CarryForwardEntryResponse>();
            foreach (var row in rows)
            {
                var retained = await evidence.AdjustmentAsync(row, token);
                if (row.EmployeeId != employee || row.TargetMonthStart != request.Month || row.TargetMonthEndExclusive != end) throw AttendanceOperation.Conflict("carry_forward_evidence_unavailable");
                if (!self && !AttendanceScopeEvaluation.Allows(actor.Identity, grants, permission,
                    [.. retained.OriginalScopes, .. await CurrentScopes(employee, row.SourceBusinessDate, request.Month, end, token)])) throw AttendanceOperation.Denied();
                var delta = CarryForwardRules.MinuteDelta(row.BeforeWorkedTicks, row.AfterWorkedTicks);
                try { ticks = checked(ticks + row.DeltaWorkedTicks); minutes = checked(minutes + delta); }
                catch (OverflowException) { throw AttendanceOperation.Conflict("carry_forward_duration_limit"); }
                entries.Add(new(row.Id, row.SourceBusinessDate, row.Revision, row.CorrectionId, row.SourcePeriodLockEventId, row.SourceSummaryId, row.TargetMonthStart, row.DeltaWorkedTicks, delta, row.AppliedAtUtc));
            }
            // Aggregate only after every original/current scope is checked; no count or hidden sum leak.
            return new CarryForwardMonthResponse(employee, request.Month, end, entries.OrderBy(x => x.SourceBusinessDate).ThenBy(x => x.Revision)
                .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArray(), entries.Count, request.Page, request.PageSize, ticks, minutes);
        }, ct, self: self, resourceScopes: self ? null : (_, token) => summaries.ResourceScopesAsync(employeeId!.Value, request.Month, end.AddDays(-1), token));
    }
    private static void ValidateRange(DateOnly from, DateOnly to, int pageSize)
    {
        ValidateDate(from);
        if (to <= from || to.DayNumber - from.DayNumber > 366 || pageSize is < 1 or > 100) throw AttendanceOperation.Invalid("invalid_carry_forward_range");
    }
}
