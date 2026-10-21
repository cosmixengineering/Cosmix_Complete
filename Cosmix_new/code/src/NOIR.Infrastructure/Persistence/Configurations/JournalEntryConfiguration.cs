namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for JournalEntry entity.
/// </summary>
public class JournalEntryConfiguration : TenantEntityConfiguration<JournalEntry>
{
    public override void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        base.Configure(builder);

        builder.ToTable("JournalEntries");

        // Identity
        builder.Property(e => e.EntryNumber)
            .HasMaxLength(30)
            .IsRequired();

        // Multi-tenant unique constraint (CLAUDE.md Rule 18)
        builder.HasIndex(e => new { e.TenantId, e.EntryNumber })
            .IsUnique()
            .HasDatabaseName("IX_JournalEntries_TenantId_EntryNumber");

        // Date-only column for accounting periods
        builder.Property(e => e.EntryDate)
            .HasColumnType("date");

        // Reference data
        builder.Property(e => e.Reference)
            .HasMaxLength(100);

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        // Lifecycle
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Source tracking
        builder.Property(e => e.SourceType)
            .HasMaxLength(50);

        // Posting audit
        builder.Property(e => e.PostedByUserId)
            .HasMaxLength(DatabaseConstants.UserIdMaxLength);

        // Void audit
        builder.Property(e => e.VoidedByUserId)
            .HasMaxLength(DatabaseConstants.UserIdMaxLength);

        builder.Property(e => e.VoidReason)
            .HasMaxLength(500);

        // Lines cascade with the entry
        builder.HasMany(e => e.Lines)
            .WithOne(l => l.JournalEntry)
            .HasForeignKey(l => l.JournalEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Filter indexes for common list queries
        builder.HasIndex(e => new { e.TenantId, e.Status })
            .HasDatabaseName("IX_JournalEntries_TenantId_Status");

        builder.HasIndex(e => new { e.TenantId, e.EntryDate })
            .HasDatabaseName("IX_JournalEntries_TenantId_EntryDate");
    }
}
