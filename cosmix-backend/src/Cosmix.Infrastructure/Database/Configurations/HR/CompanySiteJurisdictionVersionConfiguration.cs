using Cosmix.Domain.HR.CompanySettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class CompanySiteJurisdictionVersionConfiguration : IEntityTypeConfiguration<CompanySiteJurisdictionVersion>
{
    public void Configure(EntityTypeBuilder<CompanySiteJurisdictionVersion> builder)
    {
        builder.ToTable("CompanySiteJurisdictionVersions", "hr", table =>
        {
            table.HasCheckConstraint("CK_CompanySiteJurisdictions_Revision", "[ConfigurationRevision] > 1");
            table.HasCheckConstraint("CK_CompanySiteJurisdictions_Jurisdiction", "[JurisdictionCode] IN ('PK-PB','PK-SD','PK-KP','PK-BA','PK-ICT')");
            table.HasCheckConstraint("CK_CompanySiteJurisdictions_Supersession", "[SupersedesVersionId] IS NULL OR [SupersedesVersionId] <> [Id]");
            table.HasCheckConstraint("CK_CompanySiteJurisdictions_Date", "[EffectiveFrom] >= '19010101' AND [EffectiveFrom] < '99980101'");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.JurisdictionCode).HasMaxLength(10).IsRequired();
        builder.HasIndex(x => new { x.CompanyProfileId, x.ConfigurationRevision }).IsUnique();
        builder.HasIndex(x => new { x.SiteId, x.EffectiveFrom }).IsUnique().HasFilter("[SupersedesVersionId] IS NULL");
        builder.HasIndex(x => x.SupersedesVersionId).IsUnique().HasFilter("[SupersedesVersionId] IS NOT NULL");
        builder.HasOne(x => x.CompanyProfile).WithMany().HasForeignKey(x => x.CompanyProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SupersedesVersion).WithMany().HasForeignKey(x => x.SupersedesVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
