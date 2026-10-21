using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NOIR.Domain.Entities.Stock;

namespace NOIR.Infrastructure.Persistence.Configurations;

public class StockIndentConfiguration : TenantEntityConfiguration<StockIndent>
{
    public override void Configure(EntityTypeBuilder<StockIndent> builder)
    {
        base.Configure(builder);

        builder.ToTable("StockIndents");

        builder.Property(e => e.IndentNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Department)
            .HasMaxLength(200);

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        builder.Property(e => e.ProjectId)
            .HasMaxLength(100);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.ApprovedByUserId)
            .HasMaxLength(DatabaseConstants.UserIdMaxLength);

        builder.Property(e => e.RejectReason)
            .HasMaxLength(1000);

        // Unique indent number per tenant
        builder.HasIndex(e => new { e.IndentNumber, e.TenantId })
            .IsUnique()
            .HasDatabaseName("IX_StockIndents_IndentNumber_TenantId");

        // Common query indexes (TenantId first per CLAUDE.md rule #18)
        builder.HasIndex(e => new { e.TenantId, e.Status, e.CreatedAt })
            .HasDatabaseName("IX_StockIndents_TenantId_Status_CreatedAt");

        // Navigation (items are child entities without a back-reference)
        builder.HasMany(e => e.Items)
            .WithOne()
            .HasForeignKey(i => i.IndentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
