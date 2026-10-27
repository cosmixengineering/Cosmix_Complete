using Cosmix.Application.HR.PayrollImports;
using Cosmix.Domain.HR.PayrollImports;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;
public sealed class PayrollImportRepository(CosmixDbContext db) : IPayrollImportRepository
{
    public Task<PayrollSalaryImport?> FindAsync(Guid id, bool tracked, CancellationToken ct) =>
        (tracked ? db.Set<PayrollSalaryImport>() : db.Set<PayrollSalaryImport>().AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> SourceExistsAsync(string sha256, CancellationToken ct) => db.Set<PayrollSalaryImport>().AnyAsync(x => x.SourceSha256 == sha256, ct);
    public async Task<PayrollImportEmployee?> EmployeeAsync(long employeeNumber, CancellationToken ct)
    {
        var row = await db.Employees.AsNoTracking().Where(x => x.EmployeeNumber == employeeNumber)
            .Select(x => new { x.Id, x.EmployeeNumber, x.RowVersion }).SingleOrDefaultAsync(ct);
        return row is null ? null : new(row.Id, row.EmployeeNumber, Convert.ToBase64String(row.RowVersion));
    }
    public async Task<PayrollImportHistory> HistoryAsync(int page, int pageSize, CancellationToken ct)
    {
        var rows = await db.Set<PayrollSalaryImport>().AsNoTracking().OrderByDescending(x => x.UploadedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1).Select(x => new PayrollImportHistoryItem(x.Id, x.FileName,
                x.SourceSha256, x.SubmittedAt == null ? "Uploaded" : "Submitted", x.UploadedAt, x.SubmittedAt)).ToArrayAsync(ct);
        return new(rows.Take(pageSize).ToArray(), page, pageSize, rows.Length > pageSize);
    }
    public void Add(PayrollSalaryImport source) => db.Set<PayrollSalaryImport>().Add(source);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
