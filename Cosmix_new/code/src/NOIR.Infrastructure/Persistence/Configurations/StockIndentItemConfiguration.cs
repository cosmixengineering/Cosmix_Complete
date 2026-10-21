using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NOIR.Domain.Entities.Stock;

namespace NOIR.Infrastructure.Persistence.Configurations;

public class StockIndentItemConfiguration : TenantEntityConfiguration<StockIndentItem>
{
    public override void Configure(EntityTypeBuilder<StockIndentItem> builder)
    {
        base.Configure(builder);

        builder.ToTable("StockIndentItems");

        builder.Property(e => e.IndentId)
            .IsRequired();

        builder.Property(e => e.ProductId)
            .IsRequired();

        builder.Property(e => e.Quantity)
            .IsRequired()
            .HasPrecision(18, 4);

        builder.Property(e => e.Unit)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Remarks)
            .HasMaxLength(1000);

        builder.HasIndex(e => e.IndentId)
            .HasDatabaseName("IX_StockIndentItems_IndentId");
    }
}
