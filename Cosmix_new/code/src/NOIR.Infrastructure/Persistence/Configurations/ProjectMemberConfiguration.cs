namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ProjectMember entity.
/// </summary>
public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("ProjectMembers");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        // Role
        builder.Property(e => e.Role)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Unique index: one member per project per tenant
        builder.HasIndex(e => new { e.ProjectId, e.EmployeeId, e.TenantId })
            .IsUnique()
            .HasDatabaseName("IX_ProjectMembers_ProjectId_EmployeeId_TenantId");

        // Project FK (Cascade — delete members when project is deleted)
        builder.HasOne(e => e.Project)
            .WithMany(p => p.Members)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Employee FK (Restrict — cannot delete employee with project memberships)
        builder.HasOne(e => e.Employee)
            .WithMany()
            .HasForeignKey(e => e.EmployeeId)
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
