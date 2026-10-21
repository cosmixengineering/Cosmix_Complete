using Cosmix.Domain.HR.EmployeeRecords;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class PreviousEmploymentConfiguration : IEntityTypeConfiguration<PreviousEmployment>
{
    public void Configure(EntityTypeBuilder<PreviousEmployment> builder)
    {
        builder.ToTable("PreviousEmployments", "hr", table =>
        {
            table.HasCheckConstraint("CK_PreviousEmployments_1", "[EndedOn] IS NULL OR [EndedOn] >= [StartedOn]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EmployerName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RoleTitle).HasMaxLength(150).IsRequired();
        builder.Property(x => x.LeavingReason).HasMaxLength(1000);
        builder.Property(x => x.LastSalaryEncryptedReference).HasMaxLength(500);
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
