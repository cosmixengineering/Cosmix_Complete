using Cosmix.Application.Attendance.LegalReview;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Attendance.LegalReview;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class AttendanceLegalReviewRepository(CosmixDbContext db, IEmployeeSiteAssignmentRepository siteAssignments,
    IAttendancePayrollJurisdictionReader jurisdictions) : IAttendanceLegalReviewRepository
{
    public async Task<IReadOnlyList<AttendanceLegalReviewDossier>> HistoryAsync(string bindingSha256, CancellationToken ct) =>
        await HistoryQuery(bindingSha256).ToArrayAsync(ct);

    internal IQueryable<AttendanceLegalReviewDossier> HistoryQuery(string bindingSha256) =>
        db.Set<AttendanceLegalReviewDossier>().AsNoTracking().Where(x => x.BindingSha256 == bindingSha256)
            .OrderBy(x => x.Revision).Take(AttendanceLegalReviewDossier.MaximumRevisions + 1);
    internal IQueryable<EmployeeAssignment> EmploymentScopeQuery(Guid employeeId, DateOnly from, DateOnly? to) =>
        db.EmployeeAssignments.AsNoTracking().Where(x => x.EmployeeId == employeeId &&
            (to == null || x.EffectiveFrom < to) && (x.EffectiveTo == null || x.EffectiveTo > from))
            .OrderBy(x => x.EffectiveFrom).Take(2);

    public async Task<LegalReviewScopeEvidence?> ScopeEvidenceAsync(LegalReviewScope scope, DateOnly from, DateOnly? to,
        string jurisdictionCode, CancellationToken ct)
    {
        var categoryCode = "";
        if (scope.StaffCategoryId is { } categoryId)
        {
            categoryCode = await db.StaffCategories.AsNoTracking().Where(x => x.Id == categoryId && x.IsActive)
                .Select(x => x.Code).SingleOrDefaultAsync(ct) ?? "";
            if (categoryCode.Length == 0) return null;
        }
        string? establishmentCode = null;
        Guid? jurisdictionVersionId = null;
        if (scope.SiteId is { } siteId)
        {
            establishmentCode = await db.Sites.AsNoTracking().Where(x => x.Id == siteId && x.IsActive)
                .Select(x => x.Code).SingleOrDefaultAsync(ct);
            if (establishmentCode is null) return null;
            if (to is null || to <= from || to.Value.DayNumber - from.DayNumber > 366) return null;
            var days = Enumerable.Range(0, to.Value.DayNumber - from.DayNumber)
                .Select(offset => new AttendancePayrollJurisdictionDay(from.AddDays(offset), siteId)).ToArray();
            var locations = await jurisdictions.ReadAsync(days, ct);
            if (locations.Count != days.Length || locations.Select(x => x.BusinessDate).Distinct().Count() != days.Length ||
                locations.Any(x => x.SiteId != siteId || x.BusinessDate < from || x.BusinessDate >= to || x.State != "configured" ||
                    x.ConfigurationVersionId is null || x.JurisdictionCode != jurisdictionCode) ||
                locations.Select(x => x.ConfigurationVersionId).Distinct().Count() != 1) return null;
            jurisdictionVersionId = locations[0].ConfigurationVersionId;
        }
        if (scope.BranchId is { } branchId)
        {
            establishmentCode = await db.Branches.AsNoTracking().Where(x => x.Id == branchId && x.IsActive)
                .Select(x => x.Code).SingleOrDefaultAsync(ct);
            if (establishmentCode is null) return null;
        }
        if (scope.EmployeeId is not { } employeeId) return new(scope, categoryCode, null, null, null, establishmentCode,
            SiteJurisdictionVersionId: jurisdictionVersionId,
            JurisdictionEvidenceMode: scope.SiteId is null ? "recorded_hr_assertion" : "dated_site_configuration");
        if (scope.StaffCategoryId is null || !await db.Employees.AsNoTracking().AnyAsync(x => x.Id == employeeId, ct)) return null;
        var memberships = await EmploymentScopeQuery(employeeId, from, to).ToArrayAsync(ct);
        // Narrow specific scopes to a single retained assignment; transfers/ambiguous periods need separate dossiers.
        if (memberships.Length != 1) return null;
        var membership = memberships[0];
        if (membership.EffectiveFrom > from || membership.EffectiveTo is { } end && (to is null || end < to) ||
            membership.StaffCategoryId != scope.StaffCategoryId ||
            scope.BranchId is not null && membership.BranchId != scope.BranchId) return null;
        EmployeeAssignmentResponse? siteAssignment = null;
        if (scope.SiteId is not null)
        {
            // Use the actual Admin assignment module's dated history rather than reading its legacy link directly.
            // This checkout stores that history as EmployeeAssignment rows; the two provenance IDs can coincide.
            var history = await siteAssignments.ListAsync(employeeId, new(1, 100), from, ct);
            if (history.TotalCount > 100 || history.Items.Count > 100) return null;
            var matches = history.Items.Where(x => (to == null || x.EffectiveFrom < to) &&
                (x.EffectiveTo == null || x.EffectiveTo > from)).Take(2).ToArray();
            if (matches.Length != 1) return null;
            siteAssignment = matches[0];
            if (siteAssignment.EmployeeId != employeeId || siteAssignment.SiteId != scope.SiteId ||
                siteAssignment.StaffCategoryId != scope.StaffCategoryId || siteAssignment.EffectiveFrom > from ||
                siteAssignment.EffectiveTo is { } siteEnd && (to is null || siteEnd < to)) return null;
        }
        return new(scope, categoryCode, membership.Id, membership.EffectiveFrom, membership.EffectiveTo, establishmentCode,
            siteAssignment?.Id, siteAssignment?.EffectiveFrom, siteAssignment?.EffectiveTo, siteAssignment?.Version,
            jurisdictionVersionId, siteAssignment is null ? "not_applicable" : "employee_site_assignment_repository",
            scope.SiteId is null ? "recorded_hr_assertion" : "dated_site_configuration");
    }

    public void Add(AttendanceLegalReviewDossier row)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Stage legal-review evidence inside AttendanceOperation's transaction.");
        db.Set<AttendanceLegalReviewDossier>().Add(row);
    }
}
