namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Lead entity.
/// </summary>
public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        // Title
        builder.Property(e => e.Title)
            .HasMaxLength(300)
            .IsRequired();

        // Value
        builder.Property(e => e.Value)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m);

        // Currency
        builder.Property(e => e.Currency)
            .HasMaxLength(3)
            .HasDefaultValue("USD");

        // Status
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        // SortOrder
        builder.Property(e => e.SortOrder)
            .HasDefaultValue(0d);

        // LostReason
        builder.Property(e => e.LostReason)
            .HasMaxLength(1000);

        // Notes
        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        // Composite index for Kanban queries
        builder.HasIndex(e => new { e.PipelineId, e.StageId, e.Status, e.TenantId })
            .HasDatabaseName("IX_Leads_Pipeline_Stage_Status_TenantId");

        // Contact FK (Restrict — lead must have a contact)
        builder.HasOne(e => e.Contact)
            .WithMany(c => c.Leads)
            .HasForeignKey(e => e.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        // Company FK (SetNull)
        builder.HasOne(e => e.Company)
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.SetNull);

        // Owner FK (Employee, SetNull)
        builder.HasOne(e => e.Owner)
            .WithMany()
            .HasForeignKey(e => e.OwnerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Pipeline FK (Restrict)
        builder.HasOne(e => e.Pipeline)
            .WithMany()
            .HasForeignKey(e => e.PipelineId)
            .OnDelete(DeleteBehavior.Restrict);

        // Stage FK (Restrict)
        builder.HasOne(e => e.Stage)
            .WithMany()
            .HasForeignKey(e => e.StageId)
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
