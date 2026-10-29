using Cosmix.Application.Attendance.Common;

namespace Cosmix.Application.Finance.Costing;

public sealed record PayrollCostBasis(Guid EmployeeId, decimal Amount, string Currency)
{
    public string? EmployeeName { get; init; }
}
public sealed record PayrollCostShare(Guid EmployeeId, Guid? ProjectId, Guid? SiteId, int BasisPoints);
public sealed record PayrollAllocatedCost(Guid EmployeeId, Guid? ProjectId, Guid? SiteId, int BasisPoints, decimal Amount, string Currency);

public static class PayrollCostAllocation
{
    public static IReadOnlyList<PayrollAllocatedCost> Allocate(IReadOnlyList<PayrollCostBasis> basis, IReadOnlyList<PayrollCostShare> shares)
    {
        if (basis.Count is < 1 or > 10000 || shares.Count is < 1 or > 20000 ||
            basis.Any(x => x.EmployeeId == Guid.Empty || x.Amount < 0 || decimal.Round(x.Amount, 2) != x.Amount) ||
            basis.Select(x => x.EmployeeId).Distinct().Count() != basis.Count ||
            shares.Any(x => x.EmployeeId == Guid.Empty || x.ProjectId == Guid.Empty || x.SiteId == Guid.Empty || x.BasisPoints is < 1 or > 10000) ||
            shares.Select(x => (x.EmployeeId, x.ProjectId, x.SiteId)).Distinct().Count() != shares.Count ||
            !basis.Select(x => x.EmployeeId).ToHashSet().SetEquals(shares.Select(x => x.EmployeeId)))
            throw AttendanceOperation.Invalid("payroll_cost_allocation_invalid");
        var result = new List<PayrollAllocatedCost>();
        var groups = shares.GroupBy(x => x.EmployeeId).ToDictionary(x => x.Key,
            x => x.OrderBy(row => row.ProjectId).ThenBy(row => row.SiteId).ToArray());
        foreach (var employee in basis.OrderBy(x => x.EmployeeId))
        {
            var group = groups[employee.EmployeeId];
            if (group.Sum(x => x.BasisPoints) != 10000) throw AttendanceOperation.Invalid("payroll_cost_allocation_incomplete");
            var cents = employee.Amount * 100;
            var raw = group.Select(x => cents * x.BasisPoints / 10000m).ToArray();
            var rounded = raw.Select(decimal.Floor).ToArray();
            var remainder = decimal.ToInt32(cents - rounded.Sum());
            foreach (var index in Enumerable.Range(0, group.Length).OrderByDescending(i => raw[i] - rounded[i]).ThenBy(i => i).Take(remainder))
                rounded[index]++;
            for (var i = 0; i < group.Length; i++)
                result.Add(new(employee.EmployeeId, group[i].ProjectId, group[i].SiteId, group[i].BasisPoints, rounded[i] / 100, employee.Currency));
        }
        return result;
    }
}
