using Cosmix.Domain.HR.PayrollAccounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class PayrollJournalEntryConfiguration : IEntityTypeConfiguration<PayrollJournalEntry>
{
    public void Configure(EntityTypeBuilder<PayrollJournalEntry> builder)
    {
        builder.ToTable("PayrollJournalEntries", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ObligationId).IsRequired();
        builder.Property(x => x.DebitAccountCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CreditAccountCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.PostedByUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.StatutoryContributionId);
        builder.Property(x => x.Scheme).HasMaxLength(50);
        builder.Property(x => x.PostingDate);
        builder.Property(x => x.AccountingPeriodId);
        builder.Property(x => x.PostingProfileId);
        builder.Property(x => x.SourceKey).HasMaxLength(180);

        builder.HasOne(x => x.Obligation)
            .WithMany()
            .HasForeignKey(x => x.ObligationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ObligationId);
        builder.HasIndex(x => x.Kind);
        builder.HasIndex(x => x.StatutoryContributionId);
        builder.HasIndex(x => x.PostingDate);
        builder.HasIndex(x => x.AccountingPeriodId);
        builder.HasIndex(x => x.PostingProfileId);
        builder.HasIndex(x => x.SourceKey);
    }
}
