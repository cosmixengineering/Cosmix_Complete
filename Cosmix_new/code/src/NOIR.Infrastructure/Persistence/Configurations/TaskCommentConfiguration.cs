namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for TaskComment entity.
/// </summary>
public class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        builder.ToTable("TaskComments");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        // Content
        builder.Property(e => e.Content)
            .HasMaxLength(5000)
            .IsRequired();

        // IsEdited
        builder.Property(e => e.IsEdited)
            .HasDefaultValue(false);

        // Task FK (Cascade — delete comments when task is deleted)
        builder.HasOne(e => e.Task)
            .WithMany(t => t.Comments)
            .HasForeignKey(e => e.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Author FK (Employee, Restrict)
        builder.HasOne(e => e.Author)
            .WithMany()
            .HasForeignKey(e => e.AuthorId)
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

        builder.HasQueryFilter("SoftDelete", e => !e.IsDeleted);
    }
}
