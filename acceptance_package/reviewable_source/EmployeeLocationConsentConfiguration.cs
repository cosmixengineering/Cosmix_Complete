using Cosmix.Domain.Attendance.LocationConsent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class EmployeeLocationConsentConfiguration : IEntityTypeConfiguration<EmployeeLocationConsent>
{
    public void Configure(EntityTypeBuilder<EmployeeLocationConsent> builder)
    {
        builder.ToTable("EmployeeLocationConsents", "attendance");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EmployeeId).IsRequired();
        builder.Property(x => x.NoticePolicyId).IsRequired();
        builder.Property(x => x.NoticePolicyVersion).IsRequired();
        builder.Property(x => x.AcceptedAtUtc).IsRequired();
        builder.Property(x => x.DeviceMetadata).HasMaxLength(500);
        builder.Property(x => x.IpAddress).HasMaxLength(45);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.NoticePolicy)
            .WithMany()
            .HasForeignKey(x => x.NoticePolicyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.EmployeeId, x.NoticePolicyVersion }).IsUnique();
    }
}
