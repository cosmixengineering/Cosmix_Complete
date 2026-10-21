using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeAddressConfiguration : IEntityTypeConfiguration<EmployeeAddress>
{
    public void Configure(EntityTypeBuilder<EmployeeAddress> builder)
    {
        builder.ToTable("EmployeeAddresses", "hr", table =>
            table.HasCheckConstraint("CK_EmployeeAddresses_Type", "[AddressType] IN (1, 2)"));
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.CityReference).WithMany().HasForeignKey(x => x.CityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.EmployeeId, x.AddressType }).IsUnique();
        builder.Property(x => x.AddressLine1).HasMaxLength(250);
        builder.Property(x => x.AddressLine2).HasMaxLength(250);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.Province).HasMaxLength(100);
        builder.Property(x => x.PostalCode).HasMaxLength(20);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

