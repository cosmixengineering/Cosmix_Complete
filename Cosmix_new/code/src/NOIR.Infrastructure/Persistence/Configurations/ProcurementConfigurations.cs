namespace NOIR.Infrastructure.Persistence.Configurations;

public class SupplierConfiguration : TenantEntityConfiguration<Supplier>
{
    public override void Configure(EntityTypeBuilder<Supplier> builder)
    {
        base.Configure(builder);
        builder.ToTable("Suppliers");
        builder.Property(e => e.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => new { e.Code, e.TenantId }).IsUnique();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(150);
        builder.Property(e => e.Phone).HasMaxLength(50);
        builder.Property(e => e.TaxId).HasMaxLength(50);
        builder.Property(e => e.Address).HasMaxLength(500);
        builder.Property(e => e.PaymentTerms).HasMaxLength(200);
        builder.Property(e => e.Notes).HasMaxLength(2000);
    }
}

public class PurchaseOrderConfiguration : TenantEntityConfiguration<PurchaseOrder>
{
    public override void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        base.Configure(builder);
        builder.ToTable("PurchaseOrders");
        builder.Property(e => e.PoNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => new { e.PoNumber, e.TenantId }).IsUnique();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Subtotal).HasPrecision(18, 4);
        builder.Property(e => e.TaxAmount).HasPrecision(18, 4);
        builder.Property(e => e.TotalAmount).HasPrecision(18, 4);
        builder.Property(e => e.PaymentTerms).HasMaxLength(200);
        builder.Property(e => e.ShippingTerms).HasMaxLength(200);
        builder.Property(e => e.Notes).HasMaxLength(2000);

        builder.HasMany(e => e.Items)
            .WithOne()
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PurchaseOrderItemConfiguration : TenantEntityConfiguration<PurchaseOrderItem>
{
    public override void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        base.Configure(builder);
        builder.ToTable("PurchaseOrderItems");
        builder.Property(e => e.ItemDescription).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Quantity).HasPrecision(18, 4);
        builder.Property(e => e.Unit).HasMaxLength(20).IsRequired();
        builder.Property(e => e.UnitPrice).HasPrecision(18, 4);
        builder.Property(e => e.TotalPrice).HasPrecision(18, 4);
        builder.Property(e => e.ReceivedQuantity).HasPrecision(18, 4);
    }
}
