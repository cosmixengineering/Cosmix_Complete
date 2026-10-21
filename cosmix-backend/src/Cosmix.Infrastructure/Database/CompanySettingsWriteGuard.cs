using Cosmix.Domain.HR.CompanySettings;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Database;

internal static class CompanySettingsWriteGuard
{
    public static void Validate(CosmixDbContext db)
    {
        foreach (var entry in db.ChangeTracker.Entries<CompanyProfile>())
        {
            if (entry.State == EntityState.Deleted || entry.Entity.Id != CompanyProfile.SingletonId || entry.Entity.SingletonKey != 1)
                throw new InvalidOperationException("The singleton company profile cannot be removed or replaced.");
            if (entry.State == EntityState.Added && entry.Entity.Revision != 1 || entry.State == EntityState.Modified &&
                entry.Entity.Revision != entry.OriginalValues.GetValue<int>(nameof(CompanyProfile.Revision)) + 1)
                throw new InvalidOperationException("Company changes require one optimistic configuration revision.");
        }
        foreach (var entry in db.ChangeTracker.Entries<CompanySiteJurisdictionVersion>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Company site jurisdiction history is immutable.");
            if (entry.State != EntityState.Added) continue;
            var profile = db.ChangeTracker.Entries<CompanyProfile>().SingleOrDefault(x => x.Entity.Id == entry.Entity.CompanyProfileId);
            if (profile is null || profile.State != EntityState.Modified || entry.Entity.ConfigurationRevision != profile.Entity.Revision)
                throw new InvalidOperationException("A site jurisdiction publication requires its tracked optimistic company revision.");
        }
    }
}
