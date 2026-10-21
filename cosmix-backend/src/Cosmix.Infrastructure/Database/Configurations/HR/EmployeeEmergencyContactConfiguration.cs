using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeEmergencyContactConfiguration : IEntityTypeConfiguration<EmployeeEmergencyContact>
{
    public void Configure(EntityTypeBuilder<EmployeeEmergencyContact> builder)
    {
        builder.ToTable("EmployeeEmergencyContacts", "hr");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.EmployeeId).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Relationship).HasMaxLength(100);
        builder.Property(x => x.Phone).HasMaxLength(30);
        builder.Property(x => x.AlternatePhone).HasMaxLength(30);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

