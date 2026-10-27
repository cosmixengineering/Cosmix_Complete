using Cosmix.Domain.HR.StatutoryContributions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeStatutoryCoverageConfiguration : IEntityTypeConfiguration<EmployeeStatutoryCoverage>
{
    public void Configure(EntityTypeBuilder<EmployeeStatutoryCoverage> builder)
    {
        builder.ToTable("EmployeeStatutoryCoverages", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EmployeeId).IsRequired();
        builder.Property(x => x.EffectiveFrom).IsRequired();
        builder.Property(x => x.EffectiveToExclusive);
        builder.Property(x => x.Schemes).HasMaxLength(50).IsRequired();
        builder.Property(x => x.EvidenceReference).HasMaxLength(300).IsRequired();
        builder.Property(x => x.EvidenceSha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PrivateProofReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ApprovedByUserId).HasMaxLength(200);
        builder.Property(x => x.ApprovedAt);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom });
    }
}
