namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for BillOfMaterialItem entity.
/// </summary>
public class BillOfMaterialItemConfiguration : BaseEntityConfiguration<BillOfMaterialItem>
{
    public override void Configure(EntityTypeBuilder<BillOfMaterialItem> builder)
    {
        base.Configure(builder);

        builder.ToTable("BillOfMaterialItems");

        builder.Property(e => e.BillOfMaterialId)
            .IsRequired();

        builder.Property(e => e.ProductId)
            .IsRequired();

        builder.Property(e => e.Quantity)
            .HasPrecision(18, 4);

        builder.Property(e => e.Unit)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.WastePercent)
            .HasPrecision(5, 2);

        builder.Property(e => e.Notes)
            .HasMaxLength(500);

        builder.HasIndex(e => e.BillOfMaterialId)
            .HasDatabaseName("IX_BillOfMaterialItems_BillOfMaterialId");

        builder.HasIndex(e => new { e.BillOfMaterialId, e.ProductId })
            .IsUnique()
            .HasDatabaseName("IX_BillOfMaterialItems_BillOfMaterialId_ProductId");

        // Computed property - not mapped
        builder.Ignore(e => e.EffectiveQuantity);
    }
}
