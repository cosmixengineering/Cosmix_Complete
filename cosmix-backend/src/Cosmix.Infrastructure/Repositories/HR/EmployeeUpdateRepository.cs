using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class EmployeeUpdateRepository(CosmixDbContext db, MobileAuthenticationOptions options,
    IStaffTaxonomyResolver? taxonomy = null)
    : IEmployeeUpdateRepository
{
    private const int MaximumManagerNodes = 256;
    private const int MaximumTimelineRows = 256;
    private const int MaximumGraphRows = 4096;

    public async Task<IReadOnlyList<EmployeeAddress>> AddressesAsync(Guid employeeId, CancellationToken ct) =>
        await db.EmployeeAddresses.Where(x => x.EmployeeId == employeeId)
            .OrderBy(x => x.AddressType).ThenBy(x => x.Id).ToArrayAsync(ct);

    public Task<EmployeeEmergencyContact?> EmergencyAsync(Guid employeeId, CancellationToken ct) =>
        db.EmployeeEmergencyContacts.SingleOrDefaultAsync(x => x.EmployeeId == employeeId, ct);

    public Task<EmployeeAssignment?> AssignmentAsync(Guid employeeId, DateOnly? asOf, CancellationToken ct)
    {
        var rows = db.EmployeeAssignments.Where(x => x.EmployeeId == employeeId);
        if (asOf.HasValue) rows = rows.Where(x => x.EffectiveFrom <= asOf.Value);
        // Keep this tracked: the service ends the previous period in the same transaction.
        return rows.OrderByDescending(x => x.EffectiveFrom).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
    }

    public Task<EmployeeBankDetails?> BankAsync(Guid employeeId, DateOnly? asOf, CancellationToken ct)
    {
        var rows = db.EmployeeBankDetails.AsNoTracking().Where(x => x.EmployeeId == employeeId);
        if (asOf.HasValue) rows = rows.Where(x => x.EffectiveFrom <= asOf.Value);
        // An expired newest version must not reveal an older open version as current.
        return rows.OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Revision)
            .ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
    }

    public Task<EmployeeMedicalRecord?> MedicalAsync(Guid employeeId, DateOnly? asOf, CancellationToken ct)
    {
        var rows = db.EmployeeMedicalRecords.AsNoTracking().Where(x => x.EmployeeId == employeeId);
        if (asOf.HasValue) rows = rows.Where(x => x.EffectiveOn <= asOf.Value);
        return rows.OrderByDescending(x => x.EffectiveOn).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
    }

    public Task<EmployeeSubmission?> ApprovedSubmissionAsync(Employee employee, CancellationToken ct) =>
        (from decision in db.EmployeeOnboardingDecisions.AsNoTracking()
         join submission in db.EmployeeSubmissions.AsNoTracking() on decision.SubmissionId equals submission.Id
         where employee.OnboardingStatus == OnboardingStatus.Approved &&
             decision.Id == employee.ApprovedOnboardingDecisionId && decision.EmployeeId == employee.Id &&
             decision.Decision == OnboardingDecision.Approve && decision.Provenance == OnboardingProvenance.HrWorkflow &&
             submission.EmployeeId == employee.Id && submission.Provenance == OnboardingProvenance.HrWorkflow
         select submission).SingleOrDefaultAsync(ct);

    public async Task ValidateBasicAsync(EmployeeBasicInput data, CancellationToken ct)
    {
        foreach (var cityId in new[] { data.CurrentAddress.CityId, data.PermanentAddress.CityId }
            .Where(x => x.HasValue).Distinct())
            if (!await db.Cities.AnyAsync(x => x.Id == cityId && x.IsActive, ct))
                throw EmployeeOperation.Invalid("inactive_or_unknown_address_city");
    }

    public async Task ValidateIdentityUniqueAsync(Guid employeeId, EmployeeIdentityInput data, CancellationToken ct)
    {
        var number = string.Concat(data.IdentityDocumentNumber.Where(c => !char.IsWhiteSpace(c) && c != '-')).ToUpperInvariant();
        var email = string.IsNullOrWhiteSpace(data.OfficialEmail) ? null : data.OfficialEmail.Trim().ToUpperInvariant();
        if (await db.Employees.AnyAsync(x => x.Id != employeeId &&
            ((x.IdentityDocumentType == data.IdentityDocumentType && x.NormalizedIdentityDocumentNumber == number) ||
             (email != null && x.NormalizedOfficialEmail == email)), ct))
            throw EmployeeOperation.Conflict("employee_identity_or_email_conflict");
    }

    public async Task ValidateEmploymentAsync(Guid employeeId, EmployeeEmploymentInput data, EmployeeAssignment previous,
        DateOnly effectiveFrom, DateTimeOffset now, CancellationToken ct)
    {
        if (!data.ReportingManagerEmployeeId.HasValue)
            throw EmployeeOperation.Invalid("ineligible_reporting_manager");
        if (previous.EmployeeId != employeeId || previous.EffectiveTo.HasValue || effectiveFrom <= previous.EffectiveFrom ||
            await ConflictingAssignments(employeeId).AnyAsync(ct))
            throw EmployeeOperation.Conflict("employee_assignment_history_conflict");
        if (!await db.Departments.AnyAsync(x => x.Id == data.DepartmentId && x.IsActive, ct) ||
            !await db.Designations.AnyAsync(x => x.Id == data.DesignationId && x.IsActive, ct) ||
            (data.BranchId.HasValue && !await db.Branches.AnyAsync(x => x.Id == data.BranchId && x.IsActive, ct)))
            throw EmployeeOperation.Invalid("inactive_or_unknown_employee_master");
        var category = await db.StaffCategories.AsNoTracking().Where(x => x.Id == previous.StaffCategoryId && x.IsActive)
            .Select(x => x.Code).SingleOrDefaultAsync(ct);
        if (category is null) throw EmployeeOperation.Invalid("unsupported_staff_category");
        var resolution = taxonomy is null ? StaffTaxonomyResolver.Resolve(category, effectiveFrom, []) :
            await taxonomy.ResolveAsync(category, effectiveFrom, ct);
        var mapping = StaffTaxonomyResolver.RequireMapping(resolution);
        if (resolution.Baseline)
        {
            if (mapping.WorkLocation == StaffWorkLocation.Site && data.BranchId.HasValue) throw EmployeeOperation.Invalid("site_staff_cannot_select_office");
            if (mapping.WorkLocation == StaffWorkLocation.Office && !data.BranchId.HasValue) throw EmployeeOperation.Invalid("office_staff_requires_office");
        }
        // This flow preserves the existing assignment category and site. It cannot convert the employee's taxonomy.
        StaffTaxonomyResolver.ValidateLocation(resolution, data.BranchId, previous.SiteId, StaffTaxonomyValidationPhase.Activation);
        if (previous.SiteId.HasValue && !await db.Sites.AnyAsync(x => x.Id == previous.SiteId && x.IsActive &&
            (x.StartsOn == null || x.StartsOn <= effectiveFrom) && (x.EndsOn == null || x.EndsOn >= effectiveFrom), ct))
            throw EmployeeOperation.Invalid("inactive_or_unknown_employee_site");
        if (data.ReportingManagerEmployeeId is Guid managerId)
            await ValidateManagerGraphAsync(employeeId, managerId, effectiveFrom, now, ct);
    }

    private IQueryable<EmployeeAssignment> ConflictingAssignments(Guid employeeId)
    {
        var rows = db.EmployeeAssignments.AsNoTracking().Where(x => x.EmployeeId == employeeId);
        return rows.Where(row => (row.EffectiveTo != null && row.EffectiveTo <= row.EffectiveFrom) ||
            rows.Any(other => other.Id != row.Id && (other.EffectiveFrom == row.EffectiveFrom ||
                ((row.EffectiveTo == null || other.EffectiveFrom < row.EffectiveTo) &&
                 (other.EffectiveTo == null || row.EffectiveFrom < other.EffectiveTo)))));
    }

    private async Task ValidateManagerGraphAsync(Guid employeeId, Guid managerId, DateOnly effectiveFrom,
        DateTimeOffset now, CancellationToken ct)
    {
        if (managerId == employeeId) throw EmployeeOperation.Invalid("ineligible_reporting_manager");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(options.BusinessTimeZoneId); }
        catch (Exception error) when (error is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new AuthenticationFailure("business_timezone_not_configured", 503); }

        var graph = new Dictionary<Guid, ManagerTimeline>();
        var pending = new Queue<Guid>();
        var boundaries = new SortedSet<DateOnly> { effectiveFrom };
        var rowCount = 0;
        pending.Enqueue(managerId);
        while (pending.TryDequeue(out var id))
        {
            ct.ThrowIfCancellationRequested();
            if (id == employeeId || graph.ContainsKey(id)) continue;
            if (graph.Count >= MaximumManagerNodes) throw EmployeeOperation.Conflict("reporting_manager_graph_limit");
            if (await ConflictingAssignments(id).AnyAsync(ct))
                throw EmployeeOperation.Conflict("reporting_manager_history_conflict");
            var assignments = await db.EmployeeAssignments.AsNoTracking().Where(x => x.EmployeeId == id &&
                    (x.EffectiveTo == null || x.EffectiveTo > effectiveFrom))
                .OrderBy(x => x.EffectiveFrom).ThenBy(x => x.Id).Take(MaximumTimelineRows + 1).ToArrayAsync(ct);
            var statuses = await db.EmployeeStatusChanges.AsNoTracking().Where(x => x.EmployeeId == id)
                .OrderBy(x => x.EffectiveAt).ThenBy(x => x.Id).Take(MaximumTimelineRows + 1).ToArrayAsync(ct);
            rowCount += assignments.Length + statuses.Length;
            if (assignments.Length > MaximumTimelineRows || statuses.Length > MaximumTimelineRows || rowCount > MaximumGraphRows)
                throw EmployeeOperation.Conflict("reporting_manager_graph_limit");
            var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            var approved = employee is not null && await db.EmployeeOnboardingDecisions.AsNoTracking().AnyAsync(x =>
                x.Id == employee.ApprovedOnboardingDecisionId && x.EmployeeId == id && x.Decision == OnboardingDecision.Approve, ct);
            graph.Add(id, new(employee, approved, assignments, statuses));
            foreach (var assignment in assignments)
            {
                if (assignment.EffectiveFrom > effectiveFrom) boundaries.Add(assignment.EffectiveFrom);
                if (assignment.EffectiveTo is DateOnly end && end > effectiveFrom) boundaries.Add(end);
                if (assignment.ReportingManagerId is Guid next) pending.Enqueue(next);
            }
        }

        // Every reachable future start/end can change the reporting path, including a cycle introduced later.
        foreach (var date in boundaries)
        {
            var visited = new HashSet<Guid> { employeeId };
            Guid? next = managerId;
            var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
                throw EmployeeOperation.Conflict("reporting_manager_effective_time_invalid");
            var at = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
            if (at < now) at = now;
            while (next is Guid id)
            {
                if (!visited.Add(id)) throw EmployeeOperation.Invalid("reporting_manager_cycle");
                if (!graph.TryGetValue(id, out var timeline) || !EligibleManager(timeline, date, now, at, zone))
                    throw EmployeeOperation.Invalid("ineligible_reporting_manager");
                var matches = timeline.Assignments.Where(x => x.EffectiveFrom <= date &&
                    (x.EffectiveTo == null || date < x.EffectiveTo)).Take(2).ToArray();
                if (matches.Length != 1) throw EmployeeOperation.Conflict("reporting_manager_history_conflict");
                next = matches[0].ReportingManagerId;
            }
        }
    }

    private static bool EligibleManager(ManagerTimeline timeline, DateOnly date, DateTimeOffset now,
        DateTimeOffset at, TimeZoneInfo zone)
    {
        var employee = timeline.Employee;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        if (employee is null || !timeline.Approved || employee.Status != EmployeeStatus.Active ||
            employee.OnboardingStatus != OnboardingStatus.Approved || employee.JoinedOn is not DateOnly joined ||
            joined > today || joined > date) return false;
        var history = timeline.Statuses;
        // Hiring approval is the Active baseline; no lifecycle rows is a valid initial history.
        var status = EmployeeStatus.Active;
        var currentStatus = EmployeeStatus.Active;
        for (var index = 0; index < history.Length; index++)
        {
            var change = history[index];
            if (change.EffectiveAt > at) break;
            if ((index > 0 && history[index - 1].EffectiveAt == change.EffectiveAt) ||
                !Enum.IsDefined(change.NewStatus) || change.NewStatus is EmployeeStatus.Draft or EmployeeStatus.PendingApproval ||
                change.NewStatus == change.PreviousStatus || change.PreviousStatus != status || change.EffectiveAt < change.RecordedAt ||
                !change.ApprovalRequestId.HasValue || change.ApprovalRequestId == Guid.Empty)
                return false;
            status = change.NewStatus;
            if (change.EffectiveAt <= now) currentStatus = status;
        }
        return currentStatus == EmployeeStatus.Active && status == EmployeeStatus.Active;
    }

    private sealed record ManagerTimeline(Employee? Employee, bool Approved, EmployeeAssignment[] Assignments,
        EmployeeStatusChange[] Statuses);

    public Task<bool> HasConflictingHistoryAsync(Guid employeeId, EmployeeChangeSection section, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (section == EmployeeChangeSection.Employment) return ConflictingAssignments(employeeId).AnyAsync(ct);
        if (section == EmployeeChangeSection.Medical)
        {
            var rows = db.EmployeeMedicalRecords.AsNoTracking().Where(x => x.EmployeeId == employeeId);
            return rows.AnyAsync(row => rows.Any(other => other.Id != row.Id && other.EffectiveOn == row.EffectiveOn), ct);
        }
        if (section == EmployeeChangeSection.Bank)
        {
            var rows = db.EmployeeBankDetails.AsNoTracking().Where(x => x.EmployeeId == employeeId);
            // Decreasing consecutive predecessors and unique revisions imply exactly one revision-one root.
            // Bank rows have no exact EmployeeChange/approval FK; journal linkage cannot be inferred from a date.
            return rows.AnyAsync(row => row.Revision < 1 ||
                (row.EffectiveTo != null && row.EffectiveTo <= row.EffectiveFrom) ||
                rows.Any(other => other.Id != row.Id && (other.Revision == row.Revision || other.EffectiveFrom == row.EffectiveFrom)) ||
                (row.Revision == 1 && row.SupersedesEmployeeBankDetailsId != null) ||
                (row.Revision > 1 && !rows.Any(previous => previous.Id == row.SupersedesEmployeeBankDetailsId &&
                    previous.Revision == row.Revision - 1 && previous.EffectiveFrom < row.EffectiveFrom &&
                    (previous.EffectiveTo == null || previous.EffectiveTo <= row.EffectiveFrom))), ct);
        }
        if (!Enum.IsDefined(section)) throw EmployeeOperation.Invalid("invalid_employee_change_section");
        return Task.FromResult(false);
    }

    public Task<EmployeeChange?> LastChangeAsync(Guid employeeId, EmployeeChangeSection section, CancellationToken ct) =>
        db.Set<EmployeeChange>().AsNoTracking().Where(x => x.EmployeeId == employeeId && x.Section == section)
            .OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);

    public Task<EmployeeChange?> FindChangeAsync(Guid employeeId, EmployeeChangeSection section, Guid changeId, CancellationToken ct) =>
        db.Set<EmployeeChange>().AsNoTracking().SingleOrDefaultAsync(x => x.EmployeeId == employeeId &&
            x.Section == section && x.Id == changeId, ct);

    public async Task<PagedResult<EmployeeChangeSummary>> HistoryAsync(Guid employeeId, EmployeeChangeSection section,
        EmployeeChangePageQuery query, CancellationToken ct)
    {
        ValidatePage(query.Page, query.PageSize);
        var rows = db.Set<EmployeeChange>().AsNoTracking().Where(x => x.EmployeeId == employeeId && x.Section == section);
        var count = await rows.CountAsync(ct);
        var page = await rows.OrderByDescending(x => x.Revision)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new EmployeeChangeSummary(x.Id, x.EmployeeId, x.Section, x.EffectiveFrom,
                x.ApprovalRequestId, x.RecordedByUserId, x.RecordedAt)).ToArrayAsync(ct);
        return new(page, count, query.Page, query.PageSize);
    }

    public Task<bool> HasPendingAsync(Guid employeeId, string requestType, CancellationToken ct) =>
        db.ApprovalRequests.AsNoTracking().AnyAsync(x => x.SubjectId == employeeId &&
            x.RequestType == requestType && x.State == ApprovalState.Pending, ct);

    public Task<ApprovalRequest?> FindRequestAsync(Guid employeeId, string requestType, Guid requestId, CancellationToken ct) =>
        db.ApprovalRequests.SingleOrDefaultAsync(x => x.SubjectId == employeeId && x.RequestType == requestType && x.Id == requestId, ct);

    public async Task<PagedResult<EmployeeChangeRequestSummary>> RequestsAsync(Guid? employeeId, string requestType,
        EmployeeChangeQuery query, CancellationToken ct)
    {
        ValidatePage(query.Page, query.PageSize);
        var rows = db.ApprovalRequests.AsNoTracking().Where(x => x.RequestType == requestType);
        if (employeeId.HasValue) rows = rows.Where(x => x.SubjectId == employeeId.Value);
        if (query.State.HasValue) rows = rows.Where(x => x.State == query.State.Value);
        var count = await rows.CountAsync(ct);
        var page = await rows.OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new { x.Id, x.SubjectId, x.State, x.RequesterUserId, x.RequestedAt, x.ClosedAt, x.RowVersion })
            .ToArrayAsync(ct);
        return new(page.Select(x => new EmployeeChangeRequestSummary(x.Id, x.SubjectId, x.State, x.RequesterUserId,
            x.RequestedAt, x.ClosedAt, Convert.ToBase64String(x.RowVersion))).ToArray(), count, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyList<ApprovalDecision>> DecisionsAsync(Guid requestId, CancellationToken ct) =>
        await db.ApprovalDecisions.AsNoTracking().Where(x => x.ApprovalRequestId == requestId)
            .OrderBy(x => x.DecidedAt).ThenBy(x => x.Id).ToArrayAsync(ct);

    public void ExpectVersion(ApprovalRequest request, byte[] version)
    {
        if (!request.RowVersion.SequenceEqual(version)) throw EmployeeOperation.Conflict("employee_change_request_version_conflict");
        db.Entry(request).Property(x => x.RowVersion).OriginalValue = version.ToArray();
    }

    public void AddAddress(EmployeeAddress address) => db.EmployeeAddresses.Add(address);
    public void AddEmergency(EmployeeEmergencyContact contact) => db.EmployeeEmergencyContacts.Add(contact);
    public void AddAssignment(EmployeeAssignment assignment) => db.EmployeeAssignments.Add(assignment);
    public void AddBank(EmployeeBankDetails bank) => db.EmployeeBankDetails.Add(bank);
    public void AddMedical(EmployeeMedicalRecord medical) => db.EmployeeMedicalRecords.Add(medical);
    public void AddChange(EmployeeChange change) => db.Set<EmployeeChange>().Add(change);
    public void AddRequest(ApprovalRequest request) => db.ApprovalRequests.Add(request);
    public void AddDecision(ApprovalDecision decision) => db.ApprovalDecisions.Add(decision);

    private static void ValidatePage(int page, int pageSize)
    {
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 100)
            throw EmployeeOperation.Invalid("invalid_employee_change_pagination");
    }
}
