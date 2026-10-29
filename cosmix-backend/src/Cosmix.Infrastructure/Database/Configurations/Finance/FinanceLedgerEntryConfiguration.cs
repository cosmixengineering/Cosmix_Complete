using Cosmix.Application.Finance.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Finance;

public sealed class FinanceLedgerEntryConfiguration : IEntityTypeConfiguration<FinanceLedgerEntry>
{
    public void Configure(EntityTypeBuilder<FinanceLedgerEntry> builder)
    {
        builder.HasNoKey();
        builder.ToView("GeneralLedgerEntries", "finance");
        builder.Property(x => x.Debit).HasPrecision(18, 2);
        builder.Property(x => x.Credit).HasPrecision(18, 2);
    }
}
