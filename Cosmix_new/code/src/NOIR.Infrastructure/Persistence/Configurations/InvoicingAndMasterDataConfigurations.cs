namespace NOIR.Infrastructure.Persistence.Configurations;

public class SalesInvoiceConfiguration : TenantEntityConfiguration<SalesInvoice>
{
    public override void Configure(EntityTypeBuilder<SalesInvoice> builder)
    {
        base.Configure(builder);
        builder.ToTable("SalesInvoices");
        builder.Property(e => e.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => new { e.InvoiceNumber, e.TenantId }).IsUnique();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Subtotal).HasPrecision(18, 4);
        builder.Property(e => e.TaxAmount).HasPrecision(18, 4);
        builder.Property(e => e.TotalAmount).HasPrecision(18, 4);
        builder.Property(e => e.PaidAmount).HasPrecision(18, 4);
        builder.Property(e => e.PaymentTerms).HasMaxLength(200);
        builder.Property(e => e.Notes).HasMaxLength(2000);

        builder.HasMany(e => e.Items)
            .WithOne()
            .HasForeignKey(x => x.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SalesInvoiceItemConfiguration : TenantEntityConfiguration<SalesInvoiceItem>
{
    public override void Configure(EntityTypeBuilder<SalesInvoiceItem> builder)
    {
        base.Configure(builder);
        builder.ToTable("SalesInvoiceItems");
        builder.Property(e => e.Description).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Quantity).HasPrecision(18, 4);
        builder.Property(e => e.Unit).HasMaxLength(20).IsRequired();
        builder.Property(e => e.UnitPrice).HasPrecision(18, 4);
        builder.Property(e => e.Subtotal).HasPrecision(18, 4);
        builder.Property(e => e.TaxRate).HasPrecision(18, 4);
        builder.Property(e => e.TaxAmount).HasPrecision(18, 4);
        builder.Property(e => e.TotalAmount).HasPrecision(18, 4);
    }
}

public class PaymentRecordConfiguration : TenantEntityConfiguration<PaymentRecord>
{
    public override void Configure(EntityTypeBuilder<PaymentRecord> builder)
    {
        base.Configure(builder);
        builder.ToTable("PaymentRecords");
        builder.Property(e => e.PaymentNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => new { e.PaymentNumber, e.TenantId }).IsUnique();
        builder.Property(e => e.Amount).HasPrecision(18, 4);
        builder.Property(e => e.Method).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.TransactionReference).HasMaxLength(100);
        builder.Property(e => e.Notes).HasMaxLength(2000);
    }
}

public class UnitConversionConfiguration : TenantEntityConfiguration<UnitConversion>
{
    public override void Configure(EntityTypeBuilder<UnitConversion> builder)
    {
        base.Configure(builder);
        builder.ToTable("UnitConversions");
        builder.Property(e => e.FromUnit).HasMaxLength(50).IsRequired();
        builder.Property(e => e.ToUnit).HasMaxLength(50).IsRequired();
        builder.Property(e => e.ConversionFactor).HasPrecision(18, 6);
        builder.Property(e => e.Category).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(500);
    }
}
