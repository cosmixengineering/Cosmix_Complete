using Cosmix.Domain.HR.CompanySettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class CompanyProfileConfiguration : IEntityTypeConfiguration<CompanyProfile>
{
    public void Configure(EntityTypeBuilder<CompanyProfile> builder)
    {
        builder.ToTable("CompanyProfiles", "hr", table =>
        {
            table.HasCheckConstraint("CK_CompanyProfiles_Singleton", "[SingletonKey] = 1 AND [Id] = '0871aaf1-f14d-4dc6-a9ba-03eeb2c3047f'");
            table.HasCheckConstraint("CK_CompanyProfiles_Revision", "[Revision] > 0");
            table.HasCheckConstraint("CK_CompanyProfiles_Jurisdiction", "[JurisdictionCode] IN ('PK-PB','PK-SD','PK-KP','PK-BA','PK-ICT')");
            table.HasCheckConstraint("CK_CompanyProfiles_Coordinates", "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)");
        });
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.SingletonKey).IsUnique();
        builder.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.BusinessType).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(500).IsRequired();
        builder.Property(x => x.JurisdictionCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Latitude).HasPrecision(9, 6); builder.Property(x => x.Longitude).HasPrecision(9, 6);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.HeadOfficeBranch).WithMany().HasForeignKey(x => x.HeadOfficeBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.HeadOfficeCity).WithMany().HasForeignKey(x => x.HeadOfficeCityId).OnDelete(DeleteBehavior.Restrict);
    }
}
