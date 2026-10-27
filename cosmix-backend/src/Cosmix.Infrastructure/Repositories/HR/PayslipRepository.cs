using Cosmix.Application.HR.Payslips;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;
public sealed class PayslipRepository(CosmixDbContext db) : IPayslipRepository
{
    public async Task<IReadOnlyList<Guid>> OwnBatchIdsAsync(Guid employeeId, int page, int pageSize, CancellationToken ct) =>
        await db.PayrollHandoffBatches.AsNoTracking().Where(x => x.CalculationVersion == "salary-v2" && x.LockedAt != null &&
            x.Lines.Any(line => line.EmployeeId == employeeId)).OrderByDescending(x => x.PeriodStart).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1).Select(x => x.Id).ToArrayAsync(ct);
    public Task<bool> ContainsEmployeeAsync(Guid batchId, Guid employeeId, CancellationToken ct) =>
        db.PayrollHandoffBatches.AsNoTracking().AnyAsync(x => x.Id == batchId && x.CalculationVersion == "salary-v2" &&
            x.LockedAt != null && x.Lines.Any(line => line.EmployeeId == employeeId), ct);
    public async Task<PayslipBatchIdentity?> BatchAsync(Guid batchId, CancellationToken ct)
    {
        var row = await db.PayrollHandoffBatches.AsNoTracking().Where(x => x.Id == batchId)
            .Select(x => new { x.Id, x.BatchReference, x.RowVersion, x.LockedAt }).SingleOrDefaultAsync(ct);
        return row is null ? null : new(row.Id, row.BatchReference, Convert.ToBase64String(row.RowVersion), row.LockedAt);
    }
    public async Task<PayslipEmployeeIdentity?> EmployeeAsync(Guid employeeId, CancellationToken ct) =>
        await db.Employees.AsNoTracking().Where(x => x.Id == employeeId)
            .Select(x => new PayslipEmployeeIdentity(x.Id, x.EmployeeNumber, x.FullLegalName ?? "")).SingleOrDefaultAsync(ct);
    public Task<decimal> ConfirmedPaymentsAsync(Guid batchId, Guid employeeId, CancellationToken ct) =>
        db.Set<SalaryPayment>().AsNoTracking().Where(x => x.Obligation.BatchId == batchId && x.EmployeeId == employeeId &&
            x.State == SalaryPaymentState.Confirmed).SumAsync(x => x.Amount, ct);
}
