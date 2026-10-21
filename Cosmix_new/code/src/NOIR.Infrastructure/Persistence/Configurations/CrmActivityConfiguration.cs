namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for CrmActivity entity.
/// </summary>
public class CrmActivityConfiguration : IEntityTypeConfiguration<CrmActivity>
{
    public void Configure(EntityTypeBuilder<CrmActivity> builder)
    {
        builder.ToTable("CrmActivities");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        // Type
        builder.Property(e => e.Type)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Subject
        builder.Property(e => e.Subject)
            .HasMaxLength(300)
            .IsRequired();

        // Description
        builder.Property(e => e.Description)
            .HasMaxLength(4000);

        // Composite index for timeline queries
        builder.HasIndex(e => new { e.ContactId, e.LeadId, e.TenantId })
            .HasDatabaseName("IX_CrmActivities_ContactId_LeadId_TenantId");

        // Contact FK (SetNull)
        builder.HasOne(e => e.Contact)
            .WithMany()
            .HasForeignKey(e => e.ContactId)
            .OnDelete(DeleteBehavior.SetNull);

        // Lead FK (SetNull)
        builder.HasOne(e => e.Lead)
            .WithMany()
            .HasForeignKey(e => e.LeadId)
            .OnDelete(DeleteBehavior.SetNull);

        // PerformedBy FK (Employee, Restrict)
        builder.HasOne(e => e.PerformedBy)
            .WithMany()
            .HasForeignKey(e => e.PerformedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Tenant
        builder.Property(e => e.TenantId)
            .HasMaxLength(DatabaseConstants.TenantIdMaxLength);
        builder.HasIndex(e => e.TenantId);

        // Audit fields
        builder.Property(e => e.CreatedBy).HasMaxLength(DatabaseConstants.UserIdMaxLength);
        builder.Property(e => e.ModifiedBy).HasMaxLength(DatabaseConstants.UserIdMaxLength);
        builder.Property(e => e.DeletedBy).HasMaxLength(DatabaseConstants.UserIdMaxLength);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        // Soft delete query filter
        builder.HasQueryFilter("SoftDelete", e => !e.IsDeleted);
    }
}
