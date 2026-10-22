using Cosmix.Domain.Attendance.LocationConsent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class PunchLocationRedactionConfiguration : IEntityTypeConfiguration<PunchLocationRedaction>
{
    public void Configure(EntityTypeBuilder<PunchLocationRedaction> builder)
    {
        builder.ToTable("PunchLocationRedactions", "attendance");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PunchEventId).IsRequired();
        builder.Property(x => x.RedactedAtUtc).IsRequired();
        builder.Property(x => x.ActorUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RetentionPolicyDays).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(200).IsRequired();

        builder.HasOne(x => x.PunchEvent)
            .WithMany()
            .HasForeignKey(x => x.PunchEventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PunchEventId).IsUnique();
        builder.HasIndex(x => x.RedactedAtUtc);
    }
}
