using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("Sites", "attendance", table => table.HasCheckConstraint("CK_Sites_Dates",
            "[EndsOn] IS NULL OR ([StartsOn] IS NOT NULL AND [EndsOn] >= [StartsOn])"));
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.CityReference).WithMany().HasForeignKey(x => x.CityId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.ClientName).HasMaxLength(200);
        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
