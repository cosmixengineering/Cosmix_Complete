using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.HR.Employees;

public sealed class EmployeeUpdateService(EmployeeOperation operation, IEmployeeHiringRepository hiring,
    IEmployeeLifecycleRepository lifecycle, IEmployeeUpdateRepository repository, EmployeeUpdateSections sections,
    IAccessManagementRepository access, MobileAuthenticationOptions options, IPhoneNumberNormalizer phones, EmployeeFieldPolicy fieldPolicy)
{
    public static string RequestType(EmployeeChangeSection section)
    {
        EmployeeUpdateValidation.Section(section, request: true);
        return "EmployeeUpdate." + section;
    }

    public Task<EmployeeWriteResult> UpdateBasicAsync(Guid employeeId, UpdateEmployeeBasic input, string key,
        AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync([EmployeePermissions.Read, EmployeeUpdatePermissions.BasicEdit], "hr.employee.basic.updated",
            employeeId, key, input, context, async (actor, token) =>
            {
                if (input is null) throw EmployeeOperation.Invalid("invalid_employee_basic_change");
                var data = new EmployeeUpdateData(Basic: input.Data);
                EmployeeUpdateValidation.Data(EmployeeChangeSection.Basic, data, Today(actor.Now));
                data = EmployeeUpdateValidation.Normalize(data, phones);
                EmployeeUpdateValidation.Reason(input.Reason);
                var employee = await EmployeeAsync(employeeId, token);
                await ValidateFieldPolicyAsync(data, Today(actor.Now), employee, token);
                hiring.ExpectVersion(employee, OrganizationOperation.Version(input.ExpectedVersion));
                await EligibleAsync(employee, actor.Now, token);
                var before = await sections.LoadAsync(employee, EmployeeChangeSection.Basic, null, token);
                if (Equal(before.Data, data)) throw EmployeeOperation.Conflict("employee_change_has_no_changes");
                await sections.ValidateAsync(employee, EmployeeChangeSection.Basic, data, Today(actor.Now), actor.Now, token);
                var after = await sections.ApplyAsync(employee, EmployeeChangeSection.Basic, data, Today(actor.Now), actor, token);
                var change = await RecordAsync(employeeId, EmployeeChangeSection.Basic, before, after, null, input.Reason, actor, token);
                await hiring.SaveChangesAsync(token);
                return EmployeeHiringService.Result(employee) with { EmployeeChangeId = change.Id };
            }, ct);

    public Task<EmployeeWriteResult> RequestAsync(Guid employeeId, RequestEmployeeChange input, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (input is null) throw EmployeeOperation.Invalid("invalid_employee_change_request");
        var type = RequestType(input.Section);
        return operation.WriteAsync(EmployeeUpdateValidation.RequestPermissions(input.Section), "hr.employee.change.requested",
            employeeId, key, input, context, async (actor, token) =>
            {
                var today = Today(actor.Now);
                EmployeeUpdateValidation.Data(input.Section, input.Data, today);
                var proposed = EmployeeUpdateValidation.Normalize(input.Data, phones);
                EmployeeUpdateValidation.Reason(input.Reason);
                var employee = await EmployeeAsync(employeeId, token);
                await ValidateFieldPolicyAsync(proposed, today, employee, token);
                hiring.ExpectVersion(employee, OrganizationOperation.Version(input.ExpectedVersion));
                await EligibleAsync(employee, actor.Now, token);
                await ConsistentAsync(employeeId, input.Section, token);
                var before = await sections.LoadAsync(employee, input.Section, null, token);
                var effective = Schedule(employee, input.Section, before, input.EffectiveFrom, today);
                if (Equal(before.Data, proposed)) throw EmployeeOperation.Conflict("employee_change_has_no_changes");
                await sections.ValidateAsync(employee, input.Section, proposed, effective, actor.Now, token);
                if (await repository.HasPendingAsync(employeeId, type, token)) throw EmployeeOperation.Conflict("employee_change_request_outstanding");
                var snapshot = new EmployeeChangeSnapshot(1, employeeId, input.Section, before, proposed, input.EffectiveFrom, input.Reason.Trim(),
                    await FieldAuthorityVersionAsync(input.Section, today, token));
                var request = new ApprovalRequest(type, employeeId, actor.Identity.AccountId.ToString(),
                    await sections.WriteAsync(snapshot, employeeId, "changes/proposal", token), 1, actor.Now);
                repository.AddRequest(request);
                await hiring.SaveChangesAsync(token);
                return EmployeeHiringService.Result(employee) with { ChangeRequestId = request.Id };
            }, ct);
    }

    public Task<EmployeeWriteResult> DecideAsync(Guid employeeId, EmployeeChangeSection section, Guid requestId,
        DecideEmployeeChange input, string key, AuthRequestContext context, CancellationToken ct)
    {
        var type = RequestType(section);
        var permissions = EmployeeUpdateValidation.ReadPermissions(section).Append(EmployeeUpdatePermissions.Review).ToList();
        if (section is EmployeeChangeSection.Identity or EmployeeChangeSection.Medical) permissions.Add(EmployeePermissions.DocumentsRead);
        return operation.WriteAsync(permissions.ToArray(), "hr.employee.change.decided", employeeId, key,
            new { section, requestId, input }, context, async (actor, token) =>
            {
                if (input is null || input.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected))
                    throw EmployeeOperation.Invalid("invalid_employee_change_decision");
                EmployeeUpdateValidation.Reason(input.Reason, input.Decision != ApprovalState.Approved, 2000);
                var employee = await EmployeeAsync(employeeId, token);
                hiring.ExpectVersion(employee, OrganizationOperation.Version(input.ExpectedEmployeeVersion));
                var request = await FindAsync(employeeId, type, requestId, token);
                repository.ExpectVersion(request, OrganizationOperation.Version(input.ExpectedRequestVersion));
                if (request.State != ApprovalState.Pending) throw EmployeeOperation.Conflict("employee_change_request_closed");
                if (request.RequesterUserId == actor.Identity.AccountId.ToString() || actor.Identity.EmployeeId == employeeId)
                    throw EmployeeOperation.Denied();
                EmployeeChange? change = null;
                if (input.Decision == ApprovalState.Approved)
                {
                    await EligibleAsync(employee, actor.Now, token);
                    await ConsistentAsync(employeeId, section, token);
                    var snapshot = await SnapshotAsync(request, section, token);
                    var today = Today(actor.Now);
                    if (snapshot.FoundationFieldPolicyVersion != await FieldAuthorityVersionAsync(section, today, token))
                        throw EmployeeOperation.Conflict("employee_field_policy_changed_review_required");
                    EmployeeUpdateValidation.Data(section, snapshot.Proposed, today);
                    await ValidateFieldPolicyAsync(snapshot.Proposed, today, employee, token);
                    var before = await sections.LoadAsync(employee, section, null, token);
                    if (!Equal(before, snapshot.Before)) throw EmployeeOperation.Conflict("employee_details_changed_submit_new_request");
                    if (snapshot.EffectiveFrom < today) throw EmployeeOperation.Conflict("employee_change_date_elapsed_return_request");
                    var effective = Schedule(employee, section, before, snapshot.EffectiveFrom, today);
                    await sections.ValidateAsync(employee, section, snapshot.Proposed, effective, actor.Now, token);
                    var after = await sections.ApplyAsync(employee, section, snapshot.Proposed, effective, actor, token);
                    change = await RecordAsync(employeeId, section, before, after, request.Id, snapshot.Reason, actor, token);
                }
                var decision = request.Decide(input.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode,
                    input.Reason?.Trim(), actor.Now, change is null ? null : change.AfterPrivateReference);
                repository.AddDecision(decision);
                await hiring.SaveChangesAsync(token);
                return EmployeeHiringService.Result(employee) with { ChangeRequestId = requestId, DecisionId = decision.Id, EmployeeChangeId = change?.Id };
            }, ct, fixedAdminPermission: EmployeeUpdatePermissions.Review);
    }

    public Task<EmployeeWriteResult> CancelAsync(Guid employeeId, EmployeeChangeSection section, Guid requestId,
        CancelEmployeeChange input, string key, AuthRequestContext context, CancellationToken ct)
    {
        var type = RequestType(section);
        return operation.WriteAsync(EmployeeUpdateValidation.RequestPermissions(section), "hr.employee.change.cancelled", employeeId, key,
            new { section, requestId, input }, context, async (actor, token) =>
            {
                if (input is null) throw EmployeeOperation.Invalid("invalid_employee_change_request");
                var employee = await EmployeeAsync(employeeId, token);
                var request = await FindAsync(employeeId, type, requestId, token);
                repository.ExpectVersion(request, OrganizationOperation.Version(input.ExpectedRequestVersion));
                if (request.RequesterUserId != actor.Identity.AccountId.ToString()) throw EmployeeOperation.Denied();
                if (request.State != ApprovalState.Pending) throw EmployeeOperation.Conflict("employee_change_request_closed");
                request.Cancel(actor.Identity.AccountId.ToString(), actor.Now);
                await hiring.SaveChangesAsync(token);
                return EmployeeHiringService.Result(employee) with { ChangeRequestId = requestId };
            }, ct);
    }

    public Task<EmployeeCurrentUpdate> CurrentAsync(Guid employeeId, EmployeeChangeSection section,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(EmployeeUpdateValidation.ReadPermissions(section), "hr.employee.current." + section, employeeId, context,
            async (actor, token) =>
            {
                var employee = await EmployeeAsync(employeeId, token);
                await ConsistentAsync(employeeId, section, token);
                var today = Today(actor.Now);
                var current = await sections.LoadAsync(employee, section, today, token);
                var latest = await sections.LoadAsync(employee, section, null, token);
                if (current.EffectiveTo <= today) current = current with { Data = new(), BankVerified = null };
                if (latest.RecordId != current.RecordId && latest.EffectiveFrom > today &&
                    (!current.EffectiveTo.HasValue || current.EffectiveTo > latest.EffectiveFrom))
                    current = current with { EffectiveTo = latest.EffectiveFrom };
                return new EmployeeCurrentUpdate(employeeId, EmployeeHiringService.Version(employee), section, today,
                    current, latest.EffectiveFrom > today ? latest : null);
            }, ct);

    public Task<PagedResult<EmployeeChangeSummary>> HistoryAsync(Guid employeeId, EmployeeChangeSection section,
        EmployeeChangePageQuery query, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(EmployeeUpdateValidation.ReadPermissions(section), "hr.employee.changes.history." + section, employeeId, context,
            async (_, token) =>
            {
                if (query is null) throw EmployeeOperation.Invalid("invalid_employee_change_query");
                EmployeeUpdateValidation.Page(query.Page, query.PageSize);
                await EmployeeAsync(employeeId, token);
                return await repository.HistoryAsync(employeeId, section, query, token);
            }, ct);

    public Task<EmployeeChangeHistoryDetail> HistoryDetailAsync(Guid employeeId, EmployeeChangeSection section, Guid changeId,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(EmployeeUpdateValidation.ReadPermissions(section), "hr.employee.changes.evidence." + section, employeeId, context,
            async (_, token) =>
            {
                await EmployeeAsync(employeeId, token);
                var change = await repository.FindChangeAsync(employeeId, section, changeId, token) ?? throw EmployeeOperation.Missing();
                return new EmployeeChangeHistoryDetail(new(change.Id, employeeId, section, change.EffectiveFrom, change.ApprovalRequestId,
                    change.RecordedByUserId, change.RecordedAt), await sections.ReadAsync<EmployeeUpdateValue>(change.BeforePrivateReference, employeeId, "changes/history", token),
                    await sections.ReadAsync<EmployeeUpdateValue>(change.AfterPrivateReference, employeeId, "changes/history", token), change.Reason);
            }, ct, auditTarget: _ => changeId.ToString());

    public Task<PagedResult<EmployeeChangeRequestSummary>> RequestsAsync(Guid? employeeId, EmployeeChangeSection section,
        EmployeeChangeQuery query, AuthRequestContext context, CancellationToken ct)
    {
        var type = RequestType(section);
        return operation.ReadAsync(EmployeeUpdateValidation.ReadPermissions(section), "hr.employee.changes.requests." + section, employeeId, context,
            async (_, token) =>
            {
                if (query is null) throw EmployeeOperation.Invalid("invalid_employee_change_query");
                EmployeeUpdateValidation.Page(query.Page, query.PageSize);
                if (query.State.HasValue && !Enum.IsDefined(query.State.Value)) throw EmployeeOperation.Invalid("invalid_employee_change_state");
                if (employeeId.HasValue) await EmployeeAsync(employeeId.Value, token);
                return await repository.RequestsAsync(employeeId, type, query, token);
            }, ct);
    }

    public Task<EmployeeChangeRequestDetail> DetailAsync(Guid employeeId, EmployeeChangeSection section, Guid requestId,
        AuthRequestContext context, CancellationToken ct)
    {
        var type = RequestType(section);
        return operation.ReadAsync(EmployeeUpdateValidation.ReadPermissions(section), "hr.employee.changes.request_read." + section, employeeId, context,
            async (_, token) =>
            {
                await EmployeeAsync(employeeId, token);
                var request = await FindAsync(employeeId, type, requestId, token);
                var snapshot = await SnapshotAsync(request, section, token);
                var decisions = await repository.DecisionsAsync(requestId, token);
                return new EmployeeChangeRequestDetail(new(request.Id, employeeId, request.State, request.RequesterUserId,
                    request.RequestedAt, request.ClosedAt, Convert.ToBase64String(request.RowVersion)), section, snapshot.Before, snapshot.Proposed,
                    snapshot.EffectiveFrom, snapshot.Reason, decisions.Select(x => new EmployeeChangeDecision(x.Id, x.Decision, x.ActorUserId,
                        x.ActorRoleCode, x.Reason, x.DecidedAt)).ToArray());
            }, ct, auditTarget: _ => requestId.ToString());
    }

    private async Task<Employee> EmployeeAsync(Guid employeeId, CancellationToken ct)
    {
        if (employeeId == Guid.Empty) throw EmployeeOperation.Invalid("invalid_employee_id");
        var employee = await hiring.FindAsync(employeeId, ct) ?? throw EmployeeOperation.Missing();
        if (!await lifecycle.HasApprovedEmploymentAsync(employee, ct)) throw EmployeeOperation.Conflict("employee_not_approved");
        return employee;
    }

    private Task<string?> FieldAuthorityVersionAsync(EmployeeChangeSection section, DateOnly date, CancellationToken ct) =>
        section is EmployeeChangeSection.Basic or EmployeeChangeSection.Identity or EmployeeChangeSection.Documents
            ? fieldPolicy.AuthorityVersionAsync(date, ct) : Task.FromResult<string?>(null);

    private async Task ValidateFieldPolicyAsync(EmployeeUpdateData data, DateOnly today, Employee employee, CancellationToken ct)
    {
        if (data.Basic is { } basic && (await fieldPolicy.MissingFieldsAsync(new() { MaritalStatus = basic.MaritalStatus }, today, ct)).Contains("maritalStatus"))
            throw EmployeeOperation.Invalid("employee_basic_details_incomplete");
        if (data.Identity is { } identity)
        {
            var profile = new EmployeeProfileInput { Gender = identity.Gender, FatherOrSpouseName = identity.FatherOrSpouseName,
                DateOfBirth = identity.DateOfBirth, IdentityDocumentType = identity.IdentityDocumentType,
                IdentityDocumentExpiresOn = identity.IdentityDocumentExpiresOn, JoinedOn = employee.JoinedOn };
            var relevant = (await fieldPolicy.MissingFieldsAsync(profile, today, ct)).Where(x => x is "gender" or "fatherOrSpouseName" or "identityDocumentExpiresOn" or "dateOfBirth.ageAtJoining");
            if (relevant.Any()) throw EmployeeOperation.Invalid("employee_identity_details_incomplete");
        }
    }

    private async Task EligibleAsync(Employee employee, DateTimeOffset now, CancellationToken ct)
    {
        var changes = await lifecycle.CurrentChangesAsync(employee.Id, now, ct);
        if (employee.Status != EmployeeStatus.Active || !employee.JoinedOn.HasValue ||
            (changes.Count > 0 && changes[0].NewStatus != EmployeeStatus.Active) ||
            (changes.Count > 1 && changes[0].EffectiveAt == changes[1].EffectiveAt) || await lifecycle.HasFutureChangeAsync(employee.Id, now, ct))
            throw EmployeeOperation.Conflict("employee_update_requires_active_unscheduled_employment");
        var account = await lifecycle.FindAccountAsync(employee.Id, ct);
        if (account is not null && await access.HasSystemRoleAsync(account.Id, now, includeScheduled: true, ct))
            throw EmployeeOperation.Conflict("protected_owner_requires_separate_handover");
    }

    private async Task ConsistentAsync(Guid employeeId, EmployeeChangeSection section, CancellationToken ct)
    {
        if (await repository.HasConflictingHistoryAsync(employeeId, section, ct)) throw EmployeeOperation.Conflict("employee_change_history_requires_review");
    }

    private static DateOnly Schedule(Employee employee, EmployeeChangeSection section, EmployeeUpdateValue previous, DateOnly? requested, DateOnly today)
    {
        if (!EmployeeUpdateValidation.Dated(section))
        {
            if (requested.HasValue) throw EmployeeOperation.Invalid("employee_profile_changes_take_effect_on_approval");
            return today;
        }
        if (!requested.HasValue || requested == default(DateOnly) || requested < today || requested < employee.JoinedOn)
            throw EmployeeOperation.Invalid("employee_change_effective_date_required_no_backdating");
        if (previous.RecordId.HasValue)
        {
            if (previous.EffectiveFrom > today) throw EmployeeOperation.Conflict("employee_scheduled_change_outstanding");
            if (requested <= previous.EffectiveFrom) throw EmployeeOperation.Conflict("employee_change_requires_later_start");
            if (previous.EffectiveTo.HasValue && requested < previous.EffectiveTo)
                throw EmployeeOperation.Conflict("employee_change_overlaps_recorded_period");
        }
        return requested.Value;
    }

    private async Task<EmployeeChange> RecordAsync(Guid employeeId, EmployeeChangeSection section, EmployeeUpdateValue before,
        EmployeeUpdateValue after, Guid? requestId, string reason, EmployeeActor actor, CancellationToken ct)
    {
        var previous = await repository.LastChangeAsync(employeeId, section, ct);
        var change = new EmployeeChange(employeeId, section, after.EffectiveFrom, requestId,
            await sections.WriteAsync(before, employeeId, "changes/history", ct), await sections.WriteAsync(after, employeeId, "changes/history", ct),
            reason.Trim(), actor.Identity.AccountId.ToString(), actor.Now, previous is null ? 1 : checked(previous.Revision + 1));
        repository.AddChange(change);
        return change;
    }

    private async Task<ApprovalRequest> FindAsync(Guid employeeId, string type, Guid id, CancellationToken ct) =>
        await repository.FindRequestAsync(employeeId, type, id, ct) ?? throw EmployeeOperation.Missing();
    private async Task<EmployeeChangeSnapshot> SnapshotAsync(ApprovalRequest request, EmployeeChangeSection section, CancellationToken ct)
    {
        var snapshot = await sections.ReadAsync<EmployeeChangeSnapshot>(request.SnapshotPrivateReference, request.SubjectId, "changes/proposal", ct);
        if (snapshot.SchemaVersion != 1 || snapshot.EmployeeId != request.SubjectId || snapshot.Section != section || snapshot.Before is null || snapshot.Proposed is null)
            throw EmployeeOperation.Conflict("employee_change_snapshot_invalid");
        EmployeeUpdateValidation.Reason(snapshot.Reason);
        return snapshot;
    }

    private DateOnly Today(DateTimeOffset now)
    {
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(options.BusinessTimeZoneId)).DateTime); }
        catch (Exception error) when (error is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new AuthenticationFailure("business_timezone_not_configured", 503); }
    }
    private static bool Equal<T>(T left, T right) => JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);
}
