using Cosmix.Domain.HR.Leaves;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Cosmix.Infrastructure.Database.Configurations.HR;
public sealed class LeaveRequestDayConfiguration : IEntityTypeConfiguration<LeaveRequestDay>
{
    public void Configure(EntityTypeBuilder<LeaveRequestDay> b)
    {
        b.ToTable("LeaveRequestDays", "hr", t => {
            t.HasCheckConstraint("CK_LeaveRequestDays_Interval", "[EndsAtUtc] > [StartsAtUtc] AND [Minutes] > 0 AND [Minutes] <= 1440");
        });
        b.HasKey(x => x.Id);
        b.HasOne(x => x.LeaveRequest).WithMany(x => x.Days).HasForeignKey(x => x.LeaveRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Cosmix.Domain.Attendance.Shifts.ShiftVersion>().WithMany().HasForeignKey(x => x.ShiftVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.LeaveRequestId, x.Date }).IsUnique();
        b.HasIndex(x => new { x.Date, x.LeaveRequestId });
    }
}
