using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.Organization;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Infrastructure.Authentication.Access;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class EmployeeHiringRepository(CosmixDbContext db, TimeProvider clock,
    MobileAuthenticationOptions? options = null, IStaffTaxonomyResolver? taxonomy = null) : IEmployeeHiringRepository
{
    public Task<Employee?> FindAsync(Guid id, CancellationToken ct) => db.Employees.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<EmployeeHiringDraft?> FindDraftAsync(Guid id, CancellationToken ct) => db.Set<EmployeeHiringDraft>().SingleOrDefaultAsync(x => x.EmployeeId == id, ct);
    public async Task<PagedResult<EmployeeSummary>> ListAsync(EmployeeListRequest request, CancellationToken ct)
    {
        var q = db.Employees.AsNoTracking();
        if (request.ArchivedOnly) q = q.Where(x => x.IsArchived);
        else if (!request.IncludeArchived) q = q.Where(x => !x.IsArchived);
        if (request.Status.HasValue) q = q.Where(x => x.OnboardingStatus == request.Status);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            q = long.TryParse(search, out var number) ? q.Where(x => x.EmployeeNumber == number) : q.Where(x => x.FullLegalName != null && x.FullLegalName.Contains(search));
        }
        var count = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArrayAsync(ct);
        return new(rows.Select(Map).ToArray(), count, request.Page, request.PageSize);
    }
    public async Task<PagedResult<EmployeeSubmissionSummary>> SubmissionsAsync(EmployeeListRequest request, CancellationToken ct)
    {
        var q = from s in db.Set<EmployeeSubmission>().AsNoTracking()
                join e in db.Employees.AsNoTracking() on s.EmployeeId equals e.Id
                where s.Provenance == OnboardingProvenance.HrWorkflow && e.SubmittedVersion == s.Version
                    && e.OnboardingStatus == (request.Status ?? OnboardingStatus.PendingApproval)
                select new { Submission = s, Employee = e };
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            q = q.Where(x => x.Employee.FullLegalName != null && x.Employee.FullLegalName.Contains(search));
        }
        var count = await q.CountAsync(ct);
        var rows = await q.OrderBy(x => x.Submission.SubmittedAt).ThenBy(x => x.Submission.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArrayAsync(ct);
        return new(rows.Select(x => new EmployeeSubmissionSummary(x.Submission.Id, x.Employee.Id, x.Employee.EmployeeNumber,
            x.Employee.FullLegalName, x.Submission.Version, x.Submission.SubmittedByUserId, x.Submission.SubmittedAt,
            x.Employee.OnboardingStatus)).ToArray(), count, request.Page, request.PageSize);
    }
    public Task<EmployeeSubmission?> FindSubmissionAsync(Guid employeeId, Guid submissionId, CancellationToken ct) =>
        db.Set<EmployeeSubmission>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == submissionId && x.EmployeeId == employeeId && x.Provenance == OnboardingProvenance.HrWorkflow, ct);
    public async Task<IReadOnlyList<EmployeeDocument>> DocumentsAsync(Guid employeeId, CancellationToken ct) =>
        await db.EmployeeDocuments.Where(x => x.EmployeeId == employeeId).OrderBy(x => x.UploadedAt).ThenBy(x => x.Id).ToArrayAsync(ct);
    public Task<EmployeeDocument?> FindDocumentAsync(Guid employeeId, Guid documentId, CancellationToken ct) =>
        db.EmployeeDocuments.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.Id == documentId, ct);

    public async Task ValidateReferencesAsync(Guid employeeId, EmployeeProfileInput p, DateOnly today, CancellationToken ct)
    {
        // Hiring and post-hire custody share the serializable authentication transaction.
        // A new approval must not bypass operational custody by materializing a draft proposal.
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById((options ?? new()).BusinessTimeZoneId); }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        { throw new AuthenticationFailure("business_timezone_not_configured", 503); }
        foreach (var proposal in p.Assets.Where(a => !string.IsNullOrWhiteSpace(a.AssetReference) && a.IssuedOn.HasValue))
        {
            var issued = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
                proposal.IssuedOn!.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone));
            DateTimeOffset? returned = proposal.ReturnedOn.HasValue ? new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
                proposal.ReturnedOn.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone)) : null;
            if (returned.HasValue && returned <= issued) continue;
            if (await db.EmployeeAssetCustodies.AsNoTracking().AnyAsync(x => x.AssetReference == proposal.AssetReference!.Trim() &&
                (returned == null || x.IssuedAt < returned) && (x.ReturnedAt == null || x.ReturnedAt > issued), ct))
                throw new AuthenticationFailure("employee_asset_custody_conflict", 409);
        }
        if (!string.IsNullOrWhiteSpace(p.CompanyPhone))
        {
            var issuedOn = p.SimIssuedOn ?? today;
            var sims = db.EmployeeSimAssignments.AsNoTracking();
            if (await sims.AnyAsync(x => x.EmployeeId != employeeId && x.PhoneNumber == p.CompanyPhone &&
                !sims.Any(next => next.SupersedesEmployeeSimAssignmentId == x.Id) &&
                (x.Status != CompanySimStatus.Returned || x.EffectiveOn > issuedOn), ct))
                throw new AuthenticationFailure("employee_company_sim_conflict", 409);
        }
        foreach (var cityId in new[] { p.CurrentAddress?.CityId, p.PermanentAddress?.CityId }.Where(x => x.HasValue).Distinct())
            if (!await db.Cities.AnyAsync(x => x.Id == cityId && x.IsActive, ct)) throw Invalid("inactive_or_unknown_address_city");
        if ((p.DepartmentId.HasValue && !await db.Departments.AnyAsync(x => x.Id == p.DepartmentId && x.IsActive, ct)) ||
            (p.DesignationId.HasValue && !await db.Designations.AnyAsync(x => x.Id == p.DesignationId && x.IsActive, ct)) ||
            (p.EmploymentTypeId.HasValue && !await db.EmploymentTypes.AnyAsync(x => x.Id == p.EmploymentTypeId && x.IsActive, ct)) ||
            (p.BranchId.HasValue && !await db.Branches.AnyAsync(x => x.Id == p.BranchId && x.IsActive, ct)) ||
            (p.ProposedShiftId.HasValue && !await db.Shifts.AnyAsync(x => x.Id == p.ProposedShiftId && x.IsActive, ct)))
            throw Invalid("inactive_or_unknown_employee_master");
        if (p.StaffCategoryId.HasValue)
        {
            var category = await db.StaffCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == p.StaffCategoryId && x.IsActive, ct);
            if (category is null) throw Invalid("unsupported_staff_category");
            var on = p.JoinedOn ?? today;
            var resolution = taxonomy is null ? StaffTaxonomyResolver.Resolve(category.Code, on, []) :
                await taxonomy.ResolveAsync(category.Code, on, ct);
            var mapping = StaffTaxonomyResolver.RequireMapping(resolution);
            if (resolution.Baseline && mapping.WorkLocation == StaffWorkLocation.Site && p.BranchId.HasValue)
                throw Invalid("site_staff_cannot_select_office");
            StaffTaxonomyResolver.ValidateLocation(resolution, p.BranchId, null, StaffTaxonomyValidationPhase.Draft);
        }
        else if (p.BranchId.HasValue) throw Invalid("select_staff_category_before_office");
        if (p.ReportingManagerEmployeeId.HasValue)
        {
            var id = p.ReportingManagerEmployeeId.Value;
            var now = clock.GetUtcNow();
            var history = await db.Set<EmployeeStatusChange>().AsNoTracking().Where(x => x.EmployeeId == id && x.EffectiveAt <= now)
                .OrderByDescending(x => x.EffectiveAt).Take(2).ToListAsync(ct);
            if (id == employeeId || !AccessManagementRepository.HasEligibleEmploymentHistory(history, now) ||
                !await db.Employees.AnyAsync(x => x.Id == id && x.Status == EmployeeStatus.Active && x.JoinedOn <= today &&
                x.OnboardingStatus == OnboardingStatus.Approved && x.ApprovedOnboardingDecisionId != null &&
                db.Set<EmployeeOnboardingDecision>().Any(d => d.Id == x.ApprovedOnboardingDecisionId && d.EmployeeId == x.Id && d.Decision == OnboardingDecision.Approve), ct))
                throw Invalid("ineligible_reporting_manager");
        }
    }
    public async Task ValidateUniqueAsync(Employee e, CancellationToken ct)
    {
        if ((e.IdentityDocumentType.HasValue && e.NormalizedIdentityDocumentNumber is not null &&
            await db.Employees.AnyAsync(x => x.Id != e.Id && x.IdentityDocumentType == e.IdentityDocumentType && x.NormalizedIdentityDocumentNumber == e.NormalizedIdentityDocumentNumber, ct)) ||
            (e.NormalizedOfficialEmail is not null && await db.Employees.AnyAsync(x => x.Id != e.Id && x.NormalizedOfficialEmail == e.NormalizedOfficialEmail, ct)))
            throw new AuthenticationFailure("employee_identity_or_email_conflict", 409);
        if (e.ApprovedLoginPhoneNumber is { } phone &&
            (await db.Employees.AnyAsync(x => x.Id != e.Id && x.ApprovedLoginPhoneNumber == phone &&
                x.OnboardingStatus != OnboardingStatus.Rejected, ct) ||
             await db.Set<LoginPhoneBinding>().AnyAsync(x => x.PhoneNumber == phone && x.RevokedAt == null &&
                db.UserAccounts.Any(account => account.Id == x.UserAccountId && account.EmployeeId != e.Id), ct)))
            throw new AuthenticationFailure("employee_login_phone_conflict", 409);
    }
    public Task<string?> StaffCategoryCodeAsync(Guid? id, CancellationToken ct) =>
        db.StaffCategories.Where(x => x.Id == id && x.IsActive).Select(x => x.Code).SingleOrDefaultAsync(ct);
    public void AddEmployee(Employee e, EmployeeHiringDraft draft) { db.Employees.Add(e); db.Set<EmployeeHiringDraft>().Add(draft); }
    public void AddDocument(EmployeeDocument d) => db.EmployeeDocuments.Add(d);
    public void AddSubmission(EmployeeSubmission s) => db.Set<EmployeeSubmission>().Add(s);
    public void ExpectVersion(Employee e, byte[] version)
    {
        if (!e.RowVersion.SequenceEqual(version)) throw new AuthenticationFailure("employee_version_conflict", 409);
        db.Entry(e).Property(x => x.RowVersion).OriginalValue = version;
    }
    public Task<IdempotencyReceipt?> FindReceiptAsync(string actor, string operation, string key, CancellationToken ct) =>
        db.IdempotencyReceipts.SingleOrDefaultAsync(x => x.ActorUserId == actor && x.Operation == operation && x.Key == key, ct);
    public void AddReceipt(IdempotencyReceipt r) => db.IdempotencyReceipts.Add(r);
    public void AddAudit(AuditEvent a) => db.AuditEvents.Add(a);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    private static AuthenticationFailure Invalid(string code) => new(code, 400);
    private static EmployeeSummary Map(Employee e) => new(
        e.Id,
        e.EmployeeNumber,
        e.FullLegalName,
        e.Status,
        e.OnboardingStatus,
        Convert.ToBase64String(e.RowVersion),
        e.IsArchived,
        e.CompanyPhone ?? e.PersonalPhone,
        e.OfficialEmail ?? e.PersonalEmail,
        e.JoinedOn,
        e.Gender,
        e.IdentityDocumentNumber,
        e.CreatedAt,
        e.MobileAccessRequested);
}
