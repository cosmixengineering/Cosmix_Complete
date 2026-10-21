using Cosmix.Application.HR.Employees;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class EmployeeDocumentRepository(CosmixDbContext db) : IEmployeeDocumentRepository
{
    public async Task<string?> CurrentStaffCategoryCodeAsync(Guid employeeId, DateOnly asOf, CancellationToken ct)
    {
        var rows = await db.EmployeeAssignments.AsNoTracking().Where(a => a.EmployeeId == employeeId &&
                a.EffectiveFrom <= asOf && (a.EffectiveTo == null || asOf < a.EffectiveTo))
            .Join(db.StaffCategories.AsNoTracking(), a => a.StaffCategoryId, category => category.Id,
                (a, category) => new { category.Code, category.IsActive }).Take(2).ToArrayAsync(ct);
        return rows.Length == 1 && rows[0].IsActive ? rows[0].Code :
            throw EmployeeOperation.Conflict("employee_document_category_unavailable");
    }

    public void ExpectVersion(EmployeeDocument d, byte[] version)
    {
        if (!d.RowVersion.SequenceEqual(version)) throw EmployeeOperation.Conflict("employee_document_version_conflict");
        db.Entry(d).Property(x => x.RowVersion).OriginalValue = version;
    }
    public void AddHistory(EmployeeDocumentHistory history) => db.Set<EmployeeDocumentHistory>().Add(history);
    public async Task RefreshAsync(EmployeeDocument d, CancellationToken ct)
    {
        var entry = db.Entry(d);
        await entry.ReloadAsync(ct);
        if (entry.State == EntityState.Detached) throw EmployeeOperation.Missing();
    }
    public async Task<IReadOnlyList<EmployeeDocumentHistory>> HistoryAsync(Guid employeeId, Guid documentId,
        int afterRevision, int take, CancellationToken ct) => await db.Set<EmployeeDocumentHistory>().AsNoTracking()
        .Where(h => h.EmployeeId == employeeId && h.EmployeeDocumentId == documentId && h.Revision > afterRevision)
        .OrderBy(h => h.Revision).Take(take).ToArrayAsync(ct);
    public async Task<IReadOnlyList<EmployeeCertificationRequirement>> CertificationsAsync(Guid employeeId, DateOnly asOf, CancellationToken ct) =>
        await db.EmployeeCertifications.AsNoTracking().Where(c => c.EmployeeId == employeeId && c.EffectiveOn <= asOf &&
                !db.EmployeeCertifications.Any(next => next.SupersedesEmployeeCertificationId == c.Id && next.EffectiveOn <= asOf))
            .OrderBy(c => c.Id)
            .Select(c => new EmployeeCertificationRequirement("certifications." + c.Id.ToString(), c.CertificateType,
                c.IssuedOn, c.ExpiresOn, c.EmployeeDocumentId,
                c.Status == EmployeeCertificationStatus.Active && c.EmployeeDocumentId != null, c.Status)).ToArrayAsync(ct);
}
