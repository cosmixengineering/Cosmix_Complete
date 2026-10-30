using System.Collections.Concurrent;
using Cosmix.Application.HR.PayrollReconciliation;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.PayrollSalary;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class PayrollReconciliationRepository(CosmixDbContext db)
    : IHistoricalPayrollRepository, IParallelPayrollRepository
{
    private static readonly ConcurrentDictionary<Guid, ParallelPayrollRunRecord> Runs = new();

    public async Task<IReadOnlyList<ErpEmployeePayrollRecord>> GetErpPayrollRecordsAsync(
        int year,
        int month,
        CancellationToken ct)
    {
        var periodStart = new DateOnly(year, month, 1);
        var periodEndExclusive = periodStart.AddMonths(1);

        var activeEmployees = await db.Employees
            .AsNoTracking()
            .Where(e => e.Status == EmployeeStatus.Active)
            .Select(e => new
            {
                e.Id,
                e.EmployeeNumber,
                e.FullLegalName
            })
            .ToListAsync(ct);

        var components = await db.PayrollSalaryComponents
            .AsNoTracking()
            .Where(c => c.EffectiveFrom < periodEndExclusive &&
                        (!c.EffectiveToExclusive.HasValue || c.EffectiveToExclusive.Value > periodStart))
            .ToListAsync(ct);

        var results = new List<ErpEmployeePayrollRecord>();

        foreach (var emp in activeEmployees)
        {
            var empComponents = components.Where(c => c.EmployeeId == emp.Id).ToList();

            var basic = empComponents
                .Where(c => c.Kind == PayrollSalaryComponentKind.FixedEarning &&
                            (c.Code.Equals("BASIC", StringComparison.OrdinalIgnoreCase) ||
                             c.Code.Equals("BaseCompensation", StringComparison.OrdinalIgnoreCase)))
                .Sum(c => c.Amount);

            if (basic == 0m && empComponents.Any(c => c.Kind == PayrollSalaryComponentKind.FixedEarning))
            {
                basic = empComponents.First(c => c.Kind == PayrollSalaryComponentKind.FixedEarning).Amount;
            }

            var allowances = empComponents
                .Where(c => c.Kind == PayrollSalaryComponentKind.FixedEarning &&
                            !c.Code.Equals("BASIC", StringComparison.OrdinalIgnoreCase) &&
                            !c.Code.Equals("BaseCompensation", StringComparison.OrdinalIgnoreCase))
                .Sum(c => c.Amount);

            var gross = basic + allowances;
            var eobi = gross > 0 ? 300m : 0m;
            var sessi = 0m;
            var incomeTax = gross > 50000 ? (gross - 50000) * 0.05m : 0m;
            var deductions = eobi + sessi + incomeTax;
            var net = gross - deductions;

            results.Add(new ErpEmployeePayrollRecord(
                EmployeeId: emp.Id,
                EmployeeNumber: emp.EmployeeNumber,
                FullLegalName: emp.FullLegalName ?? $"EMP-{emp.EmployeeNumber}",
                BasicSalary: basic,
                Allowances: allowances,
                GrossSalary: gross,
                Overtime: 0m,
                Deductions: deductions,
                Eobi: eobi,
                Sessi: sessi,
                IncomeTax: incomeTax,
                NetSalary: net
            ));
        }

        return results;
    }

    public Task<ParallelPayrollRunRecord?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        Runs.TryGetValue(id, out var record);
        return Task.FromResult(record);
    }

    public Task<ParallelPayrollRunRecord?> GetActiveByPeriodAsync(int year, int month, CancellationToken ct)
    {
        var run = Runs.Values
            .Where(r => r.Year == year && r.Month == month && r.State != ParallelPayrollRunState.Superseded)
            .OrderByDescending(r => r.PreparedAt)
            .FirstOrDefault();
        return Task.FromResult(run);
    }

    public Task SaveRunAsync(ParallelPayrollRunRecord record, CancellationToken ct)
    {
        Runs[record.Id] = record;
        return Task.CompletedTask;
    }
}
