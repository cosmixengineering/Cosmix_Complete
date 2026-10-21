using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NOIR.Domain.Entities.Stock;

namespace NOIR.Infrastructure.Persistence.Configurations;

public class StockTransferConfiguration : TenantEntityConfiguration<StockTransfer>
{
    public override void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        base.Configure(builder);

        builder.ToTable("StockTransfers");

        builder.Property(e => e.TransferNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.FromLocation)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.ToLocation)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.ApprovedByUserId);

        builder.Property(e => e.RejectReason)
            .HasMaxLength(1000);

        // Unique transfer number per tenant
        builder.HasIndex(e => new { e.TransferNumber, e.TenantId })
            .IsUnique()
            .HasDatabaseName("IX_StockTransfers_TransferNumber_TenantId");

        // Common query indexes (TenantId first per CLAUDE.md rule #18)
        builder.HasIndex(e => new { e.TenantId, e.Status, e.CreatedAt })
            .HasDatabaseName("IX_StockTransfers_TenantId_Status_CreatedAt");

        builder.HasIndex(e => new { e.TenantId, e.FromLocation, e.ToLocation })
            .HasDatabaseName("IX_StockTransfers_TenantId_FromLocation_ToLocation");

        // Navigation (items are child entities without a back-reference)
        builder.HasMany(e => e.Items)
            .WithOne()
            .HasForeignKey(i => i.StockTransferId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
