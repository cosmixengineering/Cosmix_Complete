using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Domain.HR.PayrollSalary;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;
public sealed class PayrollSalaryRepository(CosmixDbContext db) : IPayrollSalaryRepository
{
    public Task<bool> EmployeeApprovedAsync(Guid id,CancellationToken ct) => db.Employees.AsNoTracking()
        .AnyAsync(x=>x.Id==id && x.OnboardingStatus==OnboardingStatus.Approved && x.ApprovedOnboardingDecisionId!=null,ct);
    public async Task<IReadOnlyList<PayrollSalaryComponent>> ComponentsAsync(Guid? employeeId,CancellationToken ct) =>
        await db.Set<PayrollSalaryComponent>().AsNoTracking().Include(x=>x.ApprovalRequest)
            .Where(x=>employeeId==null || x.EmployeeId==employeeId).OrderBy(x=>x.EmployeeId).ThenBy(x=>x.Code).ThenBy(x=>x.Revision).Take(65537).ToArrayAsync(ct);
    public Task<PayrollSalaryComponent?> ComponentAsync(Guid id,bool tracked,CancellationToken ct) =>
        (tracked?db.Set<PayrollSalaryComponent>():db.Set<PayrollSalaryComponent>().AsNoTracking()).Include(x=>x.ApprovalRequest).SingleOrDefaultAsync(x=>x.Id==id,ct);
    public async Task<IReadOnlyList<PayrollSalaryPolicy>> PoliciesAsync(CancellationToken ct) =>
        await db.Set<PayrollSalaryPolicy>().AsNoTracking().Include(x=>x.ApprovalRequest).OrderBy(x=>x.Revision).Take(4097).ToArrayAsync(ct);
    public Task<PayrollSalaryPolicy?> PolicyAsync(Guid id,bool tracked,CancellationToken ct) =>
        (tracked?db.Set<PayrollSalaryPolicy>():db.Set<PayrollSalaryPolicy>().AsNoTracking()).Include(x=>x.ApprovalRequest).SingleOrDefaultAsync(x=>x.Id==id,ct);
    public async Task<IReadOnlyList<ApprovalDecision>> DecisionsAsync(Guid id,CancellationToken ct) =>
        await db.ApprovalDecisions.AsNoTracking().Where(x=>x.ApprovalRequestId==id).OrderBy(x=>x.DecidedAt).ToArrayAsync(ct);
    public Task<bool> HasPreparedPeriodAsync(DateOnly from,DateOnly? until,CancellationToken ct) => db.PayrollHandoffBatches
        .Include(x => x.ApprovalRequest)
        .AnyAsync(x=>x.PeriodEnd>from && (until==null || x.PeriodStart<until) &&
            (x.ApprovalRequest == null || (x.ApprovalRequest.State != ApprovalState.Rejected && x.ApprovalRequest.State != ApprovalState.Cancelled)),ct);
    public void Add(PayrollSalaryComponent row,ApprovalRequest approval) {db.ApprovalRequests.Add(approval);db.Set<PayrollSalaryComponent>().Add(row);}
    public void Add(PayrollSalaryPolicy row,ApprovalRequest approval) {db.ApprovalRequests.Add(approval);db.Set<PayrollSalaryPolicy>().Add(row);}
    public void Add(ApprovalDecision decision)=>db.ApprovalDecisions.Add(decision);
    public void ExpectVersion(ApprovalRequest row,byte[] version)=>db.Entry(row).Property(x=>x.RowVersion).OriginalValue=version;
    public Task SaveChangesAsync(CancellationToken ct)=>db.SaveChangesAsync(ct);
}
