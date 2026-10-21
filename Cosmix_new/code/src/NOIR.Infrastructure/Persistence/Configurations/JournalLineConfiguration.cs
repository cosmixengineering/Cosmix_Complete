namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for JournalLine entity.
/// </summary>
public class JournalLineConfiguration : BaseEntityConfiguration<JournalLine>
{
    public override void Configure(EntityTypeBuilder<JournalLine> builder)
    {
        base.Configure(builder);

        builder.ToTable("JournalLines");

        // Associations
        builder.Property(e => e.JournalEntryId)
            .IsRequired();

        builder.Property(e => e.AccountId)
            .IsRequired();

        // Amounts (matches codebase decimal precision pattern)
        builder.Property(e => e.Debit)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m);

        builder.Property(e => e.Credit)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m);

        // Memo
        builder.Property(e => e.Memo)
            .HasMaxLength(500);

        // Index for entry line lookups
        builder.HasIndex(e => e.JournalEntryId)
            .HasDatabaseName("IX_JournalLines_JournalEntryId");

        // Index for account balance queries
        builder.HasIndex(e => e.AccountId)
            .HasDatabaseName("IX_JournalLines_AccountId");
    }
}
