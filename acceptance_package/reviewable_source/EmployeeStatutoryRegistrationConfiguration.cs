using Cosmix.Domain.HR.StatutoryContributions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeStatutoryRegistrationConfiguration : IEntityTypeConfiguration<EmployeeStatutoryRegistration>
{
    public void Configure(EntityTypeBuilder<EmployeeStatutoryRegistration> builder)
    {
        builder.ToTable("EmployeeStatutoryRegistrations", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EmployeeId).IsRequired();
        builder.Property(x => x.Scheme).IsRequired();
        builder.Property(x => x.RegistrationNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RegisteredAt).IsRequired();
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.EmployeeId, x.Scheme }).IsUnique();
    }
}
