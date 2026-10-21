using Cosmix.Domain.HR.EmployeeRecords;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeBankDetailsConfiguration : IEntityTypeConfiguration<EmployeeBankDetails>
{
    public void Configure(EntityTypeBuilder<EmployeeBankDetails> builder)
    {
        builder.ToTable("EmployeeBankDetails", "hr", table =>
        {
            table.HasCheckConstraint("CK_EmployeeBankDetails_1", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
            table.HasCheckConstraint("CK_EmployeeBankDetails_2", "([IsVerified] = 0 AND [VerifiedByUserId] IS NULL AND [VerifiedAt] IS NULL) OR ([IsVerified] = 1 AND [VerifiedByUserId] IS NOT NULL AND [VerifiedAt] IS NOT NULL)");
            table.HasCheckConstraint("CK_EmployeeBankDetails_3", "[PayloadSchemaVersion] > 0");
            table.HasCheckConstraint("CK_EmployeeBankDetails_Revision", "[Revision] > 0");
            table.HasCheckConstraint("CK_EmployeeBankDetails_History", "([Revision] = 1 AND [SupersedesEmployeeBankDetailsId] IS NULL) OR ([Revision] > 1 AND [SupersedesEmployeeBankDetailsId] IS NOT NULL)");
            table.HasCheckConstraint("CK_EmployeeBankDetails_Predecessor", "[SupersedesEmployeeBankDetailsId] IS NULL OR [SupersedesEmployeeBankDetailsId] <> [Id]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BankNameEncryptedReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.AccountTitleEncryptedReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.AccountNumberEncryptedReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.IbanEncryptedReference).HasMaxLength(500);
        builder.Property(x => x.EncryptionKeyReference).HasMaxLength(200).IsRequired();
        builder.Property(x => x.MaskedAccountIdentifier).HasMaxLength(40).IsRequired();
        builder.Property(x => x.VerifiedByUserId).HasMaxLength(200);
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SupersedesEmployeeBankDetails).WithMany()
            .HasForeignKey(x => new { x.SupersedesEmployeeBankDetailsId, x.EmployeeId })
            .HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SupersedesEmployeeBankDetailsId).IsUnique()
            .HasFilter("[SupersedesEmployeeBankDetailsId] IS NOT NULL");
    }
}
