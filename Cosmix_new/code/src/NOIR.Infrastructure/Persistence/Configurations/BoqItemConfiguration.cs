namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for BoqItem entity.
/// </summary>
public class BoqItemConfiguration : TenantEntityConfiguration<BoqItem>
{
    public override void Configure(EntityTypeBuilder<BoqItem> builder)
    {
        base.Configure(builder);

        builder.ToTable("BoqItems");

        // Parent BOQ
        builder.Property(e => e.BoqId)
            .IsRequired();

        builder.HasIndex(e => e.BoqId)
            .HasDatabaseName("IX_BoqItems_BoqId");

        // Item fields
        builder.Property(e => e.Section)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ItemCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(e => e.Unit)
            .HasMaxLength(20)
            .IsRequired();

        // Pricing
        builder.Property(e => e.Quantity)
            .HasPrecision(18, 4);

        builder.Property(e => e.UnitRate)
            .HasPrecision(18, 2);

        builder.Property(e => e.Amount)
            .HasPrecision(18, 2);

        // Ordering
        builder.Property(e => e.SortOrder)
            .HasDefaultValue(0);

        builder.HasIndex(e => new { e.BoqId, e.SortOrder })
            .HasDatabaseName("IX_BoqItems_BoqId_SortOrder");

        // Relationship with Boq (configured from the aggregate side as well)
        builder.HasOne<Boq>()
            .WithMany(b => b.Items)
            .HasForeignKey(e => e.BoqId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
