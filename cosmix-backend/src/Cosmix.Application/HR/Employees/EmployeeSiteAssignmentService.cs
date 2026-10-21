using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Organization;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;

namespace Cosmix.Application.HR.Employees;

public sealed class EmployeeSiteAssignmentService(IEmployeeSiteAssignmentRepository repository,
    EmployeeOperation operation, MobileAuthenticationOptions options, IStaffTaxonomyResolver? taxonomy = null)
{
    public Task<EmployeeAssignmentPage> ListAsync(Guid employeeId, EmployeeAssignmentListRequest input,
        AuthRequestContext context, CancellationToken ct) => ReadAsync(employeeId, input, false, context, ct);

    public Task<EmployeeAssignmentPage> HistoryAsync(Guid employeeId, EmployeeAssignmentListRequest input,
        AuthRequestContext context, CancellationToken ct) => ReadAsync(employeeId, input, true, context, ct);

    private Task<EmployeeAssignmentPage> ReadAsync(Guid employeeId, EmployeeAssignmentListRequest input,
        bool history, AuthRequestContext context, CancellationToken ct)
    {
        if (input is null || input.Page is < 1 or > 1000000 || input.PageSize is < 1 or > 100)
            throw EmployeeOperation.Invalid("invalid_assignment_list_request");
        return operation.ReadAsync([EmployeePermissions.Read], history ? "hr.employee.assignments.history" : "hr.employee.assignments.list",
            employeeId, context, async (actor, token) =>
            {
                var employee = await repository.FindEmployeeAsync(employeeId, token) ?? throw EmployeeOperation.Missing();
                var rows = await repository.ListAsync(employeeId, input, history ? null : Today(actor.Now), token);
                return new EmployeeAssignmentPage(employeeId, EmployeeHiringService.Version(employee), rows);
            }, ct);
    }

    public Task<EmployeeWriteResult> AssignSiteAsync(Guid employeeId, AssignEmployeeSiteRequest input,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 1000 || input.Reason.Any(char.IsControl))
            throw EmployeeOperation.Invalid("invalid_site_assignment_reason");
        var version = OrganizationOperation.Version(input.ExpectedVersion);
        if (employeeId == Guid.Empty || input.SiteId == Guid.Empty || input.EffectiveFrom == default)
            throw EmployeeOperation.Invalid("invalid_site_assignment_request");

        return operation.WriteAsync([EmployeePermissions.Review, EmployeePermissions.Edit], "hr.employee.site.assigned",
            employeeId, key, input, context, async (actor, token) =>
            {
                var employee = await repository.FindEmployeeAsync(employeeId, token) ?? throw EmployeeOperation.Missing();
                repository.ExpectVersion(employee, version);
                var today = Today(actor.Now);
                if (employee.Status != EmployeeStatus.Active || employee.OnboardingStatus != OnboardingStatus.Approved ||
                    !employee.JoinedOn.HasValue || !await repository.HasApprovedDecisionAsync(employee, token) ||
                    !await repository.HasEligibleEmploymentHistoryAsync(employeeId, actor.Now, token))
                    throw EmployeeOperation.Conflict("employee_not_approved_active");
                if (input.EffectiveFrom < today || input.EffectiveFrom < employee.JoinedOn.Value)
                    throw EmployeeOperation.Invalid("site_assignment_cannot_be_backdated");

                var open = await repository.OpenAssignmentsAsync(employeeId, token);
                if (open.Count != 1) throw EmployeeOperation.Conflict("employee_assignment_state_conflict");
                var previous = open[0];
                if (previous.EffectiveFrom > today || previous.EffectiveFrom < employee.JoinedOn.Value ||
                    await repository.HasConflictingAssignmentAsync(employeeId, previous.Id, previous.EffectiveFrom, token))
                    throw EmployeeOperation.Conflict("employee_assignment_state_conflict");
                // Equal dates cannot preserve a nonempty interval or the unique employee/start key.
                if (input.EffectiveFrom <= previous.EffectiveFrom)
                    throw EmployeeOperation.Conflict("site_assignment_requires_later_start");
                var category = await repository.ActiveStaffCategoryCodeAsync(previous.StaffCategoryId, token);
                if (category is null) throw EmployeeOperation.Conflict("employee_not_site_staff");
                var resolution = taxonomy is null ? StaffTaxonomyResolver.Resolve(category, input.EffectiveFrom, []) :
                    await taxonomy.ResolveAsync(category, input.EffectiveFrom, token);
                if (StaffTaxonomyResolver.RequireMapping(resolution).WorkLocation != StaffWorkLocation.Site || previous.BranchId is not null)
                    throw EmployeeOperation.Conflict("employee_not_site_staff");
                StaffTaxonomyResolver.ValidateLocation(resolution, null, input.SiteId, StaffTaxonomyValidationPhase.Activation);
                if (previous.SiteId == input.SiteId) throw EmployeeOperation.Conflict("employee_already_assigned_to_site");

                var site = await repository.FindSiteAsync(input.SiteId, token);
                if (site is null || !site.IsActive || site.StartsOn > input.EffectiveFrom || site.EndsOn < input.EffectiveFrom)
                    throw EmployeeOperation.Invalid("inactive_or_unknown_assignment_site");

                // Only the open interval may end. A new row preserves the earlier assignment's other references.
                var replacement = new EmployeeAssignment(employee.Id, previous.DepartmentId, previous.DesignationId,
                    previous.StaffCategoryId, previous.EmploymentTypeId, previous.BranchId, input.SiteId,
                    previous.ReportingManagerId, input.EffectiveFrom, null, actor.Identity.AccountId.ToString(), actor.Now);
                previous.EndOn(input.EffectiveFrom);
                repository.AddAssignment(replacement);
                // The tracked child changes advance the employee rowversion through the existing context guard.
                await repository.SaveChangesAsync(token);
                return EmployeeHiringService.Result(employee);
            }, ct, adminReview: true);
    }

    private DateOnly Today(DateTimeOffset now)
    {
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(options.BusinessTimeZoneId)).DateTime); }
        catch (Exception e) when (e is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new AuthenticationFailure("business_timezone_not_configured", 503); }
    }
}
