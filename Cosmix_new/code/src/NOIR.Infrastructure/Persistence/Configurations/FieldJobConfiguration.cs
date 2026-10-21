namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for FieldJob entity in Field Service Management.
/// </summary>
public class FieldJobConfiguration : TenantEntityConfiguration<FieldJob>
{
    public override void Configure(EntityTypeBuilder<FieldJob> builder)
    {
        base.Configure(builder);

        builder.ToTable("FieldJobs");

        builder.Property(e => e.JobNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(e => new { e.JobNumber, e.TenantId })
            .IsUnique()
            .HasDatabaseName("IX_FieldJobs_JobNumber_TenantId");

        builder.Property(e => e.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.SiteAddress)
            .HasMaxLength(500);

        builder.Property(e => e.CustomerOtp)
            .HasMaxLength(10);

        builder.Property(e => e.EquipmentDetails)
            .HasMaxLength(1000);

        builder.Property(e => e.MaterialUsage)
            .HasMaxLength(2000);

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        builder.HasIndex(e => e.TechnicianId)
            .HasDatabaseName("IX_FieldJobs_TechnicianId");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("IX_FieldJobs_Status");
    }
}
