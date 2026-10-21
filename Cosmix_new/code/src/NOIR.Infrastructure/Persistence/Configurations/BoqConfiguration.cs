namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Boq entity.
/// </summary>
public class BoqConfiguration : TenantEntityConfiguration<Boq>
{
    public override void Configure(EntityTypeBuilder<Boq> builder)
    {
        base.Configure(builder);

        builder.ToTable("Boqs");

        // BOQ number (unique per tenant)
        builder.Property(e => e.BoqNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.BoqNumber })
            .IsUnique()
            .HasDatabaseName("IX_Boqs_TenantId_BoqNumber");

        // Header
        builder.Property(e => e.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        // Optional references (no navigation to avoid coupling)
        builder.Property(e => e.CustomerId);
        builder.HasIndex(e => new { e.TenantId, e.CustomerId })
            .HasDatabaseName("IX_Boqs_TenantId_CustomerId");

        builder.Property(e => e.ProjectId);
        builder.HasIndex(e => new { e.TenantId, e.ProjectId })
            .HasDatabaseName("IX_Boqs_TenantId_ProjectId");

        // Status (stored as string for readability)
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(e => new { e.TenantId, e.Status })
            .HasDatabaseName("IX_Boqs_TenantId_Status");

        // Financials
        builder.Property(e => e.Currency)
            .HasMaxLength(3)
            .IsRequired()
            .HasDefaultValue("USD");

        builder.Property(e => e.Subtotal)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(e => e.MarkupPercent)
            .HasPrecision(9, 4)
            .HasDefaultValue(0);

        builder.Property(e => e.MarkupAmount)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(e => e.TotalAmount)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        // Validity / notes
        builder.Property(e => e.ValidUntil);

        builder.Property(e => e.Notes)
            .HasMaxLength(1000);

        // Date index for listings
        builder.HasIndex(e => new { e.TenantId, e.CreatedAt })
            .HasDatabaseName("IX_Boqs_TenantId_CreatedAt");

        // Line items (cascade delete)
        builder.HasMany(e => e.Items)
            .WithOne()
            .HasForeignKey(e => e.BoqId)
            .OnDelete(DeleteBehavior.Cascade);

        // Revisions (cascade delete)
        builder.HasMany(e => e.Revisions)
            .WithOne()
            .HasForeignKey(e => e.BoqId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
