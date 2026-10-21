using Cosmix.Domain.HR.AdvancesLoans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class RepaymentLedgerEntryConfiguration : IEntityTypeConfiguration<RepaymentLedgerEntry>
{
    public void Configure(EntityTypeBuilder<RepaymentLedgerEntry> builder)
    {
        builder.ToTable("RepaymentLedgerEntries", "hr", table =>
        {
            table.HasCheckConstraint("CK_RepaymentLedgerEntries_1", "[Amount] <> 0");
            table.HasCheckConstraint("CK_RepaymentLedgerEntries_2", "[Kind] BETWEEN 1 AND 4");
            table.HasCheckConstraint("CK_RepaymentLedgerEntries_3", "[ReversesEntryId] IS NULL OR [ReversesEntryId] <> [Id]");
            table.HasCheckConstraint("CK_RepaymentLedgerEntries_ReversalSource", "[Kind] <> 3 OR [ReversesEntryId] IS NOT NULL");
            table.HasCheckConstraint("CK_RepaymentLedgerEntries_4", "([Kind] IN (1, 2) AND [Amount] > 0) OR ([Kind] = 3 AND [Amount] < 0) OR [Kind] = 4");
            table.HasCheckConstraint("CK_RepaymentLedgerEntries_5", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.SourceReference).HasMaxLength(150).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.AdvanceLoanAgreement).WithMany().HasForeignKey(x => new { x.AdvanceLoanAgreementId, x.Currency }).HasPrincipalKey(x => new { x.Id, x.Currency }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RepaymentScheduleEntry).WithMany().HasForeignKey(x => new { x.RepaymentScheduleEntryId, x.AdvanceLoanAgreementId, x.Currency }).HasPrincipalKey(x => new { x.Id, x.AdvanceLoanAgreementId, x.Currency }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReversesEntry).WithMany().HasForeignKey(x => new { x.ReversesEntryId, x.AdvanceLoanAgreementId, x.Currency }).HasPrincipalKey(x => new { x.Id, x.AdvanceLoanAgreementId, x.Currency }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.AdvanceLoanAgreementId, x.IdempotencyKey }).IsUnique();
    }
}
