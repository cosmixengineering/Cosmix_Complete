using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Departments;
using Cosmix.Domain.HR.Designations;
using Cosmix.Domain.HR.Branches;
using Cosmix.Domain.HR.StaffCategories;
using Cosmix.Domain.HR.EmploymentTypes;
using Cosmix.Domain.Attendance.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeHiringDraftConfiguration : IEntityTypeConfiguration<EmployeeHiringDraft>
{
    public void Configure(EntityTypeBuilder<EmployeeHiringDraft> b)
    {
        b.ToTable("EmployeeHiringDrafts", "hr", t => t.HasCheckConstraint("CK_EmployeeHiringDrafts_Manager", "[ReportingManagerEmployeeId] IS NULL OR [ReportingManagerEmployeeId] <> [EmployeeId]"));
        b.HasKey(x => x.EmployeeId);
        b.HasOne<Employee>().WithOne().HasForeignKey<EmployeeHiringDraft>(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Designation>().WithMany().HasForeignKey(x => x.DesignationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StaffCategory>().WithMany().HasForeignKey(x => x.StaffCategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EmploymentType>().WithMany().HasForeignKey(x => x.EmploymentTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ReportingManagerEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Shift>().WithMany().HasForeignKey(x => x.ProposedShiftId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.ProfileReference).HasMaxLength(500);
        b.Property(x => x.FinancialReference).HasMaxLength(500);
        b.Property(x => x.MedicalReference).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
