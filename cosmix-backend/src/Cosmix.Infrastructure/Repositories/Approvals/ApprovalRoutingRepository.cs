using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Approvals;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Approvals;

public sealed class ApprovalRoutingRepository(CosmixDbContext db) : IApprovalRoutingRepository
{
    private const int Limit = 2000;
    public async Task<IReadOnlyList<ApprovalRouteRule>> RulesAsync(string requestType, CancellationToken ct) =>
        Bounded(await db.ApprovalRouteRules.AsNoTracking().Where(x => x.RequestType == requestType)
            .OrderBy(x => x.Code).ThenBy(x => x.Version).Take(Limit + 1).ToArrayAsync(ct));
    public async Task<IReadOnlyList<ApprovalRouteRule>> VersionsAsync(string code, CancellationToken ct) =>
        Bounded(await db.ApprovalRouteRules.AsNoTracking().Where(x => x.Code == code)
            .OrderBy(x => x.Version).Take(Limit + 1).ToArrayAsync(ct));
    public async Task ValidateTargetsAsync(Guid? categoryId, Guid? departmentId, Guid? siteId, CancellationToken ct)
    {
        if (categoryId.HasValue && !await db.StaffCategories.AnyAsync(x => x.Id == categoryId && x.IsActive, ct) ||
            departmentId.HasValue && !await db.Departments.AnyAsync(x => x.Id == departmentId && x.IsActive, ct) ||
            siteId.HasValue && !await db.Sites.AnyAsync(x => x.Id == siteId && x.IsActive, ct))
            throw new AuthenticationFailure("invalid_approval_route_target", 400);
    }
    public async Task<IReadOnlyList<ApprovalDelegation>> DelegationsAsync(Guid delegateAccountId, string requestType,
        DateTimeOffset now, CancellationToken ct) => Bounded(await db.ApprovalDelegations.AsNoTracking()
        .Where(x => x.DelegateUserId == delegateAccountId.ToString() && x.RequestType == requestType &&
            x.StartsAt <= now && now < x.EndsAt && x.RevokedAt == null && x.Permission != null && x.ScopeKind != null)
        .OrderBy(x => x.Id).Take(Limit + 1).ToArrayAsync(ct));
    public Task<ApprovalDelegation?> DelegationAsync(Guid id, CancellationToken ct) =>
        db.ApprovalDelegations.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<ApprovalDelegation>> OwnDelegationsAsync(Guid accountId, CancellationToken ct) =>
        Bounded(await db.ApprovalDelegations.AsNoTracking().Where(x => x.DelegatorUserId == accountId.ToString() && x.Permission != null)
            .OrderByDescending(x => x.StartsAt).ThenBy(x => x.Id).Take(Limit + 1).ToArrayAsync(ct));
    public Task<IdempotencyReceipt?> ReceiptAsync(string actor, string operation, string key, CancellationToken ct) =>
        db.IdempotencyReceipts.SingleOrDefaultAsync(x => x.ActorUserId == actor && x.Operation == operation && x.Key == key, ct);
    public void Add(ApprovalRouteRule rule) => db.Add(rule);
    public void Add(ApprovalDelegation delegation) => db.Add(delegation);
    public void Add(IdempotencyReceipt receipt) => db.Add(receipt);
    public void Add(AuditEvent audit) => db.Add(audit);
    public void ExpectVersion(ApprovalDelegation delegation, byte[] version) =>
        db.Entry(delegation).Property(x => x.RowVersion).OriginalValue = version;
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    private static IReadOnlyList<T> Bounded<T>(T[] rows) => rows.Length > Limit
        ? throw new AuthenticationFailure("approval_history_limit_exceeded", 409) : rows;
}
