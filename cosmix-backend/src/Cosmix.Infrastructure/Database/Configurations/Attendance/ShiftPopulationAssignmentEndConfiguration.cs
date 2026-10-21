using Cosmix.Domain.Attendance.Rosters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class ShiftPopulationAssignmentEndConfiguration : IEntityTypeConfiguration<ShiftPopulationAssignmentEnd>
{
    public void Configure(EntityTypeBuilder<ShiftPopulationAssignmentEnd> builder)
    {
        builder.ToTable("ShiftPopulationAssignmentEnds", "attendance", table =>
            table.HasCheckConstraint("CK_ShiftPopulationAssignmentEnds_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Assignment).WithOne().HasForeignKey<ShiftPopulationAssignmentEnd>(x => x.ShiftPopulationAssignmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ShiftPopulationAssignmentId).IsUnique();
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    }
}
