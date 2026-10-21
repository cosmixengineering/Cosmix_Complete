using Cosmix.Domain.HR.AdvancesLoans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class AdvanceLoanAgreementConfiguration : IEntityTypeConfiguration<AdvanceLoanAgreement>
{
    public void Configure(EntityTypeBuilder<AdvanceLoanAgreement> builder)
    {
        builder.ToTable("AdvanceLoanAgreements", "hr", table =>
        {
            table.HasCheckConstraint("CK_AdvanceLoanAgreements_1", "[ApprovedPrincipal] > 0");
            table.HasCheckConstraint("CK_AdvanceLoanAgreements_2", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ApprovedPrincipal).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.ApprovedTermsPrivateReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.AdvanceLoanRequest).WithMany().HasForeignKey(x => new { x.AdvanceLoanRequestId, x.Currency }).HasPrincipalKey(x => new { x.Id, x.Currency }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.AdvanceLoanRequestId).IsUnique();
    }
}
