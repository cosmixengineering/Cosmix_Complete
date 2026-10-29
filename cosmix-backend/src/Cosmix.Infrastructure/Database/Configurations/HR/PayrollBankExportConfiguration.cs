using Cosmix.Domain.HR.PayrollAccounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class PayrollBankFormatProfileConfiguration : IEntityTypeConfiguration<PayrollBankFormatProfile>
{
    public void Configure(EntityTypeBuilder<PayrollBankFormatProfile> builder)
    {
        builder.ToTable("PayrollBankFormatProfiles", "hr"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired(); builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.RulesJson).HasMaxLength(15000).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(200).IsRequired(); builder.Property(x => x.ApprovedByUserId).HasMaxLength(200);
        builder.Property(x => x.TemplateProofReference).HasMaxLength(2000); builder.Property(x => x.ApprovalProofReference).HasMaxLength(2000);
        builder.Property(x => x.Version).IsConcurrencyToken(); builder.HasIndex(x => new { x.Name, x.FormatVersion }).IsUnique();
    }
}
public sealed class PayrollBankExportConfiguration : IEntityTypeConfiguration<PayrollBankExport>
{
    public void Configure(EntityTypeBuilder<PayrollBankExport> builder)
    {
        builder.ToTable("PayrollBankExports", "hr"); builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceFingerprint).HasMaxLength(64).IsRequired(); builder.Property(x => x.CreatedByUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PrivateFileReference).HasMaxLength(2000).IsRequired(); builder.Property(x => x.PrivateManifestReference).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsRequired(); builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.HasOne<PayrollObligation>().WithMany().HasForeignKey(x => x.ObligationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollBankFormatProfile>().WithMany().HasForeignKey(x => x.FormatProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ObligationId);
    }
}

public sealed class PayrollBankExportRenewalConfiguration : IEntityTypeConfiguration<PayrollBankExportRenewal>
{
    public void Configure(EntityTypeBuilder<PayrollBankExportRenewal> builder)
    {
        builder.ToTable("PayrollBankExportRenewals", "hr"); builder.HasKey(x => x.Id);
        builder.Property(x => x.CreatedByUserId).HasMaxLength(200).IsRequired(); builder.Property(x => x.PrivateProofReference).HasMaxLength(2000).IsRequired();
        builder.HasOne<PayrollBankExport>().WithMany().HasForeignKey(x => x.ExportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ExportId, x.CreatedAt });
    }
}
