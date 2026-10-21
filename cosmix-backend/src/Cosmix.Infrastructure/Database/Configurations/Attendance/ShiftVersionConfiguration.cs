using Cosmix.Domain.Attendance.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class ShiftVersionConfiguration : IEntityTypeConfiguration<ShiftVersion>
{
    public void Configure(EntityTypeBuilder<ShiftVersion> builder)
    {
        const string durationNanoseconds =
            "(DATEDIFF_BIG(nanosecond, [StartTime], [EndTime]) + CAST([EndDayOffset] AS bigint) * 86400000000000)";
        builder.ToTable("ShiftVersions", "attendance", table =>
        {
            table.HasCheckConstraint("CK_ShiftVersions_VersionNumber", "[VersionNumber] > 0");
            table.HasCheckConstraint("CK_ShiftVersions_EndDayOffset", "[EndDayOffset] IN (0, 1)");
            table.HasCheckConstraint("CK_ShiftVersions_Duration",
                $"{durationNanoseconds} > 0 AND {durationNanoseconds} <= 86400000000000");
            table.HasCheckConstraint("CK_ShiftVersions_Minutes",
                $"[ScheduledMinutes] > 0 AND [ScheduledMinutes] <= {durationNanoseconds} / 60000000000.0 " +
                $"AND [PaidBreakMinutes] >= 0 AND [UnpaidBreakMinutes] >= 0 " +
                $"AND CAST([PaidBreakMinutes] AS bigint) + [UnpaidBreakMinutes] <= {durationNanoseconds} / 60000000000.0");
            table.HasCheckConstraint("CK_ShiftVersions_ApplicableWeekdays", "[ApplicableWeekdays] BETWEEN 1 AND 127");
            table.HasCheckConstraint("CK_ShiftVersions_MissingCheckoutReminderAfterMinutes",
                "[MissingCheckoutReminderAfterMinutes] IS NULL OR [MissingCheckoutReminderAfterMinutes] > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.EffectiveFrom).HasColumnType("date");
        builder.Property(x => x.StartTime).HasColumnType("time(7)");
        builder.Property(x => x.EndTime).HasColumnType("time(7)");
        builder.Property(x => x.ApplicableWeekdays).HasConversion<int>();
        builder.HasIndex(x => new { x.ShiftId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.ShiftId, x.EffectiveFrom }).IsUnique();
        builder.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
    }
}
