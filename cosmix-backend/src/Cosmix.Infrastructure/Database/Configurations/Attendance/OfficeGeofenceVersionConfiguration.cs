using Cosmix.Domain.Attendance.Offices;
using Cosmix.Domain.HR.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class OfficeGeofenceVersionConfiguration : IEntityTypeConfiguration<OfficeGeofenceVersion>
{
    public void Configure(EntityTypeBuilder<OfficeGeofenceVersion> builder)
    {
        builder.ToTable("OfficeGeofenceVersions", "attendance", t =>
        {
            t.HasCheckConstraint("CK_OfficeGeofenceVersions_Geometry", "[Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180 AND [RadiusMeters] > 0 AND [MaximumAccuracyMeters] > 0");
            t.HasCheckConstraint("CK_OfficeGeofenceVersions_Version", "[VersionNumber] > 0");
            t.HasCheckConstraint("CK_OfficeGeofenceVersions_RecordedUtc", "DATEPART(TZOFFSET,[RecordedAtUtc]) = 0");
        });
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasAlternateKey(x => new { x.BranchId, x.Id });
        builder.HasIndex(x => new { x.BranchId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.EffectiveFrom }).IsUnique();
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.EffectiveFrom).HasColumnType("date");
        builder.Property(x => x.Latitude).HasPrecision(10, 7); builder.Property(x => x.Longitude).HasPrecision(10, 7);
        builder.Property(x => x.RadiusMeters).HasPrecision(10, 2); builder.Property(x => x.MaximumAccuracyMeters).HasPrecision(10, 2);
        builder.Property(x => x.JurisdictionCode).HasMaxLength(10).IsUnicode(false).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(x => x.ApprovalReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.RecordedBy).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RecordedAtUtc).HasColumnType("datetimeoffset(7)");
    }
}
