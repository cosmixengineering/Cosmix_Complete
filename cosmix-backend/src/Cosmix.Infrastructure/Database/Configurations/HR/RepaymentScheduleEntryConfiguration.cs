using Cosmix.Domain.HR.AdvancesLoans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class RepaymentScheduleEntryConfiguration : IEntityTypeConfiguration<RepaymentScheduleEntry>
{
    public void Configure(EntityTypeBuilder<RepaymentScheduleEntry> builder)
    {
        builder.ToTable("RepaymentScheduleEntries", "hr", table =>
        {
            table.HasCheckConstraint("CK_RepaymentScheduleEntries_1", "[Sequence] > 0");
            table.HasCheckConstraint("CK_RepaymentScheduleEntries_2", "[PrincipalAmount] >= 0 AND [OtherApprovedAmount] >= 0 AND ([PrincipalAmount] + [OtherApprovedAmount]) > 0");
            table.HasCheckConstraint("CK_RepaymentScheduleEntries_3", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PrincipalAmount).HasPrecision(18, 2);
        builder.Property(x => x.OtherApprovedAmount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.HasOne(x => x.AdvanceLoanAgreement).WithMany(x => x.Schedule).HasForeignKey(x => new { x.AdvanceLoanAgreementId, x.Currency }).HasPrincipalKey(x => new { x.Id, x.Currency }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.AdvanceLoanAgreementId, x.Sequence }).IsUnique();
    }
}
