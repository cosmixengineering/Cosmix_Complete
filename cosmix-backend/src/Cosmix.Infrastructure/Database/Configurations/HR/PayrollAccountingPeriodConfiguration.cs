using Cosmix.Domain.HR.PayrollAccounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class PayrollAccountingPeriodConfiguration : IEntityTypeConfiguration<PayrollAccountingPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollAccountingPeriod> builder)
    {
        builder.ToTable("PayrollAccountingPeriods", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PeriodStart).IsRequired();
        builder.Property(x => x.PeriodEndExclusive).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.State).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ApprovedByUserId).HasMaxLength(200);
        builder.Property(x => x.ApprovedAt);
        builder.Property(x => x.ClosedByUserId).HasMaxLength(200);
        builder.Property(x => x.ClosedAt);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => new { x.Currency, x.PeriodStart, x.PeriodEndExclusive });
        builder.HasIndex(x => x.State);
    }
}
