using Cosmix.Domain.HR.Holidays;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class HolidayCalendarConfiguration : IEntityTypeConfiguration<HolidayCalendar>
{
    public void Configure(EntityTypeBuilder<HolidayCalendar> builder)
    {
        builder.ToTable("HolidayCalendars", "hr", table =>
        {
            table.HasCheckConstraint("CK_HolidayCalendars_1", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
            table.HasCheckConstraint("CK_HolidayCalendars_Revision", "[Revision] >= 1");
            table.HasCheckConstraint("CK_HolidayCalendars_Predecessor", "([Revision] = 1 AND [SupersedesCalendarId] IS NULL) OR ([Revision] > 1 AND [SupersedesCalendarId] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.GovernmentNotificationReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Revision).HasDefaultValue(1);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasOne<HolidayCalendar>().WithMany().HasForeignKey(x => x.SupersedesCalendarId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SupersedesCalendarId).IsUnique().HasFilter("[SupersedesCalendarId] IS NOT NULL");
        builder.HasIndex(x => new { x.Code, x.EffectiveFrom, x.Revision }).IsUnique();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
