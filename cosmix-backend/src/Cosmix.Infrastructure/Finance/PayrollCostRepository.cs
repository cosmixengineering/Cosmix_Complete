using Cosmix.Application.Finance.Costing;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Finance;

public sealed class PayrollCostRepository(CosmixDbContext db) : IPayrollCostRepository
{
    public async Task<IReadOnlyList<Guid>> MonthBatchIdsAsync(DateOnly start, DateOnly end, CancellationToken ct) =>
        await db.Set<PayrollObligation>().AsNoTracking().Where(x => x.State == PayrollObligationState.Finalized &&
            x.PeriodStart >= start && x.PeriodStart < end).OrderBy(x => x.BatchId).Select(x => x.BatchId).Take(501).ToArrayAsync(ct);
    public async Task<IReadOnlyList<PayrollCostPlan>> PlansAsync(Guid batchId, CancellationToken ct) =>
        await db.Set<PayrollCostPlan>().AsNoTracking().Where(x => x.BatchId == batchId).OrderBy(x => x.Revision).Take(101).ToArrayAsync(ct);
    public Task<PayrollCostPlan?> PlanAsync(Guid id, CancellationToken ct) => db.Set<PayrollCostPlan>().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<PayrollCostTargets> TargetsAsync(CancellationToken ct, bool includeInactive = false) => new(
        await db.Projects.AsNoTracking().Where(x => includeInactive || x.IsActive).OrderBy(x => x.Code).Take(5001)
            .Select(x => new PayrollCostTarget(x.Id, x.Code, x.Name, null)).ToArrayAsync(ct),
        await db.Sites.AsNoTracking().Where(x => includeInactive || x.IsActive && (x.ProjectId == null || x.Project!.IsActive)).OrderBy(x => x.Code).Take(5001)
            .Select(x => new PayrollCostTarget(x.Id, x.Code, x.Name, x.ProjectId)).ToArrayAsync(ct));
    public void Add(PayrollCostPlan plan) => db.Set<PayrollCostPlan>().Add(plan);
    public void ExpectVersion(PayrollCostPlan plan, byte[] version) => db.Entry(plan).Property(x => x.RowVersion).OriginalValue = version;
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
