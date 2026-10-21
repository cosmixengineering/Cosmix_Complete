using Cosmix.Domain.Attendance.Rosters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class TemporaryShiftChangeAmendmentConfiguration : IEntityTypeConfiguration<TemporaryShiftChangeAmendment>
{
    public void Configure(EntityTypeBuilder<TemporaryShiftChangeAmendment> builder)
    {
        AttendanceMapping.Record(builder, "TemporaryShiftChangeAmendments",
            ("DifferentChanges", "[ReplacementChangeId] IS NULL OR [OriginalChangeId] <> [ReplacementChangeId]"),
            ("Evidence", "LEN(LTRIM(RTRIM([RecordedBy]))) > 0 AND LEN(LTRIM(RTRIM([WrittenEvidenceReference]))) > 0 AND LEN(LTRIM(RTRIM([Reason]))) > 0"));
        AttendanceMapping.Strings(builder, ("RecordedBy", 200), ("WrittenEvidenceReference", 500), ("Reason", 2000));
        builder.HasOne<TemporaryShiftChange>().WithMany().HasForeignKey(x => x.OriginalChangeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TemporaryShiftChange>().WithMany().HasForeignKey(x => x.ReplacementChangeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.OriginalChangeId).IsUnique();
        builder.HasIndex(x => x.ReplacementChangeId).IsUnique().HasFilter("[ReplacementChangeId] IS NOT NULL");
    }
}
