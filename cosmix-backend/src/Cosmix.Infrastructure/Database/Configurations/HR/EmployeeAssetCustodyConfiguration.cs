using Cosmix.Domain.HR.EmployeeRecords;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeAssetCustodyConfiguration : IEntityTypeConfiguration<EmployeeAssetCustody>
{
    public void Configure(EntityTypeBuilder<EmployeeAssetCustody> builder)
    {
        builder.ToTable("EmployeeAssetCustodies", "hr", table =>
        {
            table.HasCheckConstraint("CK_EmployeeAssetCustodies_1", "[ReturnedAt] IS NULL OR [ReturnedAt] >= [IssuedAt]");
            table.HasCheckConstraint("CK_EmployeeAssetCustodies_2", "([ReturnedAt] IS NULL AND [ReturnCondition] IS NULL AND [ReturnedToUserId] IS NULL AND [ReturnEvidencePrivateReference] IS NULL) OR ([ReturnedAt] IS NOT NULL AND [ReturnCondition] IS NOT NULL AND [ReturnedToUserId] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AssetReference).HasMaxLength(100).IsRequired();
        builder.Property(x => x.IssueCondition).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.IssueEvidencePrivateReference).HasMaxLength(500);
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.EmployeeId, x.IssuedAt });
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.ReturnCondition).HasMaxLength(1000);
        builder.Property(x => x.ReturnEvidencePrivateReference).HasMaxLength(500);
        builder.Property(x => x.ReturnedToUserId).HasMaxLength(200);
        builder.HasIndex(x => x.AssetReference).IsUnique().HasFilter("[ReturnedAt] IS NULL");
    }
}
