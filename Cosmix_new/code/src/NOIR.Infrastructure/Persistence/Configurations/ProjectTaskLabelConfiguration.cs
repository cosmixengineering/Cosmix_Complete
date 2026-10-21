namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ProjectTaskLabel junction entity.
/// </summary>
public class ProjectTaskLabelConfiguration : IEntityTypeConfiguration<ProjectTaskLabel>
{
    public void Configure(EntityTypeBuilder<ProjectTaskLabel> builder)
    {
        builder.ToTable("ProjectTaskLabels");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        // Unique index: one label per task per tenant (only active)
        builder.HasIndex(e => new { e.TaskId, e.LabelId, e.TenantId })
            .IsUnique()
            .HasFilter("IsDeleted = 0")
            .HasDatabaseName("IX_ProjectTaskLabels_TaskId_LabelId_TenantId");

        // Task FK (Cascade — delete associations when task is deleted)
        builder.HasOne(e => e.Task)
            .WithMany(t => t.TaskLabels)
            .HasForeignKey(e => e.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Label FK (Restrict — remove label assignments before deleting label)
        builder.HasOne(e => e.Label)
            .WithMany()
            .HasForeignKey(e => e.LabelId)
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

        // Soft delete filter
        builder.HasQueryFilter("SoftDelete", e => !e.IsDeleted);
    }
}
