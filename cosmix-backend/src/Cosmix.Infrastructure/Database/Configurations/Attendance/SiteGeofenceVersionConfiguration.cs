using Cosmix.Domain.Attendance.Sites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class SiteGeofenceVersionConfiguration : IEntityTypeConfiguration<SiteGeofenceVersion>
{
    public void Configure(EntityTypeBuilder<SiteGeofenceVersion> builder)
    {
        builder.ToTable("SiteGeofenceVersions", "attendance", table =>
        {
            table.HasCheckConstraint("CK_SiteGeofenceVersions_VersionNumber", "[VersionNumber] > 0");
            table.HasCheckConstraint("CK_SiteGeofenceVersions_Coordinates",
                "[Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180");
            table.HasCheckConstraint("CK_SiteGeofenceVersions_Distances",
                "[RadiusMeters] > 0 AND [MaximumAccuracyMeters] > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasAlternateKey(x => new { x.SiteId, x.Id });
        builder.Property(x => x.EffectiveFrom).HasColumnType("date");
        builder.Property(x => x.Latitude).HasPrecision(10, 7);
        builder.Property(x => x.Longitude).HasPrecision(10, 7);
        builder.Property(x => x.RadiusMeters).HasPrecision(10, 2);
        builder.Property(x => x.MaximumAccuracyMeters).HasPrecision(10, 2);
        builder.HasIndex(x => new { x.SiteId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.SiteId, x.EffectiveFrom }).IsUnique();
        builder.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
    }
}
