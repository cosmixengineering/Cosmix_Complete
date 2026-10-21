using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NOIR.Domain.Entities.Stock;

namespace NOIR.Infrastructure.Persistence.Configurations;

public class StockTransferItemConfiguration : TenantEntityConfiguration<StockTransferItem>
{
    public override void Configure(EntityTypeBuilder<StockTransferItem> builder)
    {
        base.Configure(builder);

        builder.ToTable("StockTransferItems");

        builder.Property(e => e.StockTransferId)
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

        builder.HasIndex(e => e.StockTransferId)
            .HasDatabaseName("IX_StockTransferItems_StockTransferId");
    }
}
