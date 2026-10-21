namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for TaskLabel entity.
/// </summary>
public class TaskLabelConfiguration : IEntityTypeConfiguration<TaskLabel>
{
    public void Configure(EntityTypeBuilder<TaskLabel> builder)
    {
        builder.ToTable("TaskLabels");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        // Name
        builder.Property(e => e.Name)
            .HasMaxLength(50)
            .IsRequired();

        // Color (hex)
        builder.Property(e => e.Color)
            .HasMaxLength(7)
            .IsRequired();

        // Unique index: label name per project per tenant
        builder.HasIndex(e => new { e.ProjectId, e.Name, e.TenantId })
            .IsUnique()
            .HasDatabaseName("IX_TaskLabels_ProjectId_Name_TenantId");

        // Project FK (Cascade — delete labels when project is deleted)
        builder.HasOne(e => e.Project)
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tenant
        builder.Property(e => e.TenantId)
            .HasMaxLength(DatabaseConstants.TenantIdMaxLength);
        builder.HasIndex(e => e.TenantId);

        // Audit fields
        builder.Property(e => e.CreatedBy).HasMaxLength(DatabaseConstants.UserIdMaxLength);
        builder.Property(e => e.ModifiedBy).HasMaxLength(DatabaseConstants.UserIdMaxLength);
        builder.Property(e => e.DeletedBy).HasMaxLength(DatabaseConstants.UserIdMaxLength);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter("SoftDelete", e => !e.IsDeleted);
    }
}
