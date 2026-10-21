using Cosmix.Domain.HR.Holidays;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable("Holidays", "hr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NotificationPrivateReference).HasMaxLength(500).IsRequired();
        builder.HasOne(x => x.HolidayCalendar).WithMany(x => x.Holidays).HasForeignKey(x => x.HolidayCalendarId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.HolidayCalendarId, x.HolidayDate }).IsUnique();
    }
}
