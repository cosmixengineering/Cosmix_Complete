using Cosmix.Domain.HR.PayrollAccounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class SalaryPaymentConfiguration : IEntityTypeConfiguration<SalaryPayment>
{
    public void Configure(EntityTypeBuilder<SalaryPayment> builder)
    {
        builder.ToTable("SalaryPayments", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ObligationId).IsRequired();
        builder.Property(x => x.AccrualJournalEntryId).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.PaymentReference).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ConfirmedByUserId).HasMaxLength(200);
        builder.Property(x => x.FailureReason).HasMaxLength(500);

        builder.HasOne(x => x.Obligation)
            .WithMany()
            .HasForeignKey(x => x.ObligationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AccrualJournalEntry)
            .WithMany()
            .HasForeignKey(x => x.AccrualJournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PaymentJournalEntry)
            .WithMany()
            .HasForeignKey(x => x.PaymentJournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ObligationId);
        builder.HasIndex(x => x.State);
    }
}
