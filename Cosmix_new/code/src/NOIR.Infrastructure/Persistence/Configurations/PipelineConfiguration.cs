namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Pipeline entity.
/// </summary>
public class PipelineConfiguration : IEntityTypeConfiguration<Pipeline>
{
    public void Configure(EntityTypeBuilder<Pipeline> builder)
    {
        builder.ToTable("Pipelines");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        // Name (unique per tenant)
        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(e => new { e.Name, e.TenantId })
            .IsUnique()
            .HasDatabaseName("IX_Pipelines_Name_TenantId");

        // IsDefault
        builder.Property(e => e.IsDefault)
            .HasDefaultValue(false);

        // Stages (cascade delete — stages belong to pipeline)
        builder.HasMany(e => e.Stages)
            .WithOne(s => s.Pipeline)
            .HasForeignKey(s => s.PipelineId)
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

        // Soft delete query filter
        builder.HasQueryFilter("SoftDelete", e => !e.IsDeleted);
    }
}
