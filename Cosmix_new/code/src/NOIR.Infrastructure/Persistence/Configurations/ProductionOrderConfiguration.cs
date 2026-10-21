namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ProductionOrder entity.
/// </summary>
public class ProductionOrderConfiguration : TenantEntityConfiguration<ProductionOrder>
{
    public override void Configure(EntityTypeBuilder<ProductionOrder> builder)
    {
        base.Configure(builder);

        builder.ToTable("ProductionOrders");

        // Identity
        builder.Property(e => e.OrderNumber)
            .HasMaxLength(50)
            .IsRequired();

        // Unique order number per tenant
        builder.HasIndex(e => new { e.OrderNumber, e.TenantId })
            .IsUnique()
            .HasDatabaseName("IX_ProductionOrders_OrderNumber_TenantId");

        // Source
        builder.Property(e => e.BillOfMaterialId)
            .IsRequired();

        builder.Property(e => e.ProductId)
            .IsRequired();

        // Quantities
        builder.Property(e => e.PlannedQuantity)
            .HasPrecision(18, 4);

        builder.Property(e => e.ProducedQuantity)
            .HasPrecision(18, 4);

        builder.Property(e => e.ScrapQuantity)
            .HasPrecision(18, 4);

        // Lifecycle
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("IX_ProductionOrders_Status");

        builder.Property(e => e.Notes)
            .HasMaxLength(5000);

        // People
        builder.Property(e => e.CreatedByUserId);
        builder.Property(e => e.CompletedByUserId);
        builder.Property(e => e.CompletedAt);

        // Material requirements snapshot (relationship defined here)
        builder.HasMany(e => e.Items)
            .WithOne()
            .HasForeignKey(i => i.ProductionOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
