using Cosmix.Domain.Attendance.LocationConsent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class LocationNoticePolicyConfiguration : IEntityTypeConfiguration<LocationNoticePolicy>
{
    public void Configure(EntityTypeBuilder<LocationNoticePolicy> builder)
    {
        builder.ToTable("LocationNoticePolicies", "attendance");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).IsRequired();
        builder.Property(x => x.NoticeText).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PublishedByUserId).HasMaxLength(200);
        builder.HasIndex(x => x.Version).IsUnique();
        builder.HasIndex(x => x.IsActive);
    }
}
