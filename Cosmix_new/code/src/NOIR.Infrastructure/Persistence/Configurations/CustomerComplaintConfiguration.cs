namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for CustomerComplaint entity in After-Sales support.
/// </summary>
public class CustomerComplaintConfiguration : TenantEntityConfiguration<CustomerComplaint>
{
    public override void Configure(EntityTypeBuilder<CustomerComplaint> builder)
    {
        base.Configure(builder);

        builder.ToTable("CustomerComplaints");

        builder.Property(e => e.ComplaintNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(e => new { e.ComplaintNumber, e.TenantId })
            .IsUnique()
            .HasDatabaseName("IX_CustomerComplaints_ComplaintNumber_TenantId");

        builder.Property(e => e.Subject)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(e => e.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.EquipmentDetails)
            .HasMaxLength(1000);

        builder.Property(e => e.SiteLocation)
            .HasMaxLength(500);

        builder.Property(e => e.ResolutionNotes)
            .HasMaxLength(4000);

        builder.HasIndex(e => e.CustomerId)
            .HasDatabaseName("IX_CustomerComplaints_CustomerId");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("IX_CustomerComplaints_Status");
    }
}
