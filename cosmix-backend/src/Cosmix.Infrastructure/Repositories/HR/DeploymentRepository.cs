using Cosmix.Application.Attendance.Common;
using Cosmix.Application.HR.Allowances;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Allowances;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class DeploymentRepository(CosmixDbContext db) : IDeploymentRepository
{
    private sealed class DeploymentJoin
    {
        public required OutstationDeployment Deployment { get; init; }
        public required ApprovalRequest Approval { get; init; }
    }
    private IQueryable<DeploymentJoin> Rows => from d in db.Set<OutstationDeployment>()
        join a in db.ApprovalRequests on d.ApprovalRequestId equals a.Id
        select new DeploymentJoin { Deployment = d, Approval = a };
    // Keep filtering/paging on a member-bound SQL projection; construct the response record last.
    private IQueryable<DeploymentRow> Project(IQueryable<DeploymentJoin> rows) =>
        rows.Select(x => new DeploymentRow(x.Deployment, x.Approval,
            db.ApprovalDecisions.Where(d => d.ApprovalRequestId == x.Approval.Id).OrderBy(d => d.DecidedAt).FirstOrDefault()));
    public Task<DeploymentRow?> FindAsync(Guid employeeId, Guid deploymentId, CancellationToken ct) =>
        Project(Rows.Where(x => x.Deployment.Id == deploymentId && x.Deployment.EmployeeId == employeeId)).SingleOrDefaultAsync(ct);
    public Task<DeploymentRow?> FindByApprovalAsync(Guid employeeId, Guid deploymentApprovalId, CancellationToken ct) =>
        Project(Rows.Where(x => x.Deployment.ApprovalRequestId == deploymentApprovalId && x.Deployment.EmployeeId == employeeId)).SingleOrDefaultAsync(ct);
    public async Task<IReadOnlyList<DeploymentRow>> PageAsync(Guid employeeId, int skip, int take, CancellationToken ct) =>
        await Project(Rows.Where(x => x.Deployment.EmployeeId == employeeId).OrderByDescending(x => x.Deployment.RecordedAt)
            .ThenBy(x => x.Deployment.Id).Skip(skip).Take(take)).ToArrayAsync(ct);
    public async Task<IReadOnlyList<DeploymentBreakRow>> BreaksAsync(Guid deploymentId, CancellationToken ct) =>
        Bounded(await (from b in db.Set<DeploymentHomeBreakRequest>()
            join a in db.ApprovalRequests on b.ApprovalRequestId equals a.Id where b.DeploymentId == deploymentId
            orderby b.DepartedAtUtc, b.Id
            select new DeploymentBreakRow(b, a, db.ApprovalDecisions.Where(x => x.ApprovalRequestId == a.Id).OrderBy(x => x.DecidedAt).FirstOrDefault()))
            .Take(513).ToArrayAsync(ct));
    public async Task<IReadOnlyList<DeploymentClaimRow>> ClaimsAsync(Guid deploymentId, CancellationToken ct)
    {
        var rows = Bounded(await (from c in db.AllowanceClaims
            join d in db.Set<OutstationDeployment>() on c.DeploymentApprovalRequestId equals d.ApprovalRequestId
            join a in db.ApprovalRequests on c.ApprovalRequestId equals a.Id
            where d.Id == deploymentId && c.Kind == AllowanceKind.OutstationDaily && c.EmployeeId == d.EmployeeId
            orderby a.RequestedAt, c.Id
            select new DeploymentClaimRow(c, a, db.ApprovalDecisions.Where(x => x.ApprovalRequestId == a.Id).OrderBy(x => x.DecidedAt).FirstOrDefault(), d.Id))
            .Take(513).ToArrayAsync(ct));
        foreach (var row in rows) await db.Entry(row.Claim).Collection(x => x.Submissions).LoadAsync(ct);
        return rows;
    }
    public Task<bool> OverlapAsync(Guid employeeId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        Rows.AnyAsync(x => x.Deployment.EmployeeId == employeeId && x.Deployment.StartsAtUtc < to && x.Deployment.EndsAtUtc > from &&
            (x.Approval.State == ApprovalState.Pending || x.Approval.State == ApprovalState.Approved), ct);
    public Task<bool> MealOverlapAsync(Guid employeeId, DateTimeOffset startsAt, DateTimeOffset endsAt, CancellationToken ct) =>
        (from c in db.AllowanceClaims join a in db.ApprovalRequests on c.ApprovalRequestId equals a.Id
            where c.EmployeeId == employeeId && c.EligibilityPeriodStart < endsAt && c.EligibilityPeriodEnd > startsAt &&
                (a.State == ApprovalState.Pending || a.State == ApprovalState.Approved) &&
                (c.Kind == AllowanceKind.OfficeMeal || c.Kind == AllowanceKind.SiteDinner || c.Kind == AllowanceKind.SiteBreakfast || c.Kind == AllowanceKind.SiteLunch)
            select c.Id).AnyAsync(ct);
    public void Add(OutstationDeployment deployment, ApprovalRequest approval) { db.Add(deployment); db.Add(approval); }
    public void Add(DeploymentHomeBreakRequest homeBreak, ApprovalRequest approval) { db.Add(homeBreak); db.Add(approval); }
    public void Add(ApprovalRequest approval, AllowanceClaimSubmission submission) { db.Add(approval); db.Add(submission); }
    public void Add(ApprovalDecision decision) => db.Add(decision);
    public void ExpectVersion(OutstationDeployment deployment, byte[] version) => db.Entry(deployment).Property(x => x.RowVersion).OriginalValue = version;
    public void ExpectVersion(ApprovalRequest approval, byte[] version) => db.Entry(approval).Property(x => x.RowVersion).OriginalValue = version;
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    private static T[] Bounded<T>(T[] rows) => rows.Length <= 512 ? rows : throw AttendanceOperation.Conflict("deployment_source_limit");
}
