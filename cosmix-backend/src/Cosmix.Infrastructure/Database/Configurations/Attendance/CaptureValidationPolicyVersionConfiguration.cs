using Cosmix.Domain.Attendance.Punches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class CaptureValidationPolicyVersionConfiguration : IEntityTypeConfiguration<CaptureValidationPolicyVersion>
{
    public void Configure(EntityTypeBuilder<CaptureValidationPolicyVersion> b)
    {
        AttendanceMapping.Record(b, "CaptureValidationPolicyVersions",
            ("Revision", "([Revision] = 1 AND [SupersedesVersionId] IS NULL) OR ([Revision] > 1 AND [SupersedesVersionId] IS NOT NULL)"),
            ("Window", "[EffectiveFromUtc] >= [PublishedAtUtc]"),
            ("Settings", "ISJSON([SettingsJson]) = 1 AND LEN([SettingsJson]) <= 32768"),
            ("Hash", "LEN([SourceFingerprint]) = 64 AND [SourceFingerprint] NOT LIKE '%[^0-9A-F]%'"));
        AttendanceMapping.Strings(b, ("PublishedBy", 200), ("ApprovalReference", 200), ("Reason", 1000), ("ProtectedEvidenceReference", 500));
        b.Property(x => x.SettingsJson).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(x => x.SourceFingerprint).HasMaxLength(64).IsFixedLength().IsUnicode(false).UseCollation("Latin1_General_100_BIN2").IsRequired();
        b.HasIndex(x => x.Revision).IsUnique();
        b.HasIndex(x => x.SupersedesVersionId).IsUnique().HasFilter("[SupersedesVersionId] IS NOT NULL");
        b.HasIndex(x => new { x.EffectiveFromUtc, x.Revision });
        b.HasAlternateKey(x => new { x.Id, x.Revision, x.SourceFingerprint });
        b.HasOne<CaptureValidationPolicyVersion>().WithMany().HasForeignKey(x => x.SupersedesVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
