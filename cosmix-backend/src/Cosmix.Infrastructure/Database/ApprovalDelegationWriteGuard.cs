using Cosmix.Domain.Common.Approvals;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Database;

internal static class ApprovalDelegationWriteGuard
{
    public static void Validate(CosmixDbContext db)
    {
        foreach (var entry in db.ChangeTracker.Entries<ApprovalDelegation>())
        {
            if (entry.State == EntityState.Deleted) throw new InvalidOperationException("Approval delegation history cannot be deleted.");
            if (entry.State == EntityState.Added && (entry.Entity.Permission is null || entry.Entity.ScopeKind is null ||
                entry.Entity.RevokedAt.HasValue || entry.Entity.RevocationReason is not null))
                throw new InvalidOperationException("New delegation requires explicit unrevoked scoped authority.");
            if (entry.State != EntityState.Modified) continue;
            string[] allowed = [nameof(ApprovalDelegation.RevokedAt), nameof(ApprovalDelegation.RevokedByUserId),
                nameof(ApprovalDelegation.RevocationReason), nameof(ApprovalDelegation.RowVersion)];
            if (entry.Properties.Any(p => p.IsModified && !allowed.Contains(p.Metadata.Name)) ||
                entry.Property(x => x.RevokedAt).OriginalValue.HasValue || !entry.Entity.RevokedAt.HasValue ||
                string.IsNullOrWhiteSpace(entry.Entity.RevokedByUserId) || string.IsNullOrWhiteSpace(entry.Entity.RevocationReason))
                throw new InvalidOperationException("Only one reasoned delegation revocation is permitted; grant evidence is immutable.");
        }
    }
}
