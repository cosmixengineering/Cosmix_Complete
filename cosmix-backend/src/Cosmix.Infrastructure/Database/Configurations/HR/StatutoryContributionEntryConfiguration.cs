using Cosmix.Domain.HR.StatutoryContributions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class StatutoryContributionEntryConfiguration : IEntityTypeConfiguration<StatutoryContributionEntry>
{
    public void Configure(EntityTypeBuilder<StatutoryContributionEntry> builder)
    {
        builder.ToTable("StatutoryContributionEntries", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RegistrationNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EmployeeContributionAmount).HasPrecision(18, 2);
        builder.Property(x => x.EmployerContributionAmount).HasPrecision(18, 2);
        builder.Property(x => x.EvidenceReference).HasMaxLength(300).IsRequired();
        builder.Property(x => x.EvidenceSha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PrivateProofReference).HasMaxLength(500).IsRequired();
        builder.HasOne(x => x.Registration).WithMany().HasForeignKey(x => x.RegistrationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ContributionId, x.EmployeeId }).IsUnique();
        builder.HasIndex(x => x.EmployeeId);
    }
}
