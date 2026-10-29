using Cosmix.Domain.HR.PayrollAccounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class PayrollLedgerAccountConfiguration : IEntityTypeConfiguration<PayrollLedgerAccount>
{
    public void Configure(EntityTypeBuilder<PayrollLedgerAccount> builder)
    {
        builder.ToTable("PayrollLedgerAccounts", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Kind).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion().IsRequired(false);

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Kind);
        builder.HasIndex(x => x.IsActive);
    }
}
