using Cosmix.Domain.HR.PayrollAccounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class PayrollObligationConfiguration : IEntityTypeConfiguration<PayrollObligation>
{
    public void Configure(EntityTypeBuilder<PayrollObligation> builder)
    {
        builder.ToTable("PayrollObligations", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BatchId).IsRequired();
        builder.Property(x => x.TotalGrossAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.TotalNetAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.FinalizedByUserId).HasMaxLength(200);
        builder.Property(x => x.SourceFingerprint).HasMaxLength(64);
        builder.Property(x => x.PeriodStart);
        builder.Property(x => x.PeriodEndExclusive);
        builder.Property(x => x.EmployeeEntitlementsJson);

        builder.HasOne(x => x.Batch)
            .WithMany()
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.BatchId).IsUnique();
        builder.HasIndex(x => x.State);
        builder.HasIndex(x => x.SourceFingerprint);
    }
}
