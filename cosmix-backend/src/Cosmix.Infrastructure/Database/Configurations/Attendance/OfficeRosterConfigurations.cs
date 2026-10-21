using Cosmix.Domain.Attendance.Rosters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class OfficeRosterAssignmentConfiguration : IEntityTypeConfiguration<OfficeRosterAssignment>
{
    public void Configure(EntityTypeBuilder<OfficeRosterAssignment> b)
    {
        b.ToTable("OfficeRosterAssignments", "attendance", table => table.HasCheckConstraint("CK_OfficeRosterAssignments_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.EffectiveFrom).HasColumnType("date"); b.Property(x => x.EffectiveTo).HasColumnType("date");
        b.Property(x => x.RecordedBy).HasMaxLength(200).IsRequired();
        b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.HolidayCalendar).WithMany().HasForeignKey(x => x.HolidayCalendarId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BranchId, x.EffectiveFrom }).IsUnique(); b.HasIndex(x => x.ShiftId);
    }
}
public sealed class OfficeRosterAssignmentEndConfiguration : IEntityTypeConfiguration<OfficeRosterAssignmentEnd>
{
    public void Configure(EntityTypeBuilder<OfficeRosterAssignmentEnd> b)
    {
        b.ToTable("OfficeRosterAssignmentEnds", "attendance", table => table.HasCheckConstraint("CK_OfficeRosterAssignmentEnds_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.Property(x => x.EffectiveTo).HasColumnType("date");
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired(); b.Property(x => x.RecordedBy).HasMaxLength(200).IsRequired();
        b.HasOne(x => x.Assignment).WithOne().HasForeignKey<OfficeRosterAssignmentEnd>(x => x.OfficeRosterAssignmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.OfficeRosterAssignmentId).IsUnique();
    }
}
