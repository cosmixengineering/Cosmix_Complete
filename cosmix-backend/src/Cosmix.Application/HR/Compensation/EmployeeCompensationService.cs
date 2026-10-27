using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.HR.Compensation;

public sealed partial class EmployeeCompensationService(EmployeeOperation operation, IEmployeeHiringRepository hiring,
    IEmployeeLifecycleRepository lifecycle, IEmployeeCompensationRepository repository, IPrivateFileStore files,
    MobileAuthenticationOptions options)
{
    public const string RequestType = "EmployeeCompensationChange";

    public Task<EmployeeWriteResult> RequestAsync(Guid employeeId, RequestCompensationChange input,
        string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync([EmployeeCompensationPermissions.Request, EmployeeCompensationPermissions.Read],
            "hr.employee.compensation.requested", employeeId, key, input, context, async (actor, token) =>
            {
                return await StageInTransactionAsync(employeeId, input, actor, token);
            }, ct);

    public Task<EmployeeWriteResult> DecideAsync(Guid employeeId, Guid requestId, DecideCompensationChange input,
        string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync([EmployeeCompensationPermissions.Review, EmployeeCompensationPermissions.Read],
            "hr.employee.compensation.decided", employeeId, key, new { requestId, input }, context, async (actor, token) =>
            {
                if (input is null || input.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected))
                    throw EmployeeOperation.Invalid("invalid_compensation_decision");
                ValidateReason(input.Reason, input.Decision != ApprovalState.Approved, 2000);
                var employee = await EmployeeAsync(employeeId, token);
                hiring.ExpectVersion(employee, OrganizationOperation.Version(input.ExpectedEmployeeVersion));
                var request = await FindRequestAsync(employeeId, requestId, token);
                repository.ExpectVersion(request, OrganizationOperation.Version(input.ExpectedRequestVersion));
                if (request.State != ApprovalState.Pending) throw EmployeeOperation.Conflict("compensation_request_closed");
                if (request.RequesterUserId == actor.Identity.AccountId.ToString() || employeeId == actor.Identity.EmployeeId)
                    throw EmployeeOperation.Denied();
                CompensationRecord? record = null;
                string? changeReference = null;
                if (input.Decision == ApprovalState.Approved)
                {
                    await RequireEligibleAsync(employee, actor.Now, token);
                    var snapshot = await SnapshotAsync(request, token);
                    var today = Today(actor.Now);
                    var window = await WindowAsync(employeeId, today, token);
                    var before = window.Latest is null ? null : Map(window.Latest);
                    if (before != snapshot.Before)
                        throw EmployeeOperation.Conflict("compensation_changed_submit_new_request");
                    if (snapshot.EffectiveFrom < today)
                        throw EmployeeOperation.Conflict("compensation_effective_date_elapsed_return_request");
                    ValidateSchedule(employee, window.Latest, snapshot.Before?.Id, snapshot.EffectiveFrom, today);
                    var revision = window.Latest is null ? 1 : checked(window.Latest.Revision + 1);
                    var terms = snapshot.Proposed;
                    record = new CompensationRecord(employeeId, revision, window.Latest?.Id,
                        terms.BaseAmount, terms.GrossAmount, terms.Currency, terms.WageBasis, terms.PaymentMethod,
                        snapshot.EffectiveFrom, null, request.Id, actor.Identity.AccountId.ToString(), actor.Now);
                    var changes = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new { before, after = Map(record) }),
                        Purpose(employeeId), token);
                    changeReference = JsonSerializer.Serialize(changes);
                }
                var decision = request.Decide(input.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode,
                    input.Reason?.Trim(), actor.Now, changeReference);
                repository.AddDecision(decision);
                if (record is not null) repository.AddCompensation(record);
                await hiring.SaveChangesAsync(token);
                return EmployeeHiringService.Result(employee) with
                { CompensationRequestId = request.Id, DecisionId = decision.Id, CompensationRecordId = record?.Id };
            }, ct, fixedAdminPermission: EmployeeCompensationPermissions.Review);

    public Task<EmployeeWriteResult> CancelAsync(Guid employeeId, Guid requestId, CancelCompensationChange input,
        string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync([EmployeeCompensationPermissions.Request, EmployeeCompensationPermissions.Read],
            "hr.employee.compensation.cancelled", employeeId, key, new { requestId, input }, context, async (actor, token) =>
            {
                if (input is null) throw EmployeeOperation.Invalid("invalid_compensation_request");
                var employee = await EmployeeAsync(employeeId, token);
                var request = await FindRequestAsync(employeeId, requestId, token);
                repository.ExpectVersion(request, OrganizationOperation.Version(input.ExpectedRequestVersion));
                if (request.RequesterUserId != actor.Identity.AccountId.ToString()) throw EmployeeOperation.Denied();
                if (request.State != ApprovalState.Pending) throw EmployeeOperation.Conflict("compensation_request_closed");
                request.Cancel(actor.Identity.AccountId.ToString(), actor.Now);
                await hiring.SaveChangesAsync(token);
                return EmployeeHiringService.Result(employee) with { CompensationRequestId = request.Id };
            }, ct);

    public Task<EmployeeCompensationState> StateAsync(Guid employeeId, DateOnly? asOf,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync([EmployeeCompensationPermissions.Read], "hr.employee.compensation.read", employeeId,
            context, async (actor, token) =>
            {
                var employee = await EmployeeAsync(employeeId, token);
                var date = asOf ?? Today(actor.Now);
                if (date == default) throw EmployeeOperation.Invalid("invalid_compensation_date");
                var window = await WindowAsync(employeeId, date, token);
                var current = window.Candidate is not null &&
                    (!window.Candidate.EffectiveTo.HasValue || date < window.Candidate.EffectiveTo.Value)
                    ? Map(window.Candidate, window.Next?.EffectiveFrom) : null;
                // Resolve the scheduled row's end too, including any legacy subsequent version.
                var nextWindow = window.Next is null ? null : await repository.WindowAsync(employeeId, window.Next.EffectiveFrom, token);
                var next = window.Next is null ? null : Map(window.Next, nextWindow?.Next?.EffectiveFrom);
                return new EmployeeCompensationState(employeeId, EmployeeHiringService.Version(employee), date,
                    current, next, window.Latest?.Id);
            }, ct, auditTarget: result => $"{employeeId}:{result.AsOf:yyyy-MM-dd}:{result.Effective?.Id}");

    public Task<PagedResult<CompensationEntry>> HistoryAsync(Guid employeeId, CompensationPageQuery input,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync([EmployeeCompensationPermissions.Read], "hr.employee.compensation.history", employeeId,
            context, async (_, token) =>
            {
                if (input is null) throw EmployeeOperation.Invalid("invalid_compensation_query");
                ValidatePage(input.Page, input.PageSize);
                await EmployeeAsync(employeeId, token);
                await RequireConsistentHistoryAsync(employeeId, token);
                return await repository.HistoryAsync(employeeId, input, token);
            }, ct);

    public Task<PagedResult<CompensationRequestSummary>> RequestsAsync(Guid? employeeId, CompensationRequestQuery input,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync([EmployeeCompensationPermissions.Read], "hr.employee.compensation.requests", employeeId,
            context, async (_, token) =>
            {
                if (input is null) throw EmployeeOperation.Invalid("invalid_compensation_query");
                ValidatePage(input.Page, input.PageSize);
                if (input.State.HasValue && !Enum.IsDefined(input.State.Value)) throw EmployeeOperation.Invalid("invalid_compensation_state");
                if (employeeId.HasValue) await EmployeeAsync(employeeId.Value, token);
                return await repository.RequestsAsync(employeeId, input, token);
            }, ct);

    public Task<CompensationRequestDetail> DetailAsync(Guid employeeId, Guid requestId, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync([EmployeeCompensationPermissions.Read], "hr.employee.compensation.request_read", employeeId,
            context, async (_, token) =>
            {
                await EmployeeAsync(employeeId, token);
                var request = await FindRequestAsync(employeeId, requestId, token);
                var snapshot = await SnapshotAsync(request, token);
                var decisions = await repository.DecisionsAsync(requestId, token);
                return new CompensationRequestDetail(new(request.Id, employeeId, request.State, request.RequesterUserId,
                    request.RequestedAt, request.ClosedAt, Convert.ToBase64String(request.RowVersion)), snapshot.Before,
                    snapshot.Proposed, snapshot.EffectiveFrom, snapshot.Reason,
                    decisions.Select(d => new CompensationDecision(d.Id, d.Decision, d.ActorUserId, d.ActorRoleCode, d.Reason, d.DecidedAt)).ToArray());
            }, ct, auditTarget: _ => requestId.ToString());

    private async Task<Employee> EmployeeAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) throw EmployeeOperation.Invalid("invalid_employee_id");
        var employee = await hiring.FindAsync(id, ct) ?? throw EmployeeOperation.Missing();
        if (!await lifecycle.HasApprovedEmploymentAsync(employee, ct)) throw EmployeeOperation.Conflict("employee_not_approved");
        return employee;
    }

    private async Task RequireEligibleAsync(Employee employee, DateTimeOffset now, CancellationToken ct)
    {
        var current = await lifecycle.CurrentChangesAsync(employee.Id, now, ct);
        if (employee.Status != EmployeeStatus.Active || !employee.JoinedOn.HasValue ||
            (current.Count > 0 && current[0].NewStatus != EmployeeStatus.Active) ||
            (current.Count > 1 && current[0].EffectiveAt == current[1].EffectiveAt) ||
            await lifecycle.HasFutureChangeAsync(employee.Id, now, ct))
            throw EmployeeOperation.Conflict("compensation_requires_active_unscheduled_employment");
    }

    private static void ValidateSchedule(Employee employee, CompensationRecord? latest, Guid? expectedId, DateOnly effectiveFrom, DateOnly today)
    {
        if (effectiveFrom == default || effectiveFrom < today || effectiveFrom < employee.JoinedOn)
            throw EmployeeOperation.Invalid("compensation_cannot_be_backdated");
        if (expectedId == Guid.Empty || latest?.Id != expectedId)
            throw EmployeeOperation.Conflict("compensation_version_conflict");
        if (latest is null) return;
        if (latest.EffectiveFrom > today) throw EmployeeOperation.Conflict("compensation_scheduled_change_outstanding");
        if (effectiveFrom <= latest.EffectiveFrom) throw EmployeeOperation.Conflict("compensation_requires_later_start");
        if (latest.EffectiveTo.HasValue && effectiveFrom < latest.EffectiveTo)
            throw EmployeeOperation.Conflict("compensation_overlaps_recorded_period");
        if (latest.Revision == int.MaxValue) throw EmployeeOperation.Conflict("compensation_revision_limit");
    }

    private async Task RequireConsistentHistoryAsync(Guid employeeId, CancellationToken ct)
    {
        if (await repository.HasConflictingHistoryAsync(employeeId, ct))
            throw EmployeeOperation.Conflict("compensation_history_requires_review");
    }

    private async Task<CompensationWindow> WindowAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        await RequireConsistentHistoryAsync(employeeId, ct);
        return await repository.WindowAsync(employeeId, date, ct);
    }

    private async Task<ApprovalRequest> FindRequestAsync(Guid employeeId, Guid requestId, CancellationToken ct) =>
        await repository.FindRequestAsync(employeeId, requestId, ct) ?? throw EmployeeOperation.Missing();

    private async Task<CompensationChangeSnapshot> SnapshotAsync(ApprovalRequest request, CancellationToken ct)
    {
        try
        {
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(request.SnapshotPrivateReference);
            if (reference is null) throw EmployeeOperation.Conflict("compensation_snapshot_unavailable");
            var snapshot = JsonSerializer.Deserialize<CompensationChangeSnapshot>(await files.ReadAsync(reference, Purpose(request.SubjectId), ct));
            if (snapshot is null || snapshot.SchemaVersion != 1 || snapshot.EmployeeId != request.SubjectId ||
                snapshot.Proposed is null || snapshot.EffectiveFrom == default)
                throw EmployeeOperation.Conflict("compensation_snapshot_invalid");
            ValidateTerms(snapshot.Proposed.BaseAmount, snapshot.Proposed.GrossAmount, snapshot.Proposed.Currency,
                snapshot.Proposed.WageBasis, snapshot.Proposed.PaymentMethod);
            ValidateReason(snapshot.Reason, true, 1000);
            return snapshot;
        }
        catch (JsonException) { throw EmployeeOperation.Conflict("compensation_snapshot_invalid"); }
        catch (AuthenticationFailure error) when (error.StatusCode == 400)
        { throw EmployeeOperation.Conflict("compensation_snapshot_invalid"); }
    }

    private static CompensationTerms ValidateTerms(decimal? baseAmount, decimal? grossAmount, string currency,
        WageBasis wageBasis, PaymentMethod paymentMethod)
    {
        const decimal maximum = 9999999999999999.99m;
        if (!baseAmount.HasValue || !grossAmount.HasValue || baseAmount < 0 || grossAmount < baseAmount ||
            baseAmount > maximum || grossAmount > maximum || decimal.Round(baseAmount.Value, 2) != baseAmount.Value ||
            decimal.Round(grossAmount.Value, 2) != grossAmount.Value || currency is null || currency.Length != 3 ||
            currency.Any(c => c is < 'A' or > 'Z') || !Enum.IsDefined(wageBasis) || !Enum.IsDefined(paymentMethod))
            throw EmployeeOperation.Invalid("invalid_compensation_terms");
        return new(baseAmount.Value, grossAmount.Value, currency, wageBasis, paymentMethod);
    }

    private static void ValidateReason(string? reason, bool required, int max)
    {
        if ((required && string.IsNullOrWhiteSpace(reason)) || reason?.Length > max || reason?.Any(char.IsControl) == true)
            throw EmployeeOperation.Invalid("invalid_compensation_reason");
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 100)
            throw EmployeeOperation.Invalid("invalid_compensation_pagination");
    }

    private DateOnly Today(DateTimeOffset now)
    {
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(options.BusinessTimeZoneId)).DateTime); }
        catch (Exception error) when (error is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new AuthenticationFailure("business_timezone_not_configured", 503); }
    }

    private static CompensationEntry Map(CompensationRecord row, DateOnly? nextStart = null)
    {
        var end = row.EffectiveTo;
        if (nextStart.HasValue && (!end.HasValue || nextStart < end)) end = nextStart;
        return new(row.Id, row.Revision, row.SupersedesCompensationRecordId,
            new(row.BaseAmount, row.GrossAmount, row.Currency, row.WageBasis, row.PaymentMethod), row.EffectiveFrom,
            end, row.EffectiveTo, row.ApprovalRequestId, row.RecordedByUserId, row.RecordedAt);
    }

    private static string Purpose(Guid employeeId) => EmployeeHiringService.Purpose(employeeId, "compensation");
}
