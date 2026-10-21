namespace NOIR.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Employee entity.
/// </summary>
public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        // Employee code
        builder.Property(e => e.EmployeeCode)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.EmployeeCode })
            .IsUnique()
            .HasDatabaseName("IX_Employees_TenantId_EmployeeCode");

        // Personal info
        builder.Property(e => e.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.Email })
            .IsUnique()
            .HasDatabaseName("IX_Employees_TenantId_Email");

        builder.Property(e => e.Phone)
            .HasMaxLength(20);

        builder.Property(e => e.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(e => e.Position)
            .HasMaxLength(200);

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        // User link
        builder.Property(e => e.UserId)
            .HasMaxLength(DatabaseConstants.UserIdMaxLength);

        builder.HasIndex(e => new { e.TenantId, e.UserId })
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL")
            .HasDatabaseName("IX_Employees_TenantId_UserId");

        // Status & type
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.EmploymentType)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Department FK
        builder.HasOne(e => e.Department)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Manager self-reference (Restrict — application handles cascade in DeactivateEmployeeCommandHandler)
        builder.HasOne(e => e.Manager)
            .WithMany(e => e.DirectReports)
            .HasForeignKey(e => e.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for filtering
        builder.HasIndex(e => new { e.TenantId, e.Status, e.DepartmentId })
            .HasDatabaseName("IX_Employees_TenantId_Status_Department");

        builder.HasIndex(e => new { e.TenantId, e.ManagerId })
            .HasDatabaseName("IX_Employees_TenantId_ManagerId");

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

        // Ignore computed properties
        builder.Ignore(e => e.FullName);
    }
}
