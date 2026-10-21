namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for BillOfMaterial entity.
/// </summary>
public class BillOfMaterialConfiguration : TenantEntityConfiguration<BillOfMaterial>
{
    public override void Configure(EntityTypeBuilder<BillOfMaterial> builder)
    {
        base.Configure(builder);

        builder.ToTable("BillsOfMaterial");

        // Identity
        builder.Property(e => e.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.ProductId)
            .IsRequired();

        // Unique code per tenant
        builder.HasIndex(e => new { e.Code, e.TenantId })
            .IsUnique()
            .HasDatabaseName("IX_BillsOfMaterial_Code_TenantId");

        // Output
        builder.Property(e => e.OutputQuantity)
            .HasPrecision(18, 4);

        builder.Property(e => e.Unit)
            .HasMaxLength(20)
            .IsRequired();

        // State
        builder.Property(e => e.IsActive)
            .HasDefaultValue(true);

        builder.Property(e => e.Description)
            .HasMaxLength(5000);

        // Components (relationship defined here; field backing the Items collection)
        builder.HasMany(e => e.Items)
            .WithOne()
            .HasForeignKey(i => i.BillOfMaterialId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
