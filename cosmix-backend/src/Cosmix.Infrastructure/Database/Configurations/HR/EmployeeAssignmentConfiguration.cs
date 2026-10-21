using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.HR.Branches;
using Cosmix.Domain.HR.Departments;
using Cosmix.Domain.HR.Designations;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.EmploymentTypes;
using Cosmix.Domain.HR.StaffCategories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeAssignmentConfiguration : IEntityTypeConfiguration<EmployeeAssignment>
{
    public void Configure(EntityTypeBuilder<EmployeeAssignment> builder)
    {
        builder.ToTable("EmployeeAssignments", "hr", table =>
        {
            table.HasCheckConstraint("CK_EmployeeAssignments_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
            table.HasCheckConstraint("CK_EmployeeAssignments_Manager",
                "[ReportingManagerId] IS NULL OR [ReportingManagerId] <> [EmployeeId]");
        });
        builder.HasKey(x => x.Id);
        // Punch evidence uses this key to ensure the assignment belongs to the same employee.
        builder.HasAlternateKey(x => new { x.EmployeeId, x.Id });
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReportingManager).WithMany().HasForeignKey(x => x.ReportingManagerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Designation).WithMany().HasForeignKey(x => x.DesignationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.StaffCategory).WithMany().HasForeignKey(x => x.StaffCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.EmploymentType).WithMany().HasForeignKey(x => x.EmploymentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom }).IsUnique();
        builder.HasIndex(x => new { x.SiteId, x.EffectiveFrom, x.EffectiveTo });
        builder.HasIndex(x => new { x.DepartmentId, x.EffectiveFrom, x.EffectiveTo });
        builder.HasIndex(x => new { x.ReportingManagerId, x.EffectiveFrom, x.EffectiveTo });
    }
}
