namespace NOIR.Infrastructure.Persistence.Configurations;

public class DeliveryChallanConfiguration : TenantEntityConfiguration<DeliveryChallan>
{
    public override void Configure(EntityTypeBuilder<DeliveryChallan> builder)
    {
        base.Configure(builder);
        builder.ToTable("DeliveryChallans");
        builder.Property(e => e.ChallanNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => new { e.ChallanNumber, e.TenantId }).IsUnique();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.SiteAddress).HasMaxLength(500);
        builder.Property(e => e.TransporterName).HasMaxLength(150);
        builder.Property(e => e.VehicleNumber).HasMaxLength(50);
        builder.Property(e => e.DriverName).HasMaxLength(100);
        builder.Property(e => e.DriverPhone).HasMaxLength(50);
        builder.Property(e => e.CustomerOtp).HasMaxLength(10);
        builder.Property(e => e.ProofOfDeliveryUrl).HasMaxLength(1000);
        builder.Property(e => e.Notes).HasMaxLength(2000);

        builder.HasMany(e => e.Items)
            .WithOne()
            .HasForeignKey(x => x.DeliveryChallanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DeliveryChallanItemConfiguration : TenantEntityConfiguration<DeliveryChallanItem>
{
    public override void Configure(EntityTypeBuilder<DeliveryChallanItem> builder)
    {
        base.Configure(builder);
        builder.ToTable("DeliveryChallanItems");
        builder.Property(e => e.ItemDescription).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Quantity).HasPrecision(18, 4);
        builder.Property(e => e.Unit).HasMaxLength(20).IsRequired();
    }
}

public class GoodsReceiptNoteConfiguration : TenantEntityConfiguration<GoodsReceiptNote>
{
    public override void Configure(EntityTypeBuilder<GoodsReceiptNote> builder)
    {
        base.Configure(builder);
        builder.ToTable("GoodsReceiptNotes");
        builder.Property(e => e.GrnNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => new { e.GrnNumber, e.TenantId }).IsUnique();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.StoreLocation).HasMaxLength(200);
        builder.Property(e => e.VehicleNumber).HasMaxLength(50);
        builder.Property(e => e.DeliveryChallanNumber).HasMaxLength(50);
        builder.Property(e => e.InspectedBy).HasMaxLength(100);
        builder.Property(e => e.Remarks).HasMaxLength(2000);

        builder.HasMany(e => e.Items)
            .WithOne()
            .HasForeignKey(x => x.GoodsReceiptNoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class GoodsReceiptNoteItemConfiguration : TenantEntityConfiguration<GoodsReceiptNoteItem>
{
    public override void Configure(EntityTypeBuilder<GoodsReceiptNoteItem> builder)
    {
        base.Configure(builder);
        builder.ToTable("GoodsReceiptNoteItems");
        builder.Property(e => e.ItemDescription).HasMaxLength(500).IsRequired();
        builder.Property(e => e.ReceivedQuantity).HasPrecision(18, 4);
        builder.Property(e => e.AcceptedQuantity).HasPrecision(18, 4);
        builder.Property(e => e.RejectedQuantity).HasPrecision(18, 4);
        builder.Property(e => e.Unit).HasMaxLength(20).IsRequired();
        builder.Property(e => e.RejectionReason).HasMaxLength(500);
    }
}
