using Cosmix.Domain.HR.PayrollImports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;
public sealed class PayrollSalaryImportConfiguration : IEntityTypeConfiguration<PayrollSalaryImport>
{
    public void Configure(EntityTypeBuilder<PayrollSalaryImport> builder)
    {
        builder.ToTable("PayrollSalaryImports", "hr", table => table.HasCheckConstraint("CK_PayrollSalaryImports_Submission",
            "([SubmittedAt] IS NULL AND [SubmittedByUserId] IS NULL AND [AppliedFingerprint] IS NULL AND [AppliedPrivateReference] IS NULL) OR ([SubmittedAt] >= [UploadedAt] AND [SubmittedByUserId] IS NOT NULL AND [AppliedFingerprint] IS NOT NULL AND [AppliedPrivateReference] IS NOT NULL)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.SourceSha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourcePrivateReference).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.UploadedByUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SubmittedByUserId).HasMaxLength(200);
        builder.Property(x => x.AppliedFingerprint).HasMaxLength(64);
        builder.Property(x => x.AppliedPrivateReference).HasMaxLength(4000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => x.SourceSha256).IsUnique();
        builder.HasIndex(x => x.UploadedAt);
    }
}
