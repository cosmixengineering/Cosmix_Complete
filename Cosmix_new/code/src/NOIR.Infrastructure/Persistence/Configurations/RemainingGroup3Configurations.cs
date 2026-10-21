namespace NOIR.Infrastructure.Persistence.Configurations;

public class IntakeMessageConfiguration : TenantEntityConfiguration<IntakeMessage>
{
    public override void Configure(EntityTypeBuilder<IntakeMessage> builder)
    {
        base.Configure(builder);
        builder.ToTable("IntakeMessages");
        builder.Property(e => e.MessageNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => new { e.MessageNumber, e.TenantId }).IsUnique();
        builder.Property(e => e.Channel).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.SenderName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.SenderEmail).HasMaxLength(150);
        builder.Property(e => e.SenderPhone).HasMaxLength(50);
        builder.Property(e => e.Subject).HasMaxLength(300).IsRequired();
        builder.Property(e => e.RawBody).HasMaxLength(4000).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
    }
}

public class SubmittalConfiguration : TenantEntityConfiguration<Submittal>
{
    public override void Configure(EntityTypeBuilder<Submittal> builder)
    {
        base.Configure(builder);
        builder.ToTable("Submittals");
        builder.Property(e => e.SubmittalNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => new { e.SubmittalNumber, e.TenantId }).IsUnique();
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.SpecificationSection).HasMaxLength(100);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.DocumentUrl).HasMaxLength(1000);
        builder.Property(e => e.ReviewedBy).HasMaxLength(100);
        builder.Property(e => e.ReviewComments).HasMaxLength(2000);
    }
}

public class CustomerPurchaseOrderConfiguration : TenantEntityConfiguration<CustomerPurchaseOrder>
{
    public override void Configure(EntityTypeBuilder<CustomerPurchaseOrder> builder)
    {
        base.Configure(builder);
        builder.ToTable("CustomerPurchaseOrders");
        builder.Property(e => e.CustomerPoNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => new { e.CustomerPoNumber, e.TenantId }).IsUnique();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.PoAmount).HasPrecision(18, 4);
        builder.Property(e => e.Currency).HasMaxLength(10).IsRequired();
        builder.Property(e => e.DocumentUrl).HasMaxLength(1000);
        builder.Property(e => e.DeliveryTerms).HasMaxLength(200);
        builder.Property(e => e.PaymentTerms).HasMaxLength(200);
        builder.Property(e => e.ScopeOfWork).HasMaxLength(4000);
        builder.Property(e => e.DiscrepancyNotes).HasMaxLength(4000);
    }
}

public class ExceptionRecordConfiguration : TenantEntityConfiguration<ExceptionRecord>
{
    public override void Configure(EntityTypeBuilder<ExceptionRecord> builder)
    {
        base.Configure(builder);
        builder.ToTable("CommercialExceptionRecords");
        builder.Property(e => e.DocumentType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(4000).IsRequired();
    }
}

public class QaInspectionConfiguration : TenantEntityConfiguration<QaInspection>
{
    public override void Configure(EntityTypeBuilder<QaInspection> builder)
    {
        base.Configure(builder);
        builder.ToTable("QaInspections");
        builder.Property(e => e.InspectionNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => new { e.InspectionNumber, e.TenantId }).IsUnique();
        builder.Property(e => e.Stage).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.ReferenceEntityType).HasMaxLength(50);
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ChecklistSummary).HasMaxLength(2000);
        builder.Property(e => e.Result).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.InspectedBy).HasMaxLength(100);
        builder.Property(e => e.DefectNotes).HasMaxLength(4000);
        builder.Property(e => e.CorrectiveAction).HasMaxLength(4000);
        builder.Property(e => e.EvidenceDocumentUrl).HasMaxLength(1000);
    }
}
