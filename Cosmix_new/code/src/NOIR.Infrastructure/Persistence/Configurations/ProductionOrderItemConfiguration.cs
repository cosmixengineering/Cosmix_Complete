namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ProductionOrderItem entity.
/// </summary>
public class ProductionOrderItemConfiguration : BaseEntityConfiguration<ProductionOrderItem>
{
    public override void Configure(EntityTypeBuilder<ProductionOrderItem> builder)
    {
        base.Configure(builder);

        builder.ToTable("ProductionOrderItems");

        builder.Property(e => e.ProductionOrderId)
            .IsRequired();

        builder.Property(e => e.ProductId)
            .IsRequired();

        builder.Property(e => e.RequiredQuantity)
            .HasPrecision(18, 4);

        builder.Property(e => e.IssuedQuantity)
            .HasPrecision(18, 4);

        builder.Property(e => e.Unit)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Notes)
            .HasMaxLength(500);

        builder.HasIndex(e => e.ProductionOrderId)
            .HasDatabaseName("IX_ProductionOrderItems_ProductionOrderId");

        builder.HasIndex(e => new { e.ProductionOrderId, e.ProductId })
            .IsUnique()
            .HasDatabaseName("IX_ProductionOrderItems_ProductionOrderId_ProductId");
    }
}
